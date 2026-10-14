# 真实地图

`亚海悬城（真实地图）` 用的是游戏里的地图模型，不是白模。素材来源：

| 文件 | 内容 | 来源 |
|------|------|------|
| `Assets/AimTrainer/RealMaps/Ascent/ascent-visual.glb` | 带贴图的画面模型（约 55 万三角面） | SpikeAtlas 公开的地图文件，贴图从 WebP 转成 JPG/PNG、最大 1024 |
| `Assets/AimTrainer/RealMaps/Ascent/ascent-collision.glb` | 简化的碰撞模型 | 同上 |
| `Assets/AimTrainer/RealMaps/Ascent/ascent-minimap.png` | 官方小地图 | valorant-api.com |

地图模型和小地图归 Riot Games 所有。本项目按 Riot 的 “Legal Jibber Jabber” 政策只做免费、非商业的练习用途，与 Riot Games 无关，也未获其认可或赞助。

## 第一次打开

1. 打开项目时 Unity 会自动安装 **glTFast**（`Packages/manifest.json` 里的 `com.unity.cloud.gltfast`），然后导入两个 glb，第一次需要几分钟。
2. 导入完成后会自动生成 `AscentReal` 场景。如果没有自动生成（例如 glTFast 还在安装），等导入结束后点 **AimTrainer → 生成缺少的场景**。
3. 在训练菜单的“地图”里选 **亚海悬城（真实地图）**。

场景结构：

| 物体 | 作用 |
|------|------|
| `Level/MapVisual` | 画面模型，只负责显示（无光照材质，光影已经画在贴图里） |
| `Level/MapCollision` | 碰撞模型，每个网格带 MeshCollider，看不见；走路、子弹、假人视线都用它 |
| `Bots` | 预瞄点位、三个架枪位置（下面是冲出入口）、定点甩枪的刷新区域 |
| `Routes` | 三条预瞄路线 |
| `Regions` | 22 个官方地名（例如“A区 大道”“中区 窄道”），小地图下方显示离你最近的地名 |
| `Minimap` | 官方小地图和坐标换算 |

glTF 是右手坐标系，导入 Unity 时会镜像一个轴。生成场景时会根据碰撞模型的位置自动判断镜像的是哪个轴，点位按同样的方式换算。

## 训练内容

| 预瞄路线 | 出发点 | 点位 |
|---------|-------|------|
| A 大进 A 点 | A 区大厅 | A区大道、A区酒庄、A大道出口、A包点箱后、A长箱后、A区高台、A包点后角、A区树木 |
| 中路控制 | 中路坡顶 | 中区窄道、中区庭院、中区坡底、中区比萨、中区市场、中区小道 |
| B 大进 B 点 | B 区大厅 | B区大道、B大道拐角、B区船后、B区箱后、B包点后角、B区船屋、B区市场口、B区侧道 |

| 架枪位置 | 入口（假人从这里冲出） |
|---------|----------------------|
| A 点架 A 大（包点里看 A 大门） | A 大道横穿（从北）、A 大道横穿（从南）、A 大门冲出 |
| B 侧道架 B 大（B 包点东侧的侧道） | B 大道冲出、B 大道冲出右、B 区船后冲出 |
| 中路坡底架庭院 | 窄道冲出、庭院东侧冲出、小道冲出 |

B 包点是下沉的广场，包点里任何位置都看不到 B 大道的出口，所以 B 点的架枪位置放在包点东侧高一点的侧道上。

所有点位都用碰撞模型检查过（`tools/realmaps/place.py`）：

- 脚下有地面，身体（半径 0.3 米、高 1.8 米）不和墙重叠；
- 预瞄点位：沿玩家的路线走，路上某处能看到它、在出发点看不到；假人朝向第一次能看到玩家的位置；
- 架枪入口：起点从架枪位置看不到，终点看得到；冲出的路线上每 0.4 米检查一次，台阶不超过 0.45 米、不会卡墙。

假人移动时会贴着地面走（能上下 0.45 米以内的台阶和斜坡），不会悬空或钻进地里。

## 修改点位

点位写在 `tools/realmaps/spots.py`，坐标用**官方小地图的像素**（1024×1024，打开 `cache/ascent-minimap.png` 就能量），`face` 是假人看向哪个像素。改完后：

```
pip install numpy pillow
python3 tools/realmaps/fetch.py    # 第一次：下载模型和小地图，生成碰撞缓存（tools/realmaps/cache，不提交）
python3 tools/realmaps/place.py    # 检查点位，不合格的会在 2 米内自动挪动；结果写到 placements.json
python3 tools/realmaps/emit.py     # 生成 Assets/AimTrainer/Editor/RealMapData.cs
```

然后在 Unity 里删掉 `AscentReal` 场景，点 **AimTrainer → 生成缺少的场景**。

`search_hold.py`、`search_entry.py` 可以自动搜索架枪位置和冲出路线。`render.js` + `view.html` 可以用 three.js 从玩家视角渲染截图检查（需要 `npm install three` 和 Playwright）。
