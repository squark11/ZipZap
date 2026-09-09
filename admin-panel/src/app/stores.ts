import { Component, EventEmitter, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, StoreDto } from './api';

@Component({
  selector: 'app-stores',
  imports: [CommonModule, FormsModule],
  styles: [`
    .statuscell select { padding:4px 8px; border:1px solid #E5E7EB; border-radius:6px; font-size:13px; background:#fff; }
    .accept-ok { color:#128040; font-weight:600; }
    .accept-no { color:#EF4444; font-weight:600; }
  `],
  template: `
  <div class="page-head">
    <h1>Sklepy</h1>
    <div class="controls"><button class="btn ghost sm" (click)="load()">Odśwież</button></div>
  </div>

  <div class="card pad" style="margin-bottom:16px">
    <div class="formgrid">
      <label>Nazwa<input [(ngModel)]="ns.name" placeholder="np. Piekarnia Poranek" /></label>
      <label>Miasto<input [(ngModel)]="ns.city" placeholder="np. Koło" /></label>
      <label>Adres<input [(ngModel)]="ns.address" placeholder="ul. Rynek 1" /></label>
      <label>Telefon<input [(ngModel)]="ns.phone" placeholder="600 100 200" /></label>
      <label>Prowizja (%)<input [(ngModel)]="ns.commissionPct" type="number" step="0.5" /></label>
      <label>Min. zamówienie (zł)<input [(ngModel)]="ns.minimumOrderValue" type="number" step="0.01" /></label>
      <button class="btn primary" (click)="create()">Utwórz sklep</button>
    </div>
  </div>

  <div class="card">
    @if (loading) { <div class="pad muted">Ładowanie…</div> }
    @else {
      <table>
        <thead>
          <tr><th>Nazwa</th><th>Miasto</th><th>Status</th><th>Przyjmuje</th><th class="right">Min. zam.</th><th class="right">Prowizja</th><th>Akcje</th></tr>
        </thead>
        <tbody>
          @for (s of stores; track s.id) {
            <tr>
              <td><strong>{{ s.name }}</strong><br><span class="muted mono">{{ s.slug }}</span></td>
              <td class="muted">{{ s.city }}</td>
              <td class="statuscell">
                <select [ngModel]="s.status" (ngModelChange)="setStatus(s, $event)">
                  <option value="Open">Otwarty</option>
                  <option value="Closed">Zamknięty</option>
                  <option value="TemporarilyUnavailable">Niedostępny</option>
                </select>
              </td>
              <td><span [class]="s.isAcceptingOrders ? 'accept-ok' : 'accept-no'">{{ s.isAcceptingOrders ? 'TAK' : 'NIE' }}</span></td>
              <td class="right">{{ s.minimumOrderValue | number:'1.2-2' }} zł</td>
              <td class="right">{{ (s.commissionRate * 100) | number:'1.0-1' }}%</td>
              <td>
                <div class="actions">
                  <button class="btn ghost sm" (click)="editMinOrder(s)">Min. zam.</button>
                  <button class="btn ghost sm" (click)="editCommission(s)">Prowizja</button>
                  <button class="btn ghost sm" (click)="toggleActive(s)">{{ s.isActive ? 'Dezaktywuj' : 'Aktywuj' }}</button>
                </div>
              </td>
            </tr>
          }
          @if (stores.length === 0) { <tr><td colspan="7" class="muted pad">Brak sklepów. Utwórz pierwszy powyżej.</td></tr> }
        </tbody>
      </table>
    }
  </div>
  `,
})
export class StoresComponent {
  private api = inject(Api);
  @Output() changed = new EventEmitter<void>();

  stores: StoreDto[] = [];
  loading = false;
  ns = { name: '', city: '', address: '', phone: '', commissionPct: 10, minimumOrderValue: 0 };

  constructor() { this.load(); }

  load() {
    this.loading = true;
    this.api.getPublic<StoreDto[]>('/catalog/stores?onlyActive=false').subscribe({
      next: s => { this.stores = s; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  create() {
    if (!this.ns.name.trim() || !this.ns.city.trim()) { alert('Podaj nazwę i miasto.'); return; }
    const body = {
      name: this.ns.name.trim(),
      city: this.ns.city.trim(),
      address: this.ns.address.trim() || null,
      phone: this.ns.phone.trim() || null,
      commissionRate: this.pct(this.ns.commissionPct),
      minimumOrderValue: Number(this.ns.minimumOrderValue) || 0,
    };
    this.api.post('/catalog/stores', body).subscribe({
      next: () => {
        this.ns = { name: '', city: '', address: '', phone: '', commissionPct: 10, minimumOrderValue: 0 };
        this.reload();
      },
      error: e => alert(err(e)),
    });
  }

  setStatus(s: StoreDto, status: string) {
    if (status === s.status) return;
    this.api.patch(`/catalog/stores/${s.id}`, { status }).subscribe({ next: () => this.reload(), error: e => { alert(err(e)); this.reload(); } });
  }

  toggleActive(s: StoreDto) {
    this.api.patch(`/catalog/stores/${s.id}`, { isActive: !s.isActive }).subscribe({ next: () => this.reload(), error: e => alert(err(e)) });
  }

  editMinOrder(s: StoreDto) {
    const val = prompt(`Min. wartość zamówienia dla "${s.name}" (zł)`, String(s.minimumOrderValue));
    if (val === null) return;
    const minimumOrderValue = Number(val.replace(',', '.'));
    if (Number.isNaN(minimumOrderValue) || minimumOrderValue < 0) { alert('Nieprawidłowa wartość'); return; }
    this.api.patch(`/catalog/stores/${s.id}`, { minimumOrderValue }).subscribe({ next: () => this.reload(), error: e => alert(err(e)) });
  }

  editCommission(s: StoreDto) {
    const val = prompt(`Prowizja dla "${s.name}" (%)`, String(Math.round(s.commissionRate * 1000) / 10));
    if (val === null) return;
    const pct = Number(val.replace(',', '.'));
    if (Number.isNaN(pct) || pct < 0 || pct > 100) { alert('Podaj wartość 0–100'); return; }
    this.api.patch(`/catalog/stores/${s.id}`, { commissionRate: this.pct(pct) }).subscribe({ next: () => this.reload(), error: e => alert(err(e)) });
  }

  // % -> ułamek (10 -> 0.10, 12.5 -> 0.125)
  private pct(p: number): number { return (Number(p) || 0) / 100; }

  private reload() { this.load(); this.changed.emit(); }
}

function err(e: any): string { return 'Błąd: ' + (e?.error?.detail ?? e?.message ?? 'nieznany'); }
