# 《障眼法》GDD（可施工版 v2.0）

> **这份文件的唯一目的**：另一个 Agent（或另一个你）**只读这一份**，就能从零搭出这款游戏。
> 上游文档：`障眼法-推荐方案-细节.md`（设计意图与论证）→ 本文件（施工口径）→ 代码。
> 冲突时**以本文件为准**；本文件没写的，回上游文档找理由；两处都没有的，按第 26 节的"设计红线"自行决定并记录。

**如果你是接手的 Agent，按这个顺序读**：**第 0 节（v2 改版，先读）** → 第 1 节（是什么）→ 第 3 节（怎么玩）→ 第 4/5 节（长什么样）→ 第 12/13 节（判定与数据结构）→ 第 14 节（十幕内容）→ 第 17 节（代码怎么分）→ 第 18 节（Unity 怎么搭）→ 第 19/20 节（素材清单）→ 第 23 节（验收）。**第 17、18 节决定你能不能明天就跑起来，别跳。**

---

## 0. v2 改版（2026-10-24 团队定稿，与下文冲突时以本节为准）

> 下文第 1–27 节是 v1 的施工口径，仍然有效，**除非本节改写了它**。本节先列已经落地的决策，再写新增的两段式玩法。

### 0.1 已落地的工程与画面决策

| 项 | v1 写法 | v2 定稿 |
|---|---|---|
| 引擎 | 6000.3.26f1 · Built-in | **Unity 6000.6.5f1 · URP Universal Renderer**（3D 舞台需要真光照和阴影） |
| 输入 | 旧 Input Manager | **新 Input System**（`Keyboard.current`） |
| 画面 | 纯 2D 平面 | **3D 灰盒舞台 + 2D 纸片剪影**（类似《旁观者》）。剪影不受光、保持近黑，另挂只投影子的面片 |
| 相机 | 正交、正对台口 | 仍正交，**绕 x 轴俯视 8°** 露出地板进深；只绕 x 轴转，所有按 x 的判定不变 |
| 追光 | 黄色贴图 | **从正上方往下打的圆锥**：一盏 Spot Light + 半透明截头圆锥光柱 + 光柱里的浮尘 |
| 调色板 | 灰色系 | 近黑 `#0E0B0A`、暖棕 `#4A3426`、旧纸 `#D9CBB0`、深红 `#5A1418`；**黄 `#FFD21E` 仍然只给追光** |
| 渐变 / 暗角 | 禁止 | 放开：URP 后处理加胶片颗粒、轻微暗角、暖色调 |
| 观众 | 画面里看不见 | **台前一排观众剪影**压在画面底边；每个人的头绕脖子转，跟着追光看 |
| 素材 | — | `Tools/Art/gen_art.py` 生成，PNG 走 Git LFS；场景由菜单 `Sleight > 搭建 Game 场景` 生成 |

### 0.2 一幕分两段：搭台 → 表演

只躲光、按键太单薄，撑不到 10–15 分钟。v2 把被砍掉的"幕后搭台"废案接回来，每一幕都是"手忙脚乱"接"屏住呼吸"：

```
预览 2.0s  ──→  搭台（大幕关着，限时）  ──→  开幕 1.0s  ──→  表演（v1 的追光玩法）  ──→  判定
   看一眼成品图        用"手"把道具摆到位          检查道具是否到位       趁暗补救状态、最后呈现
```

| 段 | 画面 | 玩家要做的 | 失败 |
|---|---|---|---|
| **预览** | 大幕前浮出一张"成品图"卡片（剪影画出每个道具该在的位置和状态，无字），2.0s 后收起 | 记住 | — |
| **搭台** | 大幕关着，追光熄灭，只有一盏昏暗的工作灯。幕绳随时间慢慢拉紧，音乐渐响（**不出倒计时数字**） | A/D 走动，方向键控制魔术师的一只手，按住空格抓起道具、松开放下，把道具从两侧道具架搬到成品图上的槽位 | 时间到大幕自动拉开；**有目标道具不在它的槽位上 → 当场露馅** |
| **开幕** | 大幕升起 1.0s，追光亮起落在 `startZone` | — | — |
| **表演** | v1 第 3–12 节全部规则：光照到的半区不能动手，按 E 翻道具状态，空格呈现 | 补救搭台时没翻对的**状态**（位置这时已经不能改），最后在窗口内呈现 | v1 第 12.1 节的判定 |

- 搭台和表演用同一套 A/D 走动；**手只在搭台段出现**，表演段保持 v1 的按 E 交互，好上手。
- 成品图同时规定**位置**（槽位）和**状态**。位置只能在搭台时摆，状态两段都能改。搭台时把状态也翻对，表演就轻松；来不及，就得在光底下找机会补。

### 0.3 搭台段的操控（新增）

| 动作 | 键 | 参数 |
|---|---|---|
| 走 | A / D | 2.0 单位/秒（同 v1） |
| 移动手 | ↑ ↓ ← → | 手相对肩膀移动，速度 3.0 单位/秒，可达半径 **1.4 单位**，手不能低于地面线 |
| 抓 | **按住**空格 | 抓起手 **0.35 单位**内最近的道具，道具跟着手走（不做物理，硬跟随） |
| 放 | 松开空格 | 道具竖直落到地面线 y = 1.0，x 取松手处；离某个槽位 **≤ 0.3 单位**就吸附到槽位，并响一声轻"咔" |
| 翻状态 | **短按**空格（< 0.2s）对着道具 | 开合箱、掀盖布、翻镜，同 v1 的二态 |

- 抓着道具时走动，道具跟着人走；抓着东西不能翻状态。
- 手的画法：魔术师那只三指手单独一张剪影，用一条细线连到肩膀，像提线木偶。

### 0.4 失败与重来

- **只重来当前这一段**，全程 ≤ 1.0s，沿用 v1 第 12.2 节的铃 → 闪白 → 静音 0.35s。
- 搭台露馅：道具回道具架、状态回初始，重新开始搭台（不再看预览）。
- 表演露馅：道具位置保留搭台结果，状态回开幕时的样子，玩家回起点，追光回 `startZone`。
- 这取代了上一版"判定没过不重开"的临时处理（`ActRunner.resetOnFail`）。

### 0.5 三关各加一个花样

| 关 | dwell | 搭台限时 | 新花样 |
|---|---|---|---|
| 1 | 3.0s | 35s | 只学搭台和躲光 |
| 2 | 2.6s | 35s | **镜子反光**：镜子朝外时，追光落在镜子所在半区，会在另一半区以镜子为中心的对称位置打出一块宽 1.5 单位的反光。反光覆盖的地方同样不能动手、同样算"被看见"。翻镜的时机就是这一关的谜题 |
| 3 | 2.2s | 40s | **他的头会出卖他**：整排观众都跟着光看，只有前排的"他"每隔一段时间偷看暗处 1.2s。偷看前 0.6s，他的头会先转过去（和所有人方向相反）。他偷看期间在暗处动手 = 露馅 |

