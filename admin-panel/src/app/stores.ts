import { Component, EventEmitter, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, StoreDto } from './api';
import { ConfigModalComponent } from './config-modal';

@Component({
  selector: 'app-stores',
  imports: [CommonModule, FormsModule, ConfigModalComponent],
  styles: [`
    .rowlink { cursor:pointer; transition:background .12s; }
    .rowlink:hover { background:#f4faf9; }
    .rowlink:hover .link { color:#0C7D7E; }
    .link { color:#0F2A2A; }
    .accept-ok { color:#128040; font-weight:600; }
    .accept-no { color:#EF4444; font-weight:600; }
    .right { text-align:right; } .chev { color:#9aa8a8; font-size:18px; }
    .kv { display:grid; grid-template-columns:1fr 1fr; gap:12px 20px; }
    @media (max-width:520px){ .kv { grid-template-columns:1fr; } }
    .kv > div { display:flex; flex-direction:column; }
    .kv span { font-size:12px; color:#6B7280; } .kv b { color:#0F2A2A; }
    .act { border-top:1px solid #eef0f3; margin-top:16px; padding-top:14px; }
    .act .lbl { display:block; font-size:13px; font-weight:600; color:#3A3F4B; margin:0 0 8px; }
    .act .row { display:flex; gap:10px; flex-wrap:wrap; align-items:center; }
    .act select { border:1px solid #E5E7EB; border-radius:8px; padding:8px 10px; font-size:14px; background:#fff; }
  `],
  template: `
  <div class="page-head">
    <h1>Sklepy</h1>
    <div class="controls">
      <button class="btn ghost sm" (click)="creating = !creating">{{ creating ? 'Ukryj formularz' : '+ Nowy sklep' }}</button>
      <button class="btn ghost sm" (click)="load()">Odśwież</button>
    </div>
  </div>

  @if (creating) {
    <div class="card pad" style="margin-bottom:16px">
      <div class="formgrid">
        <label>Nazwa<input [(ngModel)]="ns.name" placeholder="np. Piekarnia Poranek" /></label>
        <label>Miasto<input [(ngModel)]="ns.city" placeholder="np. Koło" /></label>
        <label>Adres<input [(ngModel)]="ns.address" placeholder="ul. Rynek 1" /></label>
        <label>Telefon<input [(ngModel)]="ns.phone" placeholder="600 100 200" /></label>
        <label>Prowizja (%)<input [(ngModel)]="ns.commissionPct" type="number" step="0.5" /></label>
        <label>Min. zamówienie (zł)<input [(ngModel)]="ns.minimumOrderValue" type="number" step="0.01" /></label>
        <label>Logo (URL)<input [(ngModel)]="ns.logoUrl" placeholder="https://…/logo.png" /></label>
        <label>Szerokość (lat)<input [(ngModel)]="ns.latitude" type="number" step="0.0001" placeholder="50.0614" /></label>
        <label>Długość (lng)<input [(ngModel)]="ns.longitude" type="number" step="0.0001" placeholder="19.9366" /></label>
        <button class="btn primary" (click)="create()">Utwórz sklep</button>
      </div>
    </div>
  }

  <div class="card">
    @if (loading) { <div class="pad muted">Ładowanie…</div> }
    @else {
      <table>
        <thead>
          <tr><th>Nazwa</th><th>Miasto</th><th>Status</th><th>Przyjmuje</th><th class="right">Min. zam.</th><th class="right">Prowizja</th><th></th></tr>
        </thead>
        <tbody>
          @for (s of stores; track s.id) {
            <tr class="rowlink" (click)="open(s)">
              <td>
                <div style="display:flex;align-items:center;gap:8px">
                  @if (s.logoUrl) { <img [src]="s.logoUrl" alt="logo" style="width:28px;height:28px;border-radius:6px;object-fit:cover;border:1px solid #E5E7EB" /> }
                  <span><strong class="link">{{ s.name }}</strong><br><span class="muted mono" style="font-size:11px">{{ s.slug }}</span></span>
                </div>
              </td>
              <td class="muted">{{ s.city }}</td>
              <td><span class="pill" [attr.data-status]="s.status === 'Open' ? 'Completed' : 'Cancelled'">{{ statusLabel(s.status) }}</span></td>
              <td><span [class]="s.isAcceptingOrders ? 'accept-ok' : 'accept-no'">{{ s.isAcceptingOrders ? 'TAK' : 'NIE' }}</span></td>
              <td class="right">{{ s.minimumOrderValue | number:'1.2-2' }} zł</td>
              <td class="right">{{ (s.commissionRate * 100) | number:'1.0-1' }}%</td>
              <td class="right"><span class="chev">›</span></td>
            </tr>
          }
          @if (stores.length === 0) { <tr><td colspan="7" class="muted pad">Brak sklepów. Dodaj „+ Nowy sklep".</td></tr> }
        </tbody>
      </table>
    }
  </div>

  <!-- Szczegóły sklepu -->
  <app-config-modal [open]="!!selected" [title]="selected?.name || 'Sklep'" [showFooter]="false" (close)="selected = null">
    @if (selected; as s) {
      <div class="kv">
        <div><span>Miasto</span><b>{{ s.city }}</b></div>
        <div><span>Adres</span><b>{{ s.address || '—' }}</b></div>
        <div><span>Telefon</span><b>{{ s.phone || '—' }}</b></div>
        <div><span>NIP</span><b>{{ s.nip || '—' }}</b></div>
        <div><span>Współrzędne</span><b>@if (s.latitude != null) { {{ s.latitude | number:'1.4-4' }}, {{ s.longitude | number:'1.4-4' }} } @else { — }</b></div>
        <div><span>Przyjmuje zamówienia</span><b [style.color]="s.isAcceptingOrders ? '#128040' : '#B4232A'">{{ s.isAcceptingOrders ? 'tak' : 'nie' }}</b></div>
        <div><span>Min. zamówienie</span><b>{{ s.minimumOrderValue | number:'1.2-2' }} zł</b></div>
        <div><span>Prowizja</span><b>{{ (s.commissionRate * 100) | number:'1.0-1' }}%</b></div>
        <div><span>Konto</span><b [style.color]="s.isActive ? '#128040' : '#B4232A'">{{ s.isActive ? 'aktywny' : 'nieaktywny' }}</b></div>
        <div><span>Identyfikator</span><b class="mono" style="font-size:12px">{{ s.slug }}</b></div>
      </div>

      <div class="act">
        <label class="lbl">Status sklepu</label>
        <div class="row">
          <select [ngModel]="s.status" (ngModelChange)="setStatus(s, $event)">
            <option value="Open">Otwarty</option>
            <option value="Closed">Zamknięty</option>
            <option value="TemporarilyUnavailable">Niedostępny</option>
          </select>
          <button class="btn ghost sm" (click)="editMinOrder(s)">Min. zamówienie</button>
          <button class="btn ghost sm" (click)="editCommission(s)">Prowizja</button>
          <button class="btn ghost sm" (click)="editBranding(s)">Logo / GPS</button>
          <button class="btn" [class.primary]="!s.isActive" (click)="toggleActive(s)">{{ s.isActive ? 'Dezaktywuj' : 'Aktywuj' }}</button>
        </div>
      </div>
    }
  </app-config-modal>
  `,
})
export class StoresComponent {
  private api = inject(Api);
  @Output() changed = new EventEmitter<void>();

