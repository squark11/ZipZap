// Etap 2 — powtarzalny dowód w prawdziwej przeglądarce (czysty profil, bez pamięci podręcznej):
//   • zrzuty ekranu Start (/stores) w 320, 360, 768, 1280, 1920 px + pomiar poziomego przewijania,
//   • lista hostów, z którymi łączy się aplikacja (oczekiwane: tylko serwer aplikacji i API — 0 × Google),
//   • opcjonalnie: koszt semantyki (dwie kompilacje: z ensureSemantics i bez) — 3 przebiegi na wariant.
//
// Wymaga: Node ≥ 22 (wbudowany WebSocket), Microsoft Edge albo Google Chrome. Niczego nie instaluje.
// Serwuje katalogi z kompilacją sam (SPA: nieznana ścieżka → index.html), więc nie trzeba osobnego serwera.
//
// Użycie (z katalogu repozytorium, po `flutter build web --release --no-web-resources-cdn ... -o <katalog>`):
//   node docs/redesign/verify-e2.mjs <katalogWyników> <kompilacjaZSemantyką> [kompilacjaBezSemantyki]
// Bez trzeciego argumentu pomiar semantyki jest pomijany. Przeglądarkę można wskazać zmienną BROWSER.
// Wynik: <katalogWyników>/_wynik.json (zawsze, także przy błędzie — pole "error") i pliki PNG.
// Kod wyjścia: 0 = OK, 1 = błąd uruchomienia, 2 = znaleziono połączenia z Google lub poziome przewijanie.
import { spawn } from 'node:child_process';
import { createServer } from 'node:http';
import { existsSync, mkdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { extname, join, normalize, resolve } from 'node:path';
import { tmpdir } from 'node:os';

const [outDirArg, buildSem, buildNoSem] = process.argv.slice(2);
if (!outDirArg || !buildSem) {
  console.error('Użycie: node verify-e2.mjs <katalogWyników> <kompilacjaZSemantyką> [kompilacjaBezSemantyki]');
  process.exit(1);
}
if (typeof WebSocket === 'undefined') {
  console.error(`Potrzebny Node ≥ 22 (wbudowany WebSocket). Masz ${process.version}.`);
  process.exit(1);
}
const outDir = resolve(outDirArg);
mkdirSync(outDir, { recursive: true });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const WIDTHS = [320, 360, 768, 1280, 1920];
// Opcjonalnie PAGE="/stores/<id>" (domyślnie /stores) i WAIT_API="products" — fragment adresu API, na który czekać
// przed zrzutem (domyślnie lista sklepów). Pliki zrzutów dostają wtedy przedrostek PREFIX (domyślnie „start").
const PAGE = process.env.PAGE ?? '/stores';
const WAIT_API = process.env.WAIT_API ? new RegExp(process.env.WAIT_API) : /\/api\/catalog\/stores(\?|$)/;
const PREFIX = process.env.PREFIX ?? 'start';
// Opcjonalnie CLICKS="285,635;180,740" — kliknięcia (współrzędne CSS) po wczytaniu, co 2 s, przed zrzutem
// (np. dodanie produktu i przejście do koszyka). Te same współrzędne dla każdego rozmiaru z SIZES.
const CLICKS = (process.env.CLICKS ?? '').split(';').filter(Boolean).map((c) => c.split(',').map(Number));
// Opcjonalnie SIZES="360x640,375x812,1280x900" — inne rozmiary okna (pliki start-<szer>x<wys>.png).
const SIZES = process.env.SIZES
  ? process.env.SIZES.split(',').map((s) => { const [w, h] = s.split('x').map(Number); return { w, h, file: `${PREFIX}-${w}x${h}.png` }; })
  : WIDTHS.map((w) => ({ w, h: w < 600 ? 780 : 900, file: `${PREFIX}-${w}.png` }));
const CDP_TIMEOUT_MS = 30_000;
const API_WAIT_MS = 120_000; // darmowy Render usypia API — pierwsze żądanie może trwać ok. minuty
const result = { startedAt: new Date().toISOString(), node: process.version, browser: null, screenshots: [], hosts: {}, external: [], perf: [] };

// ---------- serwer plików (SPA) ----------
const TYPES = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.mjs': 'text/javascript', '.json': 'application/json',
  '.wasm': 'application/wasm', '.svg': 'image/svg+xml', '.png': 'image/png', '.ttf': 'font/ttf', '.otf': 'font/otf' };
