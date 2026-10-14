"""
仿端游地图的白模数据（单位：米；x 向东，z 向北，y 向上）。
运行 `python3 tools/maps/maps.py` 会：
  1. 检查每张图：区域是否连通，假人点位、探头路线、横跳范围、出生点是否和几何体重叠，是否站在地面上；
  2. 生成 Assets/AimTrainer/Editor/ValorantMapBuilders.cs。
这些布局是按公开的地图俯视图和常用点位名称凭印象近似的，比例和位置都不是精确复刻。
"""
import math, os, sys

# box: (名称, xMin, xMax, zMin, zMax, 高度, 底面 y, 材质)
# room: (区域名称, xMin, xMax, zMin, zMax)：整图的可走区域，区域以外自动生成墙
# ramp: (名称, 轴 'x'|'z', 低端坐标, 高端坐标, 宽度方向 min, max, 升高, 底面 y)
# point: (名称, x, y, z, 朝向 yaw, 行为, 探头 dx, dz, 横跳宽度, 预瞄)

W = 7.0  # 墙高

def wall(name, x0, x1, z0, z1, h=W, y=0.0): return (name, x0, x1, z0, z1, h, y, "Wall")
def block(name, x0, x1, z0, z1, h, y=0.0): return (name, x0, x1, z0, z1, h, y, "Block")
def cover(name, x0, x1, z0, z1, h, y=0.0): return (name, x0, x1, z0, z1, h, y, "Cover")
def plat(name, x0, x1, z0, z1, h, y=0.0): return (name, x0, x1, z0, z1, h, y, "Platform")
def pt(name, x, y, z, yaw, beh="Static", px=0.0, pz=0.0, width=3.0, prefire=True):
    return (name, x, y, z, yaw, beh, px, pz, width, prefire)

MAPS = []

# ---------------------------------------------------------------- 亚海悬城 A 点
MAPS.append(dict(
    scene="AscentA", title="亚海悬城 A 点（仿）", builder="BuildAscentA",
    desc="A 大、连接、发电机、三箱、天堂 / 地狱、酒窖、花园。从 A 大进点。",
    bounds=(-36, 14, -44, 40), spawn=(-10, 0, -42, 0), aim=(-14, 0, 3, 0),
    area=((-14, 4, 16), (42, 8, 30)),
    boxes=[
        wall("A大西侧", -36, -30, -30, 0), wall("中路建筑", -23, -8, -30, 0), wall("连接东侧", -3, 14, -30, 0),
        wall("花园建筑", 8, 14, 0, 40), wall("天堂后墙", -36, -18, 32, 40),
        wall("酒窖西", -18, -8, 32, 40), wall("酒窖东", -2, 8, 32, 40),
        plat("天堂", -36, -20, 24, 32, 0.4, 3.6),
        cover("天堂栏杆西", -36, -30, 24, 24.3, 1.35, 4.0), cover("天堂栏杆东", -26, -20, 24, 24.3, 1.35, 4.0),
        block("天堂立柱1", -21, -20, 24, 25, 3.6), block("天堂立柱2", -21, -20, 31, 32, 3.6),
        block("发电机_2.8", -26, -22, 8, 12, 2.8),
        cover("三箱_1.4", -12, -8, 12, 15, 1.4), cover("三箱上层_1.0", -12, -8, 13.5, 15, 1.0, 1.4),
        cover("下包掩体_1.0", -20, -16, 17, 20, 1.0),
        cover("A大出口跳箱_0.85", -29, -27.5, 3, 4.5, 0.85),
        wall("肘角墙", -23, -21.5, 0, 5, 4.0),
        cover("酒窖箱_1.4", -6, -3, 27, 30, 1.4),
        cover("A大蹲掩体_1.4", -29, -27.5, -14, -12.5, 1.4), cover("连接半身_1.0", -8, -6.5, -10, -8.5, 1.0),
        cover("大厅箱_1.4", -14, -11, -38, -35, 1.4), block("大厅高箱_2.6", 4, 6, -43, -39, 2.6),
    ],
    ramps=[("天堂斜坡", "x", -10, -20, 28.5, 31.5, 4.0, 0.0)],
    points=[
        pt("A大", -26.5, 0, -8, 180), pt("连接", -5.5, 0, -16, 180, prefire=False),
        pt("肘角", -20.8, 0, 3, 20), pt("发电机后探头", -24, 0, 13.4, 200, "Peek", -3.4, 0),
        pt("三箱后探头", -10, 0, 15.6, 180, "Peek", 3.2, 0), pt("下包位", -18, 0, 21, 200),
        pt("地狱", -28, 0, 28, 160), pt("天堂", -28, 4.0, 29, 170), pt("天堂坡口", -21.5, 4.0, 30, 150),
        pt("酒窖", -5, 0, 34, 180, "Strafe", width=3), pt("花园角", 5, 0, 24, 225), pt("连接出口", 4, 0, 4, 225),
    ]))

