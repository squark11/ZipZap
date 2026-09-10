import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, StoreDto } from './api';
import { OrdersComponent } from './orders';
import { CatalogComponent } from './catalog';
import { DashboardComponent } from './dashboard';
import { StoresComponent } from './stores';
import { TeamComponent } from './team';
import { FinanceComponent } from './finance';
import { DeliveriesComponent } from './deliveries';
import { SettingsComponent } from './settings';
import { IntegrationsComponent } from './integrations';

@Component({
  selector: 'app-root',
  imports: [CommonModule, FormsModule, OrdersComponent, CatalogComponent, DashboardComponent, StoresComponent, TeamComponent, FinanceComponent, DeliveriesComponent, SettingsComponent, IntegrationsComponent],
  template: `
  @if (!api.isLoggedIn()) {
    <div class="auth">
      <div class="auth-brand">
        <div class="logo">Zip<span class="zap">Zap</span></div>
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
        <form class="auth-card" (ngSubmit)="login()">
          <h2>Zaloguj się do panelu</h2>
          <div class="field">
            <label>Login (e-mail)</label>
            <input name="email" [(ngModel)]="email" type="email" required />
          </div>
          <div class="field">
            <label>Hasło</label>
            <input name="password" [(ngModel)]="password" type="password" required />
          </div>
          <button class="btn-lg" type="submit" [disabled]="loading">Zaloguj się</button>
          @if (error) { <p class="error">{{ error }}</p> }
          <p class="hint">Domyślny admin (dev): admin&#64;zipzap.local / Admin123!</p>
        </form>
      </div>
    </div>
  } @else {
    <div class="app-shell">
      <aside class="rail">
        <div class="rail-logo">Z</div>
        <button class="rail-btn" [class.active]="tab==='dashboard'" (click)="tab='dashboard'">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/></svg>
          <span class="tip">Pulpit</span>
        </button>
        <button class="rail-btn" [class.active]="tab==='orders'" (click)="tab='orders'">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 2h9l5 5v13a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2z"/><path d="M14 2v6h6M8 13h8M8 17h6"/></svg>
          <span class="tip">Zamówienia</span>
        </button>
        <button class="rail-btn" [class.active]="tab==='deliveries'" (click)="tab='deliveries'">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="1" y="3" width="15" height="13" rx="1"/><path d="M16 8h4l3 3v5h-7V8z"/><circle cx="5.5" cy="18.5" r="2.5"/><circle cx="18.5" cy="18.5" r="2.5"/></svg>
          <span class="tip">Dostawy</span>
        </button>
        <button class="rail-btn" [class.active]="tab==='catalog'" (click)="tab='catalog'">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M20 7l-8-4-8 4 8 4 8-4z"/><path d="M4 7v10l8 4 8-4V7"/><path d="M12 11v10"/></svg>
          <span class="tip">Oferta</span>
        </button>
        <button class="rail-btn" [class.active]="tab==='integrations'" (click)="tab='integrations'">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M9 7H6a3 3 0 0 0 0 6h3M15 7h3a3 3 0 0 1 0 6h-3M8 10h8"/></svg>
          <span class="tip">Integracje</span>
        </button>
        @if (api.isAdmin()) {
        <button class="rail-btn" [class.active]="tab==='team'" (click)="tab='team'">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/></svg>
          <span class="tip">Zespół</span>
        </button>
        }
        <button class="rail-btn" [class.active]="tab==='finance'" (click)="tab='finance'">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="5" width="20" height="14" rx="2"/><path d="M2 10h20M6 15h4"/></svg>
          <span class="tip">Rozliczenia</span>
        </button>
        @if (api.isAdmin()) {
        <button class="rail-btn" [class.active]="tab==='stores'" (click)="tab='stores'">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 9l1.5-5h15L21 9M4 9h16v10a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V9zM4 9a2.5 2.5 0 0 0 5 0 2.5 2.5 0 0 0 5 0 2.5 2.5 0 0 0 5 0"/></svg>
          <span class="tip">Sklepy</span>
        </button>
        }
        @if (api.isAdmin()) {
        <button class="rail-btn" [class.active]="tab==='settings'" (click)="tab='settings'">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09a1.65 1.65 0 0 0-1-1.51 1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09a1.65 1.65 0 0 0 1.51-1 1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9c.2.61.76 1.05 1.42 1.09H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>
          <span class="tip">Konfiguracja</span>
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
          </div>
          <div class="userchip">
            <div class="avatar">{{ initials }}</div>
            <div class="who"><b>{{ api.isAdmin() ? 'Administrator' : 'Pracownik sklepu' }}</b><span>{{ api.userEmail() }}</span></div>
          </div>
        </div>

        <div class="content">
          @if (stores.length === 0 && tab !== 'stores' && tab !== 'settings') {
            <div class="card pad"><p class="muted">Brak sklepów. Przejdź do zakładki <strong>Sklepy</strong>, aby utworzyć pierwszy.</p></div>
          } @else {
            @switch (tab) {
              @case ('dashboard') { <app-dashboard [storeId]="selectedStoreId" /> }
              @case ('orders') { <app-orders [storeId]="selectedStoreId" [query]="search" /> }
              @case ('deliveries') { <app-deliveries [storeId]="selectedStoreId" /> }
              @case ('catalog') { <app-catalog [storeId]="selectedStoreId" /> }
              @case ('integrations') { <app-integrations [storeId]="selectedStoreId" /> }
              @case ('team') { <app-team [storeId]="selectedStoreId" /> }
              @case ('finance') { <app-finance [storeId]="selectedStoreId" /> }
              @case ('stores') { <app-stores (changed)="loadStores()" /> }
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

  stores: StoreDto[] = [];
  selectedStoreId = '';
  tab: 'dashboard' | 'orders' | 'deliveries' | 'catalog' | 'integrations' | 'team' | 'finance' | 'stores' | 'settings' = 'dashboard';

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

  loadStores() {
    this.api.getPublic<StoreDto[]>('/catalog/stores?onlyActive=false').subscribe(all => {
      // Pracownik sklepu widzi tylko swój sklep; admin — wszystkie.
      const s = this.api.isAdmin() ? all : all.filter(x => this.api.storeIds().includes(x.id));
      this.stores = s;
      const stillThere = s.some(x => x.id === this.selectedStoreId);
      if (!stillThere && s.length) { this.selectedStoreId = s[0].id; }
      // Pracownik nie ma dostępu do zakładek administracyjnych.
      if (!this.api.isAdmin() && (this.tab === 'stores' || this.tab === 'team')) this.tab = 'dashboard';
    });
  }

  logout() {
    this.api.logout();
    this.stores = [];
    this.selectedStoreId = '';
    this.tab = 'dashboard';
  }
}
