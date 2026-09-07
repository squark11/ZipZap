import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

interface StoreDto { id: string; name: string; city: string; }
interface OrderDto {
  id: string; status: string; total: number; deliveryFee: number;
  commissionAmount: number; placedAtUtc: string; contactPhone: string;
}

@Component({
  selector: 'app-root',
  imports: [CommonModule, FormsModule],
  styleUrl: './app.css',
  template: `
  <header class="topbar">
    <span class="logo">Zip<span class="zap">Zap</span></span>
    <span class="subtitle">Panel administracyjny</span>
    @if (token) {
      <button class="ghost" (click)="logout()">Wyloguj</button>
    }
  </header>

  <main class="wrap">
    @if (!token) {
      <form class="card login" (ngSubmit)="login()">
        <h2>Logowanie</h2>
        <label>E-mail
          <input name="email" [(ngModel)]="email" type="email" placeholder="admin@zipzap.local" required />
        </label>
        <label>Hasło
          <input name="password" [(ngModel)]="password" type="password" placeholder="••••••••" required />
        </label>
        <button class="primary" type="submit" [disabled]="loading">Zaloguj</button>
        @if (error) { <p class="error">{{ error }}</p> }
        <p class="hint">Domyślny admin (dev): admin&#64;zipzap.local / Admin123!</p>
      </form>
    } @else {
      <section class="card">
        <div class="row">
          <h2>Zamówienia sklepu</h2>
          <div class="controls">
            <select [(ngModel)]="selectedStoreId" (ngModelChange)="loadOrders()">
              @for (s of stores; track s.id) {
                <option [value]="s.id">{{ s.name }} — {{ s.city }}</option>
              }
            </select>
            <button class="ghost" (click)="loadOrders()">Odśwież</button>
          </div>
        </div>

        @if (loading) { <p class="muted">Ładowanie…</p> }
        @else if (orders.length === 0) { <p class="muted">Brak zamówień dla tego sklepu.</p> }
        @else {
          <table>
            <thead>
              <tr><th>Zamówienie</th><th>Status</th><th>Suma</th><th>Prowizja</th><th>Dostawa</th><th>Złożono</th></tr>
            </thead>
            <tbody>
              @for (o of orders; track o.id) {
                <tr>
                  <td class="mono">{{ o.id.substring(0, 8) }}</td>
                  <td><span class="badge" [attr.data-status]="o.status">{{ o.status }}</span></td>
                  <td>{{ o.total | number:'1.2-2' }} zł</td>
                  <td>{{ o.commissionAmount | number:'1.2-2' }} zł</td>
                  <td>{{ o.deliveryFee | number:'1.2-2' }} zł</td>
                  <td class="muted">{{ o.placedAtUtc | date:'yyyy-MM-dd HH:mm' }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      </section>
    }
  </main>
  `,
})
export class App {
  private http = inject(HttpClient);
  private apiBase = 'http://localhost:5080/api';

  email = 'admin@zipzap.local';
  password = 'Admin123!';
  token: string | null = null;
  error = '';
  loading = false;

  stores: StoreDto[] = [];
  selectedStoreId = '';
  orders: OrderDto[] = [];

  login() {
    this.loading = true;
    this.error = '';
    this.http.post<{ accessToken: string }>(`${this.apiBase}/identity/login`, { email: this.email, password: this.password })
      .subscribe({
        next: r => { this.token = r.accessToken; this.loading = false; this.loadStores(); },
        error: () => { this.error = 'Nieprawidłowy e-mail lub hasło.'; this.loading = false; },
      });
  }

  loadStores() {
    this.http.get<StoreDto[]>(`${this.apiBase}/catalog/stores?onlyActive=false`).subscribe(s => {
      this.stores = s;
      if (s.length && !this.selectedStoreId) { this.selectedStoreId = s[0].id; }
      this.loadOrders();
    });
  }

  loadOrders() {
    if (!this.selectedStoreId) { return; }
    this.loading = true;
    this.http.get<OrderDto[]>(`${this.apiBase}/ordering/stores/${this.selectedStoreId}/orders`, this.auth())
      .subscribe({
        next: o => { this.orders = o; this.loading = false; },
        error: () => { this.loading = false; },
      });
  }

  logout() {
    this.token = null;
    this.orders = [];
    this.stores = [];
    this.selectedStoreId = '';
  }

  private auth() {
    return { headers: { Authorization: `Bearer ${this.token}` } };
  }
}
