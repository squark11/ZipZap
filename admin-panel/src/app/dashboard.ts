import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Api, CommissionDto, DeliveryDto, OrderDto } from './api';

@Component({
  selector: 'app-dashboard',
  imports: [CommonModule],
  template: `
  <div class="row">
    <h2>Pulpit</h2>
    <div class="controls"><button class="ghost sm" (click)="load()">Odśwież</button></div>
  </div>

  <div class="tiles">
    <div class="tile">
      <div class="k">Prowizja ZipZap (rozliczona)</div>
      <div class="v accent">{{ (commission?.totalCommission ?? 0) | number:'1.2-2' }} zł</div>
      <div class="muted">{{ commission?.entries ?? 0 }} rozliczonych zamówień</div>
    </div>
    <div class="tile">
      <div class="k">Zamówienia (łącznie)</div>
      <div class="v">{{ orders.length }}</div>
      <div class="muted">aktywne: {{ activeCount }}</div>
    </div>
    <div class="tile">
      <div class="k">Dostawy do odbioru</div>
      <div class="v green">{{ deliveries.length }}</div>
      <div class="muted">czekają na kierowcę</div>
    </div>
  </div>

  <div class="card">
    <h2 style="font-size:16px">Dostępne dostawy</h2>
    @if (deliveries.length === 0) { <p class="muted">Brak dostaw oczekujących na odbiór.</p> }
    @else {
      <table>
        <thead><tr><th>Dostawa</th><th>Zamówienie</th><th>Status</th><th>Utworzono</th></tr></thead>
        <tbody>
          @for (d of deliveries; track d.id) {
            <tr>
              <td class="mono">{{ d.id.substring(0,8) }}</td>
              <td class="mono">{{ d.orderId.substring(0,8) }}</td>
              <td><span class="badge" [attr.data-status]="d.status">{{ d.status }}</span></td>
              <td class="muted">{{ d.createdAtUtc | date:'MM-dd HH:mm' }}</td>
            </tr>
          }
        </tbody>
      </table>
    }
  </div>
  `,
})
export class DashboardComponent {
  private api = inject(Api);
  storeId = input.required<string>();

  commission: CommissionDto | null = null;
  orders: OrderDto[] = [];
  deliveries: DeliveryDto[] = [];

  constructor() {
    effect(() => { const id = this.storeId(); if (id) this.load(); });
  }

  get activeCount() {
    return this.orders.filter(o => o.status !== 'Completed' && o.status !== 'Cancelled').length;
  }

  load() {
    const id = this.storeId();
    this.api.get<CommissionDto>(`/payments/stores/${id}/commission`).subscribe({ next: c => this.commission = c, error: () => this.commission = null });
    this.api.get<OrderDto[]>(`/ordering/stores/${id}/orders`).subscribe({ next: o => this.orders = o, error: () => this.orders = [] });
    this.api.get<DeliveryDto[]>(`/delivery/available`).subscribe({ next: d => this.deliveries = d, error: () => this.deliveries = [] });
  }
}
