import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { firstValueFrom } from 'rxjs';
import * as QRCodeNS from 'qrcode';
import { Api, StoreDto } from './api';

// qrcode jest pakietem CommonJS — pod różnymi interopami eksport bywa pod .default.
const QRCode: {
  toString(text: string, opts?: unknown): Promise<string>;
  toDataURL(text: string, opts?: unknown): Promise<string>;
} = (QRCodeNS as any).default ?? (QRCodeNS as any);

interface EntrySource { source: string; count: number; }
interface EntryStats { total: number; days: number; bySource: EntrySource[]; registeredQrSources?: string[]; }

/** Tyle etykiet miejsc może mieć jeden sklep (limit serwera). */
export const MAX_QR_LABELS = 20;

/** Adres aplikacji musi być katalogiem głównym (sub)domeny — build aplikacji obsługuje tylko „/". */
export function isRootAppUrl(appUrl: string): boolean {
  try { const u = new URL(appUrl); return u.pathname === '/' && !u.search && !u.hash; } catch { return false; }
}

/** Etykieta źródła jak na serwerze: a-z, 0-9 i „-" (maks. 40 znaków razem z przedrostkiem „qr-"). */
export function qrSource(label: string): string {
  const slug = (label || '').toLowerCase()
    .normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/ł/g, 'l')
    .replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 37);
  return slug ? `qr-${slug}` : 'qr';
}

/**
 * Stały link do oferty sklepu w aplikacji klienta (web) z parametrem źródła do pomiaru wejść.
 * `new URL` zamienia domenę IDN (np. z polskimi znakami) na punycode — czytelną dla każdego skanera QR.
 */
export function storeQrUrl(appUrl: string, slug: string, source: string): string {
  const u = new URL(appUrl.replace(/\/+$/, '') + '/s/' + encodeURIComponent(slug));
  u.searchParams.set('src', source);
  return u.href;
}

/**
 * Kod QR sklepu (lokalizacji) do wydruku: prowadzi prosto do oferty w aplikacji webowej — bez instalacji.
 * Opcjonalna etykieta (np. „kasa", „witryna") rozróżnia miejsca, w których wisi kod — tylko do pomiaru wejść.
 */
