import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import geo, spots
from PIL import Image, ImageDraw
def floor_at(px, py, high=False):
    x, z = geo.px2w(px, py)
    fl = [f for f in geo.floors(x, z) if f < 15]
    if not fl: return x, None, z
    y = max(fl) if high else min(fl)
    return x, y, z
nudges = []
def resolve(item, key='at'):
    """站位不合格（没有地面或净空不够）时，在 2 米（约 14 像素）范围内找最近的合格位置。"""
    px, py = item[key]; high = item.get('high', False)
    for r in range(0, 15, 2):
        ring = [(px, py)] if r == 0 else [(px + dx, py + dy) for dx in range(-r, r + 1, 2) for dy in range(-r, r + 1, 2) if max(abs(dx), abs(dy)) == r]
        for qx, qy in ring:
            x, y, z = floor_at(qx, qy, high=high)
            if y is not None and geo.clear(x, y, z):
                if r: nudges.append(f"{item.get('name', key)}.{key} 挪到 ({qx},{qy})")
                return [round(x, 2), y, round(z, 2)]
    x, y, z = floor_at(px, py, high=high)
    return [round(x, 2), y, round(z, 2)]
problems = []
def check(label, p):
    if p[1] is None: problems.append(f"{label}: 没有地面"); return
    if not geo.clear(p[0], p[1], p[2]): problems.append(f"{label}: 净空不够（{p}）")
out = dict(routes=[], holds=[])
def path_samples(path):
    """路线折线上每 1 米一个玩家站位（地面高度沿路连续追踪）。"""
    out = []; prev = None
    for (ax, ay), (bx, by) in zip(path, path[1:]):
        wa = geo.px2w(ax, ay); wb = geo.px2w(bx, by)
        n = max(1, int(math.dist(wa, wb)))
        for i in range(n):
            t = i / n; x = wa[0] + (wb[0] - wa[0]) * t; z = wa[1] + (wb[1] - wa[1]) * t
            f = [v for v in geo.floors(x, z) if v < 15]
            if not f: continue
            if prev is None: y = min(f)
            else:
                # 能走上去的（高不超过 1.5 米）里取最接近的；都太高时取最低的（往下跳）。
                ok = [v for v in f if v - prev <= 1.5]
                if not ok: continue
                y = min(ok, key=lambda v: abs(v - prev))
            prev = y; out.append((x, y, z))
    return out
def first_seen(pos, samples):
    for k, sp in enumerate(samples):
        if math.dist((sp[0], sp[2]), (pos[0], pos[2])) < 3: continue
        if geo.los(geo.eye(sp), geo.eye(pos)): return k
    return None
for r in spots.ROUTES:
    s0 = resolve(r, 'start'); check(r['name'] + ' 起点', s0)
    samples = path_samples(r['path'])
    pts = []
    for p in r['points']:
        w = resolve(p); check(p['name'], w)
        k = first_seen(w, samples) if w[1] is not None else None
        if k is None or k == 0:
            # 在 3 米（约 21 像素）内找一个沿路能看到、但从起点看不到的位置
            best = None
            for rad in range(3, 22, 3):
                for ang in range(0, 360, 30):
                    q = dict(p); q['at'] = (p['at'][0] + rad * math.cos(math.radians(ang)), p['at'][1] + rad * math.sin(math.radians(ang)))
                    x, y, z = floor_at(*q['at'], high=p.get('high', False))
                    if y is None or not geo.clear(x, y, z): continue
                    kk = first_seen((x, y, z), samples)
                    if kk not in (None, 0): best = ([round(x, 2), y, round(z, 2)], kk, q['at']); break
                if best: break
            if best:
                nudges.append(f"{p['name']} 挪到 ({best[2][0]:.0f},{best[2][1]:.0f})，路线第 {best[1]} 米处可见"); w, k = best[0], best[1]
            else:
                problems.append(f"{p['name']}: 沿路线看不到（或在起点就能看到）")
        face = list(samples[k]) if k is not None else w
        pts.append(dict(name=p['name'], pos=w, face=[round(face[0], 2), face[1], round(face[2], 2)], seen_at=k))
    f = floor_at(*r['face'])
    out['routes'].append(dict(name=r['name'], start=s0, face=[f[0], f[1] or s0[1], f[2]], points=pts, path_len=len(samples)))
