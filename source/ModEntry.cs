using System;
using System.Collections.Generic;
using CesiumLoader.SDK.Engine;
using CesiumLoader.SDK.Gameplay;
using CesiumLoader.SDK.Inputs;
using CesiumLoader.SDK.Mods;
using CesiumLoader.SDK.UserInterface;
using GameLogic;
using party.model;
using UI;
using UnityEngine;
// UnityEngine 也有 Random(引擎随机源), 这里统一用 System.Random。
using Random = System.Random;

namespace RandomHeroSelectMod
{
    public static class ModEntry
    {
        public static void Main() { ModBase.Run(new RandomHeroSelect()); }
    }

    /// <summary>
    /// 选人阶段的「随机点卡 → 随机落定 → 自动确认」状态机。
    ///
    /// 时序(全部走真实点击，每次点击都向服务器广播一条 ChoiceHeroC2S2):
    ///   HeroSelectionStarted
    ///     → Delay  (等卡面渲染, delayMilliseconds)
    ///     → Shuffle(按洗好的整副牌逐张点, 每张每轮最多点一次, clickIntervalMs 抖动)
    ///     → Settle (停一下让选中动画走完)
    ///     → Final  (从满级候选里随机落定, 必要时补一次点击)
    ///     → Confirm(confirmDelayMilliseconds 后点「确定」)
    ///     → Done
    ///   HeroSelected(heroTaken=true) → 从剩余候选重抽(最多 maxRetry 次)
    /// </summary>
    public sealed class RandomHeroSelect : ModBase
    {
        private enum Phase
        {
            Idle,
            Delay,
            Shuffle,
            Settle,
            Final,
            Confirm,
            SkinConfirm,
            Done,
            Aborted
        }

        /// <summary>Final→Confirm 之间的停顿；让最后一次选中动画有时间走完。</summary>
        private const int SettleMilliseconds = 250;

        /// <summary>选人面板卡住超过这么久就放弃本次会话(交给游戏倒计时自动确认)。</summary>
        private const long SessionTimeoutTicks = TimeSpan.TicksPerSecond * 30;

        /// <summary>确认角色后等待皮肤阶段打开的上限。</summary>
        private const long SkinStageTimeoutTicks = TimeSpan.TicksPerSecond * 10;

        private RandomHeroSelectSettings _settings = new RandomHeroSelectSettings();
        private readonly RoomHeroDriver _driver = new RoomHeroDriver();

        private Random _random = new Random();
        private Phase _phase = Phase.Idle;
        private long _phaseAtTicks;
        private long _sessionStartedTicks;

        private RoomHeroPanel _panel;
        private readonly List<HeroCandidate> _eligible = new List<HeroCandidate>();
        private readonly List<UIRoomHero_Button_SelectHero_2> _buttons =
            new List<UIRoomHero_Button_SelectHero_2>();

        private int[] _plan = Array.Empty<int>();
        private int _planCursor;
        private long _nextClickTicks;
        private int _lastClickedListIndex = -1;

        private int _pickedHeroId;
        private int _retryCount;
        private int _lastFrame = -1;
        private bool _sessionArmed;

        /// <summary>选人 RPC 回调可能在网络线程, 这里只记标志, 真正动作留到主线程 Tick。</summary>
        private enum PendingEvent
        {
            None,
            Reroll,
            Confirmed
        }

        private PendingEvent _pending = PendingEvent.None;
        private int _pendingHeroId;
        private long _skinDeadlineTicks;

        public override string Name { get { return "随机选人"; } }
        public override string Version { get { return "1.0.0"; } }
        public override string Author { get { return "hpenguins"; } }
        public override string Description
        {
            get { return "选人阶段模拟随机点击多张满级角色卡，最后随机落定一个并自动确认；被抢时自动重抽。"; }
        }

        public override void OnInitialize()
        {
            var config = Config;
            if (config != null)
            {
                try
                {
                    RandomHeroSelectSettings.EnsureDefaults(config);
                    config.SaveIfDirty();
                }
                catch (Exception e)
                {
                    Log.Warn("写入默认配置失败（继续使用内存默认值）: " + e.Message);
                }
            }
            ReadConfig();
            ResetRandom();

            GameEvents.HeroSelectionStarted += OnHeroSelectionStarted;
            GameEvents.HeroSelected += OnHeroSelected;
            GameEvents.StartAutoHook();
            GameEvents.EnsureHooked();

            Log.Info("已订阅选人事件；满级阈值=" + _settings.ResolveMaxPveLevel() +
                     "，乱点 " + _settings.ShuffleMinClicks + "–" + _settings.ShuffleMaxClicks +
                     " 次，间隔约 " + _settings.ClickIntervalMs + "ms，自动确认=" + _settings.AutoConfirm);
        }