# ---------------------------------------------------------------- 源工重镇 B 点
MAPS.append(dict(
    scene="BindB", title="源工重镇 B 点（仿）", builder="BuildBindB",
    desc="B 长廊、水烟房窗口、B 大箱、双箱、花园、肘角。从 B 长廊进点，水烟房里的假人隔着窗口架枪。",
    bounds=(-34, 14, -44, 40), spawn=(-10, 0, -40, 0), aim=(-14, 0, 3, 0),
    area=((-12, 4, 15), (42, 8, 28)),
    boxes=[
        wall("长廊西侧", -34, -30, -30, 4), wall("中间建筑", -23, -10, -30, 0),
        wall("水烟通道西", -10, -6, -30, -12), wall("水烟通道东", -1, 14, -30, -12), wall("水烟房东", 4, 14, -12, 0),
        wall("水烟窗西墙", -10, -4, -2, 0), wall("水烟窗东墙", 0, 4, -2, 0),
        cover("水烟窗台_0.8", -4, 0, -2, 0, 0.8), wall("水烟窗上沿", -4, 0, -2, 0, 4.8, 2.2),
        cover("水烟房掩体_1.0", -9, -7, -11, -9, 1.0),
        wall("东侧建筑", 10, 14, 0, 40), wall("北侧西", -34, -14, 30, 40), wall("北侧东", -8, 10, 30, 40),
        block("B大箱_2.6", -18, -14, 10, 13, 2.6),
        cover("双箱_1.4", -8, -5, 14, 17, 1.4), cover("双箱上层_1.0", -8, -5, 15.5, 17, 1.0, 1.4),
        cover("下包掩体_1.0", -24, -20, 18, 21, 1.0), cover("长廊出口跳箱_0.85", -27, -25.5, 6, 7.5, 0.85),
        wall("花园墙", -2, -1.5, 22, 27, 4.0),
        cover("长廊蹲掩体_1.4", -29, -27.5, -16, -14.5, 1.4),
        cover("大厅箱_1.4", -16, -13, -38, -35, 1.4), block("大厅高箱_2.6", 4, 6, -43, -39, 2.6),
    ],
    ramps=[],
    points=[
        pt("B长廊", -26.5, 0, -6, 180), pt("水烟房窗口", -2, 0, -5, 0),
        pt("B大箱后探头", -16, 0, 14.4, 200, "Peek", -3.4, 0), pt("双箱后探头", -6.5, 0, 17.6, 180, "Peek", 3.0, 0),
        pt("下包位", -22, 0, 22, 200), pt("花园", 4, 0, 26, 225), pt("B回防", -11, 0, 34, 180, "Strafe", width=3),
        pt("后角", -32, 0, 16, 120), pt("肘角", -28, 0, 27, 135), pt("窗口对角", 6, 0, 6, 240),
    ]))

