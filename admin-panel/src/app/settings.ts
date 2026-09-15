import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api } from './api';

export interface ConfigStatus {
  environment: string;
  payments: { provider: string; publicUrl: string; mockPayPage: boolean };
  googleSignIn: boolean;
  email: boolean;
  rabbitMq: boolean;
  identityPublicUrl: string;
  adminSeedEmail: string;
  jwtUsingDevSecret: boolean;
}

export interface PlatformSettings {
  deliveryWaves: string[];
  defaultCommissionRate: number;
  currency: string;
  operatorName?: string;
  operatorContact?: string;
}

export interface PlatformIntegrations {
  googleClientId?: string;
  captchaProvider?: string;
  captchaSiteKey?: string;
  hasCaptchaSecret?: boolean;
  smtpHost?: string;
  smtpPort?: number;
  smtpUseSsl?: boolean;
  smtpUsername?: string;
  smtpFromEmail?: string;
  smtpFromName?: string;
  hasSmtpPassword?: boolean;
}

@Component({
  selector: 'app-settings',
  imports: [CommonModule, FormsModule],
  template: `
  <div class="page-head">
    <h1>Konfiguracja</h1>
    <div class="controls"><button class="btn ghost sm" (click)="load()">Odśwież</button></div>
  </div>

  @if (status) {
    <div class="card pad">
      <h1 style="font-size:16px;margin:0 0 12px">Status integracji</h1>
      <div class="cfg">
        <div class="row"><span>Środowisko</span><b>{{ status.environment }}</b></div>
        <div class="row"><span>Płatności</span><b>{{ status.payments.provider }}</b></div>
        <div class="row"><span>URL płatności</span><em class="muted">{{ status.payments.publicUrl }}</em></div>
        <div class="row"><span>Strona płatności (mock)</span><b [style.color]="col(status.payments.mockPayPage)">{{ txt(status.payments.mockPayPage) }}</b></div>
        <div class="row"><span>Logowanie Google</span><b [style.color]="col(status.googleSignIn)">{{ txt(status.googleSignIn) }}</b></div>
        <div class="row"><span>E-mail (SMTP)</span><b [style.color]="col(status.email)">{{ txt(status.email) }}</b></div>
        <div class="row"><span>RabbitMQ</span><b [style.color]="col(status.rabbitMq)">{{ txt(status.rabbitMq) }}</b></div>
        <div class="row"><span>Admin (seed)</span><b>{{ status.adminSeedEmail }}</b></div>
      </div>
      @if (status.jwtUsingDevSecret) {
        <p class="warn">⚠ Klucz JWT to wartość deweloperska — ustaw własny <code>JWT__SIGNINGKEY</code> (min. 32 znaki) przed produkcją.</p>
      }
    </div>
  }

  <div class="card pad">
    <div class="page-head" style="margin-bottom:6px">
      <h1 style="font-size:16px;margin:0">Ustawienia platformy</h1>
      <div class="controls">
        @if (savedP) { <span class="ok-msg">✓ Zapisano</span> }
        <button class="btn ghost sm" (click)="savePlatform()" [disabled]="savingP">Zapisz</button>
      </div>
    </div>
    <p class="muted" style="margin-top:0">Ustawienia nie‑sekretne. Godziny fal dostaw wykorzysta planowana dostawa (Faza H).</p>

    <label class="lbl">Fale dostaw (godziny)</label>
    <div class="waves">
      @for (w of platform.deliveryWaves; track $index) {
        <div class="wave">
          <input type="time" [(ngModel)]="platform.deliveryWaves[$index]" [name]="'wave'+$index" />
          <button class="btn ghost sm" (click)="removeWave($index)">Usuń</button>
        </div>
      }
      <button class="btn ghost sm" (click)="addWave()">+ Dodaj falę</button>
    </div>

    <div class="grid2">
      <div>
        <label class="lbl">Domyślna prowizja (%)</label>
        <input type="number" min="0" max="100" step="0.5" name="comm" [(ngModel)]="commissionPercent" />
      </div>
      <div>
        <label class="lbl">Waluta</label>
        <input type="text" maxlength="3" name="cur" [(ngModel)]="platform.currency" />
      </div>
    </div>
    <div class="grid2">
      <div>
        <label class="lbl">Operator (nazwa)</label>
        <input type="text" name="opn" [(ngModel)]="platform.operatorName" />
      </div>
      <div>
        <label class="lbl">Kontakt operatora</label>
        <input type="text" name="opc" [(ngModel)]="platform.operatorContact" />
      </div>
    </div>
  </div>

  <div class="card pad">
    <div class="page-head" style="margin-bottom:6px">
      <h1 style="font-size:16px;margin:0">Integracje platformy</h1>
      <div class="controls">
        @if (savedI) { <span class="ok-msg">✓ Zapisano</span> }
        <button class="btn ghost sm" (click)="saveIntegrations()" [disabled]="savingI">Zapisz</button>
      </div>
    </div>
    <p class="muted" style="margin-top:0">Konfiguracja integracji <b>tu w panelu</b> (nie w pliku env).
      Google Client ID jest jawny (nie sekret) — to identyfikator klienta typu <b>Aplikacja internetowa</b>,
      którego aplikacja używa jako <code>serverClientId</code>, a backend jako audience tokenu.</p>

    <label class="lbl">Google OAuth — Client ID (Web)</label>
    <input type="text" name="gid" [(ngModel)]="integrations.googleClientId"
           placeholder="123456789-abc.apps.googleusercontent.com" />
    <p class="muted" style="font-size:12px;margin-top:6px">Puste = logowanie Google wyłączone (zadziała fallback z env, jeśli ustawiony).</p>

    <hr style="border:none;border-top:1px solid #eef0f3;margin:16px 0" />
    <b style="font-size:14px">Captcha (ochrona formularzy)</b>
    <p class="muted" style="font-size:13px;margin:4px 0 10px">Chroni publiczne formularze (rejestracja, uwagi). Rekomendacja: <b>Cloudflare Turnstile</b> (darmowy). Puste = wyłączona.</p>
    <div class="grid2">
      <div>
        <label class="lbl">Dostawca</label>
        <select name="capprov" [(ngModel)]="integrations.captchaProvider">
          <option value="">— wyłączona —</option>
          <option value="turnstile">Cloudflare Turnstile</option>
        </select>
      </div>
      <div>
        <label class="lbl">Site key (jawny)</label>
        <input type="text" name="capsite" [(ngModel)]="integrations.captchaSiteKey" placeholder="0x4AAAA..." />
      </div>
    </div>
    <label class="lbl">Secret key @if (integrations.hasCaptchaSecret) { <span style="color:#128040;font-weight:600">• ustawiony</span> }</label>
    <input type="password" name="capsecret" [(ngModel)]="captchaSecret"
           [placeholder]="integrations.hasCaptchaSecret ? '•••••••• (bez zmian)' : 'wklej secret key'" />

    @if (integError) { <p class="warn" style="background:#FEECEC;color:#B4232A">{{ integError }}</p> }
    <p class="note" style="margin-top:12px">🔒 Secret jest szyfrowany i nigdy nie pokazywany z powrotem. Site key jest jawny (renderowany u klienta).</p>
  </div>

  <div class="card pad">
    <div class="page-head" style="margin-bottom:6px">
      <h1 style="font-size:16px;margin:0">Poczta e-mail (SMTP)</h1>
      <div class="controls">
        @if (savedI) { <span class="ok-msg">✓ Zapisano</span> }
        <button class="btn ghost sm" (click)="saveIntegrations()" [disabled]="savingI">Zapisz</button>
      </div>
    </div>
    <p class="muted" style="margin-top:0">Serwer poczty wychodzącej — do e-maili (dane logowania, weryfikacja e-mail, reset hasła). Hasło szyfrowane, write-only.</p>
    <div class="grid2">
      <div><label class="lbl">Serwer SMTP</label><input name="smtphost" [(ngModel)]="integrations.smtpHost" placeholder="mail-serwer325339.lh.pl" /></div>
      <div><label class="lbl">Port</label><input type="number" name="smtpport" [(ngModel)]="integrations.smtpPort" placeholder="465" /></div>
    </div>
    <label class="lbl"><input type="checkbox" name="smtpssl" [(ngModel)]="integrations.smtpUseSsl" style="width:auto;margin-right:6px" /> SSL bezpośredni (port 465)</label>
    <div class="grid2">
      <div><label class="lbl">Login (użytkownik)</label><input name="smtpuser" [(ngModel)]="integrations.smtpUsername" placeholder="konto@twojadomena.pl" /></div>
      <div><label class="lbl">Hasło @if (integrations.hasSmtpPassword) { <span style="color:#128040;font-weight:600">• ustawione</span> }</label>
        <input type="password" name="smtppass" [(ngModel)]="smtpPassword" [placeholder]="integrations.hasSmtpPassword ? '•••••••• (bez zmian)' : 'hasło konta pocztowego'" /></div>
    </div>
    <div class="grid2">
      <div><label class="lbl">Nadawca — e-mail</label><input name="smtpfrom" [(ngModel)]="integrations.smtpFromEmail" placeholder="no-reply@dowozka.pl" /></div>
      <div><label class="lbl">Nadawca — nazwa</label><input name="smtpfromname" [(ngModel)]="integrations.smtpFromName" placeholder="Dowózka.pl" /></div>
    </div>
    <div style="display:flex;gap:10px;align-items:center;margin-top:12px;flex-wrap:wrap">
      <input name="smtptestto" [(ngModel)]="smtpTestTo" placeholder="wyślij test na adres… (domyślnie nadawca)" style="max-width:300px" />
      <button class="btn ghost sm" (click)="sendTest()" [disabled]="testing">Wyślij testowy e-mail</button>
      @if (testMsg) { <span class="ok-msg">{{ testMsg }}</span> }
    </div>
    @if (smtpError) { <p class="warn" style="background:#FEECEC;color:#B4232A">{{ smtpError }}</p> }
    <p class="note" style="margin-top:12px">🔒 Hasło szyfrowane i nigdy nie pokazywane z powrotem. Zapisz konfigurację przed wysłaniem testu.</p>
  </div>

  <div class="card pad">
    <h1 style="font-size:16px;margin:0 0 8px">Twoje konta / sekrety</h1>
    <p class="muted">Tu zostają tylko <b>sekrety infrastruktury</b> — w zmiennych środowiskowych, nie w bazie.
      Uzupełnij <code>.env</code> wg <code>.env.example</code> — szczegóły w <code>DEPLOY.md</code>.
      Integracje (np. Google) ustawiasz wyżej, w panelu.</p>
    <ul class="check">
      <li>Baza (Postgres / Neon) — <code>CONNECTIONSTRINGS__POSTGRES</code></li>
      <li>Klucz JWT — <code>JWT__SIGNINGKEY</code></li>
      <li>Płatności — <code>PAYMENTS__*</code> (mock teraz, Przelewy24 po podpięciu)</li>
      <li>E-mail SMTP — <code>EMAIL__SMTP__*</code></li>
      <li>URL API dla aplikacji (PWA) — <code>WEB_API_BASE_URL</code></li>
      <li>Hosting / CI — Cloudflare / Fly.io / Neon (jako sekrety CI)</li>
    </ul>
  </div>

  <div class="card pad">
    <div class="page-head" style="margin-bottom:6px">
      <h1 style="font-size:16px;margin:0">Dane demo (pilotaż)</h1>
      <div class="controls">
        @if (seedMsg) { <span class="ok-msg">{{ seedMsg }}</span> }
        <button class="btn ghost sm" (click)="seedDemo()" [disabled]="seeding">Zasiej sklepy demo</button>
      </div>
    </div>
    <p class="muted" style="margin-top:0">Tworzy przykładowe sklepy <b>Rapacz</b> i <b>Lewiatan</b> z logo, produktami (zdjęcia),
      strefą i terminem dostawy — gotowe do sprzedaży i widoczne wg odległości. Idempotentne (nie duplikuje).
      Dane są <b>do podmiany</b> przez sklep.</p>
    @if (seedError) { <p class="warn">{{ seedError }}</p> }
  </div>
  `,
  styles: [`
    .cfg { display:flex; flex-direction:column; gap:2px; }
    .cfg .row { display:flex; justify-content:space-between; align-items:center; gap:16px; padding:8px 0; border-bottom:1px solid #eef0f3; }
    .cfg .row:last-child { border-bottom:0; }
    .cfg .row span { color:#6B7280; }
    .warn { margin-top:12px; background:#FFF3EA; color:#EA6A0C; padding:10px 12px; border-radius:8px; font-size:13px; }
    .note { background:#F1F3F5; color:#6B7280; padding:10px 12px; border-radius:8px; font-size:13px; }
    select { border:1px solid #E5E7EB; border-radius:8px; padding:9px 11px; font-size:14px; width:100%; box-sizing:border-box; background:#fff; }
    .check { margin:8px 0 0; padding-left:18px; color:#3A3F4B; line-height:1.9; }
    code { background:#F1F3F5; padding:1px 6px; border-radius:5px; font-size:12px; }
    .lbl { display:block; font-size:13px; font-weight:600; color:#3A3F4B; margin:14px 0 6px; }
    input { border:1px solid #E5E7EB; border-radius:8px; padding:9px 11px; font-size:14px; width:100%; box-sizing:border-box; }
    input:focus { outline:none; border-color:#14B9BA; }
    .waves { display:flex; flex-direction:column; gap:8px; align-items:flex-start; }
    .wave { display:flex; gap:8px; align-items:center; }
    .wave input { width:130px; }
    .grid2 { display:grid; grid-template-columns:1fr 1fr; gap:16px; }
    .ok-msg { color:#128040; font-size:13px; font-weight:600; margin-right:6px; }
  `],
})
export class SettingsComponent implements OnInit {
  private api = inject(Api);
  status: ConfigStatus | null = null;
  platform: PlatformSettings = { deliveryWaves: [], defaultCommissionRate: 0.1, currency: 'PLN' };
  commissionPercent = 10;
  savingP = false;
  savedP = false;

