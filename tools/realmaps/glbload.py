import json, struct, numpy as np
CT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}
def load(path):
    b = open(path, 'rb').read()
    cl = struct.unpack('<I', b[12:16])[0]
    j = json.loads(b[20:20 + cl])
    off = 20 + cl
    bl = struct.unpack('<I', b[off:off + 4])[0]
    binc = b[off + 8: off + 8 + bl]
    return j, binc
def acc(j, binc, i):
    a = j['accessors'][i]; bv = j['bufferViews'][a['bufferView']]
    n = NC[a['type']]; dt = np.dtype(CT[a['componentType']])
    start = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    stride = bv.get('byteStride', 0)
    if stride and stride != n * dt.itemsize:
        raw = np.frombuffer(binc, np.uint8, count=stride * a['count'], offset=start).reshape(a['count'], stride)
        return raw[:, :n * dt.itemsize].copy().view(dt).reshape(a['count'], n)
    return np.frombuffer(binc, dt, count=a['count'] * n, offset=start).reshape(a['count'], n)
def quat_mat(q):
    x, y, z, w = q
    return np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)], [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)], [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]])
def node_mat(n):
    if 'matrix' in n: return np.array(n['matrix']).reshape(4, 4).T
    m = np.eye(4)
    r = quat_mat(n.get('rotation', [0, 0, 0, 1])); s = np.array(n.get('scale', [1, 1, 1]))
    m[:3, :3] = r * s; m[:3, 3] = n.get('translation', [0, 0, 0]); return m
def triangles(path):
    """返回 (名称, 世界坐标三角形 Nx3x3) 列表（glTF 坐标：右手、Y 向上）。"""
    j, binc = load(path)
    out = []
    def walk(i, parent):
        n = j['nodes'][i]; m = parent @ node_mat(n)
        if 'mesh' in n:
            for p in j['meshes'][n['mesh']]['primitives']:
                if p.get('mode', 4) != 4: continue
                pos = acc(j, binc, p['attributes']['POSITION']).astype(np.float64)
                idx = acc(j, binc, p['indices']).reshape(-1) if 'indices' in p else np.arange(len(pos))
                w = pos @ m[:3, :3].T + m[:3, 3]
                out.append((n.get('name', ''), w[idx.reshape(-1, 3)]))
        for c in n.get('children', []): walk(c, m)
    for r in j['scenes'][j.get('scene', 0)]['nodes']: walk(r, np.eye(4))
    return out
