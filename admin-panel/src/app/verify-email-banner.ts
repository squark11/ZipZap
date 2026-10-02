import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Api } from './api';

/** Ile sekund po wysyłce można poprosić o kolejny link (limit na konto pilnuje serwer). */
export const RESEND_COOLDOWN_SECONDS = 60;

/** Komunikat po nieudanej ponownej wysyłce linku potwierdzającego. */
export function resendErrorMessage(e: unknown): string {
  const err = e as HttpErrorResponse;
  if (err?.error?.title === 'validation.verify_resend_limit')
    return 'Wysłaliśmy już kilka linków w ciągu ostatniej godziny. Sprawdź skrzynkę (także Spam) albo spróbuj później.';
  if (err?.status === 0) return 'Nie udało się połączyć z serwerem — sprawdź internet i spróbuj ponownie.';
  if (err?.status === 429) return 'Zbyt wiele prób. Spróbuj ponownie za minutę.';
  return 'Nie udało się wysłać linku — spróbuj ponownie.';
}

/**
 * Baner w panelu dla zalogowanego konta z niepotwierdzonym adresem e-mail: przypomina o linku z rejestracji i pozwala
 * wysłać go ponownie (istniejące `POST /identity/email/resend-verification`). Stan czytany z `/identity/me` (z bazy),
 * więc po potwierdzeniu na innym urządzeniu baner znika przy następnym wejściu.
 */
@Component({
  selector: 'app-verify-email-banner',
  imports: [CommonModule],
  styles: [`
    .bar { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; background: #FFF8EB; color: #6B3E09;
           border: 1px solid #F7D9B9; border-radius: 12px; padding: 10px 14px; margin: 0 0 16px; font-size: 13px; line-height: 1.45; }
    .bar.ok { background: #E9FBF0; border-color: #BFE9CF; color: #14532D; }
    .txt { flex: 1; min-width: 220px; }
    .txt b { color: #B45309; }
    .bar.ok .txt b { color: #15803D; }
    .note { display: block; margin-top: 2px; }
    .acts { display: flex; gap: 8px; align-items: center; }
    .x { background: none; border: 0; color: inherit; cursor: pointer; font: inherit; text-decoration: underline; padding: 4px; }
  `],
  template: `
  @if (visible) {
    <div class="bar" [class.ok]="sent" role="status" aria-live="polite">
      <div class="txt">
        @if (sent) {
          <b>Link wysłany.</b> Sprawdź skrzynkę <b>{{ email }}</b> (także folder Spam) i otwórz link z wiadomości.
        } @else {
          <b>Potwierdź adres e-mail.</b> Po rejestracji wysłaliśmy link na <b>{{ email }}</b> — otwórz go, aby potwierdzić adres.
        }
        @if (mailIsMock) { <span class="note">Poczta w tym środowisku to atrapa — wiadomości nie są wysyłane.</span> }
        @if (error) { <span class="note" role="alert">{{ error }}</span> }
      </div>
      <div class="acts">
        <button class="btn sm" type="button" [disabled]="busy || cooldown > 0" (click)="resend()">
          {{ busy ? 'Wysyłanie…' : cooldown > 0 ? 'Wyślij ponownie za ' + cooldown + ' s' : 'Wyślij link ponownie' }}</button>
        <button class="x" type="button" (click)="visible = false" aria-label="Ukryj przypomnienie">Ukryj</button>
      </div>
    </div>
  }
  `,
})
export class VerifyEmailBannerComponent implements OnInit, OnDestroy {
  private api = inject(Api);

  visible = false;
  email = '';
  sent = false;
  busy = false;
  error = '';
  cooldown = 0;
  mailIsMock = false;
  private timer: ReturnType<typeof setInterval> | null = null;

  ngOnInit() {
    this.api.me().subscribe({
      next: m => { this.email = m.email; this.visible = m.isEmailVerified === false; },
      error: () => { /* brak statusu = brak baneru (nie blokuje pracy w panelu) */ },
    });
    this.api.getPublic<{ emailDelivery?: string }>('/config/public').subscribe({
      next: c => this.mailIsMock = c.emailDelivery === 'mock',
      error: () => { },
    });
  }

  ngOnDestroy() { this.stopTimer(); }

  resend() {
    if (this.busy || this.cooldown > 0) return;
    this.busy = true; this.error = '';
    this.api.resendVerification().subscribe({
      next: r => {
        this.busy = false;
        if (!r.sent) { this.visible = false; return; } // adres został w międzyczasie potwierdzony
        this.sent = true; this.startCooldown();
      },
      error: e => { this.busy = false; this.error = resendErrorMessage(e); },
    });
  }

  private startCooldown() {
    this.stopTimer();
    this.cooldown = RESEND_COOLDOWN_SECONDS;
    this.timer = setInterval(() => { this.cooldown = Math.max(0, this.cooldown - 1); if (!this.cooldown) this.stopTimer(); }, 1000);
  }

  private stopTimer() { if (this.timer) { clearInterval(this.timer); this.timer = null; } }
}