- 第 2 关起，道具架上会多放 1 个**干扰道具**（成品图里没有它）；开幕时它不能在任何槽位上。
- 第 3 关的"他"不是规则文字，只靠头的朝向来教：玩家第一次被他抓到时，会看见那颗头正对着自己。

### 0.6 时长估算

每幕：预览 2s + 搭台 25–40s + 开幕 1s + 表演 30–60s ≈ 1.2–1.7 分钟。9 幕一次过约 11–14 分钟，加上结局和正常的重试，落在 **10–15 分钟**。

### 0.7 数据结构增量（对第 13 节的补充）

```json
{
  "actId": "L2A1",
  "setupTime": 35,
  "props": [
    { "slot": "A.box", "type": "box", "startX": 0.6, "initial": "open", "target": "sealed" }
  ],
  "decoys": [ { "type": "cloth", "startX": 9.4, "initial": "off" } ],
  "mirrorReflect": true,
  "watcherPeek": { "interval": 6.0, "duration": 1.2, "warn": 0.6 }
}
```

- `startX`：道具在道具架上的起点（左架 x ≈ 0.6，右架 x ≈ 9.4）；`slot` 变成"该放到哪"。
- 开幕检查（新增，先于第 12.1 节）：每个 `props[i]` 都有一个同类型道具在它的 `slot` 上，且任何 `decoys` 都不在槽位上；否则失败码 `F5_SetupIncomplete`。

### 0.8 代码改动预告（对第 17 节的补充）

| 新增 / 改动 | 职责 |
|---|---|
| `ActPhase`（`ActRunner` 内） | `Preview → Setup → Curtain → Show`，每段各自计时 |
| `HandController.cs` | 搭台段的手：方向键移动、按住抓、短按翻、松开吸附槽位 |
| `Curtain.cs` | 3D 大幕的落下 / 升起，幕绳收紧表现剩余时间 |
| `PreviewCard.cs` | 成品图卡片：按本幕数据用道具剪影拼出来，不另外出图 |
| `MirrorReflection.cs` | 第 2 关的反光块；`PlayerController.CanAct()` 的"被照到"要把它算进去（仍然只有一处实现） |
| `WatcherPeek.cs` | 第 3 关"他"的偷看节奏，控制他的头，并给 `CanAct()` 提供"暗处也被看着"的信号 |

---

## 1. 事实表

| 项 | 值 |
|---|---|
| 名称（暂定） | 《障眼法》/ *Sleight* |
| 类型 | 2D 横版 · 潜行/时机 · 单屏 · 无战斗 |
| 一句话 | 他看向哪，哪半边就不能动手——你要在他回头看的那一刻之前，把舞台弄成"本来就该是这样"。 |
| 平台 | WebGL（itch.io 内嵌）优先，Windows 桌面版同日构建 |
| 引擎 | ~~Unity 6000.3.26f1 · Built-in~~ → **Unity 6000.6.5f1 · URP**（见第 0.1 节） |
| 语言 | C#，无第三方插件 |
| 逻辑分辨率 | **640 × 360**，PPU **64**，1 单位 = 64px |
| 摄像机 | 正交，固定不动，`orthographicSize = 2.8125`，可见 **10 × 5.6 单位** |
| 内容量 | **3 关 × 3 幕 = 9 幕 + 结局 1 幕 = 10 幕** |
| 单次通关 | ~~4–6 分钟~~ → **10–15 分钟**（见第 0.6 节） |
| 文本量 | **零**。没有台词、没有名字、没有旁白、没有教程文字 |
| 存档 | **不做**。刷新即从头 |
| 目标 | 72 小时 Game Jam 可提交、可完整通关、评审 10 分钟内玩完 |

---

## 2. 设计支柱（冲突时的裁决依据，从上到下）

1. **读得懂优先于好看。** 玩家必须在 0.5 秒内从画面读出"哪半边现在能动手"。任何影响这条的美术或特效都砍。
2. **信息只有一个来源。** 追光 = 他在看哪。不做血条、不做箭头、不做提示图标。
3. **不手滑即死。** 误操作只失效，不判负；判负只发生在"他回头看你的那一刻"。
4. **重开就是惩罚。** 不做死亡动画、不扣命、不弹字。1 秒内回本幕起点。

### 2.1 这不是什么（防止别人替你加需求）

不是潜行暗杀、不是解谜箱庭、不是节奏音游、不是叙事 Demo、不是多结局、不是关卡生成器、不是手机竖屏游戏。

---

## 3. 核心循环

### 3.1 三个时间尺度

| 尺度 | 循环 |
|---|---|
| **秒级**（0.5–4s） | 看光在哪 → 判断能不能动手 → 走过去 / 按一下 |
| **幕级**（15–35s） | 趁暗摆道具 → 等光回来 → 在窗口内"呈现" → 他看见：一切正常 |
| **关级**（60–110s） | 连过三幕，道具目标逐幕变多 |

### 3.2 玩家动词（只有四个）

| 动词 | 按键 | 说明 |
|---|---|---|
| 走 | ← / → 或 A / D | 全场自由走，**被光照着也能走** |
| 动手（开合箱 / 掀盖布 / 翻镜） | E | 靠近道具按一下；**光下无效**（灰闪 + 闷响） |
| 呈现 | 空格 | 1.0s 不可打断的表演动作；**光下不能起手** |
| 等 | — | 不动手也是操作：站位与等待决定你赶不赶得上 |

### 3.3 一幕的完整时序（这是整个游戏的心跳）

```
t=0        光落定在 A区（他在看 A）    → B区可动手
t=0..3.0   你在 B区开箱、盖布、翻镜       → 光不动，你随便动
t=3.0      光开始移动（0.4s 缓动）       → 全场都不能动手（灰闪）
t=T-1.5    呈现窗口开始                  → 现在按空格，1.0s 后正好赶在光落下
t=T        光落定在被摆好的 B区         → 判定：画面对不对
```

**T = 光落定的时刻。呈现必须在 `[T-1.5, T]` 内完成。**
提前完成 → 他看见"这早就摆好了"，怀疑度按秒累加；到 T 还没完成 → 露馅。

---

## 4. 画面与摄像机

### 4.1 机位（关键，别画错）

- **正交、固定、无透视、无缩放、无跟随、无抖屏。**
- 机位在**舞台正面、观众那一侧**，正对台口——**不在舞台端头**（从端头只能拍到厚度，拍不出 A/B 两个半区）。
- 机位比观众高，让观众头顶落在画面底边之外 → 画面里看不见观众。
- 摄像机是场景里一个固定 Transform，`position = (5.0, 2.8, -10)`，`rotation = 0`，`orthographic = true`，`size = 2.8125`。
- **不做任何相机脚本**。屏幕左右就对应世界 x 0→10。

### 4.2 世界坐标（唯一权威）

```
 y
5.6 ┬──────────────────────────────────────  ← 画面顶
    │                                       │
4.2 ┼─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─  ← 布景天顶（墙的画面上沿），以上是纯色墙
    │   [箱] [布]  [助手位] [镜]  │  [箱] [布]  [助手位] [镜]
3.0 ┤                            │
1.0 ┼═══════════════════════════╪════════════════  ← 地面线（脚底贴这条线）
0.0 ┴──────────────────────────────────────
    0    1    2    3    4    5    6    7    8    9   10   x
      └────── A 区 ──────┘   └────── B 区 ──────┘
      1.4  2.6  3.6  4.4        6.2  7.4  8.4  9.2
```

