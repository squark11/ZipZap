import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api } from './api';
import { ConfigModalComponent } from './config-modal';
import { AccountSecurityComponent } from './account-security';
import { TestAccountsComponent } from './test-accounts';

export interface ConfigStatus {
  environment: string;
  payments: { provider: string; publicUrl: string; mockPayPage: boolean };
  googleSignIn: boolean;
  email: boolean;
  emailChannel?: string;
  rabbitMq: boolean;
  identityPublicUrl: string;
  /** Problem z adresem linków w e-mailach (reset hasła) — null = OK. */
  identityPublicUrlProblem?: string | null;
  /** Kolejka e-maili w tle (reset hasła) — liczniki i ostatni błąd (sama kategoria, bez treści). */
  emailQueue?: { sent: number; failed: number; lastFailureAtUtc?: string | null; lastFailureCategory?: string | null };
  adminSeedEmail: string;
  jwtUsingDevSecret: boolean;
  /** Efektywny adres aplikacji klienta (web) i jego źródło: „panel" / „env"; null = nieustawiony. */
  customerAppUrl?: string | null;
  customerAppUrlSource?: string | null;
  customerAppUrlEnv?: string | null;
}

export interface PlatformSettings {
  deliveryWaves: string[];
  defaultCommissionRate: number;
  currency: string;
  operatorName?: string;
  operatorContact?: string;
}

/** Harmonogram rund zakupowych (kiedy sklep kompletuje zamówienia) — osobny od fal i terminów dostaw. */
export interface PurchasingSchedule {
  timeZoneId: string;
  rounds: { localTime: string; cutoffMinutes: number }[];
  activeDays: string[];
  minDeliveryLeadMinutes: number;
  updatedAtUtc?: string;
}

const WEEK_DAYS = [
  { key: 'Monday', label: 'Pn' }, { key: 'Tuesday', label: 'Wt' }, { key: 'Wednesday', label: 'Śr' },
  { key: 'Thursday', label: 'Cz' }, { key: 'Friday', label: 'Pt' }, { key: 'Saturday', label: 'Sb' },
  { key: 'Sunday', label: 'Nd' },
];

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
  customerAppUrl?: string;
}

