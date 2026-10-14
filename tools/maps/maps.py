"""
仿端游地图的白模数据（单位：米；x 向东，z 向北，y 向上）。
运行 `python3 tools/maps/maps.py` 会：
  1. 检查每张图：假人点位、探头路线、横跳范围、出生点是否和几何体重叠，是否站在地面上；
  2. 生成 Assets/AimTrainer/Editor/ValorantMapBuilders.cs。
这些布局是按公开的地图俯视图和常用点位名称凭印象近似的，比例和位置都不是精确复刻。
"""
import math, os, sys

# box: (名称, xMin, xMax, zMin, zMax, 高度, 底面 y, 材质)
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

def check(m):
    errs = []
    sx, sy, sz, _ = m["spawn"]; errs += check_spot(m, "出生点", sx, sy, sz, 0.35)
    ax, ay, az, _ = m["aim"]; errs += check_spot(m, "甩枪出生点", ax, ay, az, 0.35)
    for name, x, y, z, yaw, beh, px, pz, width, prefire in m["points"]:
        errs += check_spot(m, name, x, y, z)
        if beh == "Peek":
            for i in range(1, 11):
                t = i / 10
                errs += check_spot(m, f"{name}探头路线{t:.1f}", x + px * t, y, z + pz * t)
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
        for name, x, y, z, yaw, beh, px, pz, width, prefire in m["points"]:
            out.append(f"            SceneKit.Point(bots, \"{name}\", new Vector3({f(x)}, {f(y)}, {f(z)}), {f(yaw)}, BotBehavior.{beh}, "
                       f"new Vector3({f(px)}, 0f, {f(pz)}), {f(width)}, {'true' if prefire else 'false'});")
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
    for m in MAPS:
        e = check(m)
        print(f"{m['title']}: {'OK' if not e else str(len(e)) + ' 个问题'}")
        for x in e: print("   ", x)
        bad |= bool(e)
    if bad: sys.exit(1)
    root = os.path.join(os.path.dirname(__file__), "..", "..")
    path = os.path.join(root, "Assets", "AimTrainer", "Editor", "ValorantMapBuilders.cs")
    with open(path, "w", encoding="utf-8", newline="\n") as fh: fh.write(emit(MAPS))
    print("已生成", os.path.normpath(path))