| 常量 | 值 |
|---|---|
| 世界左边界 | x = 0 |
| 世界右边界 | x = 10 |
| 分区线 | x = 5.0（场景里一条 1px 虚线 sprite，纯装饰，无碰撞） |
| 地面线 | **y = 1.0**（脚底、道具底边都贴这条线） |
| 布景天顶 | y = 4.2 |
| 角色高度 | 3.0 单位（头 ≈ 4.0） |
| 摄像机中心 | **(5.0, 2.8)** |

> **推导**：`orthographicSize = 2.8125` = 半高，故全高 5.625；center.y = 2.8 → 视野 y ∈ [-0.0125, 5.6125]，正好装下 0→5.6。半宽 = 2.8125 × (640/360) = 5.0，故全宽正好 10。**这条别算错，算错整个画面的地面会离底边很远。**

### 4.3 道具槽位（固定坐标，数据只引用槽位名）

| 槽位名 | x | 说明 |
|---|---|---|
| `A.box` | 1.4 | A 区箱子 |
| `A.cloth` | 2.6 | A 区布（盖着一团东西） |
| `A.assist` | 3.6 | A 区助手位（地面标记） |
| `A.mirror` | 4.4 | A 区镜子 |
| `B.box` | 6.2 | B 区箱子 |
| `B.cloth` | 7.4 | B 区布 |
| `B.assist` | 8.4 | B 区助手位 |
| `B.mirror` | 9.2 | B 区镜子 |

所有道具的**底边 y = 1.0**，宽度见第 19 节素材表。玩家与道具的**交互半径 0.7 单位**（按 x 轴距离判定，不做 y 判定）。

> **站位与交互半径的冲突已避开**：助手位到最近道具的距离是 0.8（> 0.7），所以站在助手位上按 E 不会误触道具。改坐标时必须保住这条。

### 4.4 UI 布局

| 元素 | 位置（640×360 像素坐标） | 规格 |
|---|---|---|
| 幕进度 | 左下角，起点 (20, 330) | 3 个圆点，直径 10px，间距 12px；已完成实心 `#D0D0D6`，未完成空心描边 1px |
| 全屏闪白 | 覆盖全屏 | 纯白 1 帧（约 0.05s），alpha 1 → 0 用 4 帧 |
| 淡入淡出 | 覆盖全屏 | 纯黑，0.25s |

**没有其它 UI。** 没有文字、没有倒计时数字、没有血条、没有标题卡。

---

## 5. 视觉规范

### 5.1 调色板（只有这三个色系，违反即返工）

| 用途 | Hex | 说明 |
|---|---|---|
| 剪影 / 角色 / 道具 / 道具黑 | `#0B0B0C` | 世界里的"实" |
| 背景墙 | `#2E2E33` | 比角色亮，剪影才读得出 |
| 地面 | `#55555C` | 比墙亮一档 |
| 地脚线 | `#7C7C84` | 1px 亮线，读出地面 |
| 黄（唯一强调色） | `#FFD21E` | **只给追光** |
| UI 白 | `#D0D0D6` | 只有 UI |

- **全场不得出现第二处黄色**（铃、眼睛、字幕一律黑白灰）。
- **场景必须比角色暗 2 档**——这是剪影可读性的地基。
- 不做渐变背景、不做暗角、不做粒子（除闪白）。

### 5.2 排序层（Sorting Layer）

| 层 | 顺序 | 内容 |
|---|---|---|
| `BG` | 0 | 墙、地面、地脚线、分区线 |
| `Stage` | 10 | 舞台台口装饰（可选）、助手位标记 |
| `Props` | 20 | 箱、布、镜 |
| `Player` | 30 | 玩家 |
| `Light` | 40 | 追光（半透明，`Screen` 或普通 alpha 混合） |
| `HUD` | 50 | 幕进度点、闪白、淡入淡出 |

追光在 Props **之上**、玩家**之上**（光要盖住一切才能表现"被照到"），但在 HUD 之下。

### 5.3 可读性硬规则

1. 三个道具剪影必须在 64px 缩略下靠**外形**分辨：箱子=低方、布=圆顶蓬、镜=细高竖条。
2. 追光**硬边**，边缘加 6px 外侧微光（alpha 0.25），不做羽化。
3. 追光亮度 = `0.55 + 0.45 * (suspicion/100)`；满怀疑时接近纯亮黄并开始以 2px 幅度抖动。
4. 玩家满屏只有一处黄（追光）；**眼睛剪影只用白灰色**。

---

## 6. 音频规范

| 音效 | 触发 | 规格 |
|---|---|---|
| `sfx_bell` | 露馅（他按铃） | 单声道，85–100ms，高频带金属感；**全游戏最响的一条**，每次露馅必放 |
| `sfx_reveal` | 呈现在窗口内完成 | 120ms 上扬音（升 5 度），短 |
| `sfx_heartbeat` | 怀疑度 > 60 时循环 | 低频 2 拍/秒，音量随怀疑度线性升；< 60 时静音 |
| `sfx_ambience` | 全程循环 | 空场房间底噪，音量 -24dB，几乎听不见但不能没有 |
| `bgm_loop` | 全程循环 | 60s，极简（3 个音），音量 -18dB |

- 露馅时：`sfx_bell` → **全静音 0.35s** → 重开。这一下静音比音效本身重要。
- 呈现窗口开始（T-1.5）**不加**提示音——玩家靠看光，不靠听。

---

## 7. 输入

| 动作 | 键 | 缓冲 |
|---|---|---|
| 左 | ← / A | 立即 |
| 右 | → / D | 立即 |
| 交互 | E / 空格（备用） | **0.10s 输入缓冲**（在可交互帧前 0.1s 内按下也算） |
| 呈现 | 空格 | **0.15s 输入缓冲**，不可打断 |

**禁止**：跳跃、蹲、冲刺、鼠标点击。

---

## 8. 玩家角色

### 8.1 参数

| 参数 | 值 |
|---|---|
| 走速 | **2.0 单位/秒**（≈128 px/s） |
| 加减速 | 无（瞬时到速，瞬时停——可读性优先） |
| 碰撞体 | **不需要物理**。只需 x 夹在 [0.4, 9.6] |
| 交互半径 | 0.7 单位（x 距离） |
| 画布 | 128 × 192 px（= 2.0 × 3.0 单位），**脚底固定在画布 y=0 那一行** |
| 状态 | `Idle` / `Walk` / `Interact` / `Perform` / `Caught` |

### 8.2 状态机

| 当前 | 输入 | 下一状态 | 备注 |
|---|---|---|---|
| Idle | 左/右 | Walk | |
| Walk | 停下 | Idle | |
| Idle/Walk | E 且最近道具在 0.7 内且**可动手** | Interact | 播 2 帧，0.6s（布 0.5s），锁移动 |
| Idle/Walk | 空格 且**可起手** | Perform | 1.0s，锁移动，不可打断 |
| Interact/Perform | 结束 | Idle | 动作完成事件在角色播到最后一帧时发出 |
| Idle/Walk | E 空格 但**光下** | 不变 | 灰闪 0.1s + 闷响，不算失败 |

