using System;
using System.Collections.Generic;
using CesiumLoader.SDK.Gameplay;
using GameLogic;
using party.model;
using Tools;
using UI;

namespace RandomHeroSelectMod
{
    /// <summary>
    /// 与游戏选人界面 / 选人协议打交道的一层。
    ///
    /// 两条路：
    ///   1) UI 路径(默认)：直接触发面板自己的按钮回调，游戏原生行为(选中框、音效、气泡、发包)全部照旧。
    ///      —— 点卡 = <c>UIRoomHero_Button_SelectHero_2.onClick.Call()</c>
    ///         确定 = <c>UIRoomHeroPanel.btn_SureHero.onClick.Call()</c>
    ///         选皮肤 = <c>UIRoomHeroPanel.List_SelectSkin.onClickItem.Call(item)</c> + <c>btn_SureSkin.onClick.Call()</c>
    ///   2) 协议兜底：面板结构对不上(热更改了下标/字段)时退回 <see cref="GameActions"/>，
    ///      功能不失效，只是少了界面演出。
    ///
    /// 所有方法都必须在主线程调用(由 <see cref="ModBase.OnUpdate"/> 保证)，并且自带空保护。
    /// </summary>
    internal sealed class RoomHeroDriver
    {
        private const int PanelTypeRoomHero = 17;

        /// <summary>面板上的 4 个玩家站位组件，用来找"我"的那一格。</summary>
        private static readonly string[] PlayerComponentFields =
        {
            "com_player_1", "com_player_2", "com_player_3", "com_player_4"
        };

        /// <summary>当前是否处于「可以选角色」的房间阶段。</summary>
        internal bool CanSelectHero
        {
            get
            {
                try { return GameActions.CanSelectHero; }
                catch { return false; }
            }
        }

        /// <summary>当前选人面板；拿不到(不在选人界面/面板已销毁)返回 null。</summary>
        internal RoomHeroPanel Panel
        {
            get
            {
                try
                {
                    var current = SimpleSingletonProvider<UIManager>.inst?.currentPanel;
                    if (current is RoomHeroPanel panel && panel.config.PanelType == UIPanelType.RoomHero)
                        return panel;
                }
                catch { }
                return null;
            }
        }

        /// <summary>面板是否停在「选皮肤」这一步。</summary>
        internal bool IsSelectingSkin(RoomHeroPanel panel)
        {
            try
            {
                var ui = panel != null ? panel.UIObject : null;
                return ui != null && ui.step != null && ui.step.selectedIndex == 1;
            }
            catch { return false; }
        }

        /// <summary>本局是否 PVE（决定要不要按 PVE 升星等级筛选）。</summary>
        internal bool IsPveRoom()
        {
            try
            {
                var room = SimpleSingletonProvider<GameLogicManager>.inst?.room?.curRoomInfo;
                return room != null && room.IsPVE();
            }
            catch { return false; }
        }

        /// <summary>配置表里 PVE 升星的上限等级（Levelups 的最后一项 Id）；读不到返回 0。</summary>
        internal static int ConfiguredMaxPveLevel()
        {
            try
            {
                var levelups = StaticConfigure.PVENurturance?.Levelups;
                if (levelups == null || levelups.Count == 0) return 0;
                return levelups[levelups.Count - 1].Id;
            }
            catch { return 0; }
        }

        /// <summary>本局被禁选的角色 ID 集合（拿不到就是空集合，不阻断流程）。</summary>
        internal HashSet<int> ReadBannedHeroIds()
        {
            var banned = new HashSet<int>();
            try
            {
                var room = SimpleSingletonProvider<GameLogicManager>.inst?.room?.curRoomInfo;
                var ids = room?.GetForbiddenHeroIds();
                if (ids == null) return banned;
                for (int i = 0; i < ids.Count; i++) banned.Add(ids[i]);
            }
            catch { }
            return banned;
        }

