"""
下载并准备真实地图素材（目前只有亚海悬城）：
  1. 从 SpikeAtlas 下载画面模型和碰撞模型（glb），从 valorant-api.com 下载官方小地图和地名；
  2. 把画面模型里的 WebP 贴图转成 JPG/PNG（Unity 的 glTF 导入器不支持 WebP），大于 1024 的贴图缩到 1024；
  3. 生成碰撞网格缓存（cache/ascent_col.npy），供 place.py 检查点位用。
结果：Assets/AimTrainer/RealMaps/Ascent/ 下的 ascent-visual.gltf（+ .bin 和 ascent-visual_textures/ 里的贴图，
单独的图片文件 Unity 才会压缩）、ascent-collision.glb、ascent-minimap.png。
素材归 Riot Games 所有（按 Riot 的 Legal Jibber Jabber 政策，仅限免费、非商业使用）。
用法：python3 tools/realmaps/fetch.py   （需要 numpy、Pillow）
"""
import json, os, shutil, subprocess, sys, urllib.request
HERE = os.path.dirname(os.path.abspath(__file__))
CACHE = os.path.join(HERE, 'cache')
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
OUT = os.path.join(ROOT, 'Assets', 'AimTrainer', 'RealMaps', 'Ascent')
MAPS = {
    'ascent': dict(
        visual='https://assets.spikeatlas.com/maps/ascent/f4fa161585df5a0b97d5eb498ef05f97f05c8ff11fc6754d22a56e52c06f26d9/ascent-visual.glb',
        collision='https://assets.spikeatlas.com/maps/ascent/ef12e5f817e927dd114b9e7cf2c9adb70af3050525e70e6e6bffd123535acaf3/ascent-collision.glb',
        minimap='https://media.valorant-api.com/maps/7eaecc1b-4337-bbf6-6ab9-04b8f06b3319/displayicon.png'),
}
def get(url, path):
    if os.path.exists(path): return
    print('下载', url)
    urllib.request.urlretrieve(url, path)
def main():
    os.makedirs(CACHE, exist_ok=True); os.makedirs(OUT, exist_ok=True)
    m = MAPS['ascent']
    get(m['visual'], os.path.join(CACHE, 'ascent-visual.webp.glb'))
    get(m['collision'], os.path.join(CACHE, 'ascent-collision.glb'))
    get(m['minimap'], os.path.join(CACHE, 'ascent-minimap.png'))
    for lang, name in (('en-US', 'maps_en.json'), ('zh-CN', 'maps_zh.json')):
        get(f'https://valorant-api.com/v1/maps?language={lang}', os.path.join(CACHE, name))
    subprocess.check_call([sys.executable, os.path.join(HERE, 'convert_webp.py'),
                           os.path.join(CACHE, 'ascent-visual.webp.glb'), os.path.join(OUT, 'ascent-visual.gltf')])
    shutil.copyfile(os.path.join(CACHE, 'ascent-collision.glb'), os.path.join(OUT, 'ascent-collision.glb'))
    shutil.copyfile(os.path.join(CACHE, 'ascent-minimap.png'), os.path.join(OUT, 'ascent-minimap.png'))
    # 渲染检查（render.js）用的画面模型（单个 glb 文件）
    subprocess.check_call([sys.executable, os.path.join(HERE, 'convert_webp.py'),
                           os.path.join(CACHE, 'ascent-visual.webp.glb'), os.path.join(CACHE, 'ascent-visual.glb')])
    sys.path.insert(0, HERE)
    import numpy as np
    from glbload import triangles
    tris = np.concatenate([t for _, t in triangles(os.path.join(CACHE, 'ascent-collision.glb'))])
    np.save(os.path.join(CACHE, 'ascent_col.npy'), tris.astype(np.float32))
    print('完成：', OUT)
if __name__ == '__main__':
    main()
