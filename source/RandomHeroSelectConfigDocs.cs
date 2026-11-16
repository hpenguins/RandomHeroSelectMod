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
                "总开关：开启后，进入选人阶段会自动随机点卡并落定。",
                "總開關：開啟後，進入選人階段會自動隨機點卡並落定。",
                "Master switch: when on, randomly shuffles hero cards and picks one once selection starts.",
                "全体スイッチ：オンにすると、キャラクター選択開始時に自動でランダムにカードを選び確定します。"));

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
                "进入选人阶段后先等多久再开始（毫秒，0–10000，默认 600），用于等卡面渲染完。",
                "進入選人階段後先等多久再開始（毫秒，0～10000，預設 600），用於等卡面渲染完成。",
                "Delay before starting once selection opens, in milliseconds (0–10000, default 600).",
                "選択画面が開いてから開始するまでの待ち時間（ミリ秒、0～10000、既定 600）。"));

            changed |= config.Describe("confirmDelayMilliseconds", D(
                "落定后等多久再点确定（毫秒，0–10000，默认 400）。",
                "落定後等多久再點確定（毫秒、0～10000、預設 400）。",
                "Delay before confirming the final pick, in milliseconds (0–10000, default 400).",
                "最終選択を確定するまでの待ち時間（ミリ秒、0～10000、既定 400）。"));

            changed |= config.Describe("autoConfirm", D(
                "是否自动点确定；关闭后只落定不确认，由你手动确认。",
                "是否自動點確定；關閉後只落定不確認，由你手動確認。",
                "Automatically confirm the pick; turn off to confirm manually.",
                "自動で確定するか。オフにすると手動で確定します。"));

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
                "手动触发一次随机选人的快捷键（默认空，留空不绑定）。",
                "手動觸發一次隨機選人的快捷鍵（預設空，留空不綁定）。",
                "Hotkey to trigger a random pick manually (default empty = disabled).",
                "手動でランダム選択を開始するホットキー（既定空＝無効）。"));

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

            return changed;
        }

        private static ConfigDoc D(string zhHans, string zhHant, string en, string ja)
            => ConfigDoc.Of(zhHans, zhHant, en, ja);
    }
}