  integrations: PlatformIntegrations = { googleClientId: '', captchaProvider: '', captchaSiteKey: '', hasCaptchaSecret: false, smtpUseSsl: true };
  captchaSecret = '';
  smtpPassword = '';
  smtpTestTo = '';
  testing = false;
  testMsg = '';
  smtpError = '';
  savingI = false;
  savedI = false;
  integError = '';
  seeding = false;
  seedMsg = '';
  seedError = '';

  ngOnInit() { this.load(); }

  load() {
    this.api.get<ConfigStatus>('/admin/config/status').subscribe({
      next: s => this.status = s,
      error: () => this.status = null,
    });
    this.api.get<PlatformSettings>('/admin/config/platform').subscribe({
      next: p => this.applyPlatform(p),
      error: () => {},
    });
    this.api.get<PlatformIntegrations>('/admin/config/integrations').subscribe({
      next: i => { this.integrations = this.mapIntegrations(i); this.captchaSecret = ''; },
      error: () => {},
    });
  }

  private mapIntegrations(i: PlatformIntegrations): PlatformIntegrations {
    return {
      googleClientId: i.googleClientId || '',
      captchaProvider: i.captchaProvider || '',
      captchaSiteKey: i.captchaSiteKey || '',
      hasCaptchaSecret: !!i.hasCaptchaSecret,
      smtpHost: i.smtpHost || '',
      smtpPort: i.smtpPort ?? undefined,
      smtpUseSsl: i.smtpUseSsl ?? true,
      smtpUsername: i.smtpUsername || '',
      smtpFromEmail: i.smtpFromEmail || '',
      smtpFromName: i.smtpFromName || '',
      hasSmtpPassword: !!i.hasSmtpPassword,
    };
  }

