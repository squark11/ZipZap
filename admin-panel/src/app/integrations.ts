import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api } from './api';

export interface StoreIntegrationStatus {
  provider: string;
  merchantId?: string;
  posId?: string;
  sandbox: boolean;
  hasApiKey: boolean;
  hasCrcKey: boolean;
}

export interface StoreLegal {
  termsUrl?: string;
  privacyUrl?: string;
  gdprUrl?: string;
  requiresAcceptance: boolean;
}

@Component({
  selector: 'app-integrations',
  imports: [CommonModule, FormsModule],
  template: `
  <div class="page-head">
    <h1>Integracje i dokumenty</h1>
    <div class="controls">
      @if (saved) { <span class="ok-msg">✓ Zapisano</span> }
      <button class="btn ghost sm" (click)="save()" [disabled]="saving">Zapisz płatności</button>
    </div>
  </div>

  <div class="card pad">
    <p class="muted" style="margin-top:0">Podłącz konto <b>swojej</b> bramki płatniczej dla tego sklepu.
      Sekrety (klucze/tokeny) są <b>szyfrowane</b> i <b>nigdy nie są pokazywane z powrotem</b> —
      puste pole = bez zmian.</p>

    <label class="lbl">Dostawca</label>
    <select name="prov" [(ngModel)]="form.provider">
      <option value="">— brak —</option>
      <option value="przelewy24">Przelewy24</option>
      <option value="stripe">Stripe</option>
    </select>

    <div class="grid2">
      <div><label class="lbl">Merchant ID</label><input name="mid" [(ngModel)]="form.merchantId" /></div>
      <div><label class="lbl">POS ID</label><input name="pos" [(ngModel)]="form.posId" /></div>
    </div>

    <label class="chk"><input type="checkbox" name="sb" [(ngModel)]="form.sandbox" /> Tryb testowy (sandbox)</label>

    <div class="grid2">
      <div>
        <label class="lbl">Klucz API / token @if (status?.hasApiKey) { <span class="set">• ustawiony</span> }</label>
        <input type="password" name="ak" [(ngModel)]="form.apiKey"
               [placeholder]="status?.hasApiKey ? '•••••••• (bez zmian)' : 'wklej klucz'" />
      </div>
      <div>
        <label class="lbl">Klucz CRC @if (status?.hasCrcKey) { <span class="set">• ustawiony</span> }</label>
        <input type="password" name="crc" [(ngModel)]="form.crcKey"
               [placeholder]="status?.hasCrcKey ? '•••••••• (bez zmian)' : 'wklej CRC'" />
      </div>
    </div>

    <p class="note">🔒 Tokeny trzymamy zaszyfrowane. Nie wysyłaj ich e-mailem ani nie zapisuj w treści zamówień.</p>
  </div>

  <div class="card pad" style="margin-top:16px">
    <div style="display:flex;align-items:flex-start;justify-content:space-between;gap:12px;flex-wrap:wrap">
      <div>
        <b>Dokumenty i zgody</b>
        <div class="muted" style="font-size:13px;max-width:60ch">Aby przyjmować płatności online, sklep musi udostępnić własny
          <b>regulamin</b>, <b>politykę prywatności</b> oraz informację <b>RODO</b>. Klient akceptuje je przed zakupem.</div>
      </div>
      <div style="white-space:nowrap">
        @if (legalSaved) { <span class="ok-msg">✓ Zapisano</span> }
        <button class="btn ghost sm" (click)="saveLegal()" [disabled]="legalSaving">Zapisz dokumenty</button>
      </div>
    </div>

    <label class="lbl">Regulamin (URL)</label>
    <input name="terms" [(ngModel)]="legal.termsUrl" placeholder="https://twojsklep.pl/regulamin" />

    <label class="lbl">Polityka prywatności (URL)</label>
    <input name="priv" [(ngModel)]="legal.privacyUrl" placeholder="https://twojsklep.pl/polityka-prywatnosci" />

    <label class="lbl">RODO / obowiązek informacyjny (URL, opcjonalnie)</label>
    <input name="gdpr" [(ngModel)]="legal.gdprUrl" placeholder="https://twojsklep.pl/rodo" />

    <label class="chk"><input type="checkbox" name="reqacc" [(ngModel)]="legal.requiresAcceptance" />
      Wymagaj akceptacji regulaminu i polityki prywatności przed zakupem</label>

    @if (legalError) { <p class="note" style="background:#FEECEC;color:#B4232A">{{ legalError }}</p> }
    <p class="note">Podaj adresy do dokumentów opublikowanych na stronie sklepu (http/https). Przy włączonym wymogu
      akceptacji <b>regulamin i polityka prywatności są obowiązkowe</b>.</p>
  </div>
  `,
  styles: [`
    code, .set { font-size:12px; }
    .set { color:#128040; font-weight:600; }
    .lbl { display:block; font-size:13px; font-weight:600; color:#3A3F4B; margin:14px 0 6px; }
    input, select { border:1px solid #E5E7EB; border-radius:8px; padding:9px 11px; font-size:14px; width:100%; box-sizing:border-box; background:#fff; }
    input:focus, select:focus { outline:none; border-color:#F97316; }
    .grid2 { display:grid; grid-template-columns:1fr 1fr; gap:16px; }
    .chk { display:flex; align-items:center; gap:8px; margin:16px 0 0; font-size:14px; }
    .chk input { width:auto; }
    .note { margin-top:16px; background:#F1F3F5; color:#6B7280; padding:10px 12px; border-radius:8px; font-size:13px; }
    .ok-msg { color:#128040; font-size:13px; font-weight:600; margin-right:6px; }
  `],
})
export class IntegrationsComponent {
  private api = inject(Api);
  storeId = input.required<string>();