  stores: StoreDto[] = [];
  loading = false;
  creating = false;
  selected: StoreDto | null = null;
  ns = { name: '', city: '', address: '', phone: '', commissionPct: 10, minimumOrderValue: 0, logoUrl: '', latitude: null as number | null, longitude: null as number | null };

  constructor() { this.load(); }

  open(s: StoreDto) { this.selected = s; }
  statusLabel(st: string) { return st === 'Open' ? 'Otwarty' : st === 'Closed' ? 'Zamknięty' : 'Niedostępny'; }

  load() {
    this.loading = true;
    this.api.getPublic<StoreDto[]>('/catalog/stores?onlyActive=false').subscribe({
      next: s => { this.stores = s; if (this.selected) this.selected = s.find(x => x.id === this.selected!.id) ?? null; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  create() {
    if (!this.ns.name.trim() || !this.ns.city.trim()) { alert('Podaj nazwę i miasto.'); return; }
    const body = {
      name: this.ns.name.trim(), city: this.ns.city.trim(),
      address: this.ns.address.trim() || null, phone: this.ns.phone.trim() || null,
      commissionRate: this.pct(this.ns.commissionPct), minimumOrderValue: Number(this.ns.minimumOrderValue) || 0,
      logoUrl: this.ns.logoUrl.trim() || null, latitude: this.ns.latitude, longitude: this.ns.longitude,
    };
    this.api.post('/catalog/stores', body).subscribe({
      next: () => {
        this.ns = { name: '', city: '', address: '', phone: '', commissionPct: 10, minimumOrderValue: 0, logoUrl: '', latitude: null, longitude: null };
        this.creating = false; this.reload();
      },
      error: e => alert(err(e)),
    });
  }

  editBranding(s: StoreDto) {
    const logo = prompt(`Logo (URL) dla "${s.name}" — puste = bez zmian, „-" = wyczyść`, s.logoUrl ?? '');
    if (logo === null) return;
    const coords = prompt(`Współrzędne „szerokość, długość" (np. 50.0614, 19.9366) — puste = bez zmian`,
      s.latitude != null ? `${s.latitude}, ${s.longitude}` : '');
    if (coords === null) return;
    const body: any = {};
    if (logo !== '') body.logoUrl = logo === '-' ? '' : logo.trim();
    if (coords.trim() !== '') {
      const parts = coords.split(',').map(x => Number(x.trim().replace(',', '.')));
      if (parts.length !== 2 || parts.some(Number.isNaN)) { alert('Podaj dwie liczby: szerokość, długość'); return; }
      body.latitude = parts[0]; body.longitude = parts[1];
    }
    if (Object.keys(body).length === 0) return;
    this.api.patch(`/catalog/stores/${s.id}`, body).subscribe({ next: () => this.reload(), error: e => alert(err(e)) });
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

  private pct(p: number): number { return (Number(p) || 0) / 100; }
  private reload() { this.load(); this.changed.emit(); }
}

function err(e: any): string { return 'Błąd: ' + (e?.error?.detail ?? e?.message ?? 'nieznany'); }
