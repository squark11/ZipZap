// Etap 2: zrzuty /stores w 4 szerokościach + lista hostów żądań + pomiar wydajności (semantyka wł./wył.).
// node verify-e2.mjs <outDir> <urlSem> <urlNoSem>
import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';

const [outDir, urlSem, urlNoSem] = process.argv.slice(2);
mkdirSync(outDir, { recursive: true });
const profile = join(process.env.TEMP, 'edge-e2-' + Date.now());
const edge = spawn('C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  ['--headless=new', '--remote-debugging-port=9334', `--user-data-dir=${profile}`, '--no-first-run', '--disable-extensions', 'about:blank'],
  { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

let ws, nextId = 1; const pending = new Map(); const listeners = new Set();
async function connect() {
  for (let i = 0; i < 40; i++) {
    try {
      const v = await (await fetch('http://127.0.0.1:9334/json/version')).json();
      ws = new WebSocket(v.webSocketDebuggerUrl);
      await new Promise((res, rej) => { ws.onopen = res; ws.onerror = rej; });
      ws.onmessage = (m) => {
        const d = JSON.parse(m.data);
        if (d.id && pending.has(d.id)) { const { res, rej } = pending.get(d.id); pending.delete(d.id); d.error ? rej(new Error(d.error.message)) : res(d.result); }
        else for (const l of listeners) l(d);
      };
      return;
    } catch { await sleep(250); }
  }
  throw new Error('Edge nie wystartował');
}
const send = (method, params = {}, sessionId) => new Promise((res, rej) => {
  const id = nextId++; pending.set(id, { res, rej }); ws.send(JSON.stringify({ id, method, params, sessionId }));
});

async function openPage(width, height) {
  const { targetId } = await send('Target.createTarget', { url: 'about:blank' });
  const { sessionId } = await send('Target.attachToTarget', { targetId, flatten: true });
  const s = (m, p) => send(m, p, sessionId);
  await s('Page.enable'); await s('Network.enable'); await s('Performance.enable');
  await s('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: false });
  const hosts = new Map(); let storesDone = false;
  const l = (d) => {
    if (d.sessionId !== sessionId) return;
    if (d.method === 'Network.requestWillBeSent') {
      const h = new URL(d.params.request.url).host; hosts.set(h, (hosts.get(h) ?? 0) + 1); if (/gstatic|google/.test(h)) (result.external ??= new Set()).add(d.params.request.url);
    }
    if (d.method === 'Network.responseReceived' && /\/api\/catalog\/stores(\?|$)/.test(d.params.response.url)) storesDone = true;
  };
  listeners.add(l);
  return {
    s, hosts, targetId,
    async gotoStores(base) {
      storesDone = false;
      const t0 = Date.now();
      await s('Page.navigate', { url: base + '/stores' });
      while (!storesDone && Date.now() - t0 < 90000) await sleep(200);
      const tStores = Date.now() - t0;
      await sleep(3000);
      return { storesDone, tStores };
    },
    async metrics() {
      const { metrics } = await s('Performance.getMetrics');
      return Object.fromEntries(metrics.map((m) => [m.name, m.value]));
    },
    async shot(file) {
      const { data } = await s('Page.captureScreenshot', { format: 'png' });
      writeFileSync(file, Buffer.from(data, 'base64'));
    },
    async close() { listeners.delete(l); await send('Target.closeTarget', { targetId }); },
  };
}

const result = { screenshots: [], hosts: {}, perf: [] };
try {
  await connect();
  // 1) Zrzuty
  for (const w of [320, 360, 768, 1280, 1920]) {
    const p = await openPage(w, w < 600 ? 780 : 900);
    const r = await p.gotoStores(urlSem);
    const file = join(outDir, `start-${w}.png`);
    await p.shot(file);
    const sem = await p.s('Runtime.evaluate', { expression: 'document.querySelectorAll("flt-semantics").length', returnByValue: true });
    const scroll = await p.s('Runtime.evaluate', { expression: 'document.documentElement.scrollWidth - window.innerWidth', returnByValue: true });
    result.screenshots.push({ width: w, file, storesLoaded: r.storesDone, semanticsNodes: sem.result.value, horizontalOverflowPx: scroll.result.value });
    for (const [h, n] of p.hosts) result.hosts[h] = (result.hosts[h] ?? 0) + n;
    await p.close();
  }
  result.external = [...(result.external ?? [])];
  if (process.env.SHOTS_ONLY) throw 'shots-only';
  // 2) Wydajność: 375×800, 3 przebiegi na wariant, naprzemiennie
  const wheel = async (p, n, dy) => { for (let i = 0; i < n; i++) { await p.s('Input.dispatchMouseEvent', { type: 'mouseWheel', x: 187, y: 500, deltaX: 0, deltaY: dy }); await sleep(100); } };
  const click = async (p, x, y) => {
    await p.s('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', clickCount: 1 });
    await p.s('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', clickCount: 1 });
  };
  for (let run = 1; run <= 3; run++) {
    for (const [variant, base] of [['semantyka', urlSem], ['bez', urlNoSem]]) {
      const p = await openPage(375, 800);
      const r = await p.gotoStores(base);
      const m0 = await p.metrics();
      await click(p, 187, 265);        // pierwsza karta sklepu
      await sleep(5000);
      if (run === 1) await p.shot(join(outDir, `_perf-${variant}-sklep.png`));
      await wheel(p, 25, 300); await wheel(p, 25, -300);
      await sleep(1000);
      const m1 = await p.metrics();
      const d = (k) => +(m1[k] - m0[k]).toFixed(3);
      result.perf.push({
        run, variant, storesMs: r.tStores,
        load: { task: +m0.TaskDuration.toFixed(3), script: +m0.ScriptDuration.toFixed(3), layout: +m0.LayoutDuration.toFixed(3), style: +m0.RecalcStyleDuration.toFixed(3), nodes: m0.Nodes, heapMB: +(m0.JSHeapUsedSize / 1048576).toFixed(1) },
        interact: { task: d('TaskDuration'), script: d('ScriptDuration'), layout: d('LayoutDuration'), style: d('RecalcStyleDuration'), nodes: m1.Nodes, heapMB: +(m1.JSHeapUsedSize / 1048576).toFixed(1) },
      });
      await p.close();
    }
  }
} catch (e) { if (e !== 'shots-only') throw e; } finally {
  writeFileSync(join(outDir, '_wynik.json'), JSON.stringify(result, null, 2));
  try { ws?.close(); } catch {}
  edge.kill();
  await sleep(1500);
  try { rmSync(profile, { recursive: true, force: true }); } catch {}
}
console.log(JSON.stringify(result, null, 1));
