import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Api, DeliveryDto } from './api';

interface Group { key: string; label: string; statuses: string[]; }

@Component({
  selector: 'app-deliveries',
  imports: [CommonModule],
  template: `
  <div class="page-head">
    <h1>Dostawy</h1>
    <div class="controls"><button class="btn ghost sm" (click)="load()">Odśwież</button></div>
  </div>

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
      @else if (visible().length === 0) { <div class="pad muted">Brak dostaw w tym widoku.</div> }
      @else {
        <table>
          <thead>
            <tr><th>Dostawa</th><th>Zamówienie</th><th>Status</th><th>Kierowca</th><th>Utworzono</th><th>Odebrano</th><th>Dostarczono</th></tr>
          </thead>
          <tbody>
            @for (d of visible(); track d.id) {
              <tr>
                <td class="mono">{{ d.id.substring(0,8) }}</td>
                <td class="mono num">{{ d.orderId.substring(0,8) }}</td>
                <td><span class="pill" [attr.data-status]="d.status">{{ label(d.status) }}</span></td>
                <td class="muted">{{ d.driverId ? (d.driverId.substring(0,8) + '…') : 'nieprzypisany' }}</td>
                <td class="muted">{{ d.createdAtUtc | date:'MM-dd HH:mm' }}</td>
                <td class="muted">{{ d.pickedUpAtUtc ? (d.pickedUpAtUtc | date:'MM-dd HH:mm') : '—' }}</td>
                <td class="muted">{{ d.deliveredAtUtc ? (d.deliveredAtUtc | date:'MM-dd HH:mm') : '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    </div>
  </div>
  `,
})
export class DeliveriesComponent {
  private api = inject(Api);
  storeId = input.required<string>();

  deliveries: DeliveryDto[] = [];
  loading = false;
  filter = 'all';

  groups: Group[] = [
    { key: 'all', label: 'Wszystkie', statuses: [] },
    { key: 'available', label: 'Do odbioru', statuses: ['AvailableForPickup'] },
    { key: 'ontheway', label: 'W drodze', statuses: ['Accepted', 'PickedUp'] },
    { key: 'delivered', label: 'Dostarczone', statuses: ['Delivered'] },
  ];

  constructor() {
    effect(() => { const id = this.storeId(); if (id) this.load(); });
  }

  load() {
    this.loading = true;
    this.api.get<DeliveryDto[]>(`/delivery/stores/${this.storeId()}/deliveries`).subscribe({
      next: d => { this.deliveries = d; this.loading = false; },
      error: () => { this.deliveries = []; this.loading = false; },
    });
  }

  countFor(g: Group): number {
    return g.statuses.length === 0 ? this.deliveries.length : this.deliveries.filter(d => g.statuses.includes(d.status)).length;
  }

  visible(): DeliveryDto[] {
    const g = this.groups.find(x => x.key === this.filter)!;
    return g.statuses.length === 0 ? this.deliveries : this.deliveries.filter(d => g.statuses.includes(d.status));
  }

  label(status: string): string {
    switch (status) {
      case 'AvailableForPickup': return 'Do odbioru';
      case 'Accepted': return 'Przyjęta';
      case 'PickedUp': return 'W drodze';
      case 'Delivered': return 'Dostarczona';
      default: return status;
    }
  }
}
