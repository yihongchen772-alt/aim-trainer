"""碰撞网格上的几何查询：地面高度、净空、视线。坐标是 glTF 原始坐标（米，Y 向上）。"""
import numpy as np
import os
CACHE = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'cache')
T = np.load(os.path.join(CACHE, 'ascent_col.npy')).astype(np.float64)
V0, E1, E2 = T[:, 0], T[:, 1] - T[:, 0], T[:, 2] - T[:, 0]
LO = T.min(1); HI = T.max(1)
# 俯视 2 米网格：每格记录和它重叠的三角形，射线只测经过的格子里的三角形。
CELL = 2.0
GX0, GZ0 = LO[:, 0].min(), LO[:, 2].min()
_ix0 = ((LO[:, 0] - GX0) // CELL).astype(np.int64); _ix1 = ((HI[:, 0] - GX0) // CELL).astype(np.int64)
_iz0 = ((LO[:, 2] - GZ0) // CELL).astype(np.int64); _iz1 = ((HI[:, 2] - GZ0) // CELL).astype(np.int64)
_span = (_ix1 - _ix0 + 1) * (_iz1 - _iz0 + 1)
_big = np.nonzero(_span > 64)[0]
_keys = []; _ids = []
for da in range(8):
    for db in range(8):
        m = (_span <= 64) & (_ix0 + da <= _ix1) & (_iz0 + db <= _iz1)
        ids = np.nonzero(m)[0]
        _keys.append((_ix0[ids] + da) * 100000 + (_iz0[ids] + db)); _ids.append(ids)
_keys = np.concatenate(_keys); _ids = np.concatenate(_ids)
_order = np.argsort(_keys, kind='stable'); _keys = _keys[_order]; _ids = _ids[_order]
_uk, _start = np.unique(_keys, return_index=True)
_end = np.append(_start[1:], len(_keys))
CELLS = {int(k): _ids[a:b] for k, a, b in zip(_uk, _start, _end)}
def _candidates(o, end):
    n = int(np.linalg.norm((end - o)[[0, 2]]) / (CELL * 0.5)) + 2
    keys = set()
    for t in np.linspace(0, 1, n):
        p = o + (end - o) * t
        a = int((p[0] - GX0) // CELL); b = int((p[2] - GZ0) // CELL)
        for da in (-1, 0, 1):
            for db in (-1, 0, 1):
                keys.add((a + da) * 100000 + b + db)
    arrs = [CELLS[k] for k in keys if k in CELLS]
    arrs.append(_big)
    return np.unique(np.concatenate(arrs))
def ray(o, d, maxt):
    o = np.asarray(o, float); d = np.asarray(d, float)
    end = o + d * maxt
    idx = _candidates(o, end)
    bl = np.minimum(o, end) - 1e-3; bh = np.maximum(o, end) + 1e-3
    sel = idx[np.all((HI[idx] >= bl) & (LO[idx] <= bh), axis=1)]
    if len(sel) == 0: return None
    v0, e1, e2 = V0[sel], E1[sel], E2[sel]
    p = np.cross(d, e2); det = np.einsum('ij,ij->i', e1, p)
    ok = np.abs(det) > 1e-12
    inv = np.zeros_like(det); inv[ok] = 1 / det[ok]
    tv = o - v0; u = np.einsum('ij,ij->i', tv, p) * inv
    q = np.cross(tv, e1); v = (q @ d) * inv; t = np.einsum('ij,ij->i', e2, q) * inv
    hit = ok & (u >= 0) & (v >= 0) & (u + v <= 1) & (t > 1e-4) & (t <= maxt)
    return float(t[hit].min()) if hit.any() else None
def ground(x, z, y_from):
    """从 (x, y_from, z) 往下找地面，返回地面高度或 None。"""
    t = ray((x, y_from, z), (0, -1, 0), 30)
    return None if t is None else y_from - t
def clear(x, y, z, r=0.32, h=1.8):
    """以 (x,y,z) 为脚底的胶囊里有没有障碍：竖直和水平几条射线。"""
    for dx, dz in [(0, 0), (r, 0), (-r, 0), (0, r), (0, -r)]:
        if ray((x + dx, y + 0.45, z + dz), (0, 1, 0), h - 0.45) is not None: return False
    for hy in (0.6, 1.0, 1.6):
        for ang in range(0, 360, 45):
            a = np.radians(ang); d = (np.cos(a), 0, np.sin(a))
            if ray((x, y + hy, z), d, r) is not None: return False
    return True
def los(a, b):
    """两点之间有没有遮挡（a、b 是眼睛位置）。"""
    a = np.asarray(a, float); b = np.asarray(b, float); d = b - a; L = np.linalg.norm(d)
    return ray(a, d / L, L - 0.05) is None
def eye(p): return (p[0], p[1] + 1.6, p[2])
def spot(x, z, y_hint):
    """找 (x,z) 处离 y_hint 最近的地面（从 y_hint+1.5 往下打）。"""
    g = ground(x, z, y_hint + 1.5)
    return g

XM, YM, XA, YA = 7e-05, -7e-05, 0.813895, 0.573242
def px2w(px, py):
    """官方小地图像素（1024×1024）→ glTF 坐标 (x, z)。"""
    ue_y = (px / 1024 - XA) / XM; ue_x = (py / 1024 - YA) / YM
    return ue_x / 100, ue_y / 100
def w2px(x, z):
    return ((z * 100) * XM + XA) * 1024, ((x * 100) * YM + YA) * 1024
def floors(x, z, top=40.0):
    """(x, z) 处所有能站人的面（上方净空 ≥ 1.85 米），从高到低。"""
    out = []; y = top
    while y > -10:
        t = ray((x, y, z), (0, -1, 0), y + 10)
        if t is None: break
        h = y - t
        up = ray((x, h + 0.05, z), (0, 1, 0), 1.85)
        if up is None: out.append(round(h, 2))
        y = h - 0.05
    return out