**"可动手/可起手"的判定（唯一来源）**：

```
可动手 = （追光处于落定状态）且（玩家所在半区 != 当前被照亮的半区）
```

- 追光移动的 0.4s 内，**全场不可动手**。
- 这条规则同时适用于交互和呈现，只有一处实现，不许各写一遍。

---

## 9. 追光（它就是他）

### 9.1 数据模型

每幕可以有 **N 束光**（v1 只用 1 束，字段保留数组）。

```json
{ "id": "main", "startZone": "A", "dwell": 3.0, "transition": 0.4, "startDelay": 0.0 }
```

- 行为循环：`落定在 zone` → 持续 `dwell` 秒 → `transition` 秒缓动到另一区 → 落定 → …
- **缓动曲线**：`smoothstep(0, 1, t/transition)`；位置 = 该区中心点之间的插值。
- 区中心：A = x 2.5，B = x 7.5。追光宽 **4.5 单位**，垂直覆盖 y 1.0→4.2。

### 9.2 位置与判定

- 追光是一个角色/贴图（256×256，pivot 顶点），随 `x` 平移 + 轻微缩放（宽 4.5 时缩放到覆盖 4.5×3.2）。
- **"被照到"不做像素测试**，用一个纯数学判定：

```
litZone = 当前光落定/正在覆盖的区（A 或 B）
playerIsLit = (player.x < 5.0 ? "A" : "B") == litZone
```

- 多束光时：`playerIsLit = 任一束光的覆盖面包含玩家 x 且该光未在移动`（v1 用不到，但接口留好）。

### 9.3 时间表接口（判定要用）

```
float NextArrival()        // 下一束光落定的时刻（可多次调用，必须幂等）
Zone  CurrentZone()        // 当前落定区
bool  IsSettled()          // 没在缓动
```

**`NextArrival()` 是呈现窗口判定的唯一依据**，不允许在别处重算时间。

---

## 10. 道具

### 10.1 三种道具 + 一个标记

| 道具 | 槽位 | 状态 A（错） | 状态 B（对） | 交互耗时 |
|---|---|---|---|---|
| 箱子 `box` | `*.box` | `open` 盖开着，里面有东西 | `sealed` 合上 | 0.6s |
| 布 `cloth` | `*.cloth` | `off` 被掀开，露出底下 | `on` 盖着 | 0.5s |
| 镜子 `mirror` | `*.mirror` | `normal` 镜面朝里 | `flipped` 镜面朝外 | 0.6s |
| 助手位 `assist` | `*.assist` | —— | 玩家站在上面 | 无交互 |

- **都是二态开关**：按一次翻一次。没有第三态、没有中间态动画（两帧硬切）。
- **镜子为什么重要**：镜面朝外时，观众看到的是镜子里"东西还在"的假象，所以镜子 = 障眼法的道具本体。
- **助手位**：不是可交互物，是**地面标记**。某些幕要求玩家在光落定那一刻站在标记上（±0.5 单位内）。

### 10.2 幕的初始态与目标态

每一幕在数据里给出每个 "被使用的道具槽" 的 `initial` 与 `target`。**没在本幕数据里出现的槽位，本幕不实例化**（画面上就是空的）。

> **为什么不做"无关道具"**：一个看着能按、按了没用、还要读它状态的物件，会把判定和画面的一致性破坏掉。v1 每幕只摆本幕要用的。

### 10.3 道具状态在幕内**不重置**

幕内翻错再翻回来，只花时间，不算失败。只有"光落定时状态不对"才算。

---

## 11. 呈现（核心动作）

### 11.1 定义

**呈现** = 玩家按空格，播 1.0s 的表演动作（抬手→亮相→收手）。它有两个作用：

1. 它是**本幕成功的唯一触发**：光落定时，最近一次"完成"必须落在窗口里。
2. 它在画面上是**你需要演的"那一下"**，没有它，把戏在画面上完全不可见。

### 11.2 时序判定（唯一实现）

设 `T = NextArrival()`（光下一次落定的时刻），`c = 本次呈现完成的时刻`：

| 条件 | 结果 |
|---|---|
| `c ∈ [T-1.5, T]` | **窗口内**：标记 `armed = true`，播放 `sfx_reveal`，目标区上方浮出眼睛 0.6s |
| `c < T-1.5` | **太早**：`suspicion += 25 * (T-1.5 - c)`，不 armed |
| `c > T` | **太晚**：光落定时呈现还没收手 → 该次不算，`armed` 保持 false → **F2 露馅** |

- 光落定时若 `armed == true` → 进入判定；否则 **F2 露馅**。
- **反直觉的陷阱与它的解**：起手最晚只能到 `T-1.0`（因为它要 1.0s），而 `[T-1.0, T-0.4]` 这一段站得住、按下去也合法，却必然失败。这不是 bug，是本作唯一必须教给玩家的一句口诀：**光一动，你就晚了。** 追光的 0.4s 移动本身就是提示——看见它在动，就知道来不及了，别按。
- `armed` 之后**再按空格**会重新开始 1.0s，`c` 更新，**`armed` 立即清空重算** → 若重按发生在 `T-1.0` 之后，你必然失败。**演完就别再按了。** 这条不需要文字教学：玩家会看见自己又把戏演了一遍，光还没落下，然后铃响。
- `armed` 之后**移动或翻道具不取消 armed**，但会让判定不通过（因为状态变了）——这是故意的：演完就别动。

### 11.3 窗口参数

| 参数 | 值 | 可调范围 |
|---|---|---|
| 呈现时长 | 1.0s | 0.8–1.2 |
| 提前量窗口 | 1.5s | 1.2–2.0 |
| 太早惩罚 | 25/秒 | 15–35 |

> **推导（改动前先看）**：呈现必须在光落定前 1.5s 内完成，而它本身要 1.0s，所以**起手必须落在 `[T-2.5, T-1.0]`**；又因为光移动的 0.4s 内不能起手，而这个区间整段都在移动之前，所以移动期不压缩窗口。真正约束是**暗窗长度**：目标区从"光落到另一半区"起才有暗窗，持续 `dwell` 秒，要求 `dwell ≥ 2.5 - 0.4 = 2.1s`。**2.2s 只余 0.1s 余量，这是第 15 节难度上限的来源，不许再低。**

---

## 12. 判定与失败

### 12.1 光落定那一刻，按顺序检查（首个失败即返回）

| # | 检查 | 失败码 |
|---|---|---|
| 1 | `armed == true` | `F2_PresentMissing` |
| 2 | `suspicion < 100` | `F3_SuspicionFull` |
| 3 | 本幕所有道具状态 == target | `F1_PropsMismatch` |
| 4 | 玩家所在半区 == `targetZone` | `F1_PlayerOutsideZone` |
| 5 | 若 `requireAssistant`：玩家距 `targetZone.assist` ≤ 0.5 | `F1_AssistantMissing` |

