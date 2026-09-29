import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import * as QRCodeNS from 'qrcode';
import { Api, StoreDto } from './api';

// qrcode jest pakietem CommonJS — pod różnymi interopami eksport bywa pod .default.
const QRCode: {
  toString(text: string, opts?: unknown): Promise<string>;
  toDataURL(text: string, opts?: unknown): Promise<string>;
} = (QRCodeNS as any).default ?? (QRCodeNS as any);

interface EntrySource { source: string; count: number; }
interface EntryStats { total: number; days: number; bySource: EntrySource[]; }

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
  `],
  template: `
  @if (!loaded) {
    <div class="muted">Ładowanie…</div>
  } @else if (!appUrl) {
    <div class="missing">Adres aplikacji klienta nie jest jeszcze ustawiony, więc nie da się wygenerować kodu QR.
      Administrator serwisu ustawia go w <b>Konfiguracja → Aplikacja klienta i kody QR</b>.</div>
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
        <div class="row">
          <button class="btn primary sm" (click)="downloadPng()">Pobierz PNG</button>
          <button class="btn ghost sm" (click)="downloadSvg()">Pobierz SVG (do druku)</button>
          <button class="btn ghost sm" (click)="copy()">{{ copied ? '✓ Skopiowano' : 'Kopiuj link' }}</button>
          <a class="btn ghost sm" [href]="url" target="_blank" rel="noopener" style="text-decoration:none">Otwórz</a>
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

  constructor() {
    effect(() => { const id = this.storeId(); if (id) this.load(id); });
  }

  get qrSources(): EntrySource[] { return (this.stats?.bySource ?? []).filter(s => s.source === 'qr' || s.source.startsWith('qr-')); }
  get qrTotal(): number { return this.qrSources.reduce((a, s) => a + s.count, 0); }

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
      next: s => this.stats = s, error: () => this.stats = null,
    });
  }

  render() {
    if (!this.appUrl || !this.store) return;
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

  downloadSvg() {
    QRCode.toString(this.url, { type: 'svg', margin: 4, errorCorrectionLevel: 'M' }).then(svg => {
      const blob = new Blob([svg], { type: 'image/svg+xml' });
      this.save(URL.createObjectURL(blob), this.fileName('svg'), true);
    });
  }

  downloadPng() {
    QRCode.toDataURL(this.url, { width: 1200, margin: 4, errorCorrectionLevel: 'M' })
      .then(data => this.save(data, this.fileName('png'), false));
  }

  private save(href: string, name: string, revoke: boolean) {
    const a = document.createElement('a');
    a.href = href; a.download = name;
    document.body.appendChild(a); a.click(); a.remove();
    if (revoke) setTimeout(() => URL.revokeObjectURL(href), 1000);
  }

  copy() {
    navigator.clipboard?.writeText(this.url).then(() => {
      this.copied = true; setTimeout(() => this.copied = false, 1500);
    }).catch(() => {});
  }
}
