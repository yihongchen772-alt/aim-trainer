# 战术练枪场 (Tactical Aim Trainer)

战术射击类（无畏契约风格）练枪软件，用 Unity 6 制作：

- **移动手感接近端游**：Shift 静步、松键急停、反向急停，灵敏度换算与端游一致（每个鼠标计数 0.07° × 灵敏度，水平视野 103°）
- **射击**：射线判定，首发精准，连射上跳和左右摆动的固定弹道（可以练压枪），跑动、静步、急停后三档精度
- **假人**：可以放在地图任意点位；爆头一枪死、身体 4 枪；看到你后经过反应时间开枪还击；四档难度
- **五种训练模式**：定点甩枪、移动靶、对枪、预瞄清点（可选路线）、架枪（假人从入口冲出）；统计命中率、爆头率、平均击杀耗时，结算时按点位列出最慢、最容易被打死的角度
- **真实地图**：亚海悬城使用游戏里的地图模型和官方小地图，点位按真实地形放好（见 [docs/real-maps.md](docs/real-maps.md)）
- **白模地图**：靶场、一张原创 A 点、仿亚海悬城整图（A / 中路 / B 三路，24 个命名区域，3 条预瞄路线，3 个架枪位置），以及仿亚海悬城 A、源工重镇 B、隐世修所 C、霓虹町 A 的包点白模（近似布局）
- **小地图、键位、训练记录**：右上角小地图显示位置、朝向和所在区域；键位可以改；保存最近 30 局，一键继续上次训练
- **武器**：Vandal、Phantom、Spectre、Sheriff、Ghost，射速、分段伤害和后坐力走势参考端游
- **低延迟**：关闭垂直同步、不限帧率、CPU 最多领先 GPU 一帧，一键应用高帧率项目设置

> 本项目与 Riot Games 无关，也未获其认可或赞助。“亚海悬城（真实地图）”使用的地图模型和小地图归 Riot Games 所有，按 Riot 的 “Legal Jibber Jabber” 政策仅做免费、非商业的练习用途；其他地图是原创白模。

## 快速开始

1. 安装 Unity Hub 和 **Unity 6000.0 LTS**（不低于 6000.0.58f2）。
2. Unity Hub → **Add** → **Add project from disk** → 选择**这个仓库的文件夹**（能直接看到 `Assets`、`Packages`、`ProjectSettings` 的那一层）。
3. 打开后会自动生成全部训练场景并打开“靶场”。点 **▶ Play**，在菜单里点“开始训练”。

详细步骤、按键、打包成独立程序和常见问题见 [`docs/getting-started.md`](docs/getting-started.md)。

| 按键 | 作用 |
|------|------|
| WASD | 移动（键位可以在菜单里改） |
| Shift | 静步 |
| Ctrl / C | 下蹲 |
| 空格 / 滚轮 | 跳跃 |
| 左键 | 射击 |
| R | 换弹 |
| Esc | 暂停 / 菜单 |
| F1 | 调试面板（帧率、速度曲线、急停耗时） |

## 当前状态

| 阶段 | 内容 | 状态 |
|------|------|------|
| 1 | 移动控制器 | ✅ 代码完成，待在 Unity 中验收 |
| 2 | 射击与弹道 | ✅ 代码完成，待在 Unity 中验收 |
| 3 | 假人 AI 与训练模式 | ✅ 代码完成，待在 Unity 中验收 |
| 4 | 白模地图 | ✅ 靶场 + A 点 + 仿端游包点和亚海悬城整图，待在 Unity 中验收 |
| 5 | 帧率与延迟优化 | ✅ 运行时设置 + 项目设置工具，待实测 |

代码在没有 Unity 编辑器的环境里开发：核心逻辑有 89 个单元测试（.NET 下运行），全部代码按 Unity 2021 的 API 编译检查通过，但还没有在 Unity 里实际运行过。遇到问题请看 [`docs/getting-started.md`](docs/getting-started.md) 最后的排查表。

## 文档

| 文档 | 内容 |
|------|------|
| [getting-started.md](docs/getting-started.md) | 安装、打开项目、按键、菜单、打包、常见问题 |
| [ROADMAP.md](docs/ROADMAP.md) | 五个阶段的内容和验收标准 |
| [phase1-movement.md](docs/phase1-movement.md) | 移动：急停原理、参数、移动测试场景 |
| [phase2-shooting.md](docs/phase2-shooting.md) | 射击：散布、后坐力、射速、伤害、换枪 |
| [phase3-bots.md](docs/phase3-bots.md) | 假人：反击逻辑、难度、训练模式、放置点位 |
| [phase4-greybox.md](docs/phase4-greybox.md) | 地图：尺寸标准、A 点布局、用 ProBuilder 搭图 |
| [real-maps.md](docs/real-maps.md) | 真实地图：素材来源、场景结构、点位检查、修改点位 |
| [phase5-performance.md](docs/phase5-performance.md) | 性能：延迟来源、自动设置、手动检查清单 |

## 目录

```
Assets/AimTrainer/
  Scripts/
    Movement/      移动核心算法（纯 C#，可单元测试）和参数
    Player/        第一人称控制器、输入读取
    Weapons/       射线武器、散布与后坐力、枪模、弹孔与弹道线
    Combat/        生命值、命中部位、判定框
    Bots/          假人、难度、点位、刷新区域
    Game/          训练流程、模式、设置、成绩
    UI/            准星、HUD、菜单（IMGUI，不需要任何界面资源）
    Audio/         代码合成的音效
    Core/          低延迟运行时设置
    Diagnostics/   调试面板
  Editor/          场景生成（靶场、A 点、移动测试）、项目设置工具
  Tests/EditMode/  单元测试
  RealMaps/        真实地图的模型和小地图（素材归 Riot Games 所有）
Packages/          Unity 包清单（Input System、Test Framework、glTFast）
ProjectSettings/   Unity 项目设置
docs/              文档
web-prototype/     最早的网页原型（单文件，浏览器直接打开）
```

## 网页原型

`web-prototype/index.html` 是项目开始时做的网页版原型，用 Chrome 或 Edge 直接打开即可（需要联网加载 three.js）。有三张原创小地图和定点 / 移动靶 / 对枪三种模式。浏览器的帧率最高等于显示器刷新率，还有额外的合成延迟，手感以 Unity 版为准。