@Component({
  selector: 'app-store-qr',
  imports: [CommonModule, FormsModule],
  styles: [`
    .wrap { display:flex; gap:20px; align-items:flex-start; flex-wrap:wrap; }
    .qrbox { width:188px; height:188px; border:1px solid #E5E7EB; border-radius:12px; padding:8px; background:#fff; flex:none; }
    .qrbox :is(svg) { width:100%; height:100%; display:block; }
    .side { flex:1; min-width:240px; display:flex; flex-direction:column; gap:10px; }
    .url { font-family:'JetBrains Mono',monospace; font-size:12px; word-break:break-all; background:#F7F9FA;
           border:1px solid #EEF2F4; border-radius:8px; padding:8px 10px; color:#1F2A2A; }
    .row { display:flex; gap:8px; flex-wrap:wrap; align-items:center; }
    .lbl { display:block; font-size:13px; font-weight:600; color:#3A3F4B; margin:0 0 6px; }
    .stats { font-size:13px; color:#3A3F4B; }
    .stats b { font-size:18px; color:#0F2A2A; }
    .src { font-size:12px; color:#6B7280; }
    .missing { background:#FFF8EB; border:1px solid #F7D9B9; color:#6B3E09; border-radius:10px; padding:10px 14px; font-size:13px; }
    .chips { display:flex; gap:6px; flex-wrap:wrap; align-items:center; }
    .chip { font-size:12px; border:1px solid #BFECEC; background:#EAF9F9; color:#0C7D7E; border-radius:999px; padding:3px 10px; }
    .err { background:#FEECEC; color:#B4232A; border-radius:8px; padding:8px 10px; font-size:13px; }
  `],
  template: `
  @if (!loaded) {
    <div class="muted">Ładowanie…</div>
  } @else if (!appUrl) {
    <div class="missing">Adres aplikacji klienta nie jest jeszcze ustawiony, więc nie da się wygenerować kodu QR.
      Administrator serwisu ustawia go w <b>Konfiguracja → Aplikacja klienta i kody QR</b>.</div>
  } @else if (!rootOk) {
    <div class="missing">Adres aplikacji <code>{{ appUrl }}</code> zawiera ścieżkę. Aplikacja działa tylko w katalogu głównym
      (sub)domeny, np. <code>https://app.example.pl</code> — kod QR prowadziłby na nieobsługiwaną trasę. Popraw adres w Konfiguracji.</div>
  } @else if (!store) {
    <div class="muted">Nie udało się pobrać danych sklepu.</div>
  } @else {
    <div class="wrap">
      <div class="qrbox" [innerHTML]="svg" aria-label="Kod QR do oferty sklepu"></div>
      <div class="side">
        <div>
          <label class="lbl" for="qr-label">Miejsce kodu (opcjonalnie)</label>
          <input id="qr-label" name="qrlabel" [(ngModel)]="label" (ngModelChange)="render()" placeholder="np. kasa, witryna, ulotka"
                 maxlength="37" style="max-width:260px" />
        </div>
        <div class="url">{{ url }}</div>
        @if (source !== 'qr') {
          <div class="src">
            @if (isRegistered) { ✓ Miejsce „{{ source }}” jest zarejestrowane — wejścia z tego kodu liczymy osobno. }
            @else { Miejsce „{{ source }}” zostanie zarejestrowane przy pobraniu, skopiowaniu lub otwarciu kodu
              ({{ registered.length }}/{{ maxLabels }}). }
          </div>
        }
        @if (registered.length) {
          <div class="chips">
            <span class="src">Zarejestrowane miejsca:</span>
            @for (r of registered; track r) { <button class="chip" (click)="useLabel(r)">{{ r }}</button> }
          </div>
        }
        @if (actionError) { <div class="err">{{ actionError }}</div> }
        <div class="row">
          <button class="btn primary sm" [disabled]="busy" (click)="downloadPng()">Pobierz PNG</button>
          <button class="btn ghost sm" [disabled]="busy" (click)="downloadSvg()">Pobierz SVG (do druku)</button>
          <button class="btn ghost sm" [disabled]="busy" (click)="copy()">{{ copied ? '✓ Skopiowano' : 'Kopiuj link' }}</button>
          <button class="btn ghost sm" [disabled]="busy" (click)="open()">Otwórz</button>
        </div>
        <div class="stats">
          @if (stats) {
            Wejścia z kodów QR (ostatnie {{ stats.days }} dni): <b>{{ qrTotal }}</b>
            @if (qrSources.length) {
              <div class="src">@for (s of qrSources; track s.source) { {{ s.source }}: {{ s.count }}@if (!$last) { · } }</div>
            }
          } @else {
            <span class="src">Licznik wejść niedostępny.</span>
          }
        </div>
        <p class="src" style="margin:0">Klient skanuje kod aparatem telefonu i od razu widzi ofertę — bez instalowania aplikacji.
          Źródło z kodu służy tylko do liczenia wejść.</p>
      </div>
    </div>
  }
  `,
})
export class StoreQrComponent {
  private api = inject(Api);
  private san = inject(DomSanitizer);
  storeId = input<string>('');

  loaded = false;
  appUrl: string | null = null;
  store: StoreDto | null = null;
  label = '';
  url = '';
  svg: SafeHtml | null = null;
  copied = false;
  stats: EntryStats | null = null;
  registered: string[] = [];
  busy = false;
  actionError = '';
  readonly maxLabels = MAX_QR_LABELS;

  constructor() {
    effect(() => { const id = this.storeId(); if (id) this.load(id); });
  }

