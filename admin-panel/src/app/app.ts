import { Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, StoreDto } from './api';
import { PANEL_MODULES, PanelTab } from './modules';
import { OrdersComponent } from './orders';
import { CatalogComponent } from './catalog';
import { DashboardComponent } from './dashboard';
import { StoresComponent } from './stores';
import { TeamComponent } from './team';
import { FinanceComponent } from './finance';
import { DeliveriesComponent } from './deliveries';
import { SettingsComponent } from './settings';
import { IntegrationsComponent } from './integrations';
import { OnboardingComponent } from './onboarding';
import { FeedbackComponent } from './feedback';

@Component({
  selector: 'app-root',
  imports: [CommonModule, FormsModule, OrdersComponent, CatalogComponent, DashboardComponent, StoresComponent, TeamComponent, FinanceComponent, DeliveriesComponent, SettingsComponent, IntegrationsComponent, OnboardingComponent, FeedbackComponent],
  template: `
  @if (!api.isLoggedIn()) {
    <div class="auth">
      <div class="auth-brand">
        <div class="logo">
          <svg viewBox="0 0 128 128" width="34" height="34" fill="none" stroke="#fff" stroke-width="9" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-7px;margin-right:6px">
            <path d="M18 30 H32 L46 86 H98"/><path d="M36 46 H108 L98 86"/>
            <circle cx="56" cy="103" r="7.5"/><circle cx="92" cy="103" r="7.5"/>
            <path d="M50 59 H86 V50 L112 65 L86 80 V71 H50 Z" fill="#fff" stroke="none"/>
          </svg>Dowózka<span class="zap">.pl</span></div>
        <h1>Zakupy z lokalnych sklepów z dostawą</h1>
        <p class="lead">Panel sprzedawcy do zarządzania zamówieniami, ofertą i dostawami — z jednego miejsca.</p>
        <ul class="points">
          <li><span class="dot">✓</span> Zamówienia i statusy w czasie rzeczywistym</li>
          <li><span class="dot">✓</span> Zarządzanie ofertą sklepu</li>
          <li><span class="dot">✓</span> Prowizje i dostawy pod kontrolą</li>
        </ul>
        <div class="mock"></div>
      </div>

      <div class="auth-form">
        @switch (authMode) {
          @case ('login') {
            <form class="auth-card" (ngSubmit)="login()">
              <h2>Zaloguj się do panelu</h2>
              <div class="field"><label>Login (e-mail)</label><input name="email" [(ngModel)]="email" type="email" required /></div>
              <div class="field"><label>Hasło</label><input name="password" [(ngModel)]="password" type="password" required /></div>
              <button class="btn-lg" type="submit" [disabled]="loading">Zaloguj się</button>
              @if (error) { <p class="error">{{ error }}</p> }
              <p class="hint">Domyślny admin (dev): admin&#64;zipzap.local / Admin123!</p>
              <p class="hint">Prowadzisz sklep? <a [style]="linkStyle" (click)="setMode('store')">Załóż sklep</a> · Chcesz dostarczać? <a [style]="linkStyle" (click)="setMode('driver')">Zostań dostawcą</a></p>
            </form>
          }
          @case ('store') {
            <form class="auth-card" (ngSubmit)="registerStore()">
              <h2>Załóż sklep</h2>
              <p class="hint">Konto administratora sklepu — od razu przejdziesz do konfiguracji oferty i dostaw.</p>
              <div class="field"><label>Imię i nazwisko</label><input name="fullName" [(ngModel)]="fullName" required /></div>
              <div class="field"><label>E-mail</label><input name="email" [(ngModel)]="email" type="email" required /></div>
              <div class="field"><label>Telefon (opcjonalnie)</label><input name="phone" [(ngModel)]="phone" /></div>
              <div class="field"><label>Hasło (min. 6 znaków)</label><input name="password" [(ngModel)]="password" type="password" required /></div>
              <div class="field"><label>Nazwa sklepu</label><input name="storeName" [(ngModel)]="storeName" required /></div>
              <div class="field"><label>Miasto</label><input name="city" [(ngModel)]="city" required /></div>
              <button class="btn-lg" type="submit" [disabled]="loading">Załóż sklep i zacznij</button>
              @if (error) { <p class="error">{{ error }}</p> }
              <p class="hint">Masz już konto? <a [style]="linkStyle" (click)="setMode('login')">Zaloguj się</a></p>
            </form>
          }
          @case ('driver') {
            @if (driverDone) {
              <div class="auth-card">
                <h2>Dziękujemy! 🎉</h2>
                <p>{{ driverMsg }}</p>
                <button class="btn-lg" (click)="setMode('login')">Wróć do logowania</button>
              </div>
            } @else {
              <form class="auth-card" (ngSubmit)="registerDriver()">
                <h2>Zostań dostawcą</h2>
                <p class="hint">Konto zostanie aktywowane po weryfikacji przez administratora serwisu.</p>
                <div class="field"><label>Imię i nazwisko</label><input name="fullName" [(ngModel)]="fullName" required /></div>
                <div class="field"><label>E-mail</label><input name="email" [(ngModel)]="email" type="email" required /></div>
                <div class="field"><label>Telefon (opcjonalnie)</label><input name="phone" [(ngModel)]="phone" /></div>
                <div class="field"><label>Hasło (min. 6 znaków)</label><input name="password" [(ngModel)]="password" type="password" required /></div>
                <button class="btn-lg" type="submit" [disabled]="loading">Wyślij zgłoszenie</button>
                @if (error) { <p class="error">{{ error }}</p> }
                <p class="hint">Masz już konto? <a [style]="linkStyle" (click)="setMode('login')">Zaloguj się</a></p>
              </form>
            }
          }
        }
      </div>
    </div>
  } @else {
    <div class="app-shell">
      <aside class="rail">
        <div class="rail-logo">
          <svg viewBox="0 0 128 128" width="24" height="24" fill="none" stroke="#fff" stroke-width="10" stroke-linecap="round" stroke-linejoin="round">
            <path d="M18 30 H32 L46 86 H98"/><path d="M36 46 H108 L98 86"/>
            <circle cx="56" cy="103" r="7.5"/><circle cx="92" cy="103" r="7.5"/>
            <path d="M50 59 H86 V50 L112 65 L86 80 V71 H50 Z" fill="#fff" stroke="none"/>
          </svg>
        </div>
        @for (m of visibleModules(); track m.id) {
          <button class="rail-btn" [class.active]="tab===m.id" (click)="tab=m.id">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              @switch (m.icon) {
                @case ('start') { <path d="M9 11l3 3L22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/> }
                @case ('dashboard') { <rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/> }
                @case ('orders') { <path d="M6 2h9l5 5v13a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2z"/><path d="M14 2v6h6M8 13h8M8 17h6"/> }
                @case ('deliveries') { <rect x="1" y="3" width="15" height="13" rx="1"/><path d="M16 8h4l3 3v5h-7V8z"/><circle cx="5.5" cy="18.5" r="2.5"/><circle cx="18.5" cy="18.5" r="2.5"/> }
                @case ('catalog') { <path d="M20 7l-8-4-8 4 8 4 8-4z"/><path d="M4 7v10l8 4 8-4V7"/><path d="M12 11v10"/> }
                @case ('integrations') { <path d="M9 7H6a3 3 0 0 0 0 6h3M15 7h3a3 3 0 0 1 0 6h-3M8 10h8"/> }
                @case ('finance') { <rect x="2" y="5" width="20" height="14" rx="2"/><path d="M2 10h20M6 15h4"/> }
                @case ('team') { <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/> }
                @case ('stores') { <path d="M3 9l1.5-5h15L21 9M4 9h16v10a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V9zM4 9a2.5 2.5 0 0 0 5 0 2.5 2.5 0 0 0 5 0 2.5 2.5 0 0 0 5 0"/> }
                @case ('feedback') { <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/> }
                @case ('settings') { <circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09a1.65 1.65 0 0 0-1-1.51 1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09a1.65 1.65 0 0 0 1.51-1 1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9c.2.61.76 1.05 1.42 1.09H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/> }
              }
            </svg>
            <span class="tip">{{ m.label }}</span>
          </button>
        }
        <div class="spacer"></div>
        <button class="rail-btn" (click)="logout()">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><path d="M16 17l5-5-5-5M21 12H9"/></svg>
          <span class="tip">Wyloguj</span>
        </button>
      </aside>

      <div class="main">
        <div class="topbar">
          <div class="search">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="7"/><path d="M21 21l-4-4"/></svg>
            <input placeholder="Szukaj zamówień (numer, klient)…" [(ngModel)]="search" />
          </div>
          <div class="store-select">
            <select [(ngModel)]="selectedStoreId">
              @for (s of stores; track s.id) { <option [value]="s.id">{{ s.name }} — {{ s.city }}</option> }
            </select>
            @if (api.isAdmin() || api.storeIds().length > 0) {
              <button class="btn ghost sm" style="margin-left:8px" [disabled]="addingLocation" (click)="addLocation()"
                title="Dodaj kolejną lokalizację swojego sklepu">+ Lokalizacja</button>
            }
          </div>
          <div class="userchip">
            <div class="avatar">{{ initials }}</div>
            <div class="who"><b>{{ api.roleLabel() }}</b><span>{{ api.userEmail() }}</span></div>
          </div>
        </div>

        <div class="content">
          @if (isDriverOnly) {
            <div class="card pad"><p class="muted">Panel <strong>dostawcy</strong> jest w przygotowaniu — wkrótce zobaczysz tu swoje dostawy, dostępność (online/offline) i zarobki.</p></div>
          } @else if (stores.length === 0 && (currentModule?.storeScoped ?? false)) {
            <div class="card pad"><p class="muted">Brak sklepów. Przejdź do zakładki <strong>Sklepy</strong>, aby utworzyć pierwszy.</p></div>
          } @else {
            @switch (tab) {
              @case ('onboarding') { <app-onboarding [storeId]="selectedStoreId" /> }
              @case ('dashboard') { <app-dashboard [storeId]="selectedStoreId" /> }
              @case ('orders') { <app-orders [storeId]="selectedStoreId" [query]="search" /> }
              @case ('deliveries') { <app-deliveries [storeId]="selectedStoreId" /> }
              @case ('catalog') { <app-catalog [storeId]="selectedStoreId" /> }
              @case ('integrations') { <app-integrations [storeId]="selectedStoreId" /> }
              @case ('team') { <app-team [storeId]="selectedStoreId" /> }
              @case ('finance') { <app-finance [storeId]="selectedStoreId" /> }
              @case ('stores') { <app-stores (changed)="loadStores()" /> }
              @case ('feedback') { <app-feedback /> }
              @case ('settings') { <app-settings /> }
            }
          }
        </div>
      </div>
    </div>
  }
  `,
})
export class App {
  protected api = inject(Api);

