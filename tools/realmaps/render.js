// 用法：node render.js views.json 输出目录 [glb]
const { chromium } = require('/opt/node-tools/node_modules/playwright');
const fs = require('fs'); const http = require('http'); const path = require('path');
const root = __dirname;
const server = http.createServer((req, res) => {
  const p = path.join(root, decodeURIComponent(req.url.split('?')[0]));
  if (!p.startsWith(root) || !fs.existsSync(p)) { res.writeHead(404); return res.end(); }
  const type = p.endsWith('.js') ? 'text/javascript' : p.endsWith('.html') ? 'text/html' : 'application/octet-stream';
  res.writeHead(200, { 'Content-Type': type }); fs.createReadStream(p).pipe(res);
}).listen(8765);
(async () => {
  const views = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
  const out = process.argv[3]; fs.mkdirSync(out, { recursive: true });
  const glb = process.argv[4] || 'cache/ascent-visual.glb';
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 960, height: 540 } });
  page.on('console', m => { if (m.type() === 'error') console.log('page:', m.text().slice(0, 200)); });
  await page.goto('http://localhost:8765/view.html');
  await page.waitForFunction(() => window.ready);
  await page.evaluate(u => window.loadMap(u), '/' + glb);
  for (const v of views) {
    const data = await page.evaluate(v => window.render(v), v);
    fs.writeFileSync(path.join(out, v.name + '.jpg'), Buffer.from(data.split(',')[1], 'base64'));
    console.log('wrote', v.name);
  }
  await browser.close(); server.close();
})();
