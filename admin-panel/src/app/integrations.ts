import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api } from './api';
import { ConfigModalComponent } from './config-modal';

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
  imports: [CommonModule, FormsModule, ConfigModalComponent],
  template: `
  <div class="page-head">
    <h1>Integracje i dokumenty</h1>
    <div class="controls"><button class="btn ghost sm" (click)="reload()">Odśwież</button></div>
  </div>

  <div class="tiles">
    <button class="tile" (click)="openTab('Płatności')">
      <span class="tic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><rect x="2" y="5" width="20" height="14" rx="2"/><path d="M2 10h20"/></svg></span>
      <span class="tbody"><span class="ttl">Bramka płatności</span><span class="tst" [class.on]="!!status?.provider">{{ status?.provider ? ('✓ ' + status!.provider) : '— brak' }}</span></span>
      <span class="tcta">Konfiguruj →</span>
    </button>

    <button class="tile" (click)="openTab('Dokumenty')">
      <span class="tic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6"/><path d="M9 13h6M9 17h6"/></svg></span>
      <span class="tbody"><span class="ttl">Dokumenty i zgody</span><span class="tst" [class.on]="docsConfigured">{{ docsConfigured ? '✓ ustawione' : '— brak' }}</span></span>
      <span class="tcta">Konfiguruj →</span>
    </button>
  </div>

  <app-config-modal [open]="modalOpen" title="Integracje i dokumenty"
      [tabs]="['Płatności','Dokumenty']" [active]="tab" (tabChange)="tab = $event"
      [saving]="tab === 'Płatności' ? saving : legalSaving"
      [saved]="tab === 'Płatności' ? saved : legalSaved"
      (save)="onSave()" (close)="modalOpen = false">
    @if (tab === 'Płatności') {
      <p class="muted" style="margin-top:0">Podłącz konto <b>swojej</b> bramki płatniczej. Sekrety są <b>szyfrowane</b> i nigdy nie pokazywane z powrotem — puste pole = bez zmian.</p>
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
          <input type="password" name="ak" [(ngModel)]="form.apiKey" [placeholder]="status?.hasApiKey ? '•••••••• (bez zmian)' : 'wklej klucz'" />
        </div>
        <div>
          <label class="lbl">Klucz CRC @if (status?.hasCrcKey) { <span class="set">• ustawiony</span> }</label>
          <input type="password" name="crc" [(ngModel)]="form.crcKey" [placeholder]="status?.hasCrcKey ? '•••••••• (bez zmian)' : 'wklej CRC'" />
        </div>
      </div>
      <p class="note">🔒 Tokeny trzymamy zaszyfrowane. Nie wysyłaj ich e-mailem ani nie zapisuj w treści zamówień.</p>
    }
    @if (tab === 'Dokumenty') {
      <p class="muted" style="margin-top:0">Aby przyjmować płatności online, sklep musi udostępnić własny <b>regulamin</b>, <b>politykę prywatności</b> oraz informację <b>RODO</b>. Klient akceptuje je przed zakupem.</p>
      <label class="lbl">Regulamin (URL)</label>
      <input name="terms" [(ngModel)]="legal.termsUrl" placeholder="https://twojsklep.pl/regulamin" />
      <label class="lbl">Polityka prywatności (URL)</label>
      <input name="priv" [(ngModel)]="legal.privacyUrl" placeholder="https://twojsklep.pl/polityka-prywatnosci" />
      <label class="lbl">RODO / obowiązek informacyjny (URL, opcjonalnie)</label>
      <input name="gdpr" [(ngModel)]="legal.gdprUrl" placeholder="https://twojsklep.pl/rodo" />
      <label class="chk"><input type="checkbox" name="reqacc" [(ngModel)]="legal.requiresAcceptance" /> Wymagaj akceptacji regulaminu i polityki prywatności przed zakupem</label>
      @if (legalError) { <p class="note" style="background:#FEECEC;color:#B4232A">{{ legalError }}</p> }
      <p class="note">Adresy dokumentów opublikowanych na stronie sklepu (http/https). Przy włączonym wymogu akceptacji <b>regulamin i polityka prywatności są obowiązkowe</b>.</p>
    }
  </app-config-modal>
  `,
  styles: [`
    .tiles { display:grid; grid-template-columns:repeat(auto-fill,minmax(300px,1fr)); gap:14px; }
    .tile { display:flex; align-items:center; gap:14px; text-align:left; background:#fff; border:1px solid #E5E7EB; border-radius:14px; padding:18px; cursor:pointer; transition:border-color .15s, box-shadow .15s, transform .15s; font:inherit; }
    .tile:hover { border-color:#14B9BA; box-shadow:0 12px 28px -20px rgba(12,125,126,.5); transform:translateY(-2px); }
    .tile .tic { width:44px; height:44px; flex:none; border-radius:12px; background:#E7F7F7; color:#0C7D7E; display:flex; align-items:center; justify-content:center; }
    .tile .tic svg { width:24px; height:24px; }
    .tile .tbody { flex:1; display:flex; flex-direction:column; gap:3px; min-width:0; }
    .tile .ttl { font-weight:700; color:#0F2A2A; font-size:15px; }
    .tile .tst { font-size:12.5px; color:#6B7280; }
    .tile .tst.on { color:#128040; font-weight:600; }
    .tile .tcta { color:#0C7D7E; font-weight:600; font-size:13px; white-space:nowrap; }
    .set { font-size:12px; color:#128040; font-weight:600; }
    .lbl { display:block; font-size:13px; font-weight:600; color:#3A3F4B; margin:14px 0 6px; }
    input, select { border:1px solid #E5E7EB; border-radius:8px; padding:9px 11px; font-size:14px; width:100%; box-sizing:border-box; background:#fff; }
    input:focus, select:focus { outline:none; border-color:#14B9BA; }
    .grid2 { display:grid; grid-template-columns:1fr 1fr; gap:16px; }
    @media (max-width:560px){ .grid2 { grid-template-columns:1fr; } }
    .chk { display:flex; align-items:center; gap:8px; margin:16px 0 0; font-size:14px; }
    .chk input { width:auto; }
    .note { margin-top:16px; background:#F1F3F5; color:#6B7280; padding:10px 12px; border-radius:8px; font-size:13px; }
  `],
})
export class IntegrationsComponent {
  private api = inject(Api);
  storeId = input<string>('');

  modalOpen = false;
  tab = 'Płatności';

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

  get docsConfigured(): boolean {
    return !!(this.legal.termsUrl || this.legal.privacyUrl || this.legal.requiresAcceptance);
  }

  openTab(t: string) { this.tab = t; this.modalOpen = true; }
  onSave() { if (this.tab === 'Płatności') this.save(); else this.saveLegal(); }
  reload() { const id = this.storeId(); if (id) this.load(id); }

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