        public override void OnConfigChanged()
        {
            ReadConfig();
            ResetRandom();
            Log.Info("配置已热重载：enabled=" + _settings.Enabled +
                     "，满级阈值=" + _settings.ResolveMaxPveLevel() +
                     "，自动确认=" + _settings.AutoConfirm);
        }

        public override void OnUpdate()
        {
            if (!_settings.Enabled) return;

            int frame = UnityTime.FrameCount;
            if (frame != _lastFrame) _lastFrame = frame;
            else return;

            HandleHotkeys();
            Tick();
        }

        public override void OnUnload()
        {
            GameEvents.HeroSelectionStarted -= OnHeroSelectionStarted;
            GameEvents.HeroSelected -= OnHeroSelected;
            ResetSession();
        }

        // ============================== 事件 ==============================

        private void OnHeroSelectionStarted(Room room)
        {
            if (!_settings.Enabled) return;
            _retryCount = 0;
            _pickedHeroId = 0;
            _sessionArmed = false;
            ResetSession();
            Log.Info("进入选人阶段" + (room != null ? "（房间 " + room.Id + "）" : "") + "，准备随机选人");
            ArmSession();
        }

        private void OnHeroSelected(long playerId, int heroId, bool heroTaken)
        {
            if (!Players.IsSelf(playerId)) return;

            // 回调可能不在主线程: 只记标志和结果, 重抽/皮肤确认统一在 Tick 里做。
            if (heroTaken)
            {
                _pendingHeroId = heroId;
                _pending = PendingEvent.Reroll;
                return;
            }

            _pendingHeroId = heroId;
            _pending = PendingEvent.Confirmed;
        }

        // ============================== 状态机 ==============================

        private void Tick()
        {
            try
            {
                // 离开选人阶段（开局/退出房间/被确认）后收起会话，下一次重新开始。
                if (!_driver.CanSelectHero && _phase != Phase.Idle)
                {
                    if (_phase != Phase.Done && _phase != Phase.Aborted)
                        Log.Info("已离开选人阶段，停止本次随机选人");
                    ResetSession();
                    return;
                }

                if (_phase == Phase.Idle)
                {
                    // 兜底：没收到事件但已经在选人阶段（例如 mod 中途加载）。
                    if (!_sessionArmed && _driver.CanSelectHero) ArmSession();
                    return;
                }

                long now = DateTime.UtcNow.Ticks;

                // 先处理上一帧收到的事件（网络线程投递），再做本帧阶段推进。
                if (HandlePending(now)) return;

                if (now - _sessionStartedTicks > SessionTimeoutTicks)
                {
                    Log.Warn("随机选人超时，交回游戏倒计时");
                    _phase = Phase.Aborted;
                    return;
                }

                switch (_phase)
                {
                    case Phase.Delay:
                        if (now < _phaseAtTicks) return;
                        PrepareCandidates();
                        break;

                    case Phase.Shuffle:
                        TickShuffle(now);
                        break;

                    case Phase.Settle:
                        if (now < _phaseAtTicks) return;
                        DoFinalPick(now);
                        break;

                    case Phase.Final:
                        if (now < _phaseAtTicks) return;
                        DoConfirm(now);
                        break;

                    case Phase.Confirm:
                        // 已发出确认，等服务器回 HeroSelected；只有会话超时会兜底。
                        return;

                    case Phase.SkinConfirm:
                        if (!_settings.AutoConfirmSkin) { FinishSession(); return; }
                        if (now < _phaseAtTicks) return;
                        if (!_driver.IsSelectingSkin(_driver.Panel))
                        {
                            // 皮肤阶段还没打开就先等，直到超时。
                            if (now >= _skinDeadlineTicks) FinishSession();
                            return;
                        }
                        TickSkin();
                        break;

                    case Phase.Done:
                    case Phase.Aborted:
                        return;
                }
            }
            catch (Exception e)
            {
                Log.Warn("随机选人异常，停止本次会话: " + e.Message);
                _phase = Phase.Aborted;
            }
        }

