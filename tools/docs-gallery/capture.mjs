import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { resolve, join, extname, sep } from 'node:path';
import assert from 'node:assert/strict';

// Public screenshots only: this server reads synthetic fixtures, never a live agent.
const repo = resolve(fileURLToPath(new URL('../..', import.meta.url)));
const fixtures = resolve(repo, process.argv[2] ?? 'artifacts/docs-gallery');
const output = join(repo, 'docs/images');
const app = join(repo, 'artifacts/configurator/browser');
await mkdir(output, { recursive: true });
const fixture = async name => JSON.parse(await readFile(join(fixtures, name + '.json'), 'utf8'));
const state = await fixture('state');
const config = await fixture('config');
const catalog = await fixture('catalog');
const contentTypes = { '.html': 'text/html', '.js': 'application/javascript', '.css': 'text/css', '.png': 'image/png', '.woff2': 'font/woff2' };
const server = createServer(async (req, res) => {
  try {
    const path = new URL(req.url, 'http://localhost').pathname;
    const data = path === '/api/state' ? { ...state, timestamp: new Date().toISOString() }
      : path === '/api/config' ? config : path === '/api/widget-slots' ? catalog
      : path === '/live/negotiate' ? { negotiateVersion: 1, connectionId: 'documentation', connectionToken: 'documentation', availableTransports: [{ transport: 'WebSockets', transferFormats: ['Text', 'Binary'] }] }
      : null;
    if (data) { res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify(data)); return; }
    if (path === '/api/preview.png') { res.setHeader('Content-Type', 'image/png'); res.end(await readFile(join(output, 'desktop.png'))); return; }
    const file = resolve(app, '.' + (path === '/' ? '/index.html' : path));
    if (!file.startsWith(app + sep)) { res.writeHead(403).end(); return; }
    res.setHeader('Content-Type', contentTypes[extname(file)] ?? 'application/octet-stream');
    res.end(await readFile(file));
  } catch { res.writeHead(404).end(); }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const origin = `http://127.0.0.1:${server.address().port}`;
let browser;
try {
  browser = await chromium.launch({ headless: true, ...(process.env.PULSEDECK_CHROMIUM ? { executablePath: process.env.PULSEDECK_CHROMIUM } : {}) });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1050 }, deviceScaleFactor: 1 });
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.route('**/*', route => route.request().url().startsWith(origin + '/') ? route.continue() : route.abort());
  await page.routeWebSocket('**/live?*', ws => {
    ws.onMessage(message => {
      if (String(message).includes('"protocol"')) {
        ws.send('{}\x1e');
        ws.send(JSON.stringify({ type: 1, target: 'state', arguments: [{ ...state, timestamp: new Date().toISOString() }] }) + '\x1e');
      }
    });
  });
  await page.goto(origin);
  await page.getByText('Agent online', { exact: true }).waitFor();
  await page.locator('.screen img').evaluate(img => img.decode());
  await page.screenshot({ path: join(output, 'configurator.png'), fullPage: true });
  assert.deepEqual(errors, [], 'Configurator console errors');
  const image = async name => `data:image/png;base64,${(await readFile(join(output, name + '.png'))).toString('base64')}`;
  const pictures = { desktop: await image('desktop'), gaming: await image('gaming'), music: await image('music') };
  const css = `*{box-sizing:border-box}body{margin:0;background:#080f14;color:#f2f6f2;font-family:Arial,sans-serif}main{position:relative;overflow:hidden;background:radial-gradient(ellipse at 90% 0%,#233b2b 0%,transparent 46%),#080f14;padding:58px 68px}header{display:flex;align-items:center;justify-content:space-between;font-size:22px;font-weight:700;letter-spacing:1px}.mark{display:inline-flex;align-items:center;gap:14px}.symbol{display:inline-flex;align-items:center;justify-content:center;width:34px;height:34px;background:#a9ff69;color:#080f14;border-radius:8px;font-size:28px;font-weight:700;font-style:normal;letter-spacing:0}.tag{font-size:13px;font-weight:400;letter-spacing:2px;color:#b4c6b5;border:1px solid #415345;padding:10px 15px;border-radius:30px}h1{font-size:76px;line-height:1.05;letter-spacing:-3px;margin:35px 0 14px}h1 span{color:#a9ff69}.sub{font-size:22px;color:#aab7bd;margin:0 0 32px;line-height:1.5}.frame{border:1px solid #334941;border-radius:14px;padding:10px;background:#101c20;box-shadow:0 16px 35px #0005}.frame img{display:block;width:100%;border-radius:5px}.caption{display:flex;align-items:center;justify-content:space-between;padding:0 2px 11px;font-size:13px;letter-spacing:1.8px;color:#c4d5d0}.caption b{color:#a9ff69;font-weight:400}.small{display:grid;grid-template-columns:1fr 1fr;gap:20px;margin-top:20px}.small .caption{font-size:12px}.footer{display:flex;justify-content:space-between;margin-top:24px;font-size:13px;color:#94a5a8;letter-spacing:.5px}.footer strong{color:#d3e1dc;font-weight:400}`;
  const frame = (name, label, description) => `<section class="frame"><div class="caption"><b>${label}</b><span>${description}</span></div><img src="${pictures[name]}" alt="${label} documentation preview"></section>`;
  const common = `<header><div class="mark"><i class="symbol">P</i>PULSEDECK</div><span class="tag">OPEN SOURCE · WINDOWS</span></header>`;
  const hero = `<!doctype html><html><head><meta charset="utf-8"><style>${css}</style></head><body><main style="width:1600px">${common}<h1>Your PC. <span>At a glance.</span></h1><p class="sub">Hardware, games and music. A dedicated display that follows what you do.</p>${frame('gaming', '01 / GAMING', 'FPS · SESSION TIME · YOUR SQUAD')}<div class="small">${frame('desktop', '02 / DESKTOP', 'THE ESSENTIALS')}${frame('music', '03 / MUSIC', 'SPOTIFY · LYRICS')}</div><div class="footer"><strong>Built with .NET, Angular &amp; SkiaSharp. Built for contributors.</strong><span>Actual renderer · illustrative data &amp; original artwork</span></div></main></body></html>`;
  const social = `<!doctype html><html><head><meta charset="utf-8"><style>${css}main{padding:36px 50px;width:1280px;height:640px}h1{font-size:62px;margin:25px 0 12px}.sub{font-size:19px;margin-bottom:23px}.frame{padding:8px}.caption{font-size:11px;padding-bottom:9px}.footer{margin-top:18px;font-size:11px}</style></head><body><main>${common}<h1>Your PC. <span>At a glance.</span></h1><p class="sub">A Windows display companion for hardware, games and music.</p>${frame('gaming','DESKTOP / GAMING / MUSIC','1920 × 480')}<div class="footer"><strong>github.com/cicarulez/PulseDeck</strong><span>Sample data · GPL-3.0-or-later · Contributors welcome</span></div></main></body></html>`;
  for (const [name, html, width, height] of [['hero', hero, 1600, 1100], ['social-preview', social, 1280, 640]]) {
    await page.setViewportSize({ width, height });
    await page.setContent(html);
    await page.locator('img').evaluateAll(images => Promise.all(images.map(img => img.decode())));
    await page.locator('main').screenshot({ path: join(output, name + '.png') });
    await writeFile(join(fixtures, name + '.html'), html);
  }
  console.log('Saved configurator, hero and social-preview images. Only isolated documentation fixtures were used.');
} finally {
  await browser?.close();
  await new Promise(resolve => server.close(resolve));
}
