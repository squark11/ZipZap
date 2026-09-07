import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Api, OrderDto } from './api';

interface OrderAction { label: string; action: string; cls: string; }

@Component({
  selector: 'app-orders',
  imports: [CommonModule],
  template: `
  <div class="row">
    <h2>Zamówienia sklepu</h2>
    <div class="controls">
      <button class="ghost sm" (click)="load()">Odśwież</button>
    </div>
  </div>

  @if (loading) { <p class="muted">Ładowanie…</p> }
  @else if (orders.length === 0) { <p class="muted">Brak zamówień dla tego sklepu.</p> }
  @else {
    <table>
      <thead>
        <tr><th></th><th>Zamówienie</th><th>Status</th><th class="right">Suma</th><th class="right">Prowizja</th><th>Złożono</th><th>Akcje</th></tr>
      </thead>
      <tbody>
        @for (o of orders; track o.id) {
          <tr>
            <td><button class="ghost sm" (click)="toggle(o.id)">{{ expanded.has(o.id) ? '▾' : '▸' }}</button></td>
            <td class="mono">{{ o.id.substring(0,8) }}</td>
            <td><span class="badge" [attr.data-status]="o.status">{{ o.status }}</span></td>
            <td class="right">{{ o.total | number:'1.2-2' }} zł</td>
            <td class="right">{{ o.commissionAmount | number:'1.2-2' }} zł</td>
            <td class="muted">{{ o.placedAtUtc | date:'MM-dd HH:mm' }}</td>
            <td>
              <div class="actions">
                @for (a of actionsFor(o.status); track a.action) {
                  <button [class]="a.cls + ' sm'" (click)="run(o, a.action)">{{ a.label }}</button>
                }
                @if (actionsFor(o.status).length === 0) { <span class="muted">—</span> }
              </div>
            </td>
          </tr>
          @if (expanded.has(o.id)) {
            <tr><td colspan="7">
              <div class="detail">
                <strong>Dostawa:</strong> {{ o.deliveryAddress }} · tel. {{ o.contactPhone }}
                · dostawa {{ o.deliveryFee | number:'1.2-2' }} zł
                <ul>
                  @for (it of o.items; track it.productId) {
                    <li>{{ it.quantity }} × {{ it.productName }} — {{ it.lineTotal | number:'1.2-2' }} zł</li>
                  }
                </ul>
                <span class="muted">Historia: {{ historyLine(o) }}</span>
              </div>
            </td></tr>
          }
        }
      </tbody>
    </table>
  }
  `,
})
export class OrdersComponent {
  private api = inject(Api);
  storeId = input.required<string>();
  orders: OrderDto[] = [];
  loading = false;
  expanded = new Set<string>();

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

  historyLine(o: OrderDto) { return o.history.map(h => h.toStatus).join(' → '); }
}

function a(label: string, action: string, cls: string): OrderAction { return { label, action, cls }; }