**通过则**：幕完成 → 播一次"灯亮"（追光闪一下）→ 0.6s → 进下一幕。

### 12.2 露馅流程（全过程 ≤ 1.0s）

```
铃(sfx_bell) → 全屏闪白 1 帧 → 全静音 0.35s → 本幕重置
```

- 本幕重置：玩家回 `playerStart`，道具回 `initial`，`suspicion = 0`，`armed = false`，追光回 `startZone`，计时器清零。
- **不扣命、不记次数、不弹字、不播死亡动画。**

### 12.3 什么不算失败（写进验收）

- 被光照着走路 → 不算。
- 光下按 E / 空格 → 不算，只失效。
- 道具翻错再翻回来 → 不算。
- 幕内时间没到但站着不动 → 不算（但会撞幕限时）。

### 12.4 幕限时

每幕 **45 秒**（从幕开始算）。超时 = 露馅（`F4_Timeout`），走同一套重开流程。定位是**防卡死安全网**，不是难度来源；不显示数字。

---

## 13. 数据结构（可直接落 JSON / ScriptableObject）

### 13.1 幕（Act）

```csharp
[Serializable]
public class ActData {
    public string actId;          // "L1A1"
    public int level;             // 1..3
    public int index;             // 1..3（结局幕为 0）
    public float playerStart;     // x
    public string targetZone;     // "A" | "B" | null（结局幕）
    public PropSpec[] props;      // 本幕实例化的道具
    public bool requireAssistant;
    public LightSpec[] lights;    // v1 长度 1
    public float actTimeLimit = 45f;
    public bool isEnding;
    public string designNote;     // 给美术/关卡看的备注，游戏内不显示
}

[Serializable]
public class PropSpec {
    public string slot;     // "B.box" / "A.cloth" / "B.mirror"
    public string type;     // "box" | "cloth" | "mirror"
    public string initial;  // "open" | "sealed" | "on" | "off" | "normal" | "flipped"
    public string target;   // 同上
}

[Serializable]
public class LightSpec {
    public string id;
    public string startZone;   // "A" | "B"
    public float dwell = 3.0f;
    public float transition = 0.4f;
    public float startDelay = 0f;
}
```

### 13.2 完整示例（第 1 关第 1 幕，可照抄）

```json
{
  "actId": "L1A1",
  "level": 1,
  "index": 1,
  "playerStart": 0.8,
  "targetZone": "B",
  "props": [
    { "slot": "B.box", "type": "box", "initial": "open", "target": "sealed" }
  ],
  "requireAssistant": false,
  "lights": [
    { "id": "main", "startZone": "A", "dwell": 3.0, "transition": 0.4, "startDelay": 0 }
  ],
  "actTimeLimit": 45,
  "isEnding": false,
  "designNote": "教学：光在 A 时，B 区随便动。只要求一个动作，让玩家学会'光 = 禁手'。"
}
```

### 13.3 运行时状态（ActState）

```csharp
public class ActState {
    public float time;              // 幕内时间
    public bool armed;              // 呈现已在窗口内完成
    public float lastPresentAt;     // 最近一次完成时刻
    public float suspicion;         // 0..100，幕内累计
    public Dictionary<string,string> propStates;
    public bool judged;
}
```

---

## 14. 十幕内容（施工清单）

**通用**：玩家起点见表；光落定区 ≠ 目标区时是玩家的动手窗口；`dwell` 由关决定（关 1 = 3.0s，关 2 = 2.6s，关 3 = 2.2s）；`transition = 0.4s`。

### 14.1 第 1 关（dwell 3.0s）

| 幕 | 起点 x | 目标区 | 道具（初始 → 目标） | 助手位 | 光起始区 | 设计意图 |
|---|---|---|---|---|---|---|
| **L1A1** 先把手藏起来 | 0.8 | B | `B.box` open→sealed | — | A | 只学一件事：光在 A，B 能动 |
| **L1A2** 盖住 | 4.6 | A | `A.cloth` off→on；`A.box` open→sealed | — | B | 两个动作要占两段暗窗 |
| **L1A3** 镜子 | 3.2 | B | `B.mirror` normal→flipped；`B.box` open→sealed | ✅ | A | 首次引入"人也要站到位" |

### 14.2 第 2 关（dwell 2.6s）

| 幕 | 起点 x | 目标区 | 道具（初始 → 目标） | 助手位 | 光起始区 | 设计意图 |
|---|---|---|---|---|---|---|
| **L2A1** 他在看别处 | 1.0 | A | `A.box` open→sealed；`A.cloth` off→on | — | B | 窗口变短，练习"分两个窗口做完" |
| **L2A2** 人不在也算错 | 5.2 | B | `B.mirror` normal→flipped | ✅ | A | 只一件事，但两个条件同时成立 |
| **L2A3** 三件一起 | 6.0 | A | `A.box` open→sealed；`A.cloth` off→on；`A.mirror` normal→flipped | — | B | 第一个真正要"排期"的幕 |

### 14.3 第 3 关（dwell 2.2s）

| 幕 | 起点 x | 目标区 | 道具（初始 → 目标） | 助手位 | 光起始区 | 设计意图 |
|---|---|---|---|---|---|---|
| **L3A1** 快 | 1.2 | B | `B.box` open→sealed；`B.mirror` normal→flipped | — | A | 窗口逼近下限，要求贴准 T-1.5 |
| **L3A2** 两边都有人在看 | 3.6 | A | `A.cloth` off→on；`A.box` open→sealed；`A.mirror` normal→flipped | ✅ | B | 三个道具 + 站位，全游戏最挤的一幕 |
| **L3A3** 最后一场 | 6.2 | B | `B.box` open→sealed；`B.cloth` off→on；`B.mirror` normal→flipped | ✅ | A | 全部机制叠满，通关 |

### 14.4 结局幕（LEND）

| 项 | 值 |
|---|---|
| 光 | 无（`lights` 空数组） |
| 目标区 | null |
| 计时 | 无 |
| 判定 | 无 |
| 流程 | 进场 2.0s（灯逐渐灭）→ 停 3.0s（他在走动，脚步声）→ 眼睛消失 → 淡出 1.0s → 出标题（纯图形，无字） |

> 结局**不接受输入**，只能看完。玩家唯一能做的就是站着——这正是设计意图。

### 14.5 每幕的预期解法（验收时对一遍）

| 幕 | 预期解法 |
|---|---|
| L1A1 | 从左端起步走到 B 箱子（约 5.4 单位 ≈ 2.7s）→ 合上 → 等光回来时按空格 |
| L1A2 | 从 B 侧回 A，盖布 → 等下一个暗窗，合箱 → 呈现 |
| L1A3 | 翻镜 → 站上 `B.assist` → 呈现（注意：站位要在呈现之前完成） |
| L2A1 | 暗窗 1：合箱；暗窗 2：盖布；暗窗 3：呈现 |
| L2A2 | 翻镜 → 站到 `B.assist` → 呈现 |
| L2A3 | 三个暗窗各做一件 → 第 4 个暗窗呈现 |
| L3A1 | 两个暗窗（每窗一件）→ 呈现 |
| L3A2 | 三个暗窗做三件（其中一件在站上助手位之后）→ 呈现 |
| L3A3 | 同上，且暗窗只有 2.2s，必须一窗一件、不能走错 |