@Component({
  selector: 'app-settings',
  imports: [CommonModule, FormsModule, ConfigModalComponent, AccountSecurityComponent, TestAccountsComponent],
  template: `
  <div class="page-head">
    <h1>Konfiguracja</h1>
    <div class="controls"><button class="btn ghost sm" (click)="load()">Odśwież</button></div>
  </div>

  <div class="tiles">
    <button class="tile" (click)="open('account')">
      <span class="tic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><rect x="4" y="11" width="16" height="10" rx="2"/><path d="M8 11V8a4 4 0 0 1 8 0v3"/><circle cx="12" cy="16" r="1.4"/></svg></span>
      <span class="tbody"><span class="ttl">Konto i bezpieczeństwo</span><span class="tst">hasło i weryfikacja 2FA</span></span>
      <span class="tcta">Zarządzaj →</span>
    </button>

    <button class="tile" (click)="open('testaccounts')">
      <span class="tic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 11l-3 3-1.5-1.5"/></svg></span>
      <span class="tbody"><span class="ttl">Konta testowe</span><span class="tst">zapisane loginy (klient/dostawca/sklep)</span></span>
      <span class="tcta">Zarządzaj →</span>
    </button>

    <button class="tile" (click)="open('smtp')">
      <span class="tic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 7l9 6 9-6"/></svg></span>
      <span class="tbody"><span class="ttl">Poczta e-mail (SMTP)</span><span class="tst" [class.on]="smtpConfigured">{{ smtpConfigured ? '✓ skonfigurowane' : '— nieustawione' }}</span></span>
      <span class="tcta">Konfiguruj →</span>
    </button>

    <button class="tile" (click)="open('integrations')">
      <span class="tic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><circle cx="8" cy="15" r="5"/><path d="M13 10l8-8m-3 0h3v3"/><path d="M14 9l3 3"/></svg></span>
      <span class="tbody"><span class="ttl">Logowanie i captcha</span><span class="tst" [class.on]="!!status?.googleSignIn">{{ status?.googleSignIn ? '✓ Google aktywne' : '— Google wyłączone' }}</span></span>
      <span class="tcta">Konfiguruj →</span>
    </button>

    <button class="tile" (click)="open('customerapp')">
      <span class="tic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><path d="M14 14h3v3h-3zM20 14v.01M14 20h.01M17 17h4v4"/></svg></span>
      <span class="tbody"><span class="ttl">Aplikacja klienta i kody QR</span><span class="tst" [class.on]="!!status?.customerAppUrl">{{ status?.customerAppUrl ? '✓ ' + status?.customerAppUrl : '— adres nieustawiony' }}</span></span>
      <span class="tcta">Konfiguruj →</span>
    </button>

    <button class="tile" (click)="open('platform')">
      <span class="tic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><line x1="4" y1="21" x2="4" y2="14"/><line x1="4" y1="10" x2="4" y2="3"/><line x1="12" y1="21" x2="12" y2="12"/><line x1="12" y1="8" x2="12" y2="3"/><line x1="20" y1="21" x2="20" y2="16"/><line x1="20" y1="12" x2="20" y2="3"/><line x1="1" y1="14" x2="7" y2="14"/><line x1="9" y1="8" x2="15" y2="8"/><line x1="17" y1="16" x2="23" y2="16"/></svg></span>
      <span class="tbody"><span class="ttl">Ustawienia platformy</span><span class="tst">prowizja, waluta, fale dostaw</span></span>
      <span class="tcta">Konfiguruj →</span>
    </button>

    <button class="tile" (click)="open('rounds')">
      <span class="tic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M6 7h12l-1 13H7L6 7z"/><path d="M9 7a3 3 0 0 1 6 0"/><path d="M12 11v3l2 1.5"/></svg></span>
      <span class="tbody"><span class="ttl">Rundy zakupowe</span><span class="tst">{{ roundsSummary }}</span></span>
      <span class="tcta">Konfiguruj →</span>
    </button>

    <button class="tile" (click)="open('demo')">
      <span class="tic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"/><path d="M3 12c0 1.66 4 3 9 3s9-1.34 9-3"/></svg></span>
      <span class="tbody"><span class="ttl">Dane demo (pilotaż)</span><span class="tst">sklepy Rapacz / Lewiatan</span></span>
      <span class="tcta">Otwórz →</span>
    </button>

    <button class="tile" (click)="open('status')">
      <span class="tic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><path d="M12 8v4l3 2"/></svg></span>
      <span class="tbody"><span class="ttl">Status i sekrety</span><span class="tst">{{ status?.environment || '—' }}</span></span>
      <span class="tcta">Zobacz →</span>
    </button>
  </div>

  <!-- ===== Konto i bezpieczeństwo ===== -->
  <app-account-security [open]="panel === 'account'" (close)="close()" (passwordChanged)="onPasswordChanged()"></app-account-security>

  <!-- ===== Konta testowe ===== -->
  <app-test-accounts [open]="panel === 'testaccounts'" (close)="close()"></app-test-accounts>

  <!-- ===== SMTP ===== -->
  <app-config-modal [open]="panel === 'smtp'" title="Poczta e-mail (SMTP)"
      [saving]="savingI" [saved]="savedI" (save)="saveIntegrations()" (close)="close()">
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
    <div style="display:flex;gap:10px;align-items:center;margin-top:14px;flex-wrap:wrap">
      <input name="smtptestto" [(ngModel)]="smtpTestTo" placeholder="test na adres… (domyślnie nadawca)" style="max-width:280px" />
      <button class="btn ghost sm" (click)="sendTest()" [disabled]="testing">Wyślij test</button>
      @if (testMsg) { <span class="ok-inline">{{ testMsg }}</span> }
    </div>
    @if (smtpError) { <p class="warn" style="background:#FEECEC;color:#B4232A">{{ smtpError }}</p> }
    <p class="note" style="margin-top:12px">🔒 Hasło szyfrowane, nigdy nie pokazywane z powrotem. Zapisz przed wysłaniem testu.</p>
    <p class="warn" style="margin-top:10px">⚠ Na hostingu Render porty SMTP (25/465/587) są zablokowane — SMTP zadziała lokalnie, ale nie na produkcji. Na produkcji użyj kanału <b>HTTP API</b> (Resend/Brevo) ustawianego w zmiennych środowiskowych usługi: <code>EMAIL__HTTP__PROVIDER</code>, <code>EMAIL__HTTP__APIKEY</code>, <code>EMAIL__HTTP__FROMEMAIL</code>. Aktywny kanał widać w kafelku „Status i sekrety".</p>
  </app-config-modal>

  <!-- ===== Logowanie i captcha (zakładki) ===== -->
  <app-config-modal [open]="panel === 'integrations'" title="Logowanie i captcha"
      [tabs]="['Google','Captcha']" [active]="tab" (tabChange)="tab = $event"
      [saving]="savingI" [saved]="savedI" (save)="saveIntegrations()" (close)="close()">
    @if (tab === 'Google') {
      <label class="lbl">Google OAuth — Client ID (Web)</label>
      <input type="text" name="gid" [(ngModel)]="integrations.googleClientId" placeholder="123456789-abc.apps.googleusercontent.com" />
      <p class="muted" style="font-size:12px;margin-top:6px">Jawny identyfikator klienta typu „Aplikacja internetowa". Puste = logowanie Google wyłączone.</p>
    }
    @if (tab === 'Captcha') {
      <p class="muted" style="font-size:13px;margin:0 0 10px">Chroni publiczne formularze (rejestracja, uwagi). Rekomendacja: <b>Cloudflare Turnstile</b>. Puste = wyłączona.</p>
      <div class="grid2">
        <div>
          <label class="lbl">Dostawca</label>
          <select name="capprov" [(ngModel)]="integrations.captchaProvider">
            <option value="">— wyłączona —</option>
            <option value="turnstile">Cloudflare Turnstile</option>
          </select>
        </div>
        <div><label class="lbl">Site key (jawny)</label><input type="text" name="capsite" [(ngModel)]="integrations.captchaSiteKey" placeholder="0x4AAAA..." /></div>
      </div>
      <label class="lbl">Secret key @if (integrations.hasCaptchaSecret) { <span style="color:#128040;font-weight:600">• ustawiony</span> }</label>
      <input type="password" name="capsecret" [(ngModel)]="captchaSecret" [placeholder]="integrations.hasCaptchaSecret ? '•••••••• (bez zmian)' : 'wklej secret key'" />
      <p class="note" style="margin-top:12px">🔒 Secret szyfrowany, nigdy nie pokazywany z powrotem.</p>
    }
    @if (integError) { <p class="warn" style="background:#FEECEC;color:#B4232A">{{ integError }}</p> }
  </app-config-modal>

  <!-- ===== Aplikacja klienta (web) i kody QR ===== -->
  <app-config-modal [open]="panel === 'customerapp'" title="Aplikacja klienta i kody QR"
      [saving]="savingI" [saved]="savedI" (save)="saveIntegrations()" (close)="close()">
    <p class="muted" style="margin-top:0">Adres, pod którym działa aplikacja webowa dla klientów (bez instalacji ze sklepu). Z niego powstają stałe linki
      do sklepów i kody QR: <code>{{ (integrations.customerAppUrl || status?.customerAppUrl || 'https://adres-aplikacji') }}/s/&lt;identyfikator-sklepu&gt;?src=qr</code>.</p>
    <label class="lbl">Adres aplikacji klienta (HTTPS)</label>
    <input type="url" name="custapp" [(ngModel)]="integrations.customerAppUrl" placeholder="https://app.dowózka.pl" />
    <p class="muted" style="font-size:12px;margin-top:6px">Tylko HTTPS (HTTP wyłącznie dla localhost) i <b>katalog główny (sub)domeny</b> — bez ścieżki,
      „?" i „#" (aplikacja jest budowana dla „/"). Domena z polskimi znakami zapisze się w punycode. Puste = użyj wartości z serwera
      (zmienna <code>PUBLICAPP__CUSTOMERAPPURL</code>)@if (status?.customerAppUrlEnv) { — teraz: <code>{{ status?.customerAppUrlEnv }}</code> }.</p>
    @if (status?.customerAppUrlSource === 'env') {
      <p class="note">Aktywny adres pochodzi ze zmiennej środowiskowej serwera.</p>
    }
    <p class="warn" style="margin-top:10px">⚠ Zmiana adresu po wydrukowaniu kodów QR unieważnia wydrukowane kody — ustaw go raz, na docelowej domenie,
      zanim wydrukujesz materiały. Zapis w panelu trzymany jest w pliku serwera; na hostingu bez trwałego dysku ustaw ten sam adres także w zmiennej
      <code>PUBLICAPP__CUSTOMERAPPURL</code> (przetrwa restart).</p>
    @if (integError) { <p class="warn" style="background:#FEECEC;color:#B4232A">{{ integError }}</p> }
  </app-config-modal>

  <!-- ===== Platforma ===== -->
  <app-config-modal [open]="panel === 'platform'" title="Ustawienia platformy"
      [saving]="savingP" [saved]="savedP" (save)="savePlatform()" (close)="close()">
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
      <div><label class="lbl">Domyślna prowizja (%)</label><input type="number" min="0" max="100" step="0.5" name="comm" [(ngModel)]="commissionPercent" /></div>
      <div><label class="lbl">Waluta</label><input type="text" maxlength="3" name="cur" [(ngModel)]="platform.currency" /></div>
    </div>
    <div class="grid2">
      <div><label class="lbl">Operator (nazwa)</label><input type="text" name="opn" [(ngModel)]="platform.operatorName" /></div>
      <div><label class="lbl">Kontakt operatora</label><input type="text" name="opc" [(ngModel)]="platform.operatorContact" /></div>
    </div>
  </app-config-modal>

  <!-- ===== Rundy zakupowe ===== -->
  <app-config-modal [open]="panel === 'rounds'" title="Rundy zakupowe"
      [saving]="savingR" [saved]="savedR" (save)="saveSchedule()" (close)="close()">
    <p class="muted" style="margin-top:0">Godziny, o których sklep robi zakupy i kompletuje zamówienia. Zamówienie trafia do najbliższej rundy, której termin graniczny jeszcze nie minął. To <b>nie</b> są fale ani terminy dostaw — okna dostaw ustawia się w terminach sklepu.</p>
    @if (schedule) {
      <label class="lbl">Rundy (czas lokalny: {{ schedule.timeZoneId }})</label>
      <div class="waves">
        @for (r of schedule.rounds; track $index) {
          <div class="wave">
            <input type="time" [(ngModel)]="r.localTime" [name]="'round'+$index" [id]="'round'+$index" aria-label="Godzina rundy" />
            <label class="inline-lbl" [for]="'cutoff'+$index">zamówienia do</label>
            <input type="number" min="0" max="720" class="num" [(ngModel)]="r.cutoffMinutes" [name]="'cutoff'+$index" [id]="'cutoff'+$index" />
            <span class="inline-lbl">min przed</span>
            <button class="btn ghost sm" (click)="removeRound($index)" [disabled]="schedule.rounds.length <= 1">Usuń</button>
          </div>
        }
        <button class="btn ghost sm" (click)="addRound()">+ Dodaj rundę</button>
      </div>
      <label class="lbl">Dni z rundami</label>
      <div class="days">
        @for (d of weekDays; track d.key) {
          <label class="day" [class.on]="isDay(d.key)">
            <input type="checkbox" [checked]="isDay(d.key)" (change)="toggleDay(d.key)" [name]="'day'+d.key" [id]="'day'+d.key" /> {{ d.label }}
          </label>
        }
      </div>
      <label class="lbl" for="leadmin">Dostawa najwcześniej (minut po starcie rundy)</label>
      <input type="number" min="0" max="1440" id="leadmin" name="leadmin" class="num" [(ngModel)]="schedule.minDeliveryLeadMinutes" />
      <p class="note" style="margin-top:12px">Zmiana czasu (letni/zimowy) jest uwzględniana automatycznie. Wartości startowe — 12:00 i 16:00, zamówienia do 30 min przed, pon–sob, dostawa od 60 min po rundzie — do potwierdzenia ze sklepem.</p>
    } @else {
      <p class="muted">Wczytywanie harmonogramu…</p>
    }
    @if (roundsError) { <p class="warn" style="background:#FEECEC;color:#B4232A">{{ roundsError }}</p> }
  </app-config-modal>

  <!-- ===== Dane demo ===== -->
  <app-config-modal [open]="panel === 'demo'" title="Dane demo (pilotaż)" [showFooter]="false" (close)="close()">
    <p class="muted" style="margin-top:0">Tworzy sklepy <b>Rapacz</b> i <b>Lewiatan</b> z logo, produktami (zdjęcia), strefą i dwoma terminami dostawy na jutro (13–15, 17–19 — po rundach zakupowych). Idempotentne (nie duplikuje). Dane do podmiany przez sklep.</p>
    <div style="display:flex;gap:10px;align-items:center;margin-top:10px">
      <button class="btn primary sm" (click)="seedDemo()" [disabled]="seeding">Zasiej sklepy demo</button>
      @if (seedMsg) { <span class="ok-inline">{{ seedMsg }}</span> }
    </div>
    @if (seedError) { <p class="warn">{{ seedError }}</p> }
  </app-config-modal>

  <!-- ===== Status i sekrety ===== -->
  <app-config-modal [open]="panel === 'status'" title="Status i sekrety" [showFooter]="false" (close)="close()">
    @if (status) {
      <div class="cfg">
        <div class="row"><span>Środowisko</span><b>{{ status.environment }}</b></div>
        <div class="row"><span>Płatności</span><b>{{ status.payments.provider }}</b></div>
        <div class="row"><span>Logowanie Google</span><b [style.color]="col(status.googleSignIn)">{{ txt(status.googleSignIn) }}</b></div>
        <div class="row"><span>E-mail</span><b [style.color]="col(status.email)">{{ status.email ? emailChannelLabel(status.emailChannel) : '— brak' }}</b></div>
        <div class="row"><span>RabbitMQ</span><b [style.color]="col(status.rabbitMq)">{{ txt(status.rabbitMq) }}</b></div>
        <div class="row"><span>Admin (seed)</span><b>{{ status.adminSeedEmail }}</b></div>
        <div class="row"><span>Link resetu hasła</span>
          <b [style.color]="col(!status.identityPublicUrlProblem)">{{ status.identityPublicUrl }}/reset-password</b></div>
      </div>
      @if (!status.email) {
        <p class="warn">⚠ Poczta to <b>atrapa</b> — e-maile (link resetu hasła, weryfikacja adresu) NIE są wysyłane, a użytkownicy
          widzą o tym ostrzeżenie. Ustaw dostawcę HTTP (<code>EMAIL__HTTP__APIKEY</code>) albo SMTP w „Poczta".</p>
      }
      @if (status.identityPublicUrlProblem) {
        <p class="warn">⚠ {{ status.identityPublicUrlProblem }} Ustaw <code>IDENTITY__PUBLICURL</code> na adres panelu po HTTPS
          (np. <code>https://panel.dowozka.pl</code>) — tam działa wspólna strona resetu dla wszystkich kont.</p>
      }
      @if (status.emailQueue?.failed) {
        <p class="warn">⚠ Nieudane próby wysyłki e-maili z linkami od startu API: {{ status.emailQueue?.failed }} (ostatnia: {{ status.emailQueue?.lastFailureAtUtc | date:'short' }},
          {{ status.emailQueue?.lastFailureCategory }}). Zadania nie przepadają — czekają na ponowienie (zakładka „Zdarzenia"). Sprawdź konfigurację poczty — „Wyślij test".</p>
      }
      @if (status.jwtUsingDevSecret) {
        <p class="warn">⚠ Klucz JWT to wartość deweloperska — ustaw własny <code>JWT__SIGNINGKEY</code> (min. 32 znaki) przed produkcją.</p>
      }
    }
    <p class="note" style="margin-top:14px">Sekrety infrastruktury (baza, JWT, hasło admina) trzymane w zmiennych środowiskowych usługi (Render): <code>CONNECTIONSTRINGS__POSTGRES</code>, <code>JWT__SIGNINGKEY</code>, <code>SEED__ADMINPASSWORD</code>.</p>
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
    .cfg { display:flex; flex-direction:column; gap:2px; }
    .cfg .row { display:flex; justify-content:space-between; align-items:center; gap:16px; padding:8px 0; border-bottom:1px solid #eef0f3; }
    .cfg .row:last-child { border-bottom:0; }
    .cfg .row span { color:#6B7280; }
    .warn { margin-top:12px; background:#FFF3EA; color:#EA6A0C; padding:10px 12px; border-radius:8px; font-size:13px; }
    .note { background:#F1F3F5; color:#6B7280; padding:10px 12px; border-radius:8px; font-size:13px; }
    select { border:1px solid #E5E7EB; border-radius:8px; padding:9px 11px; font-size:14px; width:100%; box-sizing:border-box; background:#fff; }
    code { background:#F1F3F5; padding:1px 6px; border-radius:5px; font-size:12px; }
    .lbl { display:block; font-size:13px; font-weight:600; color:#3A3F4B; margin:14px 0 6px; }
    input { border:1px solid #E5E7EB; border-radius:8px; padding:9px 11px; font-size:14px; width:100%; box-sizing:border-box; }
    input:focus { outline:none; border-color:#14B9BA; }
    .waves { display:flex; flex-direction:column; gap:8px; align-items:flex-start; }
    .wave { display:flex; gap:8px; align-items:center; }
    .wave input { width:130px; }
    .wave input.num, input.num { width:90px; }
    .wave { flex-wrap:wrap; }
    .inline-lbl { font-size:13px; color:#6B7280; }
    .days { display:flex; gap:6px; flex-wrap:wrap; }
    .day { display:flex; align-items:center; gap:4px; border:1px solid #E5E7EB; border-radius:8px; padding:6px 10px; font-size:13px; cursor:pointer; }
    .day.on { border-color:#14B9BA; background:#E7F7F7; color:#0C7D7E; font-weight:600; }
    .day input { width:auto; }
    .grid2 { display:grid; grid-template-columns:1fr 1fr; gap:16px; }
    @media (max-width:560px){ .grid2 { grid-template-columns:1fr; } }
    .ok-inline { color:#128040; font-size:13px; font-weight:600; }
  `],
})
export class SettingsComponent implements OnInit {
  private api = inject(Api);
  panel: string | null = null;
  tab = 'Google';

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