function serve(root) {
  root = resolve(root);
  if (!existsSync(join(root, 'index.html'))) throw new Error(`Brak ${join(root, 'index.html')} — najpierw flutter build web.`);
  const server = createServer((req, res) => {
    const rel = normalize(decodeURIComponent(new URL(req.url, 'http://x').pathname)).replace(/^([/\\])+/, '');
    let file = join(root, rel);
    if (!file.startsWith(root) || !existsSync(file) || !statSync(file).isFile()) file = join(root, 'index.html');
    res.writeHead(200, { 'Content-Type': TYPES[extname(file)] ?? 'application/octet-stream', 'Cache-Control': 'no-store' });
    res.end(readFileSync(file));
  });
  return new Promise((ok) => server.listen(0, '127.0.0.1', () => ok({ server, url: `http://localhost:${server.address().port}` })));
}

// ---------- przeglądarka ----------
function findBrowser() {
  const candidates = [process.env.BROWSER,
    'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', 'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
    'C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
    '/usr/bin/microsoft-edge', '/usr/bin/google-chrome', '/usr/bin/chromium', '/usr/bin/chromium-browser',
    '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', '/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge'];
  const found = candidates.find((p) => p && existsSync(p));
  if (!found) throw new Error('Nie znaleziono Edge ani Chrome — wskaż plik zmienną BROWSER.');
  return found;
}

let ws, nextId = 1, browser, profile;
const pending = new Map(), listeners = new Set();
async function startBrowser() {
  result.browser = findBrowser();
  profile = join(tmpdir(), 'verify-e2-' + Date.now());
  const port = 9300 + Math.floor(Math.random() * 600);
  browser = spawn(result.browser, ['--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`,
    '--no-first-run', '--no-default-browser-check', '--disable-extensions', 'about:blank'], { stdio: 'ignore' });
  browser.on('exit', (code) => { if (ws) for (const { rej } of pending.values()) rej(new Error(`przeglądarka zakończyła się (kod ${code})`)); });
  for (let i = 0; i < 80; i++) {
    try {
      const v = await (await fetch(`http://127.0.0.1:${port}/json/version`)).json();
      ws = new WebSocket(v.webSocketDebuggerUrl);
      await new Promise((ok, err) => { ws.onopen = ok; ws.onerror = () => err(new Error('WebSocket CDP: błąd połączenia')); });
      ws.onmessage = (m) => {
        const d = JSON.parse(m.data);
        if (d.id && pending.has(d.id)) { const p = pending.get(d.id); pending.delete(d.id); clearTimeout(p.t); d.error ? p.rej(new Error(`${p.method}: ${d.error.message}`)) : p.ok(d.result); }
        else for (const l of listeners) l(d);
      };
      ws.onclose = () => { for (const p of pending.values()) p.rej(new Error('połączenie z przeglądarką zamknięte')); pending.clear(); };
      return;
    } catch { await sleep(250); }
  }
  throw new Error(`Przeglądarka nie udostępniła portu debugowania ${port} w 20 s (${result.browser}).`);
}
// Każde polecenie ma limit czasu — skrypt nigdy nie „wisi" bez komunikatu.
const send = (method, params = {}, sessionId) => new Promise((ok, rej) => {
  const id = nextId++;
  const t = setTimeout(() => { pending.delete(id); rej(new Error(`${method}: brak odpowiedzi w ${CDP_TIMEOUT_MS / 1000} s`)); }, CDP_TIMEOUT_MS);
  pending.set(id, { ok, rej, t, method });
  ws.send(JSON.stringify({ id, method, params, sessionId }));
});