---

## 15. 难度曲线与数值上限

| 关 | dwell | 暗窗时长 | 一窗能做几件事 | 幕数 | 典型用时 |
|---|---|---|---|---|---|
| 1 | 3.0s | ≈3.0s | 1 件（+短距离移动） | 3 | 60–90s |
| 2 | 2.6s | ≈2.6s | 1 件 | 3 | 70–100s |
| 3 | 2.2s | ≈2.2s | 1 件（不能有走错） | 3 | 80–110s |

**硬上限**：`dwell` 不得低于 **2.2s**。低于它，起手窗口 `[T-2.5, T-1.0]` 会掉进光的移动区，玩家无法稳定完成——这是本作唯一的"手感悬崖"。

**关 1 免幕限时？不。** 三关都设 45s，因为它是防卡死网，不是难度。关 1 的正常用时远小于 45s，不会触发。

---

## 16. 流程与状态机（全局）

```
Boot（0.5s 黑屏）
  └→ Level 1: Act1 → Act2 → Act3
       └→ Level 2: Act1 → Act2 → Act3
            └→ Level 3: Act1 → Act2 → Act3
                 └→ Ending（不可跳过）→ 标题 → 结束（可重开）
```

- 幕完成：追光闪一下（0.15s 变亮）→ 0.6s 空转 → 下一幕。
- 关完成：0.3s 淡黑 → 下一关第一幕。
- **没有选关、没有暂停菜单、没有设置界面**（WebGL 里按 F 全屏由 itch 负责）。

---

## 17. 代码架构（照这个分，别自创）

### 17.1 脚本清单（8 个，全部单场景）

| 文件 | 职责 | 关键成员 |
|---|---|---|
| `GameFlow.cs` | 全局流程：加载幕、推进、结局、重开 | `LoadAct(ActData)`, `OnActPassed()`, `RestartAct()` |
| `ActRunner.cs` | 单幕宿主：计时、收事件、调判定、播露馅 | `Update()`, `TryJudge()`, `Fail(FailReason)` |
| `ActData.cs` | 数据结构 + JSON 加载（`Resources/acts.json`） | `static ActData[] LoadAll()` |
| `PlayerController.cs` | 移动、交互、呈现、缓冲 | `bool CanAct()`, `bool CanPerform()`, `bool IsOnAssist()` |
| `Spotlight.cs` | 单束光：节拍、缓动、亮度 | `float NextArrival()`, `Zone CurrentZone()`, `bool IsSettled()`, `bool Covers(float x)` |
| `PropInteractable.cs` | 单个道具：二态、动画、交互 | `string State`, `void Toggle()` |
| `Judgement.cs` | **纯函数**判定（无 MonoBehaviour，便于单测） | `static Verdict Judge(ActData d, ActState s, PlayerSnapshot p, float suspicion)` |
| `HudAudio.cs` | 幕点、闪白、淡黑、音效 | `SetActDots(int)`, `FlashWhite()`, `PlayBell()`, `PlayReveal()` |

### 17.2 每帧顺序（必须在同一个 Update 里按此顺序，或用 Script Execution Order）

```
1. PlayerController  读输入 → 移动/动作状态机
2. PropInteractable  处理"动作完成"事件（由玩家播完动画触发）
3. Spotlight         推进时间轴；到落定时刻发 OnLightSettled
4. ActRunner         OnLightSettled → Judge → Pass/Fail；同时推进幕计时
5. HudAudio          播反馈
```

**禁止**在 `Spotlight` 里调 `Judge`，也禁止在 `ActRunner` 里自己算 `NextArrival()`。**时间只由 Spotlight 算一次。**

### 17.3 判定纯函数（可直接抄）

```csharp
public static Verdict Judge(ActData d, ActState s, PlayerSnapshot p, float suspicion)
{
    if (s.isEnding) return Verdict.Pass();
    if (!s.armed)                     return Verdict.Fail(FailReason.PresentMissing);
    if (suspicion >= 100f)            return Verdict.Fail(FailReason.SuspicionFull);

    foreach (var spec in d.props)
        if (s.propStates[spec.slot] != spec.target)
            return Verdict.Fail(FailReason.PropsMismatch);

    if (p.zone != d.targetZone)       return Verdict.Fail(FailReason.PlayerOutsideZone);

    if (d.requireAssistant && p.assistDistance > 0.5f)
        return Verdict.Fail(FailReason.AssistantMissing);

    return Verdict.Pass();
}
```

### 17.4 呈现完成的代码（唯一一处写 suspicion 的地方）

```csharp
void OnPerformCompleted() {
    float T = spotlight.NextArrival();
    float c = Time.time - actStart;
    if (c >= T - 1.5f && c <= T) {
        state.armed = true;
        state.lastPresentAt = c;
        hud.PlayReveal();
        hud.ShowEye(targetZone, 0.6f);
    } else if (c < T - 1.5f) {
        state.suspicion += 25f * (T - 1.5f - c);
    }
}
```

### 17.5 事件流

```
LightSettled(zone) ──┐
                     ├─→ ActRunner.TryJudge() ──→ Pass → GameFlow.OnActPassed()
TimeLimitHit ────────┘                        └─→ Fail → ActRunner.PlayFail() → GameFlow.RestartAct()
```

---

## 18. Unity 从零搭建（照抄即可）

### 18.1 工程

1. Unity Hub → New Project → **2D (Built-In Render Pipeline)** → 版本 `6000.3.26f1`。
2. 项目名 `Sleight`，路径 `/Users/maolinlu/Unity Projects/GameJam202610/Sleight`。
3. **不装任何包**（不用 URP、不用新 Input System、不用 Cinemachine、不用 TextMeshPro）。
4. `Edit > Project Settings > Player > Resolution and Presentation`：`Default Screen Width 640 / Height 360`，`Fullscreen Mode: Windowed`。
5. `Project Settings > Graphics`：保持 Built-in；`Quality`：只留一档，关掉所有抗锯齿（像素级清晰度靠整数缩放）。

### 18.2 场景（单场景 `Scenes/Game.unity`）

层级结构：

```
Game
├── MainCamera            (Orthographic, size 2.8125, pos (5, 2.8, -10), Clear #101012)
├── Stage
│   ├── BG_Wall           (Sprite, 10×5.6, sorting BG, color #2E2E33)
│   ├── BG_Floor          (Sprite, 10×0.98, pos y 0.51, color #55555C, sorting BG)
│   ├── BG_FootLine       (Sprite, 10×0.02, pos y 1.0, color #7C7C84, sorting BG)
│   └── BG_Divider        (Sprite 0.02×3.2, pos (5, 2.6), color #3A3A40, sorting BG)
├── Spotlights
│   └── Light_Main        (Sprite 追光, sorting Light, Spotlight.cs)
├── Props                 (空物体，运行时按幕生成子物体)
├── Player                (Sprite Renderer + PlayerController)
└── HUD
    ├── Canvas            (Screen Space Overlay, CanvasScaler 640×360, Match 1)
    ├── ActDots           (3 个 Image)
    ├── FlashWhite        (全屏 Image, 初始 alpha 0)
    └── FadeBlack         (全屏 Image, 初始 alpha 0)
```