# ---------------------------------------------------------------- 隐世修所 C 点
MAPS.append(dict(
    scene="HavenC", title="隐世修所 C 点（仿）", builder="BuildHavenC",
    desc="C 长廊、车库门、木头堆、大箱、角落、C 回防。从 C 长廊进点，车库里的假人从门口架枪。",
    bounds=(-20, 36, -44, 40), spawn=(10, 0, -40, 0), aim=(14, 0, 7, 0),
    area=((12, 4, 18), (46, 8, 26)),
    boxes=[
        wall("车库通道西", -20, -2, -30, -10), wall("中间建筑", 3, 24, -30, -10), wall("长廊东侧", 31, 36, -30, 6),
        wall("车库西", -20, -8, -10, 4), wall("车库东", 8, 24, -10, 4),
        wall("车库门西墙", -8, -2, 2, 4), wall("车库门东墙", 2, 8, 2, 4),
        cover("车库箱_1.4", -6, -4, -8, -6, 1.4),
        wall("西侧建筑", -20, -12, 4, 40), wall("北侧西", -12, 6, 32, 40), wall("北侧东", 12, 36, 32, 40),
        cover("木头堆_1.4", 14, 18, 14, 17, 1.4), cover("木头堆上层_1.0", 14, 18, 15.5, 17, 1.0, 1.4),
        block("C大箱_2.6", 0, 4, 18, 21, 2.6), wall("角落墙", 28, 36, 20, 21, 4.0),
        cover("下包掩体_1.0", 22, 26, 24, 27, 1.0), cover("长廊出口跳箱_0.85", 26, 27.5, 8, 9.5, 0.85),
        cover("长廊蹲掩体_1.4", 25, 26.5, -15, -13.5, 1.4),
        cover("大厅箱_1.4", 14, 17, -38, -35, 1.4), block("大厅高箱_2.6", -6, -4, -43, -39, 2.6),
    ],
    ramps=[],
    points=[
        pt("C长廊", 27.5, 0, -8, 180), pt("车库门", 0, 0, -2, 0),
        pt("木头堆后探头", 16, 0, 17.6, 200, "Peek", -3.4, 0), pt("大箱后探头", 2, 0, 21.6, 180, "Peek", 3.2, 0),
        pt("下包位", 24, 0, 28, 200), pt("角落", 32, 0, 24, 225), pt("C回防", 9, 0, 35, 180, "Strafe", width=3),
        pt("后点", -9, 0, 28, 150), pt("长廊出口角", 21, 0, 9, 120), pt("车库门侧", -6, 0, 8, 160),
    ]))

# ---------------------------------------------------------------- 霓虹町 A 点
MAPS.append(dict(
    scene="SplitA", title="霓虹町 A 点（仿）", builder="BuildSplitA",
    desc="A 大斜坡上高处包点、屏风、塔、包点箱、天堂、A 回防。包点比进攻路线高 2 米。",
    bounds=(-34, 14, -44, 40), spawn=(-14, 0, -40, 0), aim=(-14, 2, 9, 0),
    area=((-13, 6, 19), (42, 8, 26)),
    boxes=[
        wall("A大西侧", -34, -30, -30, 6), wall("中间建筑", -23, -6, -30, 6), wall("屏风东侧", -1, 14, -30, 6),
        wall("东侧建筑", 8, 14, 6, 40), wall("天堂后墙", -34, -14, 32, 40),
        wall("北侧东", 0, 8, 32, 40), wall("北侧中", -14, -6, 32, 40),
        plat("包点平台", -34, 8, 6, 32, 2.0), plat("回防通道", -6, 0, 32, 40, 2.0),
        plat("天堂", -34, -14, 26, 32, 3.0, 2.0),
        cover("天堂栏杆西", -34, -26, 26, 26.3, 1.35, 5.0), cover("天堂栏杆东", -22, -14, 26, 26.3, 1.35, 5.0),
        block("塔_2.6", -20, -16, 12, 16, 2.6, 2.0),
        cover("包点箱_1.4", -10, -6, 16, 19, 1.4, 2.0), cover("包点箱上层_1.0", -10, -6, 17.5, 19, 1.0, 3.4),
        cover("下包掩体_1.0", -26, -22, 18, 21, 1.0, 2.0),
        cover("A大蹲掩体_1.4", -29, -27.5, -18, -16.5, 1.4),
        cover("大厅箱_1.4", 0, 3, -40, -37, 1.4), block("大厅高箱_2.6", 6, 8, -43, -39, 2.6),
    ],
    ramps=[("A大斜坡", "z", -2, 6, -30, -23, 2.0, 0.0), ("屏风斜坡", "z", -4, 6, -6, -1, 2.0, 0.0),
           ("天堂斜坡", "x", -4, -14, 28.5, 31.5, 3.0, 2.0)],
    points=[
        pt("A大", -26.5, 0, -14, 180), pt("屏风口", 2, 2, 10, 225),
        pt("塔后探头", -18, 2, 17.4, 200, "Peek", -3.4, 0), pt("包点箱后探头", -8, 2, 20.6, 180, "Peek", 3.2, 0),
        pt("下包位", -24, 2, 22, 200), pt("天堂", -24, 5, 29, 175), pt("天堂坡口", -15.5, 5, 30, 150),
        pt("A回防", -3, 2, 35, 180, "Strafe", width=3), pt("后角", -32, 2, 22, 120), pt("坡顶拐角", -20, 2, 8, 225),
    ]))