        /// <summary>已被其他玩家确认占用的角色 ID 集合（读房间的 HeroBar 盒子）。</summary>
        internal HashSet<int> ReadTakenHeroIds()
        {
            var taken = new HashSet<int>();
            try
            {
                var room = SimpleSingletonProvider<GameLogicManager>.inst?.room?.curRoomInfo;
                var box = room?.Box?.Box;
                if (box == null) return taken;
                long selfId = SelfPlayerId();
                foreach (var pair in box)
                {
                    var bar = pair.Value;
                    if (bar == null || !bar.Affirm) continue;
                    if (selfId != 0 && pair.Key == selfId) continue; // 自己那一格也算 Affirm
                    if (bar.HeroId != 0) taken.Add(bar.HeroId);
                }
            }
            catch { }
            return taken;
        }

        /// <summary>自己已确认的角色 ID（0 = 还没确认）。</summary>
        internal int SelfConfirmedHeroId()
        {
            try
            {
                var room = SimpleSingletonProvider<GameLogicManager>.inst?.room?.curRoomInfo;
                long selfId = SelfPlayerId();
                if (selfId == 0) return 0;
                var bar = room?.GetHeroBarById(selfId);
                return bar != null && bar.Affirm ? bar.HeroId : 0;
            }
            catch { return 0; }
        }

        /// <summary>把面板上的所有角色卡读成候选快照（不筛，只读）。</summary>
        internal List<HeroCandidate> ReadCards(RoomHeroPanel panel, out List<UIRoomHero_Button_SelectHero_2> buttons)
        {
            buttons = new List<UIRoomHero_Button_SelectHero_2>();
            var cards = new List<HeroCandidate>();
            try
            {
                var ui = panel != null ? panel.UIObject : null;
                var list = ui != null ? ui.list_RendererSelectRole : null;
                if (list == null) return cards;

                var banned = ReadBannedHeroIds();
                var taken = ReadTakenHeroIds();

                int count = list.numChildren;
                for (int i = 0; i < count; i++)
                {
                    var button = list.GetChildAt(i) as UIRoomHero_Button_SelectHero_2;
                    if (button == null) continue;

                    var heroCard = button.HeroCard;
                    var card = new HeroCandidate
                    {
                        // 列表下标优先用按钮自己记录的下标；它和 GetChildAt 的顺序一致。
                        ListIndex = button._index,
                        HeroId = heroCard != null ? heroCard.HeroId : 0,
                        Owned = IsOwned(heroCard),
                        PveLevel = ReadPveLevel(heroCard),
                        Banned = heroCard != null && banned.Contains(heroCard.HeroId),
                        Taken = heroCard != null && taken.Contains(heroCard.HeroId),
                        Locked = ReadLocked(button),
                        Grayed = ReadGrayed(button)
                    };
                    cards.Add(card);
                    buttons.Add(button);
                }
            }
            catch { }
            return cards;
        }

        /// <summary>真实点击一张角色卡（触发面板自己的 <c>RequestChoiceHero</c>）。返回是否已发出。</summary>
        internal bool ClickCard(UIRoomHero_Button_SelectHero_2 button)
        {
            if (button == null) return false;
            try
            {
                if (button.grayed) return false;
                if (button.onClick == null || button.onClick.isEmpty) return false;
                button.onClick.Call();
                return true;
            }
            catch { return false; }
        }

        /// <summary>点「确定」（触发面板自己的 <c>RequestSureHero</c> → <c>AffirmHeroC2S</c>）。</summary>
        internal bool ClickConfirm(RoomHeroPanel panel)
        {
            try
            {
                var ui = panel != null ? panel.UIObject : null;
                var button = ui != null ? ui.btn_SureHero : null;
                if (button == null) return false;
                if (button.grayed || !button.touchable) return false;
                if (button.onClick == null || button.onClick.isEmpty) return false;
                button.onClick.Call();
                return true;
            }
            catch { return false; }
        }

        /// <summary>协议兜底：直接选角色（不经过界面）。</summary>
        internal bool SelectHeroByProtocol(int heroId)
        {
            try { return heroId > 0 && GameActions.SelectHero(heroId); }
            catch { return false; }
        }

        /// <summary>协议兜底：直接确认。</summary>
        internal bool ConfirmHeroByProtocol()
        {
            try { return GameActions.ConfirmHero(false); }
            catch { return false; }
        }

