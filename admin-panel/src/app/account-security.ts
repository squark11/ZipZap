import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import * as QRCodeNS from 'qrcode';
import { Api } from './api';

// qrcode jest pakietem CommonJS — pod różnymi interopami eksport bywa pod .default.
const QRCode: { toString(text: string, opts?: unknown): Promise<string> } =
  (QRCodeNS as any).default ?? (QRCodeNS as any);
import { ConfigModalComponent } from './config-modal';

/**
 * Konto i bezpieczeństwo: zmiana hasła + 2FA (TOTP / aplikacja authenticator).
 * Samodzielny modal — rodzic steruje przez [open]/(close).
 */
@Component({
  selector: 'app-account-security',
  imports: [CommonModule, FormsModule, ConfigModalComponent],
  template: `
  <app-config-modal [open]="open" title="Konto i bezpieczeństwo"
      [tabs]="['Hasło','Uwierzytelnianie 2FA']" [active]="tab" (tabChange)="tab = $event"
      [showFooter]="false" (close)="onClose()">

    @if (tab === 'Hasło') {
      <p class="muted" style="margin-top:0">Zmiana hasła wyloguje wszystkie sesje — po zmianie zaloguj się ponownie nowym hasłem.</p>
      <label class="lbl">Bieżące hasło</label>
      <input type="password" name="cur" [(ngModel)]="curPass" autocomplete="current-password" />
      <div class="grid2">
        <div><label class="lbl">Nowe hasło</label><input type="password" name="np" [(ngModel)]="newPass" autocomplete="new-password" /></div>
        <div><label class="lbl">Powtórz nowe hasło</label><input type="password" name="np2" [(ngModel)]="newPass2" autocomplete="new-password" /></div>
      </div>
      <p class="hint">Minimum 6 znaków.</p>
      @if (passError) { <p class="warn">{{ passError }}</p> }
      @if (passOk) { <p class="ok">✓ Hasło zmienione. Za chwilę nastąpi wylogowanie — zaloguj się ponownie.</p> }
      <div style="margin-top:14px;display:flex;justify-content:flex-end">
        <button class="btn primary sm" (click)="changePassword()" [disabled]="passBusy">Zmień hasło</button>
      </div>
    }

    @if (tab === 'Uwierzytelnianie 2FA') {
      @if (statusLoading) { <p class="muted" style="margin-top:0">Sprawdzanie statusu…</p> }

      @else if (enabled) {
        <div class="badge on">✓ 2FA jest włączone — logowanie wymaga kodu z aplikacji authenticator.</div>
        <p class="muted">Aby wyłączyć, potwierdź aktualnym kodem z aplikacji.</p>
        <label class="lbl">Kod z aplikacji (6 cyfr)</label>
        <input class="code" name="dcode" [(ngModel)]="disableCode" inputmode="numeric" maxlength="6" placeholder="000000" />
        @if (twoFaError) { <p class="warn">{{ twoFaError }}</p> }
        <div style="margin-top:14px;display:flex;justify-content:flex-end">
          <button class="btn danger sm" (click)="disable2fa()" [disabled]="twoFaBusy || disableCode.length < 6">Wyłącz 2FA</button>
        </div>
      }

      @else if (!setup) {
        <div class="badge off">2FA jest wyłączone. Włącz, aby chronić konto administratora kodem jednorazowym.</div>
        <ol class="steps">
          <li>Zainstaluj aplikację authenticator (Google Authenticator, Authy, Microsoft Authenticator).</li>
          <li>Kliknij „Rozpocznij konfigurację" — pokażemy kod QR.</li>
          <li>Zeskanuj QR i wpisz wygenerowany kod, aby potwierdzić.</li>
        </ol>
        @if (twoFaError) { <p class="warn">{{ twoFaError }}</p> }
        <div style="margin-top:6px;display:flex;justify-content:flex-end">
          <button class="btn primary sm" (click)="beginSetup()" [disabled]="twoFaBusy">Rozpocznij konfigurację</button>
        </div>
      }

      @else {
        <p class="muted" style="margin-top:0">Zeskanuj kod QR w aplikacji authenticator, a następnie wpisz wygenerowany 6-cyfrowy kod.</p>
        <div class="qrwrap">
          @if (qr) { <div class="qr" [innerHTML]="qr"></div> }
          <div class="manual">
            <span class="mlbl">Nie możesz zeskanować? Wpisz klucz ręcznie:</span>
            <code class="mkey">{{ prettySecret }}</code>
          </div>
        </div>
        <label class="lbl">Kod z aplikacji (6 cyfr)</label>
        <input class="code" name="ecode" [(ngModel)]="enableCode" inputmode="numeric" maxlength="6" placeholder="000000" />
        @if (twoFaError) { <p class="warn">{{ twoFaError }}</p> }
        <div style="margin-top:14px;display:flex;gap:10px;justify-content:flex-end">
          <button class="btn ghost sm" (click)="cancelSetup()">Anuluj</button>
          <button class="btn primary sm" (click)="enable2fa()" [disabled]="twoFaBusy || enableCode.length < 6">Włącz 2FA</button>
        </div>
      }
    }
  </app-config-modal>
  `,
  styles: [`
    .lbl { display:block; font-size:13px; font-weight:600; color:#3A3F4B; margin:14px 0 6px; }
    input { border:1px solid #E5E7EB; border-radius:8px; padding:9px 11px; font-size:14px; width:100%; box-sizing:border-box; }
    input:focus { outline:none; border-color:#14B9BA; }
    input.code { font-family:'JetBrains Mono',monospace; font-size:20px; letter-spacing:.3em; text-align:center; max-width:220px; }
    .grid2 { display:grid; grid-template-columns:1fr 1fr; gap:16px; }
    @media (max-width:560px){ .grid2 { grid-template-columns:1fr; } }
    .hint { font-size:12px; color:#6B7280; margin:6px 0 0; }
    .warn { margin-top:12px; background:#FEECEC; color:#B4232A; padding:10px 12px; border-radius:8px; font-size:13px; }
    .ok { margin-top:12px; background:#E9FBF0; color:#128040; padding:10px 12px; border-radius:8px; font-size:13px; }
    .badge { padding:12px 14px; border-radius:10px; font-size:13.5px; font-weight:600; }
    .badge.on { background:#E9FBF0; color:#128040; }
    .badge.off { background:#FFF3EA; color:#EA6A0C; }
    .steps { margin:14px 0 0; padding-left:20px; color:#3A3F4B; font-size:13.5px; display:flex; flex-direction:column; gap:8px; }
    .qrwrap { display:flex; gap:18px; align-items:center; flex-wrap:wrap; margin:14px 0 4px; }
    .qr { width:180px; height:180px; background:#fff; padding:8px; border:1px solid #E5E7EB; border-radius:12px; }
    .qr :is(svg) { width:100%; height:100%; display:block; }
    .manual { display:flex; flex-direction:column; gap:6px; min-width:0; }
    .mlbl { font-size:12px; color:#6B7280; }
    .mkey { font-family:'JetBrains Mono',monospace; font-size:15px; letter-spacing:.08em; background:#F1F3F5; padding:8px 12px; border-radius:8px; color:#0F2A2A; word-break:break-all; }
    .btn.danger { background:#B4232A; color:#fff; border-color:#B4232A; }
    .btn.danger:hover { background:#9c1e24; }
  `],
})
export class AccountSecurityComponent {
  private api = inject(Api);
  private san = inject(DomSanitizer);

