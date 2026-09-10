import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
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

@Component({
  selector: 'app-settings',
  imports: [CommonModule],
  template: `
  <div class="page-head">
    <h1>Konfiguracja</h1>
    <div class="controls"><button class="btn ghost sm" (click)="load()">Odśwież</button></div>
  </div>

  @if (loading) { <div class="card pad muted">Ładowanie…</div> }
  @else if (!status) { <div class="card pad muted">Nie udało się pobrać statusu konfiguracji.</div> }
  @else {
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

    <div class="card pad">
      <h1 style="font-size:16px;margin:0 0 4px">Ustawienia platformy</h1>
      <p class="muted">Edytowalne ustawienia nie-sekretne (godziny fal dostaw, domyślna prowizja, waluta)
        pojawią się tutaj wkrótce.</p>
    </div>
  }
  `,
  styles: [`
    .cfg { display:flex; flex-direction:column; gap:2px; }
    .cfg .row { display:flex; justify-content:space-between; align-items:center; gap:16px; padding:8px 0; border-bottom:1px solid #eef0f3; }
    .cfg .row:last-child { border-bottom:0; }
    .cfg .row span { color:#6B7280; }
    .warn { margin-top:12px; background:#FFF3EA; color:#EA6A0C; padding:10px 12px; border-radius:8px; font-size:13px; }
    .check { margin:8px 0 0; padding-left:18px; color:#3A3F4B; line-height:1.9; }
    code { background:#F1F3F5; padding:1px 6px; border-radius:5px; font-size:12px; }
  `],
})
export class SettingsComponent implements OnInit {
  private api = inject(Api);
  status: ConfigStatus | null = null;
  loading = false;

  ngOnInit() { this.load(); }

  load() {
    this.loading = true;
    this.api.get<ConfigStatus>('/admin/config/status').subscribe({
      next: s => { this.status = s; this.loading = false; },
      error: () => { this.status = null; this.loading = false; },
    });
  }

  txt(ok: boolean) { return ok ? '✓ skonfigurowane' : '— brak'; }
  col(ok: boolean) { return ok ? '#128040' : '#6B7280'; }
}