# ---------------------------------------------------------------- 亚海悬城整图
# 整图用“可走区域”（rooms）描述：每个矩形是一个命名区域，区域以外自动填成墙。
# 两个区域的边贴在一起，重合的那一段就是通道口。
def room(name, x0, x1, z0, z1): return (name, x0, x1, z0, z1)

MAPS.append(dict(
    scene="Ascent", title="亚海悬城（整图，仿）", builder="BuildAscent",
    desc="A / 中路 / B 三路整图，24 个命名区域。支持预瞄路线（A 大、中路、B 大）和三个架枪位置。",
    bounds=(-60, 60, -56, 52), spawn=(0, 0, -50, 0), aim=(-44, 0, 6, 0),
    area=((-44, 4, 18), (26, 8, 18)),
    rooms=[
        room("进攻方出生点", -26, 26, -56, -44),
        room("A 大厅", -52, -26, -48, -30), room("A 大道", -52, -45, -30, 2), room("酒窖", -45, -40, -10, -4),
        room("A 包点", -60, -30, 2, 30), room("天堂", -60, -44, 30, 38), room("地狱", -44, -30, 30, 34),
        room("椽子", -44, -30, 34, 38), room("窗口", -30, -26, 10, 13), room("树", -30, -26, 24, 28),
        room("树", -26, -18, 10, 30), room("花园", -30, -14, 30, 48),
        room("猫道", -45, -14, -3, 1), room("中路底", -6, 6, -44, -8), room("披萨", 6, 16, -30, -20),
        room("中路庭院", -14, 14, -8, 16), room("市场", 14, 30, 6, 14), room("连接", -18, -6, 22, 28),
        room("中路顶", -6, 6, 16, 44),
        room("B 大厅", 26, 52, -48, -30), room("B 大道", 42, 49, -30, 0), room("B 包点", 30, 60, 0, 28),
        room("船屋", 52, 60, 28, 40), room("B 回防", 30, 38, 28, 44), room("防守方出生点", -14, 40, 44, 52),
    ],
    boxes=[
        # A 侧
        cover("大厅箱_1.4", -40, -37, -42, -39, 1.4), cover("A大道蹲掩体_1.4", -51, -49.5, -18, -16.5, 1.4),
        block("发电机_2.8", -50, -46, 12, 16, 2.8),
        cover("三箱_1.4", -40, -36, 16, 19, 1.4), cover("三箱上层_1.0", -40, -36, 17.5, 19, 1.0, 1.4),
        cover("下包掩体_1.0", -44, -41, 22, 25, 1.0), cover("A大出口跳箱_0.85", -57, -55.5, 4, 5.5, 0.85),
        plat("天堂平台", -60, -44, 30, 38, 3.4),
        cover("天堂栏杆西", -60, -54, 30, 30.3, 1.35, 3.4), cover("天堂栏杆东", -50, -44, 30, 30.3, 1.35, 3.4),
        plat("椽子平台", -44, -30, 34, 38, 3.4),
        cover("椽子栏杆西", -44, -38, 34, 34.3, 1.0, 3.4), cover("椽子栏杆东", -34, -30, 34, 34.3, 1.0, 3.4),
        cover("窗台_1.0", -30, -26, 10, 13, 1.0), wall("窗口上沿", -30, -26, 10, 13, 4.6, 2.4),
        cover("树下箱_1.4", -24, -21, 16, 19, 1.4), block("花园高箱_2.6", -20, -17, 38, 41, 2.6),
        # 中路
        cover("猫道半身_1.0", -28, -26.5, -3, -1.5, 1.0), block("中路方块_2.6", -3, 3, 2, 6, 2.6),
        cover("庭院箱_1.4", 8, 10, -4, -2, 1.4), cover("中路底箱_1.4", -5, -3.5, -30, -28.5, 1.4),
        cover("披萨箱_1.4", 12, 14, -28, -26, 1.4), cover("市场箱_1.4", 20, 23, 6, 8, 1.4),
        # B 侧
        cover("B大道蹲掩体_1.4", 43, 44.5, -20, -18.5, 1.4), block("B大箱_2.6", 40, 44, 12, 16, 2.6),
        cover("双箱_1.4", 50, 53, 18, 21, 1.4), cover("双箱上层_1.0", 50, 53, 19.5, 21, 1.0, 1.4),
        block("B柱子_4", 36, 37, 22, 23, 4.0), cover("B下包掩体_1.0", 46, 49, 22, 24, 1.0),
        cover("B大出口跳箱_0.85", 50, 51.5, 3, 4.5, 0.85), cover("船屋箱_1.4", 56, 58, 36, 38, 1.4),
        cover("出生点箱_1.4", -14, -11, -54, -51, 1.4), block("出生点高箱_2.6", 10, 12, -55, -51, 2.6),
    ],
    ramps=[("椽子斜坡", "x", -24, -30, 34, 38, 3.4, 0.0)],
    points=[
        # A
        pt("A大道", -48.5, 0, -8, 180), pt("酒窖", -41.5, 0, -7, 270),
        pt("A大出口角", -58, 0, 7, 45), pt("发电机后探头", -48, 0, 17.4, 180, "Peek", -3.4, 0),
        pt("三箱后探头", -38, 0, 20.4, 180, "Peek", 3.2, 0), pt("下包位", -42.5, 0, 26.2, 200),
        pt("地狱", -37, 0, 32, 180), pt("天堂", -52, 3.4, 34, 180), pt("椽子", -36, 3.4, 36, 200),
        pt("窗口", -23, 0, 11.5, 270), pt("树下", -22, 0, 26, 240), pt("花园", -24, 0, 42, 200),
        # 中路
        pt("中路底", 0, 0, -20, 180), pt("披萨", 11, 0, -22, 225),
        pt("中路方块后探头", 0, 0, 7.4, 180, "Peek", 4.2, 0), pt("猫道", -32, 0, -1, 90),
        pt("市场", 25, 0, 11, 250), pt("连接", -12, 0, 25, 135), pt("中路顶", 0, 0, 30, 180),
        # B
        pt("B大道", 45.5, 0, -12, 180), pt("B大箱后探头", 42, 0, 17.4, 180, "Peek", -3.4, 0),
        pt("双箱后探头", 51.5, 0, 22.4, 180, "Peek", 3.0, 0), pt("B柱子后", 36.5, 0, 24, 180),
        pt("船屋", 56, 0, 33, 200), pt("B市场口", 33, 0, 10, 270), pt("B包点后角", 58, 0, 3, 315),
        pt("B回防", 34, 0, 37, 180, "Strafe", width=3),
    ],
    routes=[
        ("A 大进 A 点", (-48.5, 0, -40, 0),
         ["A大道", "酒窖", "A大出口角", "发电机后探头", "三箱后探头", "下包位", "地狱", "天堂", "椽子", "窗口", "树下"]),
        ("中路控制", (0, 0, -40, 0), ["中路底", "披萨", "猫道", "中路方块后探头", "市场", "连接", "中路顶"]),
        ("B 大进 B 点", (45.5, 0, -40, 0),
         ["B大道", "B包点后角", "B大箱后探头", "双箱后探头", "B柱子后", "B市场口", "船屋", "B回防"]),
    ],
    holds=[
        # 入口的朝向（第 5 个数）会被自动改成对准架枪位置，这里随便写。
        dict(name="A 点架 A 大", pos=(-36, 0, 12, 235), entries=[
            pt("A大出口", -48.5, 0, -5, 0, "Swing", 0, 9, prefire=False),
            pt("A大出口左", -51, 0, -8, 0, "Swing", 0, 10, prefire=False),
            pt("A大出口右", -46.2, 0, -6, 0, "Swing", 0, 9, prefire=False),
        ]),
        dict(name="B 点架 B 大", pos=(57, 0, 12, 225), entries=[
            pt("B大出口", 45.5, 0, -8, 0, "Swing", 0, 10, prefire=False),
            pt("B大出口右", 47.5, 0, -6, 0, "Swing", 0, 9.5, prefire=False),
            pt("B大出口左", 43, 0, -12, 0, "Swing", 0, 14, prefire=False),
        ]),
        dict(name="中路顶架中路", pos=(0, 0, 22, 180), entries=[
            pt("猫道口", -20, 0, -1, 90, "Swing", 8, 0, prefire=False),
            pt("市场口", 20, 0, 12, 270, "Swing", -12, 0, prefire=False),
            pt("中路方块", 1.5, 0, 0.5, 0, "Swing", 3.5, 0, prefire=False),
        ]),
    ]))