  @Input() set open(v: boolean) {
    this._open = v;
    if (v) { this.reset(); this.loadStatus(); }
  }
  get open() { return this._open; }
  private _open = false;
  @Output() close = new EventEmitter<void>();
  /** Zgłaszane po zmianie hasła — rodzic wylogowuje użytkownika. */
  @Output() passwordChanged = new EventEmitter<void>();

  tab = 'Hasło';

  // Hasło
  curPass = ''; newPass = ''; newPass2 = '';
  passBusy = false; passOk = false; passError = '';

  // 2FA
  statusLoading = false;
  enabled = false;
  setup = false;
  secret = '';
  qr: SafeHtml | null = null;
  enableCode = ''; disableCode = '';
  twoFaBusy = false; twoFaError = '';

  get prettySecret() { return (this.secret.match(/.{1,4}/g) ?? []).join(' '); }

  private reset() {
    this.tab = 'Hasło';
    this.curPass = this.newPass = this.newPass2 = '';
    this.passBusy = this.passOk = false; this.passError = '';
    this.setup = false; this.secret = ''; this.qr = null;
    this.enableCode = this.disableCode = ''; this.twoFaBusy = false; this.twoFaError = '';
  }

  onClose() { this.close.emit(); }

  private loadStatus() {
    this.statusLoading = true;
    this.api.get<{ enabled: boolean }>('/identity/2fa/status').subscribe({
      next: r => { this.enabled = r.enabled; this.statusLoading = false; },
      error: () => { this.enabled = false; this.statusLoading = false; },
    });
  }

