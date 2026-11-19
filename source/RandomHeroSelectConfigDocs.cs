using CesiumLoader.SDK.Configuration;

namespace RandomHeroSelectMod
{
    /// <summary>config.json 里各字段的多语言说明。</summary>
    internal static class RandomHeroSelectConfigDocs
    {
        internal static bool Apply(ModConfig config)
        {
            if (config == null) return false;
            bool changed = false;

            changed |= config.Describe("enabled", D(
                "总开关：开启后，选人阶段右侧会显示「随机选人」按钮，点击后开始随机点卡并落定。",
                "總開關：開啟後，選人階段右側會顯示「隨機選人」按鈕，點擊後開始隨機點卡並落定。",
                "Master switch: when on, a Random Pick button appears at the right edge during hero selection; click it to shuffle and pick.",
                "全体スイッチ：オンにすると、キャラクター選択中に画面右端へ「ランダム選択」ボタンが表示され、押すと自動でランダムにカードを選び、最終候補を1枚選びます。"));

            changed |= config.Describe("pveLevel", D(
                "视为「满级」的 PVE 升星等级，0 表示自动读取配置表当前上限（现为 6）。仅对 PVE 生效。",
                "視為「滿級」的 PVE 升星等級，0 表示自動讀取設定表目前上限（現為 6）。僅對 PVE 生效。",
                "PVE star-up level considered maxed; 0 reads the current cap from config (6). PVE only.",
                "「最大レベル」とみなす PVE 昇星レベル。0 は設定表の現在の上限（6）を自動取得。PVE のみ。"));

            changed |= config.Describe("requireOwned", D(
                "是否只抽已拥有的角色（关闭后试用角色也会进候选）。",
                "是否只抽已擁有的角色（關閉後試用角色也會進候選）。",
                "Only pick heroes you own; turn off to include trial heroes.",
                "所持済みキャラクターのみ対象にする。オフにすると体験キャラクターも候補に入ります。"));

            changed |= config.Describe("shuffleMinClicks", D(
                "乱点时最少点几张卡，范围 1–99，默认 22。",
                "亂點時最少點幾張卡，範圍 1–99，預設 22。",
                "Minimum number of random card clicks during the shuffle (1–99, default 22).",
                "ランダムにクリックする最小回数（1～99、既定 22）。"));

            changed |= config.Describe("shuffleMaxClicks", D(
                "乱点时最多点几张卡，范围 1–99，默认 34；小于最小值时自动取最小值。",
                "亂點時最多點幾張卡，範圍 1–99，預設 34；小於最小值時自動取最小值。",
                "Maximum number of random card clicks during the shuffle (1–99, default 34); clamped to the minimum.",
                "ランダムにクリックする最大回数（1～99、既定 34）。最小値未満なら最小値を使用。"));

            changed |= config.Describe("clickIntervalMs", D(
                "两次点卡之间的间隔（毫秒，0–2000，默认 80），实际会上下抖动约 20%。",
                "兩次點卡之間的間隔（毫秒，0～2000，預設 80），實際會上下抖動約 20%。",
                "Delay between card clicks in milliseconds (0–2000, default 80), jittered by about 20%.",
                "カードクリック間隔（ミリ秒、0～2000、既定 80）。実際は約 20% 上下にばらつきます。"));

            changed |= config.Describe("delayMilliseconds", D(
                "点击按钮后先等多久再开始乱点（毫秒，0–10000，默认 600），用于等卡面渲染完。",
                "點擊按鈕後先等多久再開始亂點（毫秒，0～10000，預設 600），用於等卡面渲染完成。",
                "Delay after clicking the button before shuffling starts, in milliseconds (0–10000, default 600).",
                "ボタンを押してからランダム選択を始めるまでの待ち時間（ミリ秒、0～10000、既定 600）。"));

            changed |= config.Describe("confirmDelayMilliseconds", D(
                "角色确认后，自动确认皮肤前等待多久（毫秒，0–10000，默认 400）。",
                "角色確認後，自動確認造型前等待多久（毫秒、0～10000、預設 400）。",
                "Delay before auto-confirming a skin after the hero is confirmed, in milliseconds (0–10000, default 400).",
                "キャラクター確定後、スキンを自動確定するまでの待ち時間（ミリ秒、0～10000、既定 400）。"));

            changed |= config.Describe("rerollOnTaken", D(
                "落定的角色被别人抢走时，自动从剩余候选里重抽。",
                "落定的角色被別人搶走時，自動從剩餘候選裡重抽。",
                "Re-roll from the remaining candidates when someone else takes your pick first.",
                "選んだキャラクターが他プレイヤーに取られた場合、残りの候補から選び直します。"));

            changed |= config.Describe("maxRetry", D(
                "被抢后最多重抽几次，范围 0–9，默认 3。",
                "被搶後最多重抽幾次，範圍 0～9，預設 3。",
                "Maximum number of re-rolls after being taken (0–9, default 3).",
                "取り直しの最大回数（0～9、既定 3）。"));

            changed |= config.Describe("autoConfirmSkin", D(
                "角色确认之后，是否自动选定并确认皮肤（默认关闭）。",
                "角色確認之後，是否自動選定並確認造型（預設關閉）。",
                "Automatically choose and confirm a skin after the hero is confirmed (default off).",
                "キャラクター確定後、自動でスキンを選択・確定するか（既定オフ）。"));

            changed |= config.Describe("triggerKey", D(
                "手动触发一次随机选人的快捷键（默认空，留空不绑定；也可以直接点右侧按钮）。",
                "手動觸發一次隨機選人的快捷鍵（預設空，留空不綁定；也可以直接點右側按鈕）。",
                "Hotkey to trigger a random pick manually (default empty = disabled); the right-edge button does the same.",
                "手動でランダム選択を開始するホットキー（既定空＝無効）。画面右端のボタンでも開始できます。"));

            changed |= config.Describe("rerollKey", D(
                "手动重抽一次目标的快捷键（默认空，留空不绑定）。",
                "手動重抽一次目標的快捷鍵（預設空，留空不綁定）。",
                "Hotkey to re-roll the final target manually (default empty = disabled).",
                "最終ターゲットを手動で選び直すホットキー（既定空＝無効）。"));

            changed |= config.Describe("randomSeed", D(
                "随机种子，0 表示每次真随机；填非 0 可复现同一套点击顺序（调试用）。",
                "隨機種子，0 表示每次真隨機；填非 0 可重現同一套點擊順序（除錯用）。",
                "Random seed; 0 means truly random each time, non-zero reproduces the same click order (debug).",
                "乱数シード。0 は毎回真ランダム、0 以外は同じクリック順を再現します（デバッグ用）。"));

            changed |= config.Describe("buttonAnchor", D(
                "按钮停靠的屏幕位置：right（默认，右边居中）/ left / top / bottom / topLeft / topRight / bottomLeft / bottomRight / center；也接受 MiddleRight 这类枚举名。",
                "按鈕停靠的螢幕位置：right（預設，右邊居中）/ left / top / bottom / topLeft / topRight / bottomLeft / bottomRight / center；也接受 MiddleRight 這類列舉名。",
                "Where the button sits: right (default, middle of the right edge), left, top, bottom, topLeft, topRight, bottomLeft, bottomRight, center; full enum names like MiddleRight also work.",
                "ボタンの位置：right（既定、右端中央）/ left / top / bottom / topLeft / topRight / bottomLeft / bottomRight / center。MiddleRight などの列挙名も使えます。"));

            changed |= config.Describe("buttonOffsetX", D(
                "在锚点基础上再横向偏移多少像素（默认 0，正数向右，负数向左）。",
                "在錨點基礎上再橫向偏移多少像素（預設 0，正數向右，負數向左）。",
                "Extra horizontal offset in pixels from the anchor (default 0; positive moves right).",
                "アンカーからの横方向の追加オフセット（ピクセル、既定 0、正で右）。"));

            changed |= config.Describe("buttonOffsetY", D(
                "在锚点基础上再纵向偏移多少像素（默认 0，正数向上，负数向下）。",
                "在錨點基礎上再縱向偏移多少像素（預設 0，正數向上，負數向下）。",
                "Extra vertical offset in pixels from the anchor (default 0; positive moves up).",
                "アンカーからの縦方向の追加オフセット（ピクセル、既定 0、正で上）。"));

            return changed;
        }

        private static ConfigDoc D(string zhHans, string zhHant, string en, string ja)
            => ConfigDoc.Of(zhHans, zhHant, en, ja);
    }
}