  status: StoreIntegrationStatus | null = null;
  form = { provider: '', merchantId: '', posId: '', sandbox: true, apiKey: '', crcKey: '' };
  saving = false;
  saved = false;

  legal = { termsUrl: '', privacyUrl: '', gdprUrl: '', requiresAcceptance: false };
  legalSaving = false;
  legalSaved = false;
  legalError = '';

  constructor() {
    effect(() => { const id = this.storeId(); if (id) this.load(id); });
  }

  load(id: string) {
    this.api.get<StoreIntegrationStatus>(`/payments/stores/${id}/integration`).subscribe({
      next: s => {
        this.status = s;
        this.form = { provider: s.provider || '', merchantId: s.merchantId || '', posId: s.posId || '', sandbox: s.sandbox, apiKey: '', crcKey: '' };
      },
      error: () => { this.status = null; },
    });
    this.api.get<StoreLegal>(`/stores/${id}/legal`).subscribe({
      next: l => this.legal = {
        termsUrl: l.termsUrl || '', privacyUrl: l.privacyUrl || '',
        gdprUrl: l.gdprUrl || '', requiresAcceptance: !!l.requiresAcceptance,
      },
      error: () => { /* brak konfiguracji = domyślne puste */ },
    });
  }

  save() {
    const id = this.storeId();
    if (!id) return;
    this.saving = true;
    this.saved = false;
    this.api.put<StoreIntegrationStatus>(`/payments/stores/${id}/integration`, this.form).subscribe({
      next: s => { this.status = s; this.form.apiKey = ''; this.form.crcKey = ''; this.saving = false; this.saved = true; setTimeout(() => this.saved = false, 2500); },
      error: () => { this.saving = false; },
    });
  }

  saveLegal() {
    const id = this.storeId();
    if (!id) return;
    this.legalSaving = true;
    this.legalSaved = false;
    this.legalError = '';
    const body: StoreLegal = {
      termsUrl: this.legal.termsUrl.trim() || undefined,
      privacyUrl: this.legal.privacyUrl.trim() || undefined,
      gdprUrl: this.legal.gdprUrl.trim() || undefined,
      requiresAcceptance: this.legal.requiresAcceptance,
    };
    this.api.put<StoreLegal>(`/stores/${id}/legal`, body).subscribe({
      next: () => { this.legalSaving = false; this.legalSaved = true; setTimeout(() => this.legalSaved = false, 2500); },
      error: e => { this.legalSaving = false; this.legalError = e?.error?.detail ?? 'Nie udało się zapisać dokumentów.'; },
    });
  }
}