        /// <summary>
        /// 选皮肤阶段：挑一个可用的皮肤并确认。优先保持当前选中的那个。
        /// </summary>
        internal bool TryConfirmSkin(RoomHeroPanel panel)
        {
            try
            {
                var ui = panel != null ? panel.UIObject : null;
                var list = ui != null ? ui.List_SelectSkin : null;
                if (list == null) return false;

                UIRoomHero_Button_SelectSkin chosen = null;
                int count = list.numChildren;
                for (int i = 0; i < count; i++)
                {
                    var item = list.GetChildAt(i) as UIRoomHero_Button_SelectSkin;
                    if (item == null || !item.IsHas) continue;
                    if (item.selected)
                    {
                        chosen = item;
                        break;
                    }
                    if (chosen == null) chosen = item;
                }
                if (chosen == null) return false;

                // 先走一次原生的 onSelectSkin，让面板记住 _SelectSkinItem 并发 ChooseSkin(affirmed=false)。
                if (list.onClickItem != null && !list.onClickItem.isEmpty)
                {
                    list.onClickItem.Call(chosen);
                }

                var sure = ui.btn_SureSkin;
                if (sure != null && !sure.grayed && sure.touchable &&
                    sure.onClick != null && !sure.onClick.isEmpty)
                {
                    sure.onClick.Call();
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>协议兜底：直接确认皮肤。</summary>
        internal bool ConfirmSkinByProtocol(int heroId, int skinItemId)
        {
            try
            {
                if (heroId <= 0) return false;
                var room = SimpleSingletonProvider<GameLogicManager>.inst?.room;
                if (room == null) return false;
                room.RequestChooseSkinC2S(heroId, skinItemId, true);
                return true;
            }
            catch { return false; }
        }

        /// <summary>面板上当前选中的皮肤 ID（0 = 没有）。</summary>
        internal int SelectedSkinItemId(RoomHeroPanel panel)
        {
            try
            {
                var ui = panel != null ? panel.UIObject : null;
                var list = ui != null ? ui.List_SelectSkin : null;
                if (list == null) return 0;
                int count = list.numChildren;
                for (int i = 0; i < count; i++)
                {
                    var item = list.GetChildAt(i) as UIRoomHero_Button_SelectSkin;
                    if (item != null && item.IsHas && item.selected) return item.SkinItemId;
                }
            }
            catch { }
            return 0;
        }

        private static long SelfPlayerId()
        {
            try { return SimpleSingletonProvider<GameLogicManager>.inst?.account?.GetPlayerID() ?? 0; }
            catch { return 0; }
        }

        private static bool IsOwned(HeroCardData heroCard)
        {
            if (heroCard == null) return false;
            try { return heroCard.heroStatus == HeroStatus.Activate; }
            catch { return false; }
        }

        private static int ReadPveLevel(HeroCardData heroCard)
        {
            if (heroCard == null || heroCard.PveData == null) return 0;
            try { return heroCard.PveData.GetBattlePveLevel(); }
            catch { return 0; }
        }

        private static bool ReadLocked(UIRoomHero_Button_SelectHero_2 button)
        {
            try { return button.isLock != null && button.isLock.selectedIndex == 1; }
            catch { return false; }
        }

        private static bool ReadGrayed(UIRoomHero_Button_SelectHero_2 button)
        {
            try { return button.grayed; }
            catch { return true; }
        }

        /// <summary>面板类型常量（诊断/日志用）。</summary>
        internal static int RoomHeroPanelType { get { return PanelTypeRoomHero; } }

        /// <summary>把候选列表转成一行日志文本。</summary>
        internal static string Describe(IReadOnlyList<HeroCandidate> cards)
        {
            if (cards == null || cards.Count == 0) return "[]";
            var parts = new List<string>(cards.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                if (c == null) continue;
                parts.Add(c.HeroId + (c.PveLevel > 0 ? "/Lv" + c.PveLevel : ""));
            }
            return "[" + string.Join(",", parts.ToArray()) + "]";
        }
    }
}