        private void ArmSession()
        {
            if (_sessionArmed || !_settings.Enabled) return;
            if (!_driver.CanSelectHero) return;

            _panel = _driver.Panel;
            if (_panel == null) return;

            _sessionArmed = true;
            _sessionStartedTicks = DateTime.UtcNow.Ticks;
            _phase = Phase.Delay;
            _phaseAtTicks = _sessionStartedTicks + DelayTicks(_settings.DelayMilliseconds);
            _pickedHeroId = 0;
            _retryCount = 0;
            _plan = Array.Empty<int>();
            _planCursor = 0;
            _lastClickedListIndex = -1;
            _pending = PendingEvent.None;
            _pendingHeroId = 0;
        }

        private void PrepareCandidates()
        {
            _panel = _driver.Panel;
            if (_panel == null)
            {
                Log.Warn("找不到选人面板，改用协议直接选角色");
                FallbackByProtocol();
                return;
            }

            bool isPve = _driver.IsPveRoom();
            int maxLevel = _settings.ResolveMaxPveLevel();
            var all = _driver.ReadCards(_panel, out var buttons);

            _buttons.Clear();
            _buttons.AddRange(buttons);

            var selection = HeroPicker.Filter(all, isPve, _settings.RequireOwned, maxLevel);
            _eligible.Clear();
            _eligible.AddRange(selection.Eligible);

            if (_eligible.Count == 0)
            {
                Log.Warn("没有可选的满级角色（isPve=" + isPve + "，满级阈值=" + maxLevel +
                         "，面板 " + all.Count + " 张，拒绝 " + selection.Rejections.Count + " 张）");
                if (selection.Rejections.Count > 0)
                {
                    int show = Math.Min(8, selection.Rejections.Count);
                    Log.Warn("被拒样例: " + string.Join("; ", selection.Rejections.GetRange(0, show).ToArray()));
                }
                _phase = Phase.Aborted;
                return;
            }

            Log.Info("候选 " + _eligible.Count + "/" + all.Count + " 个满级角色（isPve=" + isPve +
                     "，阈值=" + maxLevel + "）: " + HeroPickerSummary());

            StartShuffle();
        }

        private void StartShuffle()
        {
            int clicks = RandomClickCount();
            _plan = ShufflePlan.Build(_eligible.Count, clicks, _random);
            _planCursor = 0;
            _phase = Phase.Shuffle;
            _nextClickTicks = DateTime.UtcNow.Ticks;
            Log.Info("开始随机点卡：计划 " + _plan.Length + " 次");
        }

        private void TickShuffle(long now)
        {
            if (now < _nextClickTicks) return;

            if (_planCursor >= _plan.Length)
            {
                _phase = Phase.Settle;
                _phaseAtTicks = now + DelayTicks(SettleMilliseconds);
                return;
            }

            int eligibleIndex = _plan[_planCursor++];
            if (eligibleIndex < 0 || eligibleIndex >= _eligible.Count)
            {
                _nextClickTicks = now;
                return;
            }

            var card = _eligible[eligibleIndex];
            if (!ClickCandidate(card))
            {
                // 这张卡已不可点（被抢/锁定/置灰），跳过它继续下一张，不额外等待。
                Log.Debug("跳过不可点的卡 " + card.HeroId);
                _nextClickTicks = now;
                return;
            }

            _nextClickTicks = now + DelayTicks(ShufflePlan.Jitter(
                _settings.ClickIntervalMs, RandomHeroSelectSettings.ClickIntervalJitter, _random));
        }

        private void DoFinalPick(long now)
        {
            _panel = _driver.Panel;
            if (_panel == null)
            {
                Log.Warn("选人面板已消失，改走协议");
                FallbackByProtocol();
                return;
            }

            // 重新读一次，剔除刚刚被抢走的
            RefreshTakenState();
            if (_eligible.Count == 0)
            {
                Log.Warn("候选在乱点过程中全部被占，放弃本次随机选人");
                _phase = Phase.Aborted;
                return;
            }

            int index = ShufflePlan.PickFinal(_eligible.Count, _random);
            var target = _eligible[index];
            _pickedHeroId = target.HeroId;

            // 落点卡也走真实点击（如果还没点过这张）。
            if (target.ListIndex != _lastClickedListIndex)
            {
                ClickCandidate(target);
            }

            Log.Info("落定：" + HeroName(target.HeroId) + "（Lv" + target.PveLevel + "）");

            _phase = Phase.Final;
            _phaseAtTicks = now + DelayTicks(_settings.ConfirmDelayMilliseconds);
        }