  readonly weekDays = WEEK_DAYS;
  schedule: PurchasingSchedule | null = null;
  savingR = false;
  savedR = false;
  roundsError = '';

  get roundsSummary(): string {
    const s = this.schedule;
    if (!s || !s.rounds.length) return '—';
    const cut = new Set(s.rounds.map(r => r.cutoffMinutes));
    const cutTxt = cut.size === 1 ? `, zamówienia do ${s.rounds[0].cutoffMinutes} min przed` : '';
    return s.rounds.map(r => r.localTime).join(' · ') + cutTxt;
  }

  get smtpConfigured(): boolean {
    return !!(this.integrations.smtpHost && this.integrations.hasSmtpPassword && this.integrations.smtpFromEmail);
  }

  open(p: string) { if (p === 'integrations') this.tab = 'Google'; this.panel = p; }
  close() { this.panel = null; }

  /// Po zmianie hasła backend unieważnia sesje — wylogowujemy i wracamy do logowania.
  onPasswordChanged() { this.panel = null; this.api.logout(); }

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
      next: i => { this.integrations = this.mapIntegrations(i); this.captchaSecret = ''; this.smtpPassword = ''; },
      error: () => {},
    });
    this.api.get<PurchasingSchedule>('/ordering/admin/purchasing-schedule').subscribe({
      next: s => this.applySchedule(s),
      error: () => {},
    });
  }

  private applySchedule(s: PurchasingSchedule) {
    this.schedule = { ...s, rounds: (s.rounds ?? []).map(r => ({ ...r })), activeDays: [...(s.activeDays ?? [])] };
  }

  isDay(key: string) { return !!this.schedule?.activeDays.includes(key); }
  toggleDay(key: string) {
    if (!this.schedule) return;
    const d = this.schedule.activeDays;
    this.schedule.activeDays = d.includes(key) ? d.filter(x => x !== key) : [...d, key];
  }
  addRound() { this.schedule?.rounds.push({ localTime: '18:00', cutoffMinutes: 30 }); }
  removeRound(i: number) { this.schedule?.rounds.splice(i, 1); }

  saveSchedule() {
    if (!this.schedule) return;
    this.savingR = true; this.savedR = false; this.roundsError = '';
    const body: PurchasingSchedule = {
      ...this.schedule,
      rounds: this.schedule.rounds.map(r => ({ localTime: r.localTime, cutoffMinutes: Number(r.cutoffMinutes) })),
      minDeliveryLeadMinutes: Number(this.schedule.minDeliveryLeadMinutes),
    };
    this.api.put<PurchasingSchedule>('/ordering/admin/purchasing-schedule', body).subscribe({
      next: s => { this.applySchedule(s); this.savingR = false; this.savedR = true; setTimeout(() => this.savedR = false, 2500); },
      error: e => { this.savingR = false; this.roundsError = e?.error?.detail ?? 'Nie udało się zapisać harmonogramu.'; },
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
      customerAppUrl: i.customerAppUrl || '',
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
      customerAppUrl: (this.integrations.customerAppUrl || '').trim() || undefined,
    };
    if (this.captchaSecret.trim()) body.captchaSecret = this.captchaSecret.trim();
    if (this.smtpPassword.trim()) body.smtpPassword = this.smtpPassword.trim();
    this.api.put<PlatformIntegrations>('/admin/config/integrations', body).subscribe({
      next: i => {
        this.integrations = this.mapIntegrations(i); this.captchaSecret = ''; this.smtpPassword = '';
        this.savingI = false; this.savedI = true; setTimeout(() => this.savedI = false, 2500);
        this.load();
      },
      error: e => { this.savingI = false; this.integError = e?.error?.detail ?? 'Nie udało się zapisać.'; },
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

  emailChannelLabel(ch?: string): string {
    if (!ch || ch === 'none') return '— brak';
    if (ch === 'smtp') return '✓ SMTP';
    if (ch.startsWith('http:')) return '✓ HTTP API (' + ch.substring(5) + ')';
    return '✓ ' + ch;
  }
}
