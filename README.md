# 随机选人 RandomHeroSelectMod 1.0.0

选人阶段自动「模拟随机点击」多张满级角色卡，最后随机落定一个并自动确认。被其他玩家抢走时会自动重抽。

## 主要特性

- **随机点卡**：进入选人阶段后，按洗好的随机顺序逐张触发游戏原生的角色卡点击，你能看到选中框一张张跳过去。
- **随机落定**：乱点结束后，从候选里随机挑一个作为终选，并补一次真实点击。
- **自动确认**：停一下让动画走完，再触发游戏原生的「确定」按钮。
- **满级筛选**：默认只抽「已拥有 + PVE 升星满级」的角色，满级阈值从配置表读取（当前 6 级），可手动覆盖。
- **安全剔除**：本局禁选、已被别人占用、场馆未解锁、卡面置灰的角色一律不进候选。
- **被抢重抽**：落定后被别人抢先确认时，自动从剩余候选里重抽，最多 3 次。
- **可选皮肤确认**：默认关闭；开启后在角色确认后自动选定并确认一个可用皮肤。

点击走的是游戏自己的按钮回调（`onClick.Call()`），因此选中动画、音效、气泡提示和发包全部是原生行为。未安装本模组的其他玩家不受影响。

## 安装

需要 CesiumLoader SDK 3.8.5 或更高版本。将 `RandomHeroSelectMod` 文件夹复制到游戏 `AstralParty_ModLoader/mods/` 目录下，重启游戏即可生效。

```text
AstralParty_ModLoader/
└── mods/
    └── RandomHeroSelectMod/
        ├── RandomHeroSelectMod.dll
        ├── RandomHeroSelectMod.json
        └── config.json
```

## 配置

修改 `mods\RandomHeroSelectMod\config.json`，保存后约 1 秒自动热重载（也可在 AstralParty.Toys 的模组配置页修改）。

| 字段 | 默认 | 说明 |
| --- | --- | --- |
| `enabled` | `true` | 总开关 |
| `pveLevel` | `0` | 视为「满级」的 PVE 升星等级；`0` = 自动读取配置表当前上限（现为 6） |
| `requireOwned` | `true` | 只抽已拥有的角色；关闭后试用角色也进候选 |
| `shuffleMinClicks` | `22` | 乱点时最少点几张卡（1–99） |
| `shuffleMaxClicks` | `34` | 乱点时最多点几张卡（1–99） |
| `clickIntervalMs` | `80` | 两次点卡的间隔（0–2000），实际上下抖动约 20% |
| `delayMilliseconds` | `600` | 进入选人阶段后先等多久再开始（0–10000） |
| `confirmDelayMilliseconds` | `400` | 落定后等多久再点确定（0–10000） |
| `autoConfirm` | `true` | 自动点确定；关闭后只落定不确认 |
| `rerollOnTaken` | `true` | 被抢后自动重抽 |
| `maxRetry` | `3` | 最多重抽几次（0–9） |
| `autoConfirmSkin` | `false` | 角色确认后是否自动确认皮肤 |
| `triggerKey` | 空 | 手动触发一次随机选人的快捷键，留空不绑定 |
| `rerollKey` | 空 | 手动重抽目标的快捷键，留空不绑定 |
| `randomSeed` | `0` | 随机种子；`0` 为真随机，非 0 可复现同一套点击顺序 |

### 满级口径

「满级」指 **PVE 升星等级**（选人卡面右下角显示的数字），上限取自 `PVENurturance.Levelups` 的最后一项。它与角色好感度等级（1–5）无关。

PVP 没有 PVE 升星等级概念，此时只按「已拥有 + 未被禁 + 未被占用」筛选。

### 节奏调优

默认 22–34 次点击、间隔约 80ms，整段约 2–3 秒走完。想更花哨可以把 `shuffleMaxClicks` 提到 40+；想更安静就降到 5–10。

每次点击都会向服务器广播一条「我当前选谁」，这是模拟点击的固有行为，但**只有最后的「确定」才会真正落实**。若担心请求量过大，可以调大 `clickIntervalMs` 或减少点击次数。

## 许可

AGPL-3.0