        private void DoConfirm(long now)
        {
            if (!_settings.AutoConfirm)
            {
                Log.Info("已落定 " + HeroName(_pickedHeroId) + "，autoConfirm=false，等待你手动确认");
                FinishSession();
                return;
            }

            _panel = _driver.Panel;
            if (_pickedHeroId <= 0)
            {
                _phase = Phase.Aborted;
                return;
            }

            bool sent;
            if (_panel != null)
            {
                sent = _driver.ClickConfirm(_panel);
                if (!sent) sent = _driver.ConfirmHeroByProtocol();
            }
            else
            {
                sent = _driver.ConfirmHeroByProtocol();
            }

            if (sent)
            {
                Log.Info("已发出确认：" + HeroName(_pickedHeroId));
                _phase = Phase.Confirm;
                _phaseAtTicks = now + DelayTicks(_settings.ConfirmDelayMilliseconds);
            }
            else
            {
                Log.Warn("确认发送失败（可能已不在选人阶段）");
                _phase = Phase.Aborted;
            }
        }

        /// <summary>处理上一帧收到的选人结果事件。返回 true 表示本帧已消费掉、不要再推进阶段。</summary>
        private bool HandlePending(long now)
        {
            var pending = _pending;
            if (pending == PendingEvent.None) return false;

            _pending = PendingEvent.None;
            int heroId = _pendingHeroId;

            if (pending == PendingEvent.Reroll)
            {
                Log.Warn("落定的角色 " + HeroName(heroId) + " 已被其他玩家占用");

                if (!_settings.RerollOnTaken || _retryCount >= _settings.MaxRetry)
                {
                    Log.Warn("放弃重抽（rerollOnTaken=" + _settings.RerollOnTaken +
                             "，已重抽 " + _retryCount + "/" + _settings.MaxRetry + " 次）");
                    _phase = Phase.Aborted;
                    return true;
                }
                _retryCount++;
                Reroll();
                return true;
            }

            // Confirmed
            Log.Info("角色已确认：" + HeroName(heroId) + "（重抽 " + _retryCount + " 次）");
            BeginSkinStage(now);
            return false;
        }

        /// <summary>确认之后进入皮肤阶段（可配置是否自动确认皮肤）。</summary>
        private void BeginSkinStage(long now)
        {
            if (!_settings.AutoConfirmSkin)
            {
                FinishSession();
                return;
            }

            _phase = Phase.SkinConfirm;
            _phaseAtTicks = now + DelayTicks(_settings.ConfirmDelayMilliseconds);
            _skinDeadlineTicks = now + SkinStageTimeoutTicks;
            Log.Info("等待皮肤阶段…");
        }

        private void TickSkin()
        {
            _panel = _driver.Panel;
            if (_panel == null || !_driver.IsSelectingSkin(_panel))
            {
                FinishSession();
                return;
            }

            int heroId = _pickedHeroId > 0 ? _pickedHeroId : _driver.SelfConfirmedHeroId();
            int skinId = _driver.SelectedSkinItemId(_panel);
            bool sent = _driver.TryConfirmSkin(_panel) ||
                        _driver.ConfirmSkinByProtocol(heroId, skinId);
            Log.Info(sent ? "已确认皮肤" : "皮肤确认失败（可能已跳过皮肤阶段）");
            FinishSession();
        }

        private void Reroll()
        {
            _panel = _driver.Panel;
            if (_panel == null)
            {
                Log.Warn("面板已消失，无法重抽");
                _phase = Phase.Aborted;
                return;
            }

            RefreshTakenState();
            if (_eligible.Count == 0)
            {
                Log.Warn("没有剩余候选可重抽");
                _phase = Phase.Aborted;
                return;
            }

            int index = ShufflePlan.PickFinal(_eligible.Count, _random);
            var target = _eligible[index];
            _pickedHeroId = target.HeroId;

            ClickCandidate(target);
            Log.Info("重抽 #" + _retryCount + " → " + HeroName(target.HeroId) + "（Lv" + target.PveLevel + "）");

            _phase = Phase.Final;
            _phaseAtTicks = DateTime.UtcNow.Ticks + DelayTicks(_settings.ConfirmDelayMilliseconds);
        }

