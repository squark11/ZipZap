import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, OrderDto } from './api';

interface OrderAction { label: string; action: string; cls: string; }
interface Group { key: string; label: string; statuses: string[]; }

@Component({
  selector: 'app-orders',
  imports: [CommonModule, FormsModule],
  template: `
  <div class="page-head">
    <h1>Zamówienia</h1>
    <div class="controls">
      <button class="btn ghost sm" (click)="load()">Odśwież</button>
      <button class="filter-toggle" [class.open]="filtersOpen" (click)="filtersOpen = !filtersOpen">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 4h18l-7 8v6l-4 2v-8L3 4z"/></svg>
        Filtrowanie zamówień
        <svg class="chev" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 9l6 6 6-6"/></svg>
      </button>
    </div>
  </div>

  @if (filtersOpen) {
    <div class="filterpanel">
      <div class="fp-body">
        <div class="fp-grid">
          <label class="fp-field"><span>Numer / telefon</span><input [(ngModel)]="f.text" placeholder="np. 4a1b… lub 601…" /></label>
          <label class="fp-field"><span>Kwota od (zł)</span><input type="number" step="0.01" [(ngModel)]="f.amountMin" /></label>
          <label class="fp-field"><span>Kwota do (zł)</span><input type="number" step="0.01" [(ngModel)]="f.amountMax" /></label>
          <label class="fp-field"><span>Data od</span><input type="date" [(ngModel)]="f.dateFrom" /></label>
          <label class="fp-field"><span>Data do</span><input type="date" [(ngModel)]="f.dateTo" /></label>
        </div>
      </div>
      <div class="fp-foot">
        <span class="fp-count">Pasujące zamówienia: <strong>{{ visible().length }}</strong></span>
        <span class="spacer"></span>
        <button class="btn" (click)="clearFilters()">Wyczyść filtry</button>
        <button class="btn primary" (click)="filtersOpen = false">Zwiń</button>
      </div>
    </div>
  }

  <div class="orders-grid">
    <div class="card statusnav">
      @for (g of groups; track g.key) {
        <button [class.active]="filter === g.key" (click)="filter = g.key">
          <span class="dotc"></span>{{ g.label }}<span class="count">{{ countFor(g) }}</span>
        </button>
      }
    </div>

    <div class="card">
      @if (loading) { <div class="pad muted">Ładowanie…</div> }
      @else if (visible().length === 0) { <div class="pad muted">Brak zamówień w tym widoku.</div> }
      @else {
        <table>
          <thead>
            <tr><th></th><th>Numer</th><th>Kontakt</th><th class="right">Kwota</th><th class="right">Prowizja</th><th>Status</th><th>Złożono</th><th>Akcje</th></tr>
          </thead>
          <tbody>
            @for (o of visible(); track o.id) {
              <tr>
                <td><button class="btn ghost sm" (click)="toggle(o.id)">{{ expanded.has(o.id) ? '▾' : '▸' }}</button></td>
                <td><span class="num mono">{{ o.id.substring(0,8) }}</span></td>
                <td class="muted">{{ o.contactPhone }}</td>
                <td class="right">{{ o.total | number:'1.2-2' }} zł</td>
                <td class="right muted">{{ o.commissionAmount | number:'1.2-2' }} zł</td>
                <td><span class="pill" [attr.data-status]="o.status">{{ o.status }}</span></td>
                <td class="muted">{{ o.placedAtUtc | date:'MM-dd HH:mm' }}</td>
                <td>
                  <div class="actions">
                    @for (a of actionsFor(o.status); track a.action) {
                      <button [class]="'btn ' + a.cls + ' sm'" (click)="run(o, a.action)">{{ a.label }}</button>
                    }
                    @if (actionsFor(o.status).length === 0) { <span class="muted">—</span> }
                  </div>
                </td>
              </tr>
              @if (expanded.has(o.id)) {
                <tr><td colspan="8">
                  <div class="detail">
                    <strong>Dostawa:</strong> {{ o.deliveryAddress }} · tel. {{ o.contactPhone }} · opłata {{ o.deliveryFee | number:'1.2-2' }} zł
                    <ul>
                      @for (it of o.items; track it.productId) {
                        <li>{{ it.quantity }} × {{ it.productName }} — {{ it.lineTotal | number:'1.2-2' }} zł</li>
                      }
                    </ul>
                    <span class="muted">Historia: {{ history(o) }}</span>
                  </div>
                </td></tr>
              }
            }
          </tbody>
        </table>
      }
    </div>
  </div>
  `,
})
export class OrdersComponent {
  private api = inject(Api);
  storeId = input.required<string>();
  query = input<string>('');

