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
    <h1 style="font-size:16px;margin:0 0 8px">Twoje konta / sekrety</h1>
    <p class="muted">Sekrety trzymamy w zmiennych środowiskowych (nie w bazie). Uzupełnij <code>.env</code>
      wg szablonu <code>.env.example</code> — szczegóły w <code>DEPLOY.md</code>.</p>
    <ul class="check">
      <li>Baza (Postgres / Neon) — <code>CONNECTIONSTRINGS__POSTGRES</code></li>
      <li>Klucz JWT — <code>JWT__SIGNINGKEY</code></li>
      <li>Płatności — <code>PAYMENTS__*</code> (mock teraz, Przelewy24 po podpięciu)</li>
      <li>Google OAuth — <code>GOOGLE__CLIENTID</code></li>
      <li>E-mail SMTP — <code>EMAIL__SMTP__*</code></li>
      <li>URL API dla aplikacji (PWA) — <code>WEB_API_BASE_URL</code></li>
      <li>Hosting / CI — Cloudflare / Fly.io / Neon (jako sekrety CI)</li>
    </ul>
  </div>
  `,
  styles: [`
    .cfg { display:flex; flex-direction:column; gap:2px; }
    .cfg .row { display:flex; justify-content:space-between; align-items:center; gap:16px; padding:8px 0; border-bottom:1px solid #eef0f3; }
    .cfg .row:last-child { border-bottom:0; }
    .cfg .row span { color:#6B7280; }
    .warn { margin-top:12px; background:#FFF3EA; color:#EA6A0C; padding:10px 12px; border-radius:8px; font-size:13px; }
    .check { margin:8px 0 0; padding-left:18px; color:#3A3F4B; line-height:1.9; }
    code { background:#F1F3F5; padding:1px 6px; border-radius:5px; font-size:12px; }
    .lbl { display:block; font-size:13px; font-weight:600; color:#3A3F4B; margin:14px 0 6px; }
    input { border:1px solid #E5E7EB; border-radius:8px; padding:9px 11px; font-size:14px; width:100%; box-sizing:border-box; }
    input:focus { outline:none; border-color:#F97316; }
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

  txt(ok: boolean) { return ok ? '✓ skonfigurowane' : '— brak'; }
  col(ok: boolean) { return ok ? '#128040' : '#6B7280'; }
}
