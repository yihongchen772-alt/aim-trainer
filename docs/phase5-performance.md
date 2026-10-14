# 第五阶段：帧率与输入延迟

目标：鼠标跟手程度和帧率接近竞技游戏的水平。

## 延迟从哪里来

从移动鼠标到屏幕上看到画面变化，要经过：鼠标采样 → 系统传给游戏 → 游戏读取输入并更新视角 → CPU 准备这一帧 → GPU 渲染 → 等待显示器刷新 → 显示器把画面亮出来。

能在游戏里优化的是中间几步：

| 环节 | 做法 | 本项目 |
|------|------|--------|
| 输入读取 | 每帧在逻辑更新之前读取最新的鼠标位移，用原始输入（不受系统鼠标加速影响） | Input System 的 Dynamic Update 模式；Windows 上鼠标位移来自原始输入 |
| 视角更新 | 先转视角再算移动，摄像机在同一帧里转好再渲染 | `PlayerController` 每帧第一步就处理视角 |
| CPU 领先 GPU | 默认允许 CPU 领先 GPU 两三帧，每多一帧多一帧延迟 | `QualitySettings.maxQueuedFrames = 1` |
| 垂直同步 | 开启后每帧都要等显示器刷新，延迟增加一到几帧 | 关闭 |
| 帧率 | 帧率越高，每帧的时间越短，整条链路越快 | 默认不限帧率 |
| 全屏方式 | 窗口模式经过系统桌面合成会多一帧 | 无边框全屏 + DXGI 翻转模型 |

## 运行时自动应用（PerformanceSettings 组件）

每个训练场景的 `Training`（或 `Systems`）物体上都有 **Performance Settings** 组件，场景加载时自动：

- 关闭垂直同步（`QualitySettings.vSyncCount = 0`）
- 帧率上限按菜单设置，默认不限（`Application.targetFrameRate = -1`）
- `QualitySettings.maxQueuedFrames = 1`：CPU 最多领先 GPU 一帧。帧率不稳时可以在组件里改成 2
- Input System 设为每帧 Update 之前处理输入（`ProcessEventsInDynamicUpdate`）

菜单里的“帧率上限”可以选不限 / 60 / 144 / 240 / 360。显卡发热或画面撕裂严重时，限制在显示器刷新率的 2 倍左右也很合适。

## 项目设置（菜单 AimTrainer → 应用高帧率项目设置）

一键修改以下项目设置：

| 设置 | 改为 | 原因 |
|------|------|------|
| Fullscreen Mode | Fullscreen Window（无边框全屏） | 配合翻转模型，延迟与独占全屏相当，切窗口更快 |
| Use DXGI flip model swapchain | 开 | Windows 10/11 上绕过桌面合成，减少一帧延迟 |
| Default Is Native Resolution | 开 | 用显示器原生分辨率，避免缩放 |
| Run In Background / Visible In Background | 关 | 切出去时不占用显卡 |
| 所有画质档位的 VSync Count | Don't Sync | 同上 |
| Shadow Distance / Cascades | 60 米 / 2 级 | 白模地图不需要远处阴影，省 GPU 时间 |
| Realtime Reflection Probes | 关 | 白模用不到实时反射 |
| Soft Particles | 关 | 用不到 |

## 手动检查清单

下面这些没有自动修改，需要按你的项目情况检查：

**渲染管线**
- 用 **内置渲染管线（Built-in）** 延迟最低、设置最少。生成的场景在内置管线和 URP 下都能用。
- 用 **URP** 时，在 **Project Settings → Graphics** 找到正在用的 URP Asset：
  - 关闭 **HDR**、关闭 **Post Processing**（或在摄像机上关掉 Post Processing）
  - **Anti Aliasing (MSAA)** 设为 Disabled 或 2x，不要用 TAA
  - **Shadows → Max Distance** 设为 60 左右，Cascade Count 设为 2
  - 保持 **SRP Batcher** 开启
- 任何管线下都不要开：动态模糊（Motion Blur）、景深（Depth of Field）、时间性抗锯齿（TAA）、屏幕空间反射。它们要么增加延迟，要么让画面在快速转动时变糊。

**图形 API（Windows）**
- **Project Settings → Player → Other Settings → Graphics APIs for Windows**。Direct3D11 和 Direct3D12 都可以，两者都试一下，在调试面板里对比帧时间，选更稳的那个。修改后需要重启编辑器。

**系统与硬件**
- 显示器设为最高刷新率（Windows 设置 → 显示 → 高级显示设置）。
- 鼠标回报率设为 1000 Hz 或更高（鼠标驱动里设置）。
- 关闭 Windows 的“提高指针精确度”不会影响本项目（用的是原始输入），但会影响桌面操作手感，按个人习惯。
- NVIDIA Reflex 这类显卡厂商的低延迟技术需要额外的插件，本项目没有集成；`maxQueuedFrames = 1` 能拿到其中的大部分收益。

## 怎么验证

1. 按 F1 打开调试面板，看帧时间（ms）和“最慢”一帧。白模场景在中端显卡上应该稳定在 300 FPS 以上，“最慢”一帧不应该频繁跳到 10 ms 以上。
2. 一定要用**打包后的程序**测试：编辑器本身会占用很多 CPU，Game 视图里的帧率和延迟都更差。打包步骤见 [getting-started.md](getting-started.md)。
3. 对比测试：在菜单里把帧率上限分别设成 60 和不限，快速左右甩鼠标，感受视角跟手程度的差别。

## 验收

对应 `docs/ROADMAP.md` 第五阶段：

1. 打包后的程序在白模场景中稳定 300 FPS 以上（中端显卡）。
2. 调试面板里帧时间平稳，没有频繁的尖峰。
3. 关闭垂直同步后快速转动视角，没有可感知的拖滞。