### 18.3 预制体

| 预制体 | 组成 |
|---|---|
| `P_Player` | SpriteRenderer(Player 层) + Animator + PlayerController |
| `P_Prop_Box` / `P_Prop_Cloth` / `P_Prop_Mirror` | SpriteRenderer(Props 层) + PropInteractable |
| `P_Marker_Assist` | SpriteRenderer(Stage 层)，半透明白灰椭圆，`sortingStage` 10 |
| `P_Spot` | SpriteRenderer(Light 层) + Spotlight |
| `P_EyeFlash` | SpriteRenderer(白灰眼睛剪影)，默认 inactive |

### 18.4 输入（旧 Input Manager）

只用 `Input.GetAxisRaw("Horizontal")` + `Input.GetKeyDown(KeyCode.E / Space)`。**不要接新 Input System**，会多花半天。

### 18.5 数据加载

- 把 `acts.json` 放 `Assets/Resources/`，用 `JsonUtility.FromJson<ActWrapper>(text)` 读。
- 或者改成本地 `ScriptableObject` 列表（可读性更好，但手写 JSON 更快）。**v1 用 JSON。**

### 18.6 素材导入设置（每一张图都要）

| 设置 | 值 |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Pixels Per Unit | **64** |
| Filter Mode | **Point (no filter)** |
| Compression | None |
| Mesh Type | Full Rect |
| Pivot | 脚部/底边：`Bottom Center`；追光：`Top Center` |

### 18.7 动画的最省做法（别建 Animator 状态机）

Prefab 上挂 `Animator` 会多出一整套状态机、过渡、参数，72 小时里不值。**改用代码逐帧换图**：

```csharp
public class FrameAnim : MonoBehaviour {
    public Sprite[] frames;
    public float fps = 12f;
    public bool loop = true;
    SpriteRenderer sr; float t; int i;
    void Awake(){ sr = GetComponent<SpriteRenderer>(); }
    public void Play(Sprite[] f, float f2, bool lp){ frames=f; fps=f2; loop=lp; t=0; i=0; sr.sprite=frames[0]; }
    public bool Done => !loop && i >= frames.Length-1;
    void Update(){
        if (frames == null || frames.Length == 0) return;
        t += Time.deltaTime;
        if (t < 1f/fps) return;
        t = 0;
        if (i < frames.Length-1) { i++; sr.sprite = frames[i]; }
        else if (loop) { i = 0; sr.sprite = frames[0]; }
    }
}
```

- 走 = `frames=[walk_00..03]`, `fps=10`, loop
- 待机 = `[idle_00..01]`, `fps=2`, loop
- 动手 = `[act_00..01]`, `fps = 帧数/时长`, 不循环；播放结束事件用来"真正改道具状态"
- 呈现 = `[perform_00..02]`, 时长必须精确 1.0s（`fps = 3/1.0 = 3`），不循环

> **注意**：动手/呈现的"完成时刻"必须以**动画播完**为准，不能用 `Invoke(duration)` 另开计时器——两个时间源会漂。

### 18.8 调试热键（只在 Editor 生效，构建里去掉）

| 键 | 作用 |
|---|---|
| `F1` | 跳过当前幕（直接判通过） |
| `F2` | 立即触发露馅（测重开流程） |
| `F3` | 当前幕重开 |
| `F4` | 打印当前判定明细（armed / 道具状态 / 怀疑度 / T） |

用 `#if UNITY_EDITOR` 包住。

---

## 19. 美术资产清单（共 26 张 + 1 张标题）

### 19.1 玩家（12 帧，128×192，脚底 y=0）

| 文件 | 帧数 | 说明 |
|---|---|---|
| `player_idle_00..01` | 2 | 站立呼吸 |
| `player_walk_00..03` | 4 | 行走 |
| `player_act_00..01` | 2 | 动手（伸手 → 收回） |
| `player_perform_00..02` | 3 | 呈现（抬手 → 亮相 → 收手） |
| `player_caught_00` | 1 | 被抓住定格（兼作露馅帧） |

### 19.2 道具（7 张，均贴地面线）

| 文件 | 尺寸 | 说明 |
|---|---|---|
| `prop_box_open` | 48×56 | 箱盖开，里面有东西露出 |
| `prop_box_sealed` | 48×44 | 合上 |
| `prop_cloth_off` | 72×40 | 布被掀开，露出底下的一团 |
| `prop_cloth_on` | 72×56 | 布盖着（圆顶蓬） |
| `prop_mirror_normal` | 40×112 | 细高竖条，镜面朝里（暗） |
| `prop_mirror_flipped` | 40×112 | 镜面朝外（一道亮边） |
| `prop_marker_assist` | 64×16 | 地面椭圆标记，半透明白灰 |

### 19.3 场景 / 光 / 特效（7 张）

| 文件 | 尺寸 | 说明 |
|---|---|---|
| `bg_wall` | 640×360 | 纯色 `#2E2E33`，可带极淡的竖向纹理 |
| `bg_floor` | 640×64 | `#55555C` |
| `bg_footline` | 640×2 | `#7C7C84` |
| `bg_divider` | 2×205 | `#3A3A40` |
| `spot_main` | 256×256 | 硬边扇形/锥形，pivot 顶点，纯黄 |
| `eye_flash` | 64×40 | 一只眼睛的剪影（白灰） |
| `title_card` | 640×360 | 结局后的标题（纯图形，**无文字**；若要字，用图形化的字标） |

### 19.4 出图方式

**脚本生成，不手绘。** 用 Python/PIL 或 Unity Editor 脚本按上面尺寸画纯色色块 + 简单多边形，改配色重跑即可。参考本仓库已有的 `障眼法-镜头示意图.png` 的画法（同一套调色板）。

---

## 20. 音效资产清单（5 条）

| 文件 | 时长 | 说明 |
|---|---|---|
| `sfx_bell.wav` | 0.1s | 最响，露馅 |
| `sfx_reveal.wav` | 0.12s | 上扬，窗口内完成 |
| `sfx_heartbeat.wav` | 1.0s 循环 | 怀疑度 > 60 时循环 |
| `sfx_ambience.wav` | 8s 循环 | 空场底噪 |
| `bgm_loop.wav` | 60s 循环 | 极简三音 |

免费素材站取，不做原创。

---

## 21. HUD 细节

- `ActDots`：3 个点，只显示当前关；进新关重置为 0/3。
- `FlashWhite`：露馅时 alpha 1，4 帧内到 0。
- `FadeBlack`：进关/结局用，0.25s 到 1，0.25s 到 0。
- **不做**怀疑条、不做倒计时、不做文字提示。

---

## 22. 构建

