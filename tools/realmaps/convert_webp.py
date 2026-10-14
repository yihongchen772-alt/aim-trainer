"""把 glb 里的 WebP 贴图转成 JPG（不透明）或 PNG（有透明），去掉 EXT_texture_webp，重新打包成 glb。"""
import json, struct, io, sys
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
    while len(new_bin) % 4: new_bin.append(0)
    nb = dict(bv); nb['byteOffset'] = len(new_bin); nb['byteLength'] = len(data)
    new_bin += data
    new_views.append(nb)
j['bufferViews'] = new_views
for k, mime in converted.items(): j['images'][k]['mimeType'] = mime
for t in j.get('textures', []):
    ext = t.get('extensions', {}).pop('EXT_texture_webp', None)
    if ext is not None:
        t['source'] = ext['source']
    if 'extensions' in t and not t['extensions']: del t['extensions']
for key in ('extensionsUsed', 'extensionsRequired'):
    if key in j:
        j[key] = [e for e in j[key] if e != 'EXT_texture_webp']
        if not j[key]: del j[key]
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