# ---------------------------------------------------------------- 检查
R = 0.32  # 假人 / 玩家半径（略大于 0.3）
H = 1.8

def overlaps(b, x, y, z, r=R, h=H):
    _, x0, x1, z0, z1, bh, by, _ = b
    if by >= y + h - 1e-3 or by + bh <= y + 1e-3: return False
    return x + r > x0 and x - r < x1 and z + r > z0 and z - r < z1

def ramp_footprint(rp):
    name, axis, lo, hi, w0, w1, rise, base = rp
    a0, a1 = min(lo, hi), max(lo, hi)
    return (a0, a1, w0, w1) if axis == "x" else (w0, w1, a0, a1)

def on_ramp(rp, x, z, r=R):
    x0, x1, z0, z1 = ramp_footprint(rp)
    return x + r > x0 and x - r < x1 and z + r > z0 and z - r < z1

def support(m, x, y, z):
    if abs(y) < 1e-3: return True
    for b in m["boxes"]:
        _, x0, x1, z0, z1, bh, by, _ = b
        if abs(by + bh - y) < 1e-3 and x0 <= x <= x1 and z0 <= z <= z1: return True
    return False

def check_spot(m, label, x, y, z, r=R, need_support=True):
    errs = []
    bx0, bx1, bz0, bz1 = m["bounds"]
    if not (bx0 + r <= x <= bx1 - r and bz0 + r <= z <= bz1 - r): errs.append(f"{label} 出界 ({x},{z})")
    for b in m["boxes"]:
        if overlaps(b, x, y, z, r): errs.append(f"{label} ({x},{y},{z}) 和 {b[0]} 重叠")
    for rp in m["ramps"]:
        if on_ramp(rp, x, z, r): errs.append(f"{label} ({x},{z}) 在斜坡 {rp[0]} 上")
    if need_support and not support(m, x, y, z): errs.append(f"{label} ({x},{y},{z}) 脚下没有地面")
    return errs

