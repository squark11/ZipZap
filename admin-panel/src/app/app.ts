import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, StoreDto } from './api';
import { OrdersComponent } from './orders';
import { CatalogComponent } from './catalog';
import { DashboardComponent } from './dashboard';

@Component({
  selector: 'app-root',
  imports: [CommonModule, FormsModule, OrdersComponent, CatalogComponent, DashboardComponent],
  template: `
  <header class="topbar">
    <span class="logo">Zip<span class="zap">Zap</span></span>
    <span class="subtitle">Panel administracyjny</span>
    @if (api.isLoggedIn()) {
      <span class="spacer"></span>
      <span class="userbox">
        {{ api.userEmail() }}
        <button class="ghost sm" (click)="logout()">Wyloguj</button>
      </span>
    }
  </header>

  <main class="wrap">
    @if (!api.isLoggedIn()) {
      <form class="card login" (ngSubmit)="login()">
        <h2>Logowanie</h2>
        <label>E-mail<input name="email" [(ngModel)]="email" type="email" required /></label>
        <label>Hasło<input name="password" [(ngModel)]="password" type="password" required /></label>
        <button class="primary" type="submit" [disabled]="loading">Zaloguj</button>
        @if (error) { <p class="error">{{ error }}</p> }
        <p class="hint">Domyślny admin (dev): admin&#64;zipzap.local / Admin123!</p>
      </form>
    } @else {
      <div class="row">
        <label style="flex-direction:row; align-items:center; gap:8px">
          <span class="muted">Sklep:</span>
          <select [(ngModel)]="selectedStoreId">
            @for (s of stores; track s.id) { <option [value]="s.id">{{ s.name }} — {{ s.city }}</option> }
          </select>
        </label>
      </div>

      @if (stores.length === 0) {
        <div class="card"><p class="muted">Brak sklepów. Utwórz sklep przez API (POST /api/catalog/stores) jako admin.</p></div>
      } @else {
        <div class="tabs">
          <button [class.active]="tab==='dashboard'" (click)="tab='dashboard'">Pulpit</button>
          <button [class.active]="tab==='orders'" (click)="tab='orders'">Zamówienia</button>
          <button [class.active]="tab==='catalog'" (click)="tab='catalog'">Oferta</button>
        </div>

        <div class="card">
          @switch (tab) {
            @case ('dashboard') { <app-dashboard [storeId]="selectedStoreId" /> }
            @case ('orders') { <app-orders [storeId]="selectedStoreId" /> }
            @case ('catalog') { <app-catalog [storeId]="selectedStoreId" /> }
          }
        </div>
      }
    }
  </main>
  `,
})
export class App {
  protected api = inject(Api);

  email = 'admin@zipzap.local';
  password = 'Admin123!';
  error = '';
  loading = false;

  stores: StoreDto[] = [];
  selectedStoreId = '';
  tab: 'dashboard' | 'orders' | 'catalog' = 'dashboard';

  login() {
    this.loading = true;
    this.error = '';
    this.api.login(this.email, this.password).subscribe({
      next: () => { this.loading = false; this.loadStores(); },
      error: () => { this.error = 'Nieprawidłowy e-mail lub hasło.'; this.loading = false; },
    });
  }

  loadStores() {
    this.api.getPublic<StoreDto[]>('/catalog/stores?onlyActive=false').subscribe(s => {
      this.stores = s;
      if (s.length) { this.selectedStoreId = s[0].id; }
    });
  }

  logout() {
    this.api.logout();
    this.stores = [];
    this.selectedStoreId = '';
    this.tab = 'dashboard';
  }
}