  saveIntegrations() {
    this.savingI = true;
    this.savedI = false;
    this.integError = '';
    const body: any = {
      googleClientId: (this.integrations.googleClientId || '').trim() || undefined,
      captchaProvider: (this.integrations.captchaProvider || '').trim(),
      captchaSiteKey: (this.integrations.captchaSiteKey || '').trim() || undefined,
      smtpHost: (this.integrations.smtpHost || '').trim() || undefined,
      smtpPort: this.integrations.smtpPort ? Number(this.integrations.smtpPort) : undefined,
      smtpUseSsl: !!this.integrations.smtpUseSsl,
      smtpUsername: (this.integrations.smtpUsername || '').trim() || undefined,
      smtpFromEmail: (this.integrations.smtpFromEmail || '').trim() || undefined,
      smtpFromName: (this.integrations.smtpFromName || '').trim() || undefined,
    };
    if (this.captchaSecret.trim()) body.captchaSecret = this.captchaSecret.trim();
    if (this.smtpPassword.trim()) body.smtpPassword = this.smtpPassword.trim();
    this.api.put<PlatformIntegrations>('/admin/config/integrations', body).subscribe({
      next: i => {
        this.integrations = this.mapIntegrations(i); this.captchaSecret = ''; this.smtpPassword = '';
        this.savingI = false; this.savedI = true; setTimeout(() => this.savedI = false, 2500);
        this.load(); // odśwież status „Logowanie Google" / „E-mail (SMTP)"
      },
      error: e => { this.savingI = false; this.integError = e?.error?.detail ?? 'Nie udało się zapisać integracji.'; },
    });
  }