def complement_walls(m):
    """区域以外的部分填成墙：按所有区域的边把地图切成网格，没被任何区域覆盖的格子是墙，再合并成大方块。"""
    bx0, bx1, bz0, bz1 = m["bounds"]
    rooms = m["rooms"]
    xs = sorted({bx0, bx1} | {v for r in rooms for v in (r[1], r[2]) if bx0 <= v <= bx1})
    zs = sorted({bz0, bz1} | {v for r in rooms for v in (r[3], r[4]) if bz0 <= v <= bz1})
    def covered(x, z): return any(r[1] <= x <= r[2] and r[3] <= z <= r[4] for r in rooms)
    strips = []  # 每一行（z 方向一格）里连续的墙段
    for j in range(len(zs) - 1):
        zc = (zs[j] + zs[j + 1]) / 2
        runs, start = [], None
        for i in range(len(xs) - 1):
            solid = not covered((xs[i] + xs[i + 1]) / 2, zc)
            if solid and start is None: start = xs[i]
            if not solid and start is not None: runs.append((start, xs[i])); start = None
        if start is not None: runs.append((start, xs[-1]))
        strips.append(runs)
    walls, open_runs = [], {}
    for j, runs in enumerate(strips + [[]]):
        keep = {}
        for run in runs:
            keep[run] = open_runs.pop(run, zs[j])
        for (x0, x1), z0 in open_runs.items():
            walls.append((x0, x1, z0, zs[j]))
        open_runs = keep
    return [wall(f"墙{i:02d}", x0, x1, z0, z1) for i, (x0, x1, z0, z1) in enumerate(sorted(walls, key=lambda w: (w[2], w[0])))]

def prepare(m):
    m.setdefault("rooms", []); m.setdefault("routes", []); m.setdefault("holds", [])
    if m["rooms"]: m["boxes"] = complement_walls(m) + m["boxes"]
    # 架枪入口的假人朝向自动对准架枪位置：冲出来时玩家正好在它的视野圆锥中间。
    for h in m["holds"]:
        hx, _, hz, _ = h["pos"]
        h["entries"] = [(name, x, y, z, round(math.degrees(math.atan2(hx - (x + px), hz - (z + pz)))) % 360, beh, px, pz, width, prefire)
                        for name, x, y, z, yaw, beh, px, pz, width, prefire in h["entries"]]

def touching(a, b, min_gap=1.0):
    _, ax0, ax1, az0, az1 = a; _, bx0, bx1, bz0, bz1 = b
    if abs(ax1 - bx0) < 1e-6 or abs(bx1 - ax0) < 1e-6: return min(az1, bz1) - max(az0, bz0) >= min_gap
    if abs(az1 - bz0) < 1e-6 or abs(bz1 - az0) < 1e-6: return min(ax1, bx1) - max(ax0, bx0) >= min_gap
    return min(ax1, bx1) - max(ax0, bx0) > 0 and min(az1, bz1) - max(az0, bz0) > 0