  email = 'admin@zipzap.local';
  password = 'Admin123!';
  error = '';
  loading = false;
  search = '';

  // Rejestracja per-kanał (web): logowanie / „Załóż sklep" / „Zostań dostawcą".
  authMode: 'login' | 'store' | 'driver' = 'login';
  fullName = '';
  phone = '';
  storeName = '';
  city = '';
  driverDone = false;
  driverMsg = '';
  readonly linkStyle = 'color:#14b9ba;cursor:pointer;font-weight:600';

  stores: StoreDto[] = [];
  selectedStoreId = '';
  addingLocation = false;
  tab: PanelTab = 'onboarding';

  /// Moduły widoczne dla ról zalogowanego usera (nawigacja generowana z rejestru).
  readonly visibleModules = computed(() => PANEL_MODULES.filter(m => this.api.hasAnyRole(m.roles)));

  get currentModule() { return PANEL_MODULES.find(m => m.id === this.tab); }

  /// Dostawca bez roli admina/sklepu — pełny interfejs kierowcy dopiero w R8 (placeholder).
  get isDriverOnly() { return this.api.isDriver() && !this.api.isAdmin() && !this.api.isStoreAdmin(); }

  get initials(): string {
    const e = this.api.userEmail();
    return e ? e.substring(0, 2).toUpperCase() : 'ZZ';
  }