  sendTest() {
    this.testing = true; this.testMsg = ''; this.smtpError = '';
    const to = (this.smtpTestTo || '').trim();
    const qs = to ? ('?to=' + encodeURIComponent(to)) : '';
    this.api.post<{ sent: boolean; to: string }>('/admin/config/smtp/test' + qs, {}).subscribe({
      next: r => { this.testing = false; this.testMsg = '✓ wysłano do ' + r.to; setTimeout(() => this.testMsg = '', 5000); },
      error: e => { this.testing = false; this.smtpError = 'Test nieudany: ' + (e?.error?.detail ?? 'błąd'); },
    });
  }

  private applyPlatform(p: PlatformSettings) {
    this.platform = { ...p, deliveryWaves: [...(p.deliveryWaves ?? [])] };
    this.commissionPercent = Math.round((p.defaultCommissionRate ?? 0) * 1000) / 10;
  }

  addWave() { this.platform.deliveryWaves.push('12:00'); }
  removeWave(i: number) { this.platform.deliveryWaves.splice(i, 1); }

  savePlatform() {
    this.savingP = true;
    this.savedP = false;
    const body: PlatformSettings = { ...this.platform, defaultCommissionRate: this.commissionPercent / 100 };
    this.api.put<PlatformSettings>('/admin/config/platform', body).subscribe({
      next: p => { this.applyPlatform(p); this.savingP = false; this.savedP = true; setTimeout(() => this.savedP = false, 2500); },
      error: () => { this.savingP = false; },
    });
  }

  seedDemo() {
    this.seeding = true; this.seedMsg = ''; this.seedError = '';
    this.api.post<{ created: string[]; skipped: string[] }>('/admin/seed/pilot', {}).subscribe({
      next: r => {
        this.seeding = false;
        const c = r.created?.length ?? 0, s = r.skipped?.length ?? 0;
        this.seedMsg = `✓ utworzono ${c}, pominięto ${s}`;
        setTimeout(() => this.seedMsg = '', 4000);
      },
      error: e => { this.seeding = false; this.seedError = 'Nie udało się: ' + (e?.error?.detail ?? 'błąd'); },
    });
  }

  txt(ok: boolean) { return ok ? '✓ skonfigurowane' : '— brak'; }
  col(ok: boolean) { return ok ? '#128040' : '#6B7280'; }
}
