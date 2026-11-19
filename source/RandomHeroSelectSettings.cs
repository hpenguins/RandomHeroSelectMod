using System;
using CesiumLoader.SDK.Configuration;
using CesiumLoader.SDK.UserInterface;
using UnityEngine;

namespace RandomHeroSelectMod
{
    /// <summary>
    /// 读 config.json。所有数值都做范围收敛，缺键时用默认值，
    /// 保证手改配置写坏了也不会把流程带偏。
    /// </summary>
    internal sealed class RandomHeroSelectSettings
    {
        internal const int MaxClicksUpperBound = 99;
        internal const int MaxDelayMilliseconds = 10000;
        internal const int MaxClickIntervalMilliseconds = 2000;
        internal const double ClickIntervalJitter = 0.2;

        internal bool Enabled = true;

        /// <summary>视为满级的 PVE 升星等级；0 = 读配置表当前上限。</summary>
        internal int PveLevel = 0;

        internal bool RequireOwned = true;
        internal int ShuffleMinClicks = 22;
        internal int ShuffleMaxClicks = 34;
        internal int ClickIntervalMs = 80;
        internal int DelayMilliseconds = 600;
        internal int ConfirmDelayMilliseconds = 400;
        internal bool RerollOnTaken = true;
        internal int MaxRetry = 3;
        internal bool AutoConfirmSkin = false;
        internal int RandomSeed = 0;

        /// <summary>按钮停靠的屏幕锚点；默认右边居中。</summary>
        internal WidgetAnchor ButtonAnchor = WidgetAnchor.MiddleRight;

        /// <summary>在锚点基础上额外横向偏移（像素，正数向右）。</summary>
        internal int ButtonOffsetX = 0;

        /// <summary>在锚点基础上额外纵向偏移（像素，正数向上）。</summary>
        internal int ButtonOffsetY = 0;

        internal KeyCode ParsedTriggerKey = KeyCode.None;
        internal KeyCode ParsedRerollKey = KeyCode.None;

        internal static RandomHeroSelectSettings Read(ModConfig config)
        {
            var value = new RandomHeroSelectSettings();
            if (config == null) return value;

            value.Enabled = config.GetBool("enabled", true);
            value.PveLevel = Clamp(config.GetInt("pveLevel", 0), 0, 99);
            value.RequireOwned = config.GetBool("requireOwned", true);
            value.ShuffleMinClicks = Clamp(config.GetInt("shuffleMinClicks", 22), 1, MaxClicksUpperBound);
            value.ShuffleMaxClicks = Clamp(config.GetInt("shuffleMaxClicks", 34), 1, MaxClicksUpperBound);
            if (value.ShuffleMaxClicks < value.ShuffleMinClicks)
                value.ShuffleMaxClicks = value.ShuffleMinClicks;
            value.ClickIntervalMs = Clamp(config.GetInt("clickIntervalMs", 80), 0, MaxClickIntervalMilliseconds);
            value.DelayMilliseconds = Clamp(config.GetInt("delayMilliseconds", 600), 0, MaxDelayMilliseconds);
            value.ConfirmDelayMilliseconds =
                Clamp(config.GetInt("confirmDelayMilliseconds", 400), 0, MaxDelayMilliseconds);
            value.RerollOnTaken = config.GetBool("rerollOnTaken", true);
            value.MaxRetry = Clamp(config.GetInt("maxRetry", 3), 0, 9);
            value.AutoConfirmSkin = config.GetBool("autoConfirmSkin", false);
            value.RandomSeed = config.GetInt("randomSeed", 0);

            value.ButtonAnchor = ParseAnchor(config.GetString("buttonAnchor", "right"), WidgetAnchor.MiddleRight);
            value.ButtonOffsetX = Clamp(config.GetInt("buttonOffsetX", 0), -4000, 4000);
            value.ButtonOffsetY = Clamp(config.GetInt("buttonOffsetY", 0), -4000, 4000);

            value.ParsedTriggerKey = ParseKey(config.GetString("triggerKey", ""), KeyCode.None);
            value.ParsedRerollKey = ParseKey(config.GetString("rerollKey", ""), KeyCode.None);
            return value;
        }

        /// <summary>把缺失的键补回 config.json（不动已有值）。</summary>
        internal static void EnsureDefaults(ModConfig config)
        {
            if (config == null) return;
            bool changed = RandomHeroSelectConfigDocs.Apply(config);
            Add(config, "enabled", true, ref changed);
            Add(config, "pveLevel", 0, ref changed);
            Add(config, "requireOwned", true, ref changed);
            Add(config, "shuffleMinClicks", 22, ref changed);
            Add(config, "shuffleMaxClicks", 34, ref changed);
            Add(config, "clickIntervalMs", 80, ref changed);
            Add(config, "delayMilliseconds", 600, ref changed);
            Add(config, "confirmDelayMilliseconds", 400, ref changed);
            Add(config, "rerollOnTaken", true, ref changed);
            Add(config, "maxRetry", 3, ref changed);
            Add(config, "autoConfirmSkin", false, ref changed);
            Add(config, "triggerKey", "", ref changed);
            Add(config, "rerollKey", "", ref changed);
            Add(config, "randomSeed", 0, ref changed);
            Add(config, "buttonAnchor", "right", ref changed);
            Add(config, "buttonOffsetX", 0, ref changed);
            Add(config, "buttonOffsetY", 0, ref changed);
            if (changed) config.SaveIfDirty();
        }

        /// <summary>本次实际生效的满级阈值。</summary>
        internal int ResolveMaxPveLevel()
        {
            if (PveLevel > 0) return PveLevel;
            return RoomHeroDriver.ConfiguredMaxPveLevel();
        }

        private static void Add(ModConfig config, string key, object value, ref bool changed)
        {
            if (config.Has(key)) return;
            config.Set(key, value);
            changed = true;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }

        private static KeyCode ParseKey(string text, KeyCode fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            if (Enum.TryParse<KeyCode>(text.Trim(), true, out var parsed)) return parsed;
            return fallback;
        }

        /// <summary>
        /// 解析按钮锚点。接受 right / left / top / bottom / topRight / bottomLeft / center
        /// 这类简称，也接受 WidgetAnchor 的完整枚举名（TopLeft、MiddleRight…）。
        /// </summary>
        private static WidgetAnchor ParseAnchor(string text, WidgetAnchor fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;

            string key = text.Trim().Replace("-", "").Replace("_", "").Replace(" ", "");
            switch (key.ToLowerInvariant())
            {
                case "topleft": return WidgetAnchor.TopLeft;
                case "top":
                case "topcenter": return WidgetAnchor.Top;
                case "topright": return WidgetAnchor.TopRight;
                case "left":
                case "middleleft": return WidgetAnchor.MiddleLeft;
                case "center":
                case "centre":
                case "middle": return WidgetAnchor.Center;
                case "right":
                case "middleright": return WidgetAnchor.MiddleRight;
                case "bottomleft": return WidgetAnchor.BottomLeft;
                case "bottom":
                case "bottomcenter": return WidgetAnchor.Bottom;
                case "bottomright": return WidgetAnchor.BottomRight;
            }

            if (Enum.TryParse<WidgetAnchor>(text.Trim(), true, out var parsed) &&
                parsed != WidgetAnchor.Stretch)
            {
                return parsed;
            }
            return fallback;
        }
    }
}