  changePassword() {
    this.passError = ''; this.passOk = false;
    if (this.newPass.length < 6) { this.passError = 'Nowe hasło musi mieć co najmniej 6 znaków.'; return; }
    if (this.newPass !== this.newPass2) { this.passError = 'Hasła nie są identyczne.'; return; }
    this.passBusy = true;
    this.api.post('/identity/password/change', { currentPassword: this.curPass, newPassword: this.newPass }).subscribe({
      next: () => {
        this.passBusy = false; this.passOk = true;
        this.curPass = this.newPass = this.newPass2 = '';
        setTimeout(() => this.passwordChanged.emit(), 2500);
      },
      error: e => { this.passBusy = false; this.passError = e?.error?.detail ?? 'Nie udało się zmienić hasła.'; },
    });
  }

  beginSetup() {
    this.twoFaBusy = true; this.twoFaError = '';
    this.api.post<{ secret: string; otpauthUri: string }>('/identity/2fa/setup', {}).subscribe({
      next: r => {
        this.twoFaBusy = false; this.setup = true; this.secret = r.secret; this.enableCode = '';
        QRCode.toString(r.otpauthUri, { type: 'svg', margin: 1, width: 200 })
          .then(svg => this.qr = this.san.bypassSecurityTrustHtml(svg))
          .catch(() => this.qr = null);
      },
      error: e => { this.twoFaBusy = false; this.twoFaError = e?.error?.detail ?? 'Nie udało się rozpocząć konfiguracji.'; },
    });
  }

  cancelSetup() { this.setup = false; this.secret = ''; this.qr = null; this.enableCode = ''; this.twoFaError = ''; }

  enable2fa() {
    this.twoFaBusy = true; this.twoFaError = '';
    this.api.post('/identity/2fa/enable', { code: this.enableCode.trim() }).subscribe({
      next: () => { this.twoFaBusy = false; this.setup = false; this.enabled = true; this.qr = null; this.secret = ''; },
      error: e => { this.twoFaBusy = false; this.twoFaError = e?.error?.detail ?? 'Nieprawidłowy kod.'; },
    });
  }

  disable2fa() {
    this.twoFaBusy = true; this.twoFaError = '';
    this.api.post('/identity/2fa/disable', { code: this.disableCode.trim() }).subscribe({
      next: () => { this.twoFaBusy = false; this.enabled = false; this.disableCode = ''; },
      error: e => { this.twoFaBusy = false; this.twoFaError = e?.error?.detail ?? 'Nieprawidłowy kod.'; },
    });
  }
}
