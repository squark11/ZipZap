import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, DeliveryDto, OrderDto, PaymentDto } from './api';

interface OrderAction { label: string; action: string; cls: string; }
interface Group { key: string; label: string; statuses: string[]; }

@Component({
  selector: 'app-orders',
  imports: [CommonModule, FormsModule],
  styles: [`
    .paybadge { display:inline-block; padding:2px 8px; border-radius:6px; font-size:12px; font-weight:600; white-space:nowrap; }
    .pay-ok   { background:#E9FBF0; color:#128040; }
    .pay-wait { background:#FFF3EA; color:#C2560A; }
    .pay-bad  { background:#FEECEC; color:#EF4444; }
    .pay-none { background:#F1F3F5; color:#6B7280; }
    .detail-grid { display:grid; grid-template-columns:1fr 1fr; gap:16px; margin-bottom:10px; }
    .detail-grid .box { background:#F7F8FA; border:1px solid #E5E7EB; border-radius:10px; padding:10px 12px; }
    .detail-grid .box h4 { margin:0 0 6px; font-size:12px; text-transform:uppercase; letter-spacing:.04em; color:#6B7280; }
    .detail-grid .kv { font-size:13px; margin:2px 0; }
    @media (max-width: 720px) { .detail-grid { grid-template-columns:1fr; } }
  `],
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
            <tr><th></th><th>Numer</th><th>Kontakt</th><th class="right">Kwota</th><th class="right">Prowizja</th><th>Płatność</th><th>Status</th><th>Złożono</th><th>Akcje</th></tr>
          </thead>
          <tbody>
            @for (o of visible(); track o.id) {
              <tr>
                <td><button class="btn ghost sm" (click)="toggle(o.id)">{{ expanded.has(o.id) ? '▾' : '▸' }}</button></td>
                <td><span class="num mono">{{ o.id.substring(0,8) }}</span></td>
                <td class="muted">{{ o.contactPhone }}</td>
                <td class="right">{{ o.total | number:'1.2-2' }} zł</td>
                <td class="right muted">{{ o.commissionAmount | number:'1.2-2' }} zł</td>
                <td><span class="paybadge" [class]="payClass(o.id)">{{ payLabel(o.id) }}</span></td>
                <td><span class="pill" [attr.data-status]="o.status">{{ o.status }}</span></td>
                <td class="muted">{{ o.placedAtUtc | date:'dd.MM HH:mm' }}</td>
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
                <tr><td colspan="9">
                  <div class="detail">
                    <div class="detail-grid">
                      <div class="box">
                        <h4>Płatność</h4>
                        @if (payments[o.id] === undefined) { <div class="kv muted">Ładowanie…</div> }
                        @else if (payments[o.id] === null) { <div class="kv muted">Brak płatności (jeszcze nie utworzona).</div> }
                        @else {
                          <div class="kv"><span class="paybadge" [class]="payClass(o.id)">{{ payLabel(o.id) }}</span></div>
                          <div class="kv">Kwota: <strong>{{ payments[o.id]!.amount | number:'1.2-2' }} zł</strong> (w tym dostawa {{ payments[o.id]!.deliveryFee | number:'1.2-2' }} zł)</div>
                          <div class="kv muted">Dostawca: {{ payments[o.id]!.provider || '—' }}<span *ngIf="payments[o.id]!.providerRef"> · ref {{ payments[o.id]!.providerRef }}</span></div>
                        }
                      </div>
                      <div class="box">
                        <h4>Dostawa</h4>
                        @if (deliveries[o.id] === undefined) { <div class="kv muted">Ładowanie…</div> }
                        @else if (deliveries[o.id] === null) { <div class="kv muted">Brak zlecenia dostawy (przed „Gotowe do odbioru").</div> }
                        @else {
                          <div class="kv"><span class="pill" [attr.data-status]="deliveries[o.id]!.status">{{ deliveryLabel(deliveries[o.id]!.status) }}</span></div>
                          <div class="kv">Kierowca: {{ deliveries[o.id]!.driverId ? (deliveries[o.id]!.driverId!.substring(0,8) + '…') : 'nieprzypisany' }}</div>
                          @if (deliveries[o.id]!.pickedUpAtUtc) { <div class="kv muted">Odebrano: {{ deliveries[o.id]!.pickedUpAtUtc | date:'dd.MM HH:mm' }}</div> }
                          @if (deliveries[o.id]!.deliveredAtUtc) { <div class="kv muted">Dostarczono: {{ deliveries[o.id]!.deliveredAtUtc | date:'dd.MM HH:mm' }}</div> }
                        }
                      </div>
                    </div>
                    <strong>Adres:</strong> {{ o.deliveryAddress }} · tel. {{ o.contactPhone }} · opłata {{ o.deliveryFee | number:'1.2-2' }} zł
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

  // undefined = nie pobrano, null = brak (404), obiekt = dane
  payments: Record<string, PaymentDto | null | undefined> = {};
  deliveries: Record<string, DeliveryDto | null | undefined> = {};

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
    this.payments = {};
    this.deliveries = {};
    this.api.get<OrderDto[]>(`/ordering/stores/${this.storeId()}/orders`).subscribe({
      next: o => {
        this.orders = o;
        this.loading = false;
        this.loadPayments(o);
        for (const id of this.expanded) this.loadDelivery(id); // odśwież rozwinięte
      },
      error: () => { this.loading = false; },
    });
  }

  // Status płatności per zamówienie (widok „na pierwszy rzut oka").
  private loadPayments(orders: OrderDto[]) {
    for (const o of orders) {
      this.api.get<PaymentDto>(`/payments/orders/${o.id}`).subscribe({
        next: p => this.payments[o.id] = p,
        error: () => this.payments[o.id] = null,
      });
    }
  }

  private loadDelivery(orderId: string) {
    if (this.deliveries[orderId] !== undefined) return;
    this.api.get<DeliveryDto>(`/delivery/orders/${orderId}`).subscribe({
      next: d => this.deliveries[orderId] = d,
      error: () => this.deliveries[orderId] = null,
    });
  }

  payLabel(orderId: string): string {
    const p = this.payments[orderId];
    if (p === undefined) return '…';
    if (p === null) return '—';
    switch (p.status) {
      case 'Pending': return 'Oczekuje';
      case 'Authorized': return 'Opłacone';
      case 'Settled': return 'Rozliczone';
      case 'Failed': return 'Nieudane';
      case 'Refunded': return 'Zwrot';
      default: return p.status;
    }
  }

  payClass(orderId: string): string {
    const p = this.payments[orderId];
    if (!p) return 'pay-none';
    switch (p.status) {
      case 'Authorized':
      case 'Settled': return 'pay-ok';
      case 'Failed': return 'pay-bad';
      case 'Refunded': return 'pay-none';
      default: return 'pay-wait';
    }
  }

  deliveryLabel(status: string): string {
    switch (status) {
      case 'AvailableForPickup': return 'Do odbioru';
      case 'Accepted': return 'Przyjęta przez kierowcę';
      case 'PickedUp': return 'Odebrana';
      case 'Delivered': return 'Dostarczona';
      default: return status;
    }
  }

  countFor(g: Group): number {
    return g.statuses.length === 0 ? this.orders.length : this.orders.filter(o => g.statuses.includes(o.status)).length;
  }

  visible(): OrderDto[] {
    const g = this.groups.find(x => x.key === this.filter)!;
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
      case 'ReadyForPickup': return [a('Anuluj', 'cancel', 'danger')]; // odbiór realizuje kierowca
      case 'InDelivery': return []; // dostarczenie realizuje kierowca
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

  toggle(id: string) {
    if (this.expanded.has(id)) { this.expanded.delete(id); }
    else { this.expanded.add(id); this.loadDelivery(id); }
  }

  history(o: OrderDto) { return o.history.map(h => h.toStatus).join(' → '); }
}

function a(label: string, action: string, cls: string): OrderAction { return { label, action, cls }; }