  orders: OrderDto[] = [];
  loading = false;
  expanded = new Set<string>();
  filter = 'all';
  filtersOpen = false;

  f: { text: string; amountMin: number | null; amountMax: number | null; dateFrom: string; dateTo: string } =
    { text: '', amountMin: null, amountMax: null, dateFrom: '', dateTo: '' };

  groups: Group[] = [
    { key: 'all', label: 'Wszystkie', statuses: [] },
    { key: 'new', label: 'Nowe', statuses: ['Placed', 'Confirmed'] },
    { key: 'progress', label: 'W realizacji', statuses: ['Picking', 'ReadyForPickup'] },
    { key: 'delivery', label: 'W dostawie', statuses: ['InDelivery'] },
    { key: 'done', label: 'Zrealizowane', statuses: ['Delivered', 'Completed'] },
    { key: 'cancelled', label: 'Anulowane', statuses: ['Cancelled'] },
  ];

  constructor() {
    effect(() => { const id = this.storeId(); if (id) this.load(); });
  }

  load() {
    this.loading = true;
    this.api.get<OrderDto[]>(`/ordering/stores/${this.storeId()}/orders`).subscribe({
      next: o => { this.orders = o; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  countFor(g: Group): number {
    return g.statuses.length === 0 ? this.orders.length : this.orders.filter(o => g.statuses.includes(o.status)).length;
  }

  visible(): OrderDto[] {
    const g = this.groups.find(x => x.key === this.filter)!;
    const q = (this.query() + ' ' + this.f.text).trim().toLowerCase();
    return this.orders.filter(o => {
      if (g.statuses.length && !g.statuses.includes(o.status)) return false;
      if (this.f.text && !(o.id.toLowerCase().includes(this.f.text.toLowerCase()) || (o.contactPhone ?? '').includes(this.f.text))) return false;
      if (this.query() && !(o.id.toLowerCase().includes(this.query().toLowerCase()) || (o.contactPhone ?? '').includes(this.query()))) return false;
      if (this.f.amountMin != null && o.total < this.f.amountMin) return false;
      if (this.f.amountMax != null && o.total > this.f.amountMax) return false;
      const d = (o.placedAtUtc ?? '').substring(0, 10);
      if (this.f.dateFrom && d < this.f.dateFrom) return false;
      if (this.f.dateTo && d > this.f.dateTo) return false;
      return true;
    });
  }

  clearFilters() {
    this.f = { text: '', amountMin: null, amountMax: null, dateFrom: '', dateTo: '' };
  }

  actionsFor(status: string): OrderAction[] {
    switch (status) {
      case 'Placed': return [a('Potwierdź', 'confirm', 'primary'), a('Anuluj', 'cancel', 'danger')];
      case 'Confirmed': return [a('Kompletuj', 'start-picking', 'primary'), a('Anuluj', 'cancel', 'danger')];
      case 'Picking': return [a('Gotowe do odbioru', 'ready', 'success'), a('Anuluj', 'cancel', 'danger')];
      case 'ReadyForPickup': return [a('Odebrane', 'pick-up', 'primary'), a('Anuluj', 'cancel', 'danger')];
      case 'InDelivery': return [a('Dostarczone', 'delivered', 'success')];
      case 'Delivered': return [a('Zakończ', 'complete', 'success')];
      default: return [];
    }
  }

  run(order: OrderDto, action: string) {
    this.api.post(`/ordering/orders/${order.id}/${action}`, {}).subscribe({
      next: () => this.load(),
      error: e => alert('Nie udało się: ' + (e?.error?.detail ?? action)),
    });
  }

  toggle(id: string) { this.expanded.has(id) ? this.expanded.delete(id) : this.expanded.add(id); }
  history(o: OrderDto) { return o.history.map(h => h.toStatus).join(' → '); }
}

function a(label: string, action: string, cls: string): OrderAction { return { label, action, cls }; }
