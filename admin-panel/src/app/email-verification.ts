import { Component, OnDestroy, OnInit, inject, input, output } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { NavigationEnd, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { Api } from './api';
import { readResetToken } from './password-recovery';

type View = 'checking' | 'check-error' | 'verified' | 'used' | 'expired' | 'invalid';

/** Kod błędu linku potwierdzającego z odpowiedzi API (pole „title" problem+json) → widok. */
export function verifyErrorView(e: unknown): 'invalid' | 'expired' | 'used' | null {
  const title = (e as HttpErrorResponse)?.error?.title;
  if (title === 'validation.verify_token_expired') return 'expired';
  if (title === 'validation.verify_token_used') return 'used';
  if (title === 'validation.verify_token_invalid') return 'invalid';
  return null;
}

/**
 * Potwierdzenie adresu e-mail — strona `/verify-email` z linku w wiadomości po rejestracji, wspólna dla WSZYSTKICH
 * kont (klient aplikacji, właściciel sklepu); woła istniejące `POST /identity/email/verify`. Token jest czytany z adresu
 * (`#token=…`, starsze maile: `?token=…`), od razu usuwany z paska adresu i trzymany tylko w pamięci komponentu.
 */
@Component({
  selector: 'app-email-verification',
  imports: [CommonModule],
  styles: [`
    .back { background: none; border: 0; padding: 0; color: var(--zz-orange-600); font: inherit; font-weight: 600; font-size: 14px; cursor: pointer; margin: 0 0 14px; }
    .lead { color: var(--muted); font-size: 14px; line-height: 1.5; margin: -12px 0 20px; }
    .badge { width: 64px; height: 64px; border-radius: 50%; display: grid; place-items: center; margin: 0 0 18px; }
    .badge.ok { background: #E9FBF0; color: #15803D; }
    .badge.warn { background: #FFF8EB; color: #B45309; }
    .option { border: 1px solid var(--border); border-radius: 12px; padding: 14px; margin: 0 0 12px; }
    .option b { display: block; font-size: 14px; margin-bottom: 8px; }
    .option p { margin: 0 0 10px; font-size: 13px; color: var(--muted); line-height: 1.5; }
    .option p:last-child { margin: 0; }
    .option .btn-lg { margin: 0; }
    a.btn-lg { display: block; text-align: center; text-decoration: none; box-sizing: border-box; }
    .center { text-align: center; }
    .spin { width: 28px; height: 28px; border-radius: 50%; border: 3px solid #D5F1F1; border-top-color: var(--zz-orange); animation: zzspin .9s linear infinite; margin: 8px auto 16px; }
    @keyframes zzspin { to { transform: rotate(360deg); } }
    @media (prefers-reduced-motion: reduce) { .spin { animation-duration: 3s; } }
  `],
  template: `
  <div class="auth-card" aria-live="polite">
    @switch (view) {
      @case ('checking') {
        <div class="spin" role="status" aria-label="Potwierdzamy adres e-mail"></div>
        <p class="center lead" style="margin:0">Potwierdzamy adres e-mail…</p>
      }
      @case ('check-error') {
        <h2>Nie udało się potwierdzić adresu</h2>
        <p class="lead">{{ error }}</p>
        <button class="btn-lg" type="button" (click)="verify()">Spróbuj ponownie</button>
      }
      @case ('invalid') {
        <div class="badge warn" aria-hidden="true"><svg viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M12 7v6M12 17h.01"/><circle cx="12" cy="12" r="9.5"/></svg></div>
        <h2>Nieprawidłowy link</h2>
        <p class="lead">Link jest niepełny lub uszkodzony. Skopiuj go z wiadomości w całości i otwórz ponownie.</p>
        <p class="center"><button type="button" class="back" (click)="back.emit()">{{ panelLabel }}</button></p>
      }
      @case ('expired') {
        <div class="badge warn" aria-hidden="true"><svg viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M12 7v5l3 2"/><circle cx="12" cy="12" r="9.5"/></svg></div>
        <h2>Ten link wygasł</h2>
        <p class="lead">Link potwierdzający działa przez ograniczony czas i ten już stracił ważność. Konto działa normalnie — możesz się zalogować.</p>
        <p class="center"><button type="button" class="back" (click)="back.emit()">{{ panelLabel }}</button></p>
      }
      @default {
        <!-- verified / used: adres jest potwierdzony — dalsze kroki zależą od tego, gdzie ktoś korzysta z Dowózka.pl. -->
        <div class="badge ok" aria-hidden="true"><svg viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M5 12.5l4.5 4.5L19 7.5"/></svg></div>
        @if (view === 'verified') {
          <h2>Adres e-mail potwierdzony</h2>
          <p class="lead">Dziękujemy! Adres <b>{{ maskedEmail }}</b> jest potwierdzony. Możesz wrócić tam, gdzie korzystasz z Dowózka.pl:</p>
        } @else {
          <h2>Adres jest już potwierdzony</h2>
          <p class="lead">Ten link został już wykorzystany — nie musisz nic więcej robić. Wróć tam, gdzie korzystasz z Dowózka.pl:</p>
        }
        <div class="option"><b>Panel sklepu lub kierowcy</b>
          <button class="btn-lg" type="button" (click)="back.emit()">{{ panelLabel }}</button></div>
        <div class="option"><b>Aplikacja Dowózka.pl (zakupy)</b>
          <p>Wróć do aplikacji na telefonie — potwierdzenie jest już zapisane na Twoim koncie.</p>
          @if (customerAppUrl) { <a class="btn-lg ghost" [href]="customerAppUrl" rel="noopener">Otwórz aplikację Dowózka.pl</a> }
        </div>
      }
    }
  </div>
  `,
})
export class EmailVerificationComponent implements OnInit, OnDestroy {
  private api = inject(Api);
  private location = inject(Location);
  private router = inject(Router);
  private navSub: Subscription | null = null;

  /** Czy w tej karcie jest aktywna sesja panelu (wtedy „Przejdź do panelu" zamiast logowania). */
  readonly loggedIn = input(false);
  /** Do logowania (albo do panelu przy aktywnej sesji). */
  readonly back = output<void>();

  view: View = 'checking';
  maskedEmail = '';
  error = '';
  customerAppUrl: string | null = null;
  private token = '';

  get panelLabel() { return this.loggedIn() ? 'Przejdź do panelu' : 'Zaloguj się do panelu'; }

  ngOnInit() {
    this.api.getPublic<{ customerAppUrl?: string | null }>('/config/public').subscribe({
      next: c => { this.customerAppUrl = c.customerAppUrl || null; },
      error: () => { /* brak konfiguracji nie blokuje potwierdzenia */ },
    });
    this.takeTokenFromAddress();
    // Nowy link wklejony do karty z otwartą już tą stroną zmienia tylko fragment adresu (bez przeładowania);
    // router kończy wtedy nawigację z tokenem w adresie — dopiero po niej czyścimy pasek adresu.
    this.navSub = this.router.events.subscribe(e => {
      if (e instanceof NavigationEnd && readResetToken(location.hash, '')) this.takeTokenFromAddress();
    });
  }

  ngOnDestroy() { this.navSub?.unsubscribe(); }

  private takeTokenFromAddress() {
    this.token = readResetToken(location.hash, location.search);
    // Token nie zostaje w pasku adresu ani w historii przeglądarki (np. przy kopiowaniu adresu, zrzutach ekranu).
    this.location.replaceState('/verify-email');
    this.maskedEmail = '';
    this.verify();
  }

  verify() {
    if (!this.token) { this.view = 'invalid'; return; }
    this.view = 'checking'; this.error = '';
    this.api.verifyEmail(this.token).subscribe({
      next: r => { this.maskedEmail = r.email; this.view = 'verified'; },
      error: e => {
        const v = verifyErrorView(e);
        if (v) { this.view = v; return; }
        const status = (e as HttpErrorResponse)?.status;
        this.error = status === 0 ? 'Nie udało się połączyć z serwerem — sprawdź internet i spróbuj ponownie.'
          : status === 429 ? 'Zbyt wiele prób. Spróbuj ponownie za minutę.'
          : 'Sprawdź połączenie z internetem i spróbuj ponownie.';
        this.view = 'check-error';
      },
    });
  }
}