  login() {
    this.loading = true;
    this.error = '';
    this.api.login(this.email, this.password).subscribe({
      next: () => { this.loading = false; this.loadStores(); },
      error: () => { this.error = 'Nieprawidłowy e-mail lub hasło.'; this.loading = false; },
    });
  }

  setMode(m: 'login' | 'store' | 'driver') {
    this.authMode = m;
    this.error = '';
    this.driverDone = false;
    this.fullName = ''; this.phone = ''; this.storeName = ''; this.city = '';
    if (m === 'login') { this.email = 'admin@zipzap.local'; this.password = 'Admin123!'; }
    else { this.email = ''; this.password = ''; }
  }

  registerStore() {
    if (!this.email || !this.password || !this.fullName || !this.storeName || !this.city) {
      this.error = 'Uzupełnij wszystkie wymagane pola.';
      return;
    }
    this.loading = true; this.error = '';
    this.api.registerStore({
      email: this.email.trim(), password: this.password, fullName: this.fullName.trim(),
      phone: this.phone.trim() || undefined, storeName: this.storeName.trim(), city: this.city.trim(),
    }).subscribe({
      next: () => { this.loading = false; this.loadStores(); },
      error: e => { this.loading = false; this.error = e?.error?.detail ?? 'Nie udało się założyć sklepu.'; },
    });
  }

