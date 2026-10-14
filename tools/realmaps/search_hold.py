import sys, math; sys.path.insert(0, __import__('os').path.dirname(__import__('os').path.abspath(__file__)))
import geo
def lowest(px,py, near=None):
    x,z=geo.px2w(px,py); fl=[f for f in geo.floors(x,z) if f<15]
    if not fl: return None
    if near is not None: fl=sorted(fl,key=lambda f:abs(f-near))
    else: fl=sorted(fl)
    return (x,fl[0],z)
def search(name, region, entries, step=6):
    """region: 像素矩形；entries: [(起点px, 终点px)]。返回能看到全部终点、看不到任何起点的站位，按到终点的平均距离排序。"""
    E=[(lowest(*a),lowest(*b)) for a,b in entries]
    if any(a is None or b is None for a,b in E): print('entry 没有地面', E); return []
    res=[]
    for py in range(region[1],region[3],step):
        for px in range(region[0],region[2],step):
            p=lowest(px,py)
            if p is None or not geo.clear(*p): continue
            e=geo.eye(p)
            if all(geo.los(e,geo.eye(b)) for a,b in E) and not any(geo.los(e,geo.eye(a)) for a,b in E):
                d=sum(math.dist((p[0],p[2]),(b[0],b[2])) for a,b in E)/len(E)
                res.append((round(d,1),(px,py),round(p[1],2)))
    res.sort(key=lambda r: -r[0])
    print(name, len(res), 'candidates', res[:8])
    return res
if __name__=='__main__':
    search('B', (212,750,375,872), [((420,655),(372,668)),((420,690),(372,678))])
    search('B(lane-only)', (212,750,375,872), [((420,655),(372,668))])
