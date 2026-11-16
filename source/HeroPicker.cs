using System;
using System.Collections.Generic;

namespace RandomHeroSelectMod
{
    /// <summary>
    /// 一张角色卡的判定快照。纯数据，不含任何游戏类型，便于离线单测。
    ///
    /// 由 <see cref="RoomHeroDriver"/> 从选人面板读出后填入，
    /// 再由 <see cref="HeroPicker"/> 决定它能不能进候选。
    /// </summary>
    internal sealed class HeroCandidate
    {
        /// <summary>面板列表里的下标（点卡时用）。</summary>
        internal int ListIndex;

        /// <summary>角色 ID。</summary>
        internal int HeroId;

        /// <summary>是否已拥有（<c>HeroStatus.Activate</c>）。</summary>
        internal bool Owned;

        /// <summary>本局生效的 PVE 升星等级（<c>GetBattlePveLevel()</c>）。</summary>
        internal int PveLevel;

        /// <summary>本局被禁选（<c>RoomInfo.GetForbiddenHeroIds()</c>）。</summary>
        internal bool Banned;

        /// <summary>已被别人确认占用。</summary>
        internal bool Taken;

        /// <summary>场馆/模式未解锁（面板上的小锁）。</summary>
        internal bool Locked;

        /// <summary>面板已置灰（不可点）。</summary>
        internal bool Grayed;

        public override string ToString()
        {
            return "HeroId=" + HeroId + " Lv=" + PveLevel + " Owned=" + Owned +
                   " Banned=" + Banned + " Taken=" + Taken + " Locked=" + Locked + " Grayed=" + Grayed;
        }
    }

    /// <summary>筛选结果：进了候选的角色 + 各自被拒的原因（用于日志）。</summary>
    internal sealed class HeroSelection
    {
        internal readonly List<HeroCandidate> Eligible = new List<HeroCandidate>();
        internal readonly List<string> Rejections = new List<string>();

        internal int MaxLevel;
        internal bool IsPve;
    }

    /// <summary>候选筛选规则。纯逻辑，无游戏引用。</summary>
    internal static class HeroPicker
    {
        /// <summary>
        /// 按配置筛出可以点的角色。
        /// </summary>
        /// <param name="all">面板上所有卡。</param>
        /// <param name="isPve">本局是否 PVE（PVP 没有 PVE 等级概念，不做等级筛选）。</param>
        /// <param name="requireOwned">是否只允许已拥有的角色。</param>
        /// <param name="maxLevel">满级阈值；&lt;= 0 表示不按等级筛。</param>
        internal static HeroSelection Filter(
            IReadOnlyList<HeroCandidate> all, bool isPve, bool requireOwned, int maxLevel)
        {
            var result = new HeroSelection { IsPve = isPve, MaxLevel = maxLevel };
            if (all == null) return result;

            for (int i = 0; i < all.Count; i++)
            {
                var card = all[i];
                if (card == null) continue;

                string reason = Reject(card, isPve, requireOwned, maxLevel);
                if (reason == null)
                {
                    result.Eligible.Add(card);
                }
                else
                {
                    result.Rejections.Add(card.HeroId + ":" + reason);
                }
            }
            return result;
        }

        /// <summary>单张卡的拒绝原因；可以进候选时返回 null。</summary>
        internal static string Reject(HeroCandidate card, bool isPve, bool requireOwned, int maxLevel)
        {
            if (card == null) return "null";
            if (card.Grayed) return "grayed";
            if (card.Locked) return "locked";
            if (card.Banned) return "banned";
            if (card.Taken) return "taken";
            if (requireOwned && !card.Owned) return "not-owned";
            if (isPve && maxLevel > 0 && card.PveLevel < maxLevel) return "level";
            return null;
        }
    }
}