        private void FallbackByProtocol()
        {
            if (_eligible.Count == 0)
            {
                _phase = Phase.Aborted;
                return;
            }

            int index = ShufflePlan.PickFinal(_eligible.Count, _random);
            var target = _eligible[index];
            _pickedHeroId = target.HeroId;

            if (!_driver.SelectHeroByProtocol(target.HeroId))
            {
                Log.Warn("协议选角也失败，放弃");
                _phase = Phase.Aborted;
                return;
            }

            Log.Info("已用协议选中：" + HeroName(target.HeroId));
            _phase = Phase.Final;
            _phaseAtTicks = DateTime.UtcNow.Ticks + DelayTicks(_settings.ConfirmDelayMilliseconds);
        }

        private void FinishSession()
        {
            _phase = Phase.Done;
            _plan = Array.Empty<int>();
            _planCursor = 0;
        }

        private void ResetSession()
        {
            _phase = Phase.Idle;
            _plan = Array.Empty<int>();
            _planCursor = 0;
            _eligible.Clear();
            _buttons.Clear();
            _panel = null;
            _pickedHeroId = 0;
            _lastClickedListIndex = -1;
            _sessionArmed = false;
            _pending = PendingEvent.None;
            _pendingHeroId = 0;
            _skinDeadlineTicks = 0;
        }

        // ============================== 辅助 ==============================

        /// <summary>点一张卡，并记录下标（用于避免同一张连点）。</summary>
        private bool ClickCandidate(HeroCandidate card)
        {
            if (card == null) return false;

            UIRoomHero_Button_SelectHero_2 button = null;
            // _buttons 与 _eligible 的顺序不一定对应（筛选会丢元素），按 ListIndex 找回。
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] != null && _buttons[i]._index == card.ListIndex)
                {
                    button = _buttons[i];
                    break;
                }
            }
            if (button == null) return _driver.SelectHeroByProtocol(card.HeroId);

            if (!_driver.ClickCard(button)) return false;
            _lastClickedListIndex = card.ListIndex;
            return true;
        }

        /// <summary>重新读一次面板，剔除已被别人占用的候选。</summary>
        private void RefreshTakenState()
        {
            if (_panel == null) return;
            var taken = _driver.ReadTakenHeroIds();
            for (int i = _eligible.Count - 1; i >= 0; i--)
            {
                var card = _eligible[i];
                if (card == null || taken.Contains(card.HeroId))
                {
                    if (card != null) Log.Info("候选 " + HeroName(card.HeroId) + " 已被占用，剔除");
                    _eligible.RemoveAt(i);
                }
            }
        }

        private int RandomClickCount()
        {
            int min = _settings.ShuffleMinClicks;
            int max = _settings.ShuffleMaxClicks;
            if (max <= min) return min;
            return min + _random.Next(max - min + 1);
        }

        private void HandleHotkeys()
        {
            try
            {
                if (_settings.ParsedTriggerKey != KeyCode.None &&
                    InputService.IsKeyDown(_settings.ParsedTriggerKey))
                {
                    if (!_driver.CanSelectHero)
                    {
                        Log.Info("快捷键触发：当前不在选人阶段");
                    }
                    else
                    {
                        _sessionArmed = false;
                        ResetSession();
                        ArmSession();
                        Log.Info("快捷键触发随机选人");
                    }
                }

                if (_settings.ParsedRerollKey != KeyCode.None &&
                    InputService.IsKeyDown(_settings.ParsedRerollKey) &&
                    _driver.CanSelectHero && _eligible.Count > 0)
                {
                    Log.Info("快捷键触发重抽");
                    Reroll();
                }
            }
            catch { }
        }

        private void ReadConfig()
        {
            _settings = RandomHeroSelectSettings.Read(Config);
        }

        private void ResetRandom()
        {
            _random = _settings.RandomSeed != 0 ? new Random(_settings.RandomSeed) : new Random();
        }

        private string HeroPickerSummary()
        {
            var parts = new List<string>(_eligible.Count);
            for (int i = 0; i < _eligible.Count && i < 12; i++)
            {
                var card = _eligible[i];
                if (card != null) parts.Add(card.HeroId + "/Lv" + card.PveLevel);
            }
            if (_eligible.Count > 12) parts.Add("…");
            return string.Join(",", parts.ToArray());
        }

        private static string HeroName(int heroId)
        {
            if (heroId <= 0) return "?";
            try { return Names.Character(heroId) + "(" + heroId + ")"; }
            catch { return heroId.ToString(); }
        }

        private static long DelayTicks(int milliseconds)
        {
            return milliseconds <= 0 ? 0 : milliseconds * TimeSpan.TicksPerMillisecond;
        }
    }
}
