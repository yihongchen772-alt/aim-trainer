"""
把 glb 里的 WebP 贴图转成 JPG（不透明）或 PNG（有透明），去掉 EXT_texture_webp，并去掉没有物体的空场景。
输出文件名以 .gltf 结尾时，贴图写成单独的图片文件（同目录下的 <名称>_textures/），网格数据写成 .bin：
这样 Unity 会用 TextureImporter 导入贴图并压缩，显存比嵌在 glb 里（不压缩）小得多。
"""
import json, os, struct, io, sys
from PIL import Image
src, dst = sys.argv[1], sys.argv[2]
b = open(src, 'rb').read()
cl = struct.unpack('<I', b[12:16])[0]
j = json.loads(b[20:20 + cl])
off = 20 + cl
bl = struct.unpack('<I', b[off:off + 4])[0]
binc = b[off + 8: off + 8 + bl]

views = j['bufferViews']
new_bin = bytearray()
new_views = []
image_views = {img['bufferView']: k for k, img in enumerate(j.get('images', [])) if 'bufferView' in img}
converted = {}
external = dst.endswith('.gltf')
base = os.path.splitext(os.path.basename(dst))[0]
tex_dir = os.path.join(os.path.dirname(os.path.abspath(dst)), base + '_textures')
if external: os.makedirs(tex_dir, exist_ok=True)
drop_views = set()
for vi, bv in enumerate(views):
    data = binc[bv.get('byteOffset', 0): bv.get('byteOffset', 0) + bv['byteLength']]
    if vi in image_views and j['images'][image_views[vi]].get('mimeType') == 'image/webp':
        im = Image.open(io.BytesIO(data))
        if max(im.size) > 1024:  # 2048 的贴图缩到 1024，显存占用降低约四分之一
            im = im.resize((max(1, im.size[0] * 1024 // max(im.size)), max(1, im.size[1] * 1024 // max(im.size))), Image.LANCZOS)
        has_alpha = im.mode in ('RGBA', 'LA') and im.getchannel('A').getextrema()[0] < 250
        out = io.BytesIO()
        if has_alpha:
            im.save(out, 'PNG', optimize=True); mime = 'image/png'
        else:
            im.convert('RGB').save(out, 'JPEG', quality=88, optimize=True); mime = 'image/jpeg'
        data = out.getvalue()
        converted[image_views[vi]] = mime
    if external and vi in image_views:
        k = image_views[vi]; img = j['images'][k]
        mime = converted.get(k, img.get('mimeType'))
        name = f"tex_{k:03d}.{'png' if mime == 'image/png' else 'jpg'}"
        open(os.path.join(tex_dir, name), 'wb').write(data)
        img.pop('bufferView', None); img.pop('mimeType', None); img['uri'] = base + '_textures/' + name
        drop_views.add(vi)
        data = b''
    while len(new_bin) % 4: new_bin.append(0)
    nb = dict(bv); nb['byteOffset'] = len(new_bin); nb['byteLength'] = len(data)
    new_bin += data
    new_views.append(nb)
# 去掉图片用过的 bufferView（外部图片模式），其余 bufferView 重新编号。
remap = {}
kept = []
for vi, nb in enumerate(new_views):
    if vi in drop_views: continue
    remap[vi] = len(kept); kept.append(nb)
j['bufferViews'] = kept
for a in j.get('accessors', []):
    if 'bufferView' in a: a['bufferView'] = remap[a['bufferView']]
for img in j.get('images', []):
    if 'bufferView' in img: img['bufferView'] = remap[img['bufferView']]
for k, mime in converted.items():
    if 'bufferView' in j['images'][k]: j['images'][k]['mimeType'] = mime
# 去掉没有物体的空场景（例如烘焙用的 SourceMaterialBake），默认场景跟着重新编号。
scenes = j.get('scenes', [])
default = scenes[j.get('scene', 0)] if scenes else None
j['scenes'] = [sc for sc in scenes if sc.get('nodes')]
if default is not None and default in j['scenes']: j['scene'] = j['scenes'].index(default)
for t in j.get('textures', []):
    ext = t.get('extensions', {}).pop('EXT_texture_webp', None)
    if ext is not None:
        t['source'] = ext['source']
    if 'extensions' in t and not t['extensions']: del t['extensions']
for key in ('extensionsUsed', 'extensionsRequired'):
    if key in j:
        j[key] = [e for e in j[key] if e != 'EXT_texture_webp']
        if not j[key]: del j[key]
if external:
    while len(new_bin) % 4: new_bin.append(0)
    j['buffers'] = [{'byteLength': len(new_bin), 'uri': base + '.bin'}]
    open(os.path.join(os.path.dirname(os.path.abspath(dst)), base + '.bin'), 'wb').write(new_bin)
    json.dump(j, open(dst, 'w'), separators=(',', ':'))
    print('converted', len(converted), 'images; external textures in', tex_dir, '; bin', len(new_bin) // 1024 // 1024, 'MB')
    sys.exit(0)
j['buffers'] = [{'byteLength': len(new_bin)}]
js = json.dumps(j, separators=(',', ':')).encode()
while len(js) % 4: js += b' '
while len(new_bin) % 4: new_bin.append(0)
total = 12 + 8 + len(js) + 8 + len(new_bin)
with open(dst, 'wb') as f:
    f.write(struct.pack('<4sII', b'glTF', 2, total))
    f.write(struct.pack('<I4s', len(js), b'JSON')); f.write(js)
    f.write(struct.pack('<I4s', len(new_bin), b'BIN\0')); f.write(new_bin)
print('converted', len(converted), 'images; png', sum(1 for m in converted.values() if m == 'image/png'), '; size', total // 1024 // 1024, 'MB')
