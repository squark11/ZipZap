import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, StoreDto } from './api';

interface ReadyStep { key: string; label: string; done: boolean; required: boolean; hint: string; }
interface Readiness { storeId: string; readyToSell: boolean; steps: ReadyStep[]; }
interface Zone { id: string; name: string; deliveryFee: number; isActive: boolean; }
interface Slot {
  id: string; deliveryZoneId: string; date: string; startTime: string; endTime: string;
  maxOrders: number; remainingCapacity: number;
}

@Component({
  selector: 'app-onboarding',
  imports: [CommonModule, FormsModule],
  styles: [`
    .steps { display:flex; flex-direction:column; gap:8px; }
    .step { display:flex; align-items:flex-start; gap:10px; }
    .mark { width:22px; height:22px; border-radius:50%; display:inline-flex; align-items:center; justify-content:center;
            font-size:13px; font-weight:700; background:#FEECEC; color:#B4232A; flex:none; }
    .mark.on { background:#E9FBF0; color:#128040; }
    .lbl { display:block; font-size:13px; font-weight:600; color:#3A3F4B; margin:0 0 6px; }
    .ok-msg { color:#128040; font-size:13px; font-weight:600; margin-right:6px; }
    h3 { color:#3A3F4B; }
  `],
  template: `
  <div class="page-head">
    <h1>Start sklepu</h1>
    <div class="controls">
      @if (ready?.readyToSell) { <span class="ok-msg">✓ Gotowy do sprzedaży</span> }
      <button class="btn ghost sm" (click)="load()">Odśwież</button>
    </div>
  </div>

  <div class="card pad" style="margin-bottom:16px">
    <h2 style="font-size:16px;margin:0 0 10px">Gotowość sklepu</h2>
    @if (ready) {
      <div class="steps">
        @for (s of ready.steps; track s.key) {
          <div class="step">
            <span class="mark" [class.on]="s.done">{{ s.done ? '✓' : (s.required ? '!' : '·') }}</span>
            <div>
              <b>{{ s.label }}</b> @if (!s.required) { <span class="muted" style="font-size:12px">(opcjonalne)</span> }
              @if (!s.done) { <div class="muted" style="font-size:12px">{{ s.hint }}</div> }
            </div>
          </div>
        }
      </div>
      <div style="margin-top:14px">
        <button class="btn primary" [disabled]="publishing" (click)="publish()">Opublikuj sklep (Otwarty)</button>
        @if (!ready.readyToSell) { <span class="muted" style="margin-left:10px">Uzupełnij wymagane kroki (✗), aby przyjmować zamówienia.</span> }
      </div>
    } @else { <div class="muted">Ładowanie…</div> }
  </div>

  <div class="card pad" style="margin-bottom:16px">
    <div class="page-head" style="margin-bottom:6px">
      <h2 style="font-size:16px;margin:0">Profil sklepu</h2>
      <div class="controls">
        @if (savedP) { <span class="ok-msg">✓ Zapisano</span> }
        <button class="btn ghost sm" (click)="saveProfile()" [disabled]="savingP">Zapisz profil</button>
      </div>
    </div>
    <div class="formgrid">
      <label>Logo (URL)<input [(ngModel)]="prof.logoUrl" placeholder="https://…/logo.png" /></label>
      <label>Szerokość (lat)<input [(ngModel)]="prof.latitude" type="number" step="0.0001" placeholder="50.0614" /></label>
      <label>Długość (lng)<input [(ngModel)]="prof.longitude" type="number" step="0.0001" placeholder="19.9366" /></label>
      <label>Status
        <select [(ngModel)]="prof.status">
          <option value="Open">Otwarty</option>
          <option value="Closed">Zamknięty</option>
          <option value="TemporarilyUnavailable">Niedostępny</option>
        </select>
      </label>
    </div>
  </div>

  <div class="card pad">
    <h2 style="font-size:16px;margin:0 0 4px">Dostawa</h2>
    <p class="muted" style="margin:0 0 12px;font-size:13px">Klient wybiera strefę i termin przy zamówieniu — bez nich checkout nie zadziała.</p>

    <h3 style="font-size:14px;margin:6px 0">Strefy dostawy</h3>
    <table>
      <thead><tr><th>Nazwa</th><th class="right">Opłata</th><th>Aktywna</th></tr></thead>
      <tbody>
        @for (z of zones; track z.id) {
          <tr><td>{{ z.name }}</td><td class="right">{{ z.deliveryFee | number:'1.2-2' }} zł</td><td>{{ z.isActive ? 'tak' : 'nie' }}</td></tr>
        }
        @if (zones.length === 0) { <tr><td colspan="3" class="muted pad">Brak stref — dodaj pierwszą.</td></tr> }
      </tbody>
    </table>
    <div class="formgrid" style="margin-top:10px">
      <label>Nazwa strefy<input [(ngModel)]="nz.name" placeholder="np. Centrum" /></label>
      <label>Opłata (zł)<input [(ngModel)]="nz.fee" type="number" step="0.01" /></label>
      <label>Kody pocztowe (opc.)<input [(ngModel)]="nz.postal" placeholder="62-500, 62-510" /></label>
      <button class="btn" (click)="addZone()">Dodaj strefę</button>
    </div>

    <h3 style="font-size:14px;margin:18px 0 6px">Terminy dostaw</h3>
    <table>
      <thead><tr><th>Data</th><th>Okno</th><th>Strefa</th><th class="right">Wolne</th></tr></thead>
      <tbody>
        @for (s of slots; track s.id) {
          <tr><td>{{ s.date }}</td><td>{{ s.startTime.substring(0,5) }}–{{ s.endTime.substring(0,5) }}</td>
              <td class="muted">{{ zoneName(s.deliveryZoneId) }}</td><td class="right">{{ s.remainingCapacity }}</td></tr>
        }
        @if (slots.length === 0) { <tr><td colspan="4" class="muted pad">Brak terminów — dodaj pierwszy.</td></tr> }
      </tbody>
    </table>
    <div class="formgrid" style="margin-top:10px">
      <label>Strefa
        <select [(ngModel)]="ns.zoneId">
          <option [ngValue]="''">—</option>
          @for (z of zones; track z.id) { <option [ngValue]="z.id">{{ z.name }}</option> }
        </select>
      </label>
      <label>Data<input [(ngModel)]="ns.date" type="date" /></label>
      <label>Od<input [(ngModel)]="ns.start" type="time" /></label>
      <label>Do<input [(ngModel)]="ns.end" type="time" /></label>
      <label>Limit zamówień<input [(ngModel)]="ns.maxOrders" type="number" /></label>
      <button class="btn" (click)="addSlot()">Dodaj termin</button>
    </div>
  </div>
  `,
})
export class OnboardingComponent {
  private api = inject(Api);
  storeId = input<string>('');

