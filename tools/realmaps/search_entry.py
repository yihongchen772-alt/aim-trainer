import sys, math; sys.path.insert(0, __import__('os').path.dirname(__import__('os').path.abspath(__file__)))
import geo
def fl(px, py, near=None, high=False):
    x, z = geo.px2w(px, py); f = [v for v in geo.floors(x, z) if v < 15]
    if not f: return None
    v = (min(f, key=lambda q: abs(q - near)) if near is not None else (max(f) if high else min(f)))
    return (x, v, z)
def path_ok(a, b):
    n = max(2, int(math.dist((a[0], a[2]), (b[0], b[2])) / 0.4)); prev = a[1]
    for i in range(1, n + 1):
        t = i / n; x = a[0] + (b[0] - a[0]) * t; z = a[2] + (b[2] - a[2]) * t
        g = [g for g in geo.floors(x, z) if abs(g - prev) <= 0.45]
        if not g: return False
        prev = min(g, key=lambda v: abs(v - prev))
        if not geo.clear(x, prev, z, r=0.3): return False
    return True
def search(hold_px, region, step=6, lens=(25, 35, 45)):
    h = fl(*hold_px); he = geo.eye(h); found = []
    for py in range(region[1], region[3], step):
        for px in range(region[0], region[2], step):
            b = fl(px, py)
            if b is None or not geo.clear(*b) or not geo.los(he, geo.eye(b)): continue
            for L in lens:
                for ang in range(0, 360, 45):
                    sx = px + L * math.cos(math.radians(ang)); sy = py + L * math.sin(math.radians(ang))
                    a = fl(sx, sy, near=b[1])
                    if a is None or abs(a[1] - b[1]) > 1.5 or not geo.clear(*a) or geo.los(he, geo.eye(a)): continue
                    if path_ok(a, b): found.append(((round(sx), round(sy)), (px, py), round(math.dist((a[0], a[2]), (b[0], b[2])), 1)))
    return found
if __name__ == '__main__':
    import json
    which = sys.argv[1]
    if which == 'B':
        r = search((358, 824), (370, 640, 470, 720))
    else:
        r = search((410, 510), (440, 430, 600, 570), step=8)
    for x in r[:40]: print(x)
    print(len(r))