def check_rooms(m):
    rooms = [r for r in m["rooms"] if r[0] != "窗口"]  # 窗口有窗台，不算走得通的通道
    if not rooms: return []
    seen, todo = {0}, [0]
    while todo:
        i = todo.pop()
        for j, r in enumerate(rooms):
            if j not in seen and touching(rooms[i], r): seen.add(j); todo.append(j)
    return [f"区域 {r[0]} ({r[1]},{r[3]}) 和出生点不连通" for j, r in enumerate(rooms) if j not in seen]

def check_path(m, label, x, y, z, px, pz):
    errs = []
    for i in range(1, 11):
        t = i / 10
        errs += check_spot(m, f"{label}路线{t:.1f}", x + px * t, y, z + pz * t)
    return errs

def check(m):
    errs = check_rooms(m)
    names = [p[0] for p in m["points"]]
    if len(set(names)) != len(names): errs.append("点位名称重复")
    for rname, (rx, ry, rz, _), route in m["routes"]:
        errs += check_spot(m, f"路线 {rname} 起点", rx, ry, rz, 0.35)
        errs += [f"路线 {rname} 里的 {n} 不是点位名称" for n in route if n not in names]
    for h in m["holds"]:
        hx, hy, hz, _ = h["pos"]
        errs += check_spot(m, f"架枪 {h['name']}", hx, hy, hz, 0.35)
        for name, x, y, z, yaw, beh, px, pz, width, prefire in h["entries"]:
            errs += check_spot(m, f"{h['name']}/{name}", x, y, z)
            errs += check_path(m, f"{h['name']}/{name}冲出", x, y, z, px, pz)
    sx, sy, sz, _ = m["spawn"]; errs += check_spot(m, "出生点", sx, sy, sz, 0.35)
    ax, ay, az, _ = m["aim"]; errs += check_spot(m, "甩枪出生点", ax, ay, az, 0.35)
    for name, x, y, z, yaw, beh, px, pz, width, prefire in m["points"]:
        errs += check_spot(m, name, x, y, z)
        if beh == "Peek":
            errs += check_path(m, f"{name}探头", x, y, z, px, pz)
        if beh == "Strafe":
            rad = math.radians(yaw); rx, rz = math.cos(rad), -math.sin(rad)
            for i in range(-5, 6):
                t = i / 10 * width
                errs += check_spot(m, f"{name}横跳{t:+.1f}", x + rx * t, y, z + rz * t)
    # 甩枪出生点到刷新区域的距离
    (cx, cy, cz), (wx, wy, wz) = m["area"]
    far = max(math.hypot(cx + dx * wx / 2 - ax, cz + dz * wz / 2 - az) for dx in (-1, 1) for dz in (-1, 1))
    if far > 45: errs.append(f"刷新区域最远角距离甩枪出生点 {far:.0f} 米，超过 40 米太多")
    return errs

# ---------------------------------------------------------------- 生成 C#
def f(v): return f"{float(v):g}f"