  registerDriver() {
    if (!this.email || !this.password || !this.fullName) {
      this.error = 'Uzupełnij imię i nazwisko, e-mail i hasło.';
      return;
    }
    this.loading = true; this.error = '';
    this.api.registerDriver({
      email: this.email.trim(), password: this.password, fullName: this.fullName.trim(),
      phone: this.phone.trim() || undefined,
    }).subscribe({
      next: r => { this.loading = false; this.driverDone = true; this.driverMsg = r.message; },
      error: e => { this.loading = false; this.error = e?.error?.detail ?? 'Nie udało się wysłać zgłoszenia.'; },
    });
  }

  loadStores() {
    // Dostawca nie zarządza katalogiem sklepów — nie pobiera listy do przełącznika.
    if (this.api.isDriver() && !this.api.isAdmin() && !this.api.isStoreAdmin()) { this.ensureVisibleTab(); return; }
    this.api.getPublic<StoreDto[]>('/catalog/stores?onlyActive=false').subscribe(all => {
      // Administrator sklepu widzi tylko swoje sklepy; administrator serwisu — wszystkie.
      const s = this.api.isAdmin() ? all : all.filter(x => this.api.storeIds().includes(x.id));
      this.stores = s;
      const stillThere = s.some(x => x.id === this.selectedStoreId);
      if (!stillThere && s.length) { this.selectedStoreId = s[0].id; }
      this.ensureVisibleTab();
    });
  }

  /// Jeśli bieżąca zakładka nie jest dostępna dla roli — przełącz na pierwszy widoczny moduł.
  private ensureVisibleTab() {
    const visible = this.visibleModules();
    if (!visible.some(m => m.id === this.tab)) this.tab = visible[0]?.id ?? 'dashboard';
  }

  // Multi-lokalizacja: właściciel dodaje kolejną lokalizację (nowy sklep przypisany do siebie).
  addLocation() {
    const name = prompt('Nazwa nowej lokalizacji (np. „Rapacz — Rynek 5")');
    if (name === null || !name.trim()) return;
    const city = prompt('Miasto lokalizacji');
    if (city === null || !city.trim()) return;
    this.addingLocation = true;
    this.api.post<StoreDto>('/merchant/stores', {
      name: name.trim(), city: city.trim(), commissionRate: 0.10, minimumOrderValue: 0,
    }).subscribe({
      next: created => {
        // Token nie zawiera jeszcze nowego store_id — odśwież, potem pokaż i wybierz lokalizację.
        this.api.refresh().subscribe({
          next: () => { this.addingLocation = false; this.loadStores(); this.selectedStoreId = created.id; },
          error: () => { this.addingLocation = false; this.loadStores(); },
        });
      },
      error: e => { this.addingLocation = false; alert('Nie udało się dodać lokalizacji: ' + (e?.error?.detail ?? 'błąd')); },
    });
  }

  logout() {
    this.api.logout();
    this.stores = [];
    this.selectedStoreId = '';
    this.tab = 'onboarding';
  }
}