| 目标 | 设置 |
|---|---|
| WebGL | IL2CPP 关（用 Mono 更快，体积换时间），Compression: Brotli，Decompression Fallback: 开；目标体积 < 25MB |
| 分辨率 | 640×360，`Canvas` 与相机都按这个逻辑尺寸 |
| 音频 | 忽略 WebGL 的音频压缩问题，用 WAV 短音 + OGG 长音 |
| **当天要试构建一次** | 不要留到最后一晚（体积与首屏加载最容易炸） |

---

## 23. 验收清单（每条都要真跑一遍，不许跳）

### 23.1 功能

- [ ] 从开场到结局，10 幕能连续走完，不需要重开进程
- [ ] 光在 A 时，B 区道具可按；光在 B 时，A 区可按
- [ ] 光在移动的 0.4s 内，任何区都按不动（灰闪 + 闷响）
- [ ] 光下按 E / 空格：只失效，**不露馅、不闪白**
- [ ] 呈现在窗口内完成 → 眼睛浮出 + `sfx_reveal`
- [ ] 呈现太早 → 怀疑度上涨，追光变亮；满 100 → 露馅
- [ ] 光落定时道具错 → 铃 → 闪白 → 1.0s 内回本幕起点
- [ ] 露馅后道具回 `initial`、怀疑度归零、光回 `startZone`
- [ ] 幕限时 45s 会触发同样的露馅流程
- [ ] 助手位要求的幕，人不在位上 → 失败；在位上 → 通过
- [ ] 结局幕不接受任何输入，能自己演完

### 23.2 手感

- [ ] 走路 2.0 单位/秒，横穿全屏 5s
- [ ] 输入缓冲：在可动手前 0.1s 内按也算
- [ ] 呈现不可打断（中途按其它键无反应）
- [ ] 整个游戏没有一帧出现第二种黄色

### 23.3 性能

- [ ] WebGL 首屏 < 5s
- [ ] 稳定 60fps（这游戏几乎没有性能风险）

---

## 24. 72 小时排期（按 8 小时/天节奏，可压缩）

| 阶段 | 小时 | 交付 |
|---|---|---|
| H0–2 | 2 | 工程 + 单场景 + 固定相机 + 玩家能走 |
| H2–6 | 4 | 追光节拍 + "能否动手"判定 + 一个可失败的幕 |
| H6–10 | 4 | 道具系统 + 呈现 + 判定 + 露馅重开（第 1 关三幕能连跑） |
| H10–14 | 4 | 美术色块替换 + 音效 + HUD |
| H14–20 | 6 | 第 2、3 关数据 + 调手感（dwell / 窗口 / 惩罚） |
| H20–26 | 6 | 结局 + 标题 + WebGL 构建 + itch 页 |
| H26–32 | 6 | 试玩 5 人、修 bug、录 GIF |
| H32+ | — | **冻结功能**，只修 bug |

---

## 25. 风险与回退

| 风险 | 概率 | 回退 |
|---|---|---|
| "等光"体验太被动 | 中 | 把 dwell 从 3.0 → 2.4，抬高节奏；**不加**倒计时数字 |
| 呈现窗口太紧，玩家骂 | 中 | 提前量窗口 1.5 → 2.0；呈现时长 1.0 → 0.8 |
| 道具太容易混（三个长得像） | 低 | 按 19.2 的"低方/圆蓬/细高"重画外形，只改形状不改颜色 |
| 三关玩起来一样 | 中 | 只有 dwell 差异时明显；若 24h 有余，再加第二束光（schema 已支持 `lights[]` 长度 2） |
| 时间来不及 | 中 | **降级成 D2（帘子）**：删掉注视判定，换成固定周期帘子扫过左半屏；关卡数据格式不变，只换 `Spotlight` 的驱动源，一天内能出 |

---

## 26. 设计红线（别的 Agent 接手时不许越界）

1. **不加第四种颜色。** 黄只属于追光。
2. **不加文字**：没有台词、没有名字、没有教程、没有"按 E 交互"提示。
3. **不加第二块画面**：不做分屏、不做角标小窗。
4. **不改机位**：正交、正对台口、固定。不做俯视、不做 2.5D、不跟随。
5. **不做命中判定/血条/连击/评分/命数。**
6. **不做手滑即死**：光下按错只失效。
7. **不做多结局、不做选关、不做存档、不做关卡生成器。**
8. **不改核心动词**：走 / 动手 / 呈现 / 等。任何新动词都要先删一个旧的。
9. **重开必须 ≤ 1 秒且无加载。**
10. **黄=他在看。** 任何让玩家误解"黄色还有别的意思"的表现都要砍。

---

## 27. 附录

### A. 参数总表（调手感只改这张表）

| 参数 | 当前值 | 文件中的实现位置 |
|---|---|---|
| 走速 | 2.0 u/s | `PlayerController.moveSpeed` |
| 交互半径 | 0.7 u | `PlayerController.interactRadius` |
| 箱/镜交互时长 | 0.6s | 动画 clip 长度 |
| 布交互时长 | 0.5s | 同上 |
| 呈现时长 | 1.0s | 动画 clip 长度 |
| 提前量窗口 | 1.5s | `ActRunner.presentWindow` |
| 太早惩罚 | 25/秒 | `ActRunner.earlyPenaltyPerSec` |
| 怀疑度上限 | 100 | 常量 |
| 光 dwell | 3.0 / 2.6 / 2.2 | per-act `LightSpec.dwell` |
| 光缓动 | 0.4s | `LightSpec.transition` |
| 光宽 | 4.5 u | `Spotlight.width` |
| 幕限时 | 45s | `ActData.actTimeLimit` |
| 露馅全流程 | 1.0s | `ActRunner.failDuration` |
| 幕过渡 | 0.6s | `GameFlow.actGap` |

### B. 术语表

| 词 | 含义 |
|---|---|
| 他 | 台下观众席里那一个人的注视；追光就是它的形状；也是剧院老板 |
| 追光 | 唯一光源，黄，落在哪半边 = 他在看哪 |
| 落定 | 光停止移动、停在一个半区的状态 |
| 暗窗 | 光在另一半区的整段时间，是玩家唯一能动手的窗口 |
| 动手 | 开合箱 / 掀盖布 / 翻镜 |
| 呈现 | 1.0s 表演动作，本幕成功的唯一触发 |
| armed | 呈现已在窗口内完成，等光落定判定 |
| 露馅 | 判定失败 → 铃 → 闪白 → 回本幕起点 |

### C. 与上游文档的关系

| 文档 | 作用 |
|---|---|
| `TRICK-头脑风暴.md` 第 1–3 节 | 选题阶段的 15 条提案与评分（历史） |
| `D-障眼法-剖析.md` | D 的 5 个版本对比与推荐理由 |
| `障眼法-推荐方案-细节.md` | 叙事、机位、画面、玩法的设计论证 |
| `障眼法-镜头示意图.png` | 机位与画面示意图 |
| **`障眼法-GDD.md`（本文件）** | **施工口径，冲突时以此为准** |

---

*版本 v1.0 · 2026-10-24 · 概念已定，未开工。最后一个数字改动都记在这里，不要口头约定。*