for h in spots.HOLDS:
    s = resolve(h); check(h['name'], s)
    f = floor_at(*h['face'])
    ents = []
    for e in h['entries']:
        a = resolve(e); b = resolve(e, 'to'); check(e['name'] + ' 起点', a); check(e['name'] + ' 终点', b)
        if a[1] is not None and b[1] is not None:
            # 冲出路线上每隔 0.5 米检查一次
            # 冲出路线：每 0.4 米检查一次脚下地面（高度变化不超过 0.45 米，假人能走上台阶）和身体净空。
            n = max(2, int(math.dist((a[0], a[2]), (b[0], b[2])) / 0.4))
            prev = a[1]
            for i in range(1, n + 1):
                t = i / n; px = a[0] + (b[0] - a[0]) * t; pz = a[2] + (b[2] - a[2]) * t
                g = [g for g in geo.floors(px, pz) if abs(g - prev) <= 0.45]
                if not g: problems.append(f"{e['name']} 冲出路线 {t:.2f} 地面断了（上一格 {prev}）"); break
                prev = min(g, key=lambda v: abs(v - prev))
                if not geo.clear(px, prev, pz, r=0.3): problems.append(f"{e['name']} 冲出路线 {t:.2f} 会卡住"); break
            se = geo.los(geo.eye(s), geo.eye(a)); ee = geo.los(geo.eye(s), geo.eye(b))
            print(f"  {h['name']}/{e['name']}: 起点{'可见!!' if se else '隐藏'} 终点{'可见' if ee else '看不到!!'}")
        ents.append(dict(name=e['name'], pos=a, to=b))
    out['holds'].append(dict(name=h['name'], pos=s, face=[f[0], f[1] or s[1], f[2]], entries=ents))
for key in ('SPAWN', 'AIM'):
    it = getattr(spots, key); w = resolve(it); check(key, w); f = floor_at(*it['face'])
    out[key.lower()] = dict(pos=w, face=[f[0], f[1] or w[1], f[2]])
c, half = spots.AIM_AREA; ax, ay, az = floor_at(*c); out['aim_area'] = dict(center=[ax, ay, az], half=half)
json.dump(out, open(os.path.join(geo.CACHE, '..', 'placements.json'), 'w'), ensure_ascii=False, indent=1)
print('\n'.join(nudges)); print('\n'.join(problems) if problems else '全部点位：有地面、净空足够')
# 画在小地图上
im = Image.open(os.path.join(geo.CACHE, 'ascent-minimap.png')).convert('RGBA'); bg = Image.new('RGBA', im.size, (25, 25, 25, 255)); bg.alpha_composite(im); im = bg.convert('RGB'); d = ImageDraw.Draw(im)
def dot(pxy, col, label):
    X, Y = pxy; d.ellipse([X-5, Y-5, X+5, Y+5], fill=col); d.text((X+7, Y-6), label, fill=col)
for i, r in enumerate(spots.ROUTES):
    col = [(255, 80, 80), (80, 200, 255), (255, 200, 60)][i]
    dot(r['start'], (255, 255, 255), f"R{i+1} start"); prev = r['start']
    for k, p in enumerate(r['points']):
        dot(p['at'], col, f"{i+1}.{k+1}")
for h in spots.HOLDS:
    dot(h['at'], (80, 255, 120), 'H')
    for e in h['entries']:
        d.line([e['at'], e['to']], fill=(80, 255, 120), width=2); dot(e['to'], (80, 255, 120), '')
im.save(os.path.join(geo.CACHE, 'placements_minimap.png'))