def emit(maps):
    out = ["// <auto-generated>由 tools/maps/maps.py 生成，请修改那个文件后重新运行，不要直接改这里。</auto-generated>",
           "using AimTrainer.Bots;", "using AimTrainer.Game;", "using UnityEngine;", "using UnityEngine.SceneManagement;", "",
           "namespace AimTrainer.EditorTools", "{",
           "    /// <summary>仿端游地图的白模场景（近似布局，不是精确复刻）。数据来源：tools/maps/maps.py。</summary>",
           "    public static class ValorantMapBuilders", "    {"]
    for m in maps:
        x0, x1, z0, z1 = m["bounds"]
        out += [f"        /// <summary>{m['title']}：{m['desc']}</summary>",
                f"        public static void {m['builder']}()", "        {",
                "            SceneKit.EnsureFolders();",
                f"            SceneKit.Palette p = SceneKit.CreatePalette(SceneCatalog.{m['scene']}Scene, new Vector2({f(x1-x0)}, {f(z1-z0)}));",
                "            Scene scene = SceneKit.NewScene();",
                "            Transform level = SceneKit.Group(null, \"Level\");",
                f"            SceneKit.Shell(level, {f(x0)}, {f(x1)}, {f(z0)}, {f(z1)}, {f(W)}, p);"]
        for name, bx0, bx1, bz0, bz1, h, y, mat in m["boxes"]:
            out.append(f"            SceneKit.Box(level, \"{name}\", {f(bx0)}, {f(bx1)}, {f(bz0)}, {f(bz1)}, {f(h)}, p.{mat}, {f(y)});")
        for name, axis, lo, hi, w0, w1, rise, base in m["ramps"]:
            fn = "RampAlongX" if axis == "x" else "RampAlongZ"
            out.append(f"            SceneKit.{fn}(level, \"{name}\", {f(lo)}, {f(hi)}, {f(w0)}, {f(w1)}, {f(rise)}, p.Platform, {f(base)});")
        out.append("            Transform bots = SceneKit.Group(null, \"Bots\");")
        var = {}
        for k, (name, x, y, z, yaw, beh, px, pz, width, prefire) in enumerate(m["points"]):
            var[name] = f"p{k}"
            out.append(f"            BotSpawnPoint p{k} = SceneKit.Point(bots, \"{name}\", new Vector3({f(x)}, {f(y)}, {f(z)}), {f(yaw)}, BotBehavior.{beh}, "
                       f"new Vector3({f(px)}, 0f, {f(pz)}), {f(width)}, {'true' if prefire else 'false'});")
        if m["rooms"]:
            out.append("            Transform regions = SceneKit.Group(null, \"Regions\");")
            for name, rx0, rx1, rz0, rz1 in m["rooms"]:
                out.append(f"            SceneKit.Region(regions, \"{name}\", {f(rx0)}, {f(rx1)}, {f(rz0)}, {f(rz1)});")
        if m["routes"]:
            out.append("            Transform routes = SceneKit.Group(null, \"Routes\");")
            for rname, (rx, ry, rz, ryaw), route in m["routes"]:
                refs = ", ".join(var[n] for n in route)
                out.append(f"            SceneKit.Route(routes, \"{rname}\", new Vector3({f(rx)}, {f(ry)}, {f(rz)}), {f(ryaw)}, new[] {{ {refs} }});")
        for k, h in enumerate(m["holds"]):
            hx, hy, hz, hyaw = h["pos"]
            out.append(f"            Transform hold{k} = SceneKit.Hold(bots, \"{h['name']}\", new Vector3({f(hx)}, {f(hy)}, {f(hz)}), {f(hyaw)});")
            for name, x, y, z, yaw, beh, px, pz, width, prefire in h["entries"]:
                out.append(f"            SceneKit.Point(hold{k}, \"{name}\", new Vector3({f(x)}, {f(y)}, {f(z)}), {f(yaw)}, BotBehavior.{beh}, "
                           f"new Vector3({f(px)}, 0f, {f(pz)}), {f(width)}, false);")
        (cx, cy, cz), (wx, wy, wz) = m["area"]
        sx, sy, sz, syaw = m["spawn"]; ax, ay, az, ayaw = m["aim"]
        out += [f"            SceneKit.Area(bots, \"SiteSpawnArea\", new Vector3({f(cx)}, {f(cy)}, {f(cz)}), new Vector3({f(wx)}, {f(wy)}, {f(wz)}));",
                f"            SceneKit.PlayerRig rig = SceneKit.CreatePlayer(new Vector3({f(sx)}, {f(sy)}, {f(sz)}), {f(syaw)}, withTraining: true);",
                "            var aimSpawn = new GameObject(\"AimModeSpawn\").transform;",
                f"            aimSpawn.SetPositionAndRotation(new Vector3({f(ax)}, {f(ay)}, {f(az)}), Quaternion.Euler(0f, {f(ayaw)}, 0f));",
                "            rig.Session.SetAimSpawn(aimSpawn);",
                f"            SceneKit.SaveScene(scene, SceneCatalog.{m['scene']}Scene);", "        }", ""]
    out[-1:] = ["    }", "}", ""]
    return "\n".join(out)

if __name__ == "__main__":
    bad = False
    for m in MAPS: prepare(m)
    for m in MAPS:
        e = check(m)
        print(f"{m['title']}: {'OK' if not e else str(len(e)) + ' 个问题'}（{len(m['boxes'])} 个方块）")
        for x in e: print("   ", x)
        bad |= bool(e)
    if bad: sys.exit(1)
    root = os.path.join(os.path.dirname(__file__), "..", "..")
    path = os.path.join(root, "Assets", "AimTrainer", "Editor", "ValorantMapBuilders.cs")
    with open(path, "w", encoding="utf-8", newline="\n") as fh: fh.write(emit(MAPS))
    print("已生成", os.path.normpath(path))