  ready: Readiness | null = null;
  zones: Zone[] = [];
  slots: Slot[] = [];
  publishing = false;
  savingP = false;
  savedP = false;

  prof = { logoUrl: '', latitude: null as number | null, longitude: null as number | null, status: 'Open' };
  nz = { name: '', fee: 0, postal: '' };
  ns = { zoneId: '', date: '', start: '', end: '', maxOrders: 20 };

  constructor() {
    effect(() => { const id = this.storeId(); if (id) this.load(); });
  }

  load() {
    const id = this.storeId();
    this.api.get<Readiness>(`/stores/${id}/readiness`).subscribe({ next: r => this.ready = r, error: () => this.ready = null });
    this.api.getPublic<StoreDto>(`/catalog/stores/${id}`).subscribe(s => {
      this.prof = { logoUrl: s.logoUrl || '', latitude: s.latitude ?? null, longitude: s.longitude ?? null, status: s.status || 'Open' };
    });
    this.api.getPublic<Zone[]>(`/ordering/stores/${id}/zones`).subscribe(z => this.zones = z);
    this.api.getPublic<Slot[]>(`/ordering/stores/${id}/slots`).subscribe(s => this.slots = s);
  }

  saveProfile() {
    this.savingP = true; this.savedP = false;
    const body: any = { logoUrl: this.prof.logoUrl.trim() || '', status: this.prof.status };
    if (this.prof.latitude != null && this.prof.longitude != null) { body.latitude = this.prof.latitude; body.longitude = this.prof.longitude; }
    this.api.patch(`/catalog/stores/${this.storeId()}`, body).subscribe({
      next: () => { this.savingP = false; this.savedP = true; setTimeout(() => this.savedP = false, 2000); this.load(); },
      error: e => { this.savingP = false; alert(err(e)); },
    });
  }

  addZone() {
    if (!this.nz.name.trim()) { alert('Podaj nazwę strefy.'); return; }
    const postalCodes = this.nz.postal.split(',').map(x => x.trim()).filter(x => x.length > 0);
    this.api.post(`/ordering/stores/${this.storeId()}/zones`, {
      name: this.nz.name.trim(), deliveryFee: Number(this.nz.fee) || 0, postalCodes: postalCodes.length ? postalCodes : null,
    }).subscribe({ next: () => { this.nz = { name: '', fee: 0, postal: '' }; this.load(); }, error: e => alert(err(e)) });
  }

  addSlot() {
    if (!this.ns.zoneId) { alert('Wybierz strefę.'); return; }
    if (!this.ns.date || !this.ns.start || !this.ns.end) { alert('Podaj datę i godziny.'); return; }
    const t = (v: string) => (v.length === 5 ? v + ':00' : v);
    this.api.post(`/ordering/stores/${this.storeId()}/slots`, {
      deliveryZoneId: this.ns.zoneId, date: this.ns.date,
      startTime: t(this.ns.start), endTime: t(this.ns.end), maxOrders: Number(this.ns.maxOrders) || 1,
    }).subscribe({ next: () => { this.ns = { zoneId: '', date: '', start: '', end: '', maxOrders: 20 }; this.load(); }, error: e => alert(err(e)) });
  }

  publish() {
    this.publishing = true;
    this.api.patch(`/catalog/stores/${this.storeId()}`, { status: 'Open', isActive: true }).subscribe({
      next: () => { this.publishing = false; this.load(); },
      error: e => { this.publishing = false; alert(err(e)); },
    });
  }

  zoneName(id: string) { return this.zones.find(z => z.id === id)?.name ?? '—'; }
}

function err(e: any): string { return 'Błąd: ' + (e?.error?.detail ?? e?.message ?? 'nieznany'); }
