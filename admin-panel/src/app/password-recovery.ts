import { Component, OnDestroy, OnInit, inject, input, output } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { NavigationEnd, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { Api } from './api';

type View = 'forgot' | 'sent' | 'checking' | 'check-error' | 'form' | 'invalid' | 'expired' | 'used' | 'done';

/** Minimalna długość hasła — ta sama reguła co w API (rejestracja, zmiana i reset hasła). */
export const MIN_PASSWORD = 6;

/** Kod błędu linku resetu z odpowiedzi API (pole „title" problem+json) → widok. */
export function resetErrorView(e: unknown): 'invalid' | 'expired' | 'used' | null {
  const title = (e as HttpErrorResponse)?.error?.title;
  if (title === 'validation.reset_token_expired') return 'expired';
  if (title === 'validation.reset_token_used') return 'used';
  if (title === 'validation.reset_token_invalid') return 'invalid';
  return null;
}

/** Token z linku e-mail: `#token=…` (zalecane — nie trafia do serwera) albo starsze `?token=…`. */
export function readResetToken(hash: string, search: string): string {
  const fromHash = new URLSearchParams(hash.replace(/^#/, '')).get('token');
  const fromQuery = new URLSearchParams(search).get('token');
  return (fromHash ?? fromQuery ?? '').trim();
}

/**
 * Odzyskiwanie hasła — wspólne dla WSZYSTKICH kont (sklep, kierowca, klient aplikacji):
 * prośba o link (jednakowa odpowiedź dla każdego adresu) oraz strona `/reset-password` z linku w e-mailu.
 * Token jest czytany z adresu, od razu usuwany z paska adresu i trzymany tylko w pamięci komponentu.
 * Projekt ekranów: Figma „Dowózka.pl — Odzyskiwanie hasła (UX/UI)", P1–P6.
 */
@Component({
  selector: 'app-password-recovery',
  imports: [CommonModule, FormsModule],
  styles: [`
    .back { background: none; border: 0; padding: 0; color: var(--zz-orange-600); font: inherit; font-weight: 600; font-size: 14px; cursor: pointer; margin: 0 0 14px; }
    .lead { color: var(--muted); font-size: 14px; line-height: 1.5; margin: -12px 0 20px; }
    .alert { border-radius: 10px; padding: 12px 14px; font-size: 13px; line-height: 1.45; margin: 0 0 16px; }
    .alert b { display: block; margin-bottom: 2px; }
    .alert.warn { background: #FFF8EB; color: #6B3E09; }
    .alert.warn b { color: #B45309; }
    .alert.err { background: #FEECEC; color: #7F1D1D; }
    .badge { width: 64px; height: 64px; border-radius: 50%; display: grid; place-items: center; margin: 0 0 18px; }
    .badge.brand { background: #E6F7F7; color: var(--zz-orange-600); }
    .badge.ok { background: #E9FBF0; color: #15803D; }
    .badge.warn { background: #FFF8EB; color: #B45309; }
    .help { border: 1px solid var(--border); border-radius: 10px; padding: 12px 14px; font-size: 13px; color: var(--muted); margin: 0 0 16px; }
    .help b { color: var(--text); display: block; margin-bottom: 4px; }
    .help ul { margin: 0; padding-left: 18px; line-height: 1.6; }
    .reqs { list-style: none; padding: 0; margin: -4px 0 16px; font-size: 13px; display: grid; gap: 4px; }
    .reqs li { color: var(--muted); }
    .reqs li.ok { color: #15803D; }
    .pw { position: relative; }
    .pw input { width: 100%; padding-right: 72px; box-sizing: border-box; }
    .pw button { position: absolute; right: 8px; top: 50%; transform: translateY(-50%); background: none; border: 0; color: var(--zz-orange-600); font: inherit; font-weight: 600; font-size: 13px; cursor: pointer; padding: 6px; }
    .option { border: 1px solid var(--border); border-radius: 12px; padding: 14px; margin: 0 0 12px; }
    .option b { display: block; font-size: 14px; margin-bottom: 8px; }
    .option p { margin: 0 0 10px; font-size: 13px; color: var(--muted); line-height: 1.5; }
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
      @case ('forgot') {
        <form (ngSubmit)="sendLink()" novalidate>
          <button type="button" class="back" (click)="back.emit()">← Wróć do logowania</button>
          <h2>Nie pamiętasz hasła?</h2>
          <p class="lead">Podaj e-mail konta — wyślemy link do ustawienia nowego hasła. Link jest ważny 1 godzinę i działa jeden raz.</p>
          <div class="field"><label for="rec-email">E-mail</label>
            <input id="rec-email" name="email" type="email" autocomplete="email" [(ngModel)]="email" required autofocus /></div>
          @if (mailIsMock) { <ng-container *ngTemplateOutlet="mockAlert" /> }
          <button class="btn-lg" type="submit" [disabled]="busy">{{ busy ? 'Wysyłanie…' : 'Wyślij link' }}</button>
          @if (error) { <p class="error" role="alert">{{ error }}</p> }
          <p class="hint">Ten sam formularz działa dla kont sklepów, kierowców i klientów aplikacji Dowózka.pl.</p>
        </form>
      }
      @case ('sent') {
        <div class="badge brand" aria-hidden="true"><svg viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 7l9 6 9-6"/></svg></div>
        <h2>Sprawdź skrzynkę</h2>
        <p class="lead">Jeśli istnieje konto z adresem <b>{{ sentTo }}</b>, wysłaliśmy na nie link do zmiany hasła. Link jest ważny 1 godzinę.</p>
        @if (mailIsMock) { <ng-container *ngTemplateOutlet="mockAlert" /> }
        <div class="help"><b>Nie widzisz wiadomości?</b>
          <ul><li>Zajrzyj do folderu Spam lub Oferty.</li><li>Sprawdź, czy adres jest wpisany poprawnie.</li><li>Wiadomość może dojść z opóźnieniem kilku minut.</li></ul>
        </div>
        <button class="btn-lg ghost" type="button" [disabled]="busy || cooldown > 0" (click)="sendLink()">
          {{ cooldown > 0 ? 'Wyślij ponownie za ' + cooldown + ' s' : 'Wyślij ponownie' }}</button>
        @if (error) { <p class="error" role="alert">{{ error }}</p> }
        <p class="center"><button type="button" class="back" (click)="back.emit()">Wróć do logowania</button></p>
      }
      @case ('checking') {
        <div class="spin" role="status" aria-label="Sprawdzamy link"></div>
        <p class="center lead" style="margin:0">Sprawdzamy link…</p>
      }
      @case ('check-error') {
        <h2>Nie udało się sprawdzić linku</h2>
        <p class="lead">{{ error }}</p>
        <button class="btn-lg" type="button" (click)="checkToken()">Spróbuj ponownie</button>
      }
      @case ('form') {
        <form (ngSubmit)="savePassword()" novalidate>
          <h2>Ustaw nowe hasło</h2>
          <p class="lead">Konto: {{ maskedEmail }}</p>
          <div class="field"><label for="rec-pw">Nowe hasło</label>
            <div class="pw"><input id="rec-pw" name="pw" [type]="showPassword ? 'text' : 'password'" autocomplete="new-password"
                   [(ngModel)]="password" required autofocus />
              <button type="button" (click)="showPassword = !showPassword" [attr.aria-pressed]="showPassword">{{ showPassword ? 'Ukryj' : 'Pokaż' }}</button></div></div>
          <div class="field"><label for="rec-pw2">Powtórz nowe hasło</label>
            <input id="rec-pw2" name="pw2" [type]="showPassword ? 'text' : 'password'" autocomplete="new-password" [(ngModel)]="password2" required /></div>
          <ul class="reqs">
            <li [class.ok]="longEnough">{{ longEnough ? '✓' : '•' }} Co najmniej {{ minPassword }} znaków</li>
            <li [class.ok]="matches">{{ matches ? '✓' : '•' }} Oba hasła są takie same</li>
          </ul>
          <button class="btn-lg" type="submit" [disabled]="busy || !longEnough || !matches">{{ busy ? 'Zapisywanie…' : 'Zapisz nowe hasło' }}</button>
          @if (error) { <p class="error" role="alert">{{ error }}</p> }
          <p class="hint">Po zmianie wylogujemy Cię ze wszystkich urządzeń.</p>
        </form>
      }
      @case ('done') {
        <div class="badge ok" aria-hidden="true"><svg viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M5 12.5l4.5 4.5L19 7.5"/></svg></div>
        <h2>Hasło zmienione</h2>
        <p class="lead">Wylogowaliśmy Cię ze wszystkich urządzeń. Zaloguj się nowym hasłem tam, gdzie korzystasz z Dowózka.pl:</p>
        <div class="option"><b>Panel sklepu lub kierowcy</b>
          <button class="btn-lg" type="button" (click)="back.emit()">Zaloguj się do panelu</button></div>
        <div class="option"><b>Aplikacja Dowózka.pl (zakupy)</b>
          <p>Otwórz aplikację na telefonie i zaloguj się nowym hasłem.</p>
          @if (customerAppUrl) { <a class="btn-lg ghost" [href]="customerAppUrl" rel="noopener">Otwórz aplikację Dowózka.pl</a> }
        </div>
      }
      @default {
        <div class="badge warn" aria-hidden="true"><svg viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M12 7v6M12 17h.01"/><circle cx="12" cy="12" r="9.5"/></svg></div>
        <h2>{{ linkTitle }}</h2>
        <p class="lead">{{ linkText }}</p>
        <button class="btn-lg" type="button" (click)="startForgot()">Wyślij nowy link</button>
        <p class="center"><button type="button" class="back" (click)="back.emit()">Wróć do logowania</button></p>
      }
    }
  </div>

  <ng-template #mockAlert>
    <div class="alert warn" role="status"><b>Poczta w tym środowisku to atrapa</b>
      Wiadomości nie są wysyłane, więc link nie dotrze. Skontaktuj się z administratorem serwisu.</div>
  </ng-template>
  `,
})
export class PasswordRecoveryComponent implements OnInit, OnDestroy {
  private api = inject(Api);
  private location = inject(Location);
  private router = inject(Router);
  private navSub: Subscription | null = null;

  /** „forgot" — prośba o link; „reset" — strona z linku w e-mailu. */
  readonly mode = input<'forgot' | 'reset'>('forgot');
  readonly initialEmail = input<string>('');
  /** Powrót do logowania. */
  readonly back = output<void>();

  view: View = 'forgot';
  email = '';
  sentTo = '';
  password = '';
  password2 = '';
  showPassword = false;
  maskedEmail = '';
  busy = false;
  error = '';
  cooldown = 0;
  mailIsMock = false;
  customerAppUrl: string | null = null;
  readonly minPassword = MIN_PASSWORD;
  private token = '';
  private timer: ReturnType<typeof setInterval> | null = null;

  get longEnough() { return this.password.length >= MIN_PASSWORD; }
  get matches() { return this.password.length > 0 && this.password === this.password2; }
  get linkTitle() {
    return this.view === 'expired' ? 'Ten link wygasł' : this.view === 'used' ? 'Ten link został już użyty' : 'Nieprawidłowy link';
  }
  get linkText() {
    return this.view === 'expired'
      ? 'Link do zmiany hasła jest ważny 1 godzinę. Wyślij nowy — zajmie to chwilę.'
      : this.view === 'used'
        ? 'Hasło zostało już zmienione tym linkiem. Jeśli to nie Ty — od razu wyślij nowy link i zmień hasło.'
        : 'Link jest niepełny lub uszkodzony. Skopiuj go z wiadomości w całości albo wyślij nowy.';
  }

  ngOnInit() {
    this.email = this.initialEmail();
    this.api.getPublic<{ emailDelivery?: string; customerAppUrl?: string | null }>('/config/public').subscribe({
      next: c => { this.mailIsMock = c.emailDelivery === 'mock'; this.customerAppUrl = c.customerAppUrl || null; },
      error: () => { /* brak konfiguracji nie blokuje odzyskiwania */ },
    });
    if (this.mode() === 'reset') this.takeTokenFromAddress();
    // Nowy link wklejony do karty z otwartą już stroną resetu zmienia tylko fragment adresu (bez przeładowania);
    // router kończy wtedy nawigację z tokenem w adresie — dopiero po niej czyścimy pasek adresu.
    this.navSub = this.router.events.subscribe(e => {
      if (e instanceof NavigationEnd && this.mode() === 'reset' && readResetToken(location.hash, '')) this.takeTokenFromAddress();
    });
  }

  private takeTokenFromAddress() {
    this.token = readResetToken(location.hash, location.search);
    // Token nie zostaje w pasku adresu ani w historii przeglądarki (np. przy kopiowaniu adresu, zrzutach ekranu).
    this.location.replaceState('/reset-password');
    this.password = ''; this.password2 = ''; this.maskedEmail = '';
    this.checkToken();
  }

  ngOnDestroy() { this.stopTimer(); this.navSub?.unsubscribe(); }

  checkToken() {
    if (!this.token) { this.view = 'invalid'; return; }
    this.view = 'checking'; this.error = '';
    this.api.checkResetToken(this.token).subscribe({
      next: r => { this.maskedEmail = r.email; this.view = 'form'; },
      error: e => {
        const v = resetErrorView(e);
        if (v) { this.view = v; return; }
        this.error = this.networkMessage(e, 'Sprawdź połączenie z internetem i spróbuj ponownie.');
        this.view = 'check-error';
      },
    });
  }

  savePassword() {
    if (!this.longEnough || !this.matches || this.busy) return;
    this.busy = true; this.error = '';
    this.api.resetPassword(this.token, this.password).subscribe({
      next: () => {
        this.busy = false; this.token = ''; this.password = ''; this.password2 = '';
        this.api.logout(); // serwer unieważnił sesje — lokalna też nie może zostać
        this.view = 'done';
      },
      error: e => {
        this.busy = false;
        const v = resetErrorView(e);
        if (v) { this.view = v; return; }
        this.error = (e as HttpErrorResponse)?.status === 400
          ? ((e as HttpErrorResponse).error?.detail ?? 'Nie udało się zapisać hasła.')
          : this.networkMessage(e, 'Nie udało się zapisać hasła — spróbuj ponownie.');
      },
    });
  }

  startForgot() {
    this.token = ''; this.error = ''; this.email = ''; this.view = 'forgot';
  }

  sendLink() {
    const email = this.email.trim();
    if (!email || !email.includes('@')) { this.error = 'Podaj poprawny adres e-mail.'; return; }
    if (this.busy) return;
    this.busy = true; this.error = '';
    this.api.forgotPassword(email).subscribe({
      next: () => { this.busy = false; this.sentTo = email; this.view = 'sent'; this.startCooldown(); },
      error: e => { this.busy = false; this.error = this.networkMessage(e, 'Nie udało się wysłać prośby — spróbuj ponownie.'); },
    });
  }

  private networkMessage(e: unknown, fallback: string): string {
    const status = (e as HttpErrorResponse)?.status;
    if (status === 0) return 'Nie udało się połączyć z serwerem — sprawdź internet i spróbuj ponownie.';
    if (status === 429) return 'Zbyt wiele prób. Spróbuj ponownie za minutę.';
    return fallback;
  }

  private startCooldown() {
    this.stopTimer();
    this.cooldown = 60;
    this.timer = setInterval(() => { this.cooldown = Math.max(0, this.cooldown - 1); if (!this.cooldown) this.stopTimer(); }, 1000);
  }

  private stopTimer() { if (this.timer) { clearInterval(this.timer); this.timer = null; } }
}