  get qrSources(): EntrySource[] { return (this.stats?.bySource ?? []).filter(s => s.source === 'qr' || s.source.startsWith('qr-')); }
  get qrTotal(): number { return this.qrSources.reduce((a, s) => a + s.count, 0); }
  get rootOk(): boolean { return !!this.appUrl && isRootAppUrl(this.appUrl); }
  get source(): string { return qrSource(this.label); }
  get isRegistered(): boolean { return this.source === 'qr' || this.registered.includes(this.source); }

  useLabel(source: string) { this.label = source.slice(3); this.actionError = ''; this.render(); }

  /**
   * Etykieta miejsca musi być zarejestrowana, zanim kod opuści panel — inaczej wejścia z niego trafią do „qr-other".
   * Anonimowy klient nie może rejestrować etykiet (limit {@link MAX_QR_LABELS} na sklep).
   */
  private async ensureRegistered(): Promise<boolean> {
    this.actionError = '';
    if (this.isRegistered) return true;
    this.busy = true;
    try {
      const r = await firstValueFrom(this.api.post<{ registeredQrSources: string[] }>(
        `/catalog/stores/${this.store!.id}/qr-sources`, { source: this.source }));
      this.registered = r.registeredQrSources ?? [];
      return true;
    } catch (e: any) {
      this.actionError = e?.error?.detail ?? 'Nie udało się zarejestrować miejsca kodu — spróbuj ponownie.';
      return false;
    } finally {
      this.busy = false;
    }
  }

  private load(id: string) {
    this.loaded = false;
    this.api.getPublic<{ customerAppUrl?: string | null }>('/config/public').subscribe({
      next: c => {
        this.appUrl = c.customerAppUrl || null;
        this.api.getPublic<StoreDto>(`/catalog/stores/${id}`).subscribe({
          next: s => { this.store = s; this.loaded = true; this.render(); },
          error: () => { this.store = null; this.loaded = true; },
        });
      },
      error: () => { this.appUrl = null; this.loaded = true; },
    });
    this.api.get<EntryStats>(`/catalog/stores/${id}/entries?days=30`).subscribe({
      next: s => { this.stats = s; this.registered = s.registeredQrSources ?? []; },
      error: () => this.stats = null,
    });
  }

  render() {
    if (!this.appUrl || !this.rootOk || !this.store) return;
    try {
      this.url = storeQrUrl(this.appUrl, this.store.slug, qrSource(this.label));
    } catch {
      this.url = ''; this.svg = null; return;
    }
    QRCode.toString(this.url, { type: 'svg', margin: 2, errorCorrectionLevel: 'M' })
      .then(svg => this.svg = this.san.bypassSecurityTrustHtml(svg))
      .catch(() => this.svg = null);
  }

  private fileName(ext: string) {
    const src = qrSource(this.label);
    return `dowozka-qr-${this.store?.slug ?? 'sklep'}${src === 'qr' ? '' : '-' + src.slice(3)}.${ext}`;
  }

  async downloadSvg() {
    if (!await this.ensureRegistered()) return;
    const svg = await QRCode.toString(this.url, { type: 'svg', margin: 4, errorCorrectionLevel: 'M' });
    const blob = new Blob([svg], { type: 'image/svg+xml' });
    this.save(URL.createObjectURL(blob), this.fileName('svg'), true);
  }

  async downloadPng() {
    if (!await this.ensureRegistered()) return;
    const data = await QRCode.toDataURL(this.url, { width: 1200, margin: 4, errorCorrectionLevel: 'M' });
    this.save(data, this.fileName('png'), false);
  }

  async open() {
    if (!await this.ensureRegistered()) return;
    window.open(this.url, '_blank', 'noopener');
  }

  private save(href: string, name: string, revoke: boolean) {
    const a = document.createElement('a');
    a.href = href; a.download = name;
    document.body.appendChild(a); a.click(); a.remove();
    if (revoke) setTimeout(() => URL.revokeObjectURL(href), 1000);
  }

  async copy() {
    if (!await this.ensureRegistered()) return;
    navigator.clipboard?.writeText(this.url).then(() => {
      this.copied = true; setTimeout(() => this.copied = false, 1500);
    }).catch(() => {});
  }
}