async function openPage(width, height) {
  const { targetId } = await send('Target.createTarget', { url: 'about:blank' });
  const { sessionId } = await send('Target.attachToTarget', { targetId, flatten: true });
  const s = (m, p) => send(m, p, sessionId);
  await s('Page.enable'); await s('Network.enable'); await s('Performance.enable');
  await s('Network.setCacheDisabled', { cacheDisabled: true });
  await s('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: false });
  const hosts = new Map(); let storesDone = false;
  const l = (d) => {
    if (d.sessionId !== sessionId) return;
    if (d.method === 'Network.requestWillBeSent') {
      const url = d.params.request.url;
      if (!/^https?:/.test(url)) return;
      const h = new URL(url).host; hosts.set(h, (hosts.get(h) ?? 0) + 1);
      if (/google|gstatic/.test(h) && !result.external.includes(url)) result.external.push(url);
    }
    if (d.method === 'Network.responseReceived' && WAIT_API.test(d.params.response.url)) storesDone = true;
  };
  listeners.add(l);
  return {
    s, hosts,
    async gotoStores(base) {
      storesDone = false;
      const t0 = Date.now();
      await s('Page.navigate', { url: base + PAGE });
      while (!storesDone && Date.now() - t0 < API_WAIT_MS) await sleep(200);
      const tStores = Date.now() - t0;
      await sleep(3000); // czas na wyrysowanie kart i doczytanie obrazów
      return { storesDone, tStores };
    },
    async eval(expression) { return (await s('Runtime.evaluate', { expression, returnByValue: true })).result.value; },
    async metrics() { return Object.fromEntries((await s('Performance.getMetrics')).metrics.map((m) => [m.name, m.value])); },
    async shot(file) { writeFileSync(file, Buffer.from((await s('Page.captureScreenshot', { format: 'png' })).data, 'base64')); },
    async close() { listeners.delete(l); await send('Target.closeTarget', { targetId }); },
  };
}

const servers = [];
let exitCode = 0;
try {
  const sem = await serve(buildSem); servers.push(sem.server);
  const noSem = buildNoSem ? await serve(buildNoSem) : null; if (noSem) servers.push(noSem.server);
  await startBrowser();

  // 1) Zrzuty + hosty + przewijanie
  for (const { w, h, file } of SIZES) {
    const p = await openPage(w, h);
    const r = await p.gotoStores(sem.url);
    if (!r.storesDone) throw new Error(`API nie odpowiedziało w ${API_WAIT_MS / 1000} s (szerokość ${w}) — sprawdź adres API w kompilacji.`);
    for (const [x, y] of CLICKS) {
      await p.s('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', clickCount: 1 });
      await p.s('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', clickCount: 1 });
      await sleep(2000);
    }
    await p.shot(join(outDir, file));
    result.screenshots.push({ width: w, height: h, file, storesMs: r.tStores,
      semanticsNodes: await p.eval('document.querySelectorAll("flt-semantics").length'),
      horizontalOverflowPx: await p.eval('document.documentElement.scrollWidth - window.innerWidth') });
    for (const [h, n] of p.hosts) result.hosts[h] = (result.hosts[h] ?? 0) + n;
    await p.close();
  }

  // 2) Koszt semantyki: 375×800, wejście → pierwszy sklep → 50 przewinięć; 3 przebiegi naprzemiennie
  if (noSem) {
    const click = async (p, x, y) => {
      await p.s('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', clickCount: 1 });
      await p.s('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', clickCount: 1 });
    };
    const wheel = async (p, n, dy) => { for (let i = 0; i < n; i++) { await p.s('Input.dispatchMouseEvent', { type: 'mouseWheel', x: 187, y: 500, deltaX: 0, deltaY: dy }); await sleep(100); } };
    const r3 = (v) => +v.toFixed(3);
    for (let run = 1; run <= 3; run++) {
      for (const [variant, url] of [['semantyka', sem.url], ['bez', noSem.url]]) {
        const p = await openPage(375, 800);
        const r = await p.gotoStores(url);
        const m0 = await p.metrics();
        await click(p, 187, 265);
        await sleep(5000);
        await wheel(p, 25, 300); await wheel(p, 25, -300);
        await sleep(1000);
        const m1 = await p.metrics();
        const d = (k) => r3(m1[k] - m0[k]);
        result.perf.push({ run, variant, storesMs: r.tStores,
          load: { taskS: r3(m0.TaskDuration), scriptS: r3(m0.ScriptDuration), layoutS: r3(m0.LayoutDuration), nodes: m0.Nodes, heapMB: +(m0.JSHeapUsedSize / 1048576).toFixed(1) },
          interact: { taskS: d('TaskDuration'), scriptS: d('ScriptDuration'), layoutS: d('LayoutDuration'), nodes: m1.Nodes, heapMB: +(m1.JSHeapUsedSize / 1048576).toFixed(1) } });
        await p.close();
      }
    }
  }

  const overflow = result.screenshots.filter((s) => s.horizontalOverflowPx > 0).map((s) => s.width);
  result.verdict = { googleRequests: result.external.length, horizontalOverflowAt: overflow };
  if (result.external.length || overflow.length) exitCode = 2;
} catch (e) {
  result.error = String(e?.message ?? e);
  exitCode = 1;
} finally {
  result.finishedAt = new Date().toISOString();
  writeFileSync(join(outDir, '_wynik.json'), JSON.stringify(result, null, 2) + '\n');
  try { ws?.close(); } catch {}
  try { browser?.kill(); } catch {}
  for (const s of servers) s.close();
  await sleep(1500);
  try { if (profile) rmSync(profile, { recursive: true, force: true }); } catch {}
}
console.log(JSON.stringify({ error: result.error, verdict: result.verdict, hosts: result.hosts, screenshots: result.screenshots.length, perfRuns: result.perf.length }, null, 1));
process.exit(exitCode);
