import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api } from './api';
import { RouteLink, routeLinks, telHref } from './maps';

export interface BoardDelivery {
  id: string; orderId: string; orderCode: string; customerCode: string; status: string;
  driverId?: string; driverName?: string; stopSequence?: number; version: number;
  deliveryDate?: string; windowStart?: string; windowEnd?: string;
  address?: string; phone?: string; itemCount: number; isTestOrder: boolean;
  assignedAtUtc?: string; pickedUpAtUtc?: string; deliveredAtUtc?: string;
}
interface Driver { id: string; fullName: string; }
interface Board { deliveries: BoardDelivery[]; drivers: Driver[]; }
interface Change { action: string; fromStatus: string; toStatus: string; stopSequence?: number; actor?: string; atUtc: string; }
interface Route { key: string; driverId: string; driverName: string; date?: string; start?: string; end?: string; stops: BoardDelivery[]; dirty: boolean; }

const STATUS: Record<string, string> = {
  AvailableForPickup: 'Nieprzypisana', Assigned: 'Przypisana', InTransit: 'W drodze', Delivered: 'Dostarczona',
};
const ACTION: Record<string, string> = {
  assigned: 'przypisano', unassigned: 'zdjęto przypisanie', reordered: 'zmieniono kolejność', picked_up: 'odebrano', delivered: 'dostarczono',
};

@Component({
  selector: 'app-deliveries',
  imports: [CommonModule, FormsModule],
  styles: [`
    .bar2 { display:flex; gap:10px; align-items:center; flex-wrap:wrap; margin-bottom:14px; }
    .bar2 input[type=date] { padding:8px 10px; border:1px solid var(--border); border-radius:10px; font:inherit; }
    .fchip { background:#fff; border:1px solid var(--border); border-radius:999px; padding:6px 12px; font-weight:600; font-size:13px; color:var(--text); }
    .fchip.on { border-color:var(--zz-orange); color:var(--zz-orange); background:rgba(20,185,186,.08); }
    .fchip b { margin-left:6px; font-variant-numeric:tabular-nums; }
    .sec { margin-bottom:18px; }
    .sec h2 { font-size:15px; margin:0; padding:14px 18px; border-bottom:1px solid var(--border); display:flex; gap:10px; align-items:center; flex-wrap:wrap; }
    .sec h2 .grow { flex:1; }
    .stop { display:grid; grid-template-columns:34px minmax(120px,.9fr) minmax(180px,1.6fr) auto; gap:8px 14px; align-items:center; padding:11px 18px; border-bottom:1px solid var(--border); }
    .stop:last-child { border-bottom:0; }
    .no { width:28px; height:28px; border-radius:50%; background:var(--zz-orange); color:#fff; font-weight:700; display:grid; place-items:center; font-variant-numeric:tabular-nums; }
    .no.off { background:#CBD2DD; }
    .code { font-family:ui-monospace,monospace; font-weight:700; color:var(--blue); }
    .sub { color:var(--muted); font-size:12.5px; }
    .acts { display:flex; gap:6px; flex-wrap:wrap; justify-content:flex-end; align-items:center; }
    .acts select { padding:6px 8px; border:1px solid var(--border); border-radius:8px; font:inherit; font-size:13px; max-width:190px; }
    .maplink { font-weight:600; font-size:13px; color:var(--zz-orange-600); }
    .note { background:#FFF3EA; color:#C2560A; border-radius:10px; padding:10px 14px; font-size:13px; margin-bottom:14px; }
    .err { background:#FEECEC; color:#B4232A; border-radius:10px; padding:10px 14px; font-size:13px; margin-bottom:14px; }
    .hist { grid-column:2 / -1; margin:0; padding-left:18px; font-size:12.5px; color:var(--muted); }
    .badge-test { background:#EAF1FE; color:var(--blue); font-size:11px; font-weight:700; padding:2px 7px; border-radius:6px; }
    .empty { padding:22px 18px; color:var(--muted); }
    @media (max-width: 760px) { .stop { grid-template-columns:34px 1fr; } .stop .acts { grid-column:1 / -1; justify-content:flex-start; } }
  `],
  template: `
  <div class="page-head">
    <h1>Dostawy</h1>
    <div class="controls"><button class="btn ghost sm" (click)="load()">Odśwież</button></div>
  </div>

  <div class="bar2">
    <label class="sub" for="dday">Dzień dostawy</label>
    <input type="date" id="dday" [(ngModel)]="day" (ngModelChange)="load()" />
    @for (f of filters; track f.key) {
      <button class="fchip" [class.on]="filter === f.key" (click)="filter = f.key">{{ f.label }}<b>{{ count(f.key) }}</b></button>
    }
  </div>

  @if (!storeId()) { <div class="card empty">Wybierz sklep, aby planować dostawy.</div> }
  @else if (error) { <div class="err">{{ error }} <button class="btn sm" (click)="load()">Spróbuj ponownie</button></div> }
  @else if (loading && !board) { <div class="card empty">Wczytywanie dostaw…</div> }
  @else if (board) {
    @if (!board.drivers.length) {
      <div class="note">Brak aktywnych kierowców przypisanych do tego sklepu — przypisanie dostaw nie jest możliwe. Dodaj kierowcę w „Zespół” lub poproś administratora o zatwierdzenie dostawcy.</div>
    }
    @if (message) { <div class="err">{{ message }}</div> }

    @if (!board.deliveries.length) {
      <div class="card empty">Brak dostaw na {{ day }}. Dostawa pojawia się, gdy zamówienie zostanie oznaczone jako „gotowe do odbioru”.</div>
    } @else if (!shown().length) {
      <div class="card empty">Brak dostaw o wybranym statusie.</div>
    } @else {
      @if (show('AvailableForPickup')) {
        <section class="card sec">
          <h2>Nieprzypisane <span class="sub">({{ unassigned().length }})</span></h2>
          @if (!unassigned().length) { <div class="empty">Wszystkie dostawy z tego dnia są przypisane.</div> }
          @for (d of unassigned(); track d.id) {
            <div class="stop">
              <span class="no off">–</span>
              <div><span class="code">#{{ d.orderCode }}</span> <div class="sub">okno {{ win(d) }} · {{ d.itemCount }} poz.
                @if (d.isTestOrder) { <span class="badge-test">testowe</span> }</div></div>
              <div>{{ d.address || '—' }} <div class="sub"><a [href]="tel(d.phone)">{{ d.phone }}</a></div></div>
              <div class="acts">
                <select [(ngModel)]="pick[d.id]" [attr.aria-label]="'Kierowca dla #' + d.orderCode" [disabled]="!board.drivers.length">
                  <option [ngValue]="undefined">— kierowca —</option>
                  @for (k of board.drivers; track k.id) { <option [ngValue]="k.id">{{ k.fullName }}</option> }
                </select>
                <button class="btn primary sm" [disabled]="!pick[d.id] || busy" (click)="assign(d)">Przypisz</button>
                <button class="btn ghost sm" (click)="toggleHistory(d)">Historia</button>
              </div>
              @if (history[d.id]; as h) { <ol class="hist">@for (c of h; track $index) { <li>{{ c.atUtc | date:'dd.MM HH:mm' }} · {{ c.actor || '—' }}: {{ act(c.action) }}</li> } @empty { <li>Brak zmian.</li> }</ol> }
            </div>
          }
        </section>
      }

      @for (r of routes(); track r.key) {
        <section class="card sec">
          <h2>{{ r.driverName }} <span class="sub">okno {{ r.start ? (hm(r.start) + '–' + hm(r.end)) : 'bez terminu' }} · {{ r.stops.length }} przyst.</span>
            <span class="grow"></span>
            @for (l of links(r); track l.url) { <a class="maplink" [href]="l.url" target="_blank" rel="noopener noreferrer">{{ l.label }} ↗</a> }
            @if (r.dirty) { <button class="btn primary sm" [disabled]="busy" (click)="saveOrder(r)">Zapisz kolejność</button>
              <button class="btn ghost sm" (click)="load()">Cofnij</button> }
          </h2>
          @for (d of r.stops; track d.id; let i = $index; let last = $last) {
            <div class="stop">
              <span class="no">{{ r.dirty ? i + 1 : (d.stopSequence ?? i + 1) }}</span>
              <div><span class="code">#{{ d.orderCode }}</span> <span class="pill" [attr.data-status]="pillStatus(d.status)">{{ label(d.status) }}</span>
                <div class="sub">{{ d.itemCount }} poz. @if (d.isTestOrder) { <span class="badge-test">testowe</span> }</div></div>
              <div>{{ d.address || '—' }} <div class="sub"><a [href]="tel(d.phone)">{{ d.phone }}</a></div></div>
              <div class="acts">
                <button class="btn ghost sm" [disabled]="i === 0" (click)="move(r, i, -1)" [attr.aria-label]="'Wyżej: #' + d.orderCode">↑</button>
                <button class="btn ghost sm" [disabled]="last" (click)="move(r, i, 1)" [attr.aria-label]="'Niżej: #' + d.orderCode">↓</button>
                @if (d.status === 'Assigned') { <button class="btn ghost sm" [disabled]="busy || r.dirty" (click)="unassign(d)">Zdejmij</button> }
                <button class="btn ghost sm" (click)="toggleHistory(d)">Historia</button>
              </div>
              @if (history[d.id]; as h) { <ol class="hist">@for (c of h; track $index) { <li>{{ c.atUtc | date:'dd.MM HH:mm' }} · {{ c.actor || '—' }}: {{ act(c.action) }}@if (c.stopSequence) { (przystanek {{ c.stopSequence }}) }</li> }</ol> }
            </div>
          }
        </section>
      }

      @if (show('Delivered') && delivered().length) {
        <section class="card sec">
          <h2>Dostarczone <span class="sub">({{ delivered().length }})</span></h2>
          @for (d of delivered(); track d.id) {
            <div class="stop">
              <span class="no off">✓</span>
              <div><span class="code">#{{ d.orderCode }}</span><div class="sub">{{ d.driverName }}</div></div>
              <div class="sub">{{ d.deliveredAtUtc | date:'dd.MM HH:mm' }}</div>
              <div class="acts"><button class="btn ghost sm" (click)="toggleHistory(d)">Historia</button></div>
              @if (history[d.id]; as h) { <ol class="hist">@for (c of h; track $index) { <li>{{ c.atUtc | date:'dd.MM HH:mm' }} · {{ c.actor || '—' }}: {{ act(c.action) }}</li> }</ol> }
            </div>
          }
        </section>
      }
    }
  }
  `,
})
export class DeliveriesComponent {
  private api = inject(Api);
  storeId = input<string>('');

  day = new Date().toLocaleDateString('sv-SE'); // RRRR-MM-DD w strefie przeglądarki
  board: Board | null = null;
  loading = false;
  busy = false;
  error = '';
  message = '';
  filter = 'all';
  pick: Record<string, string | undefined> = {};
  history: Record<string, Change[] | undefined> = {};
  private routeCache: Route[] = [];

  readonly filters = [
    { key: 'all', label: 'Wszystkie' }, { key: 'AvailableForPickup', label: 'Nieprzypisane' },
    { key: 'Assigned', label: 'Przypisane' }, { key: 'InTransit', label: 'W drodze' }, { key: 'Delivered', label: 'Dostarczone' },
  ];

  constructor() {
    effect(() => { if (this.storeId()) this.load(); });
  }

  load() {
    const id = this.storeId();
    if (!id) return;
    this.loading = true; this.error = '';
    this.api.get<Board>(`/delivery/stores/${id}/board?date=${this.day}`).subscribe({
      next: b => { this.board = b; this.loading = false; this.routeCache = this.buildRoutes(b); this.history = {}; },
      error: e => { this.loading = false; this.board = null; this.error = e?.error?.detail ?? 'Nie udało się wczytać dostaw.'; },
    });
  }

  // ---------- Widok ----------
  show(status: string) { return this.filter === 'all' || this.filter === status; }
  count(key: string) { const d = this.board?.deliveries ?? []; return key === 'all' ? d.length : d.filter(x => x.status === key).length; }
  shown() { return (this.board?.deliveries ?? []).filter(d => this.show(d.status)); }
  unassigned() { return (this.board?.deliveries ?? []).filter(d => d.status === 'AvailableForPickup'); }
  delivered() { return (this.board?.deliveries ?? []).filter(d => d.status === 'Delivered'); }
  routes() { return this.routeCache.filter(r => r.stops.some(s => this.show(s.status))); }

  private buildRoutes(b: Board): Route[] {
    const map = new Map<string, Route>();
    for (const d of b.deliveries.filter(x => x.status === 'Assigned' || x.status === 'InTransit')) {
      const key = [d.driverId, d.deliveryDate, d.windowStart, d.windowEnd].join('|');
      if (!map.has(key)) map.set(key, { key, driverId: d.driverId!, driverName: d.driverName ?? 'kierowca', date: d.deliveryDate,
        start: d.windowStart, end: d.windowEnd, stops: [], dirty: false });
      map.get(key)!.stops.push(d);
    }
    for (const r of map.values()) r.stops.sort((a, c) => (a.stopSequence ?? 999) - (c.stopSequence ?? 999));
    return [...map.values()].sort((a, c) => (a.start ?? '').localeCompare(c.start ?? '') || a.driverName.localeCompare(c.driverName, 'pl'));
  }

  links(r: Route): RouteLink[] { return routeLinks(r.stops.map(s => s.address ?? '')); }

  // ---------- Akcje ----------
  assign(d: BoardDelivery) {
    this.run(this.api.post(`/delivery/stores/${this.storeId()}/deliveries/${d.id}/assign`,
      { driverId: this.pick[d.id], expectedVersion: d.version }));
  }

  unassign(d: BoardDelivery) {
    this.run(this.api.post(`/delivery/stores/${this.storeId()}/deliveries/${d.id}/unassign`, { expectedVersion: d.version }));
  }

  move(r: Route, i: number, delta: number) {
    const s = r.stops; [s[i], s[i + delta]] = [s[i + delta], s[i]];
    r.dirty = true;
  }

  saveOrder(r: Route) {
    this.run(this.api.put(`/delivery/stores/${this.storeId()}/route`, {
      driverId: r.driverId, deliveryDate: r.date ?? null, windowStart: r.start ?? null, windowEnd: r.end ?? null,
      stops: r.stops.map(s => ({ deliveryId: s.id, expectedVersion: s.version })),
    }));
  }

  toggleHistory(d: BoardDelivery) {
    if (this.history[d.id]) { delete this.history[d.id]; return; }
    this.api.get<Change[]>(`/delivery/stores/${this.storeId()}/deliveries/${d.id}/history`)
      .subscribe({ next: h => this.history[d.id] = h, error: () => this.history[d.id] = [] });
  }

  /// Po każdej zmianie przeładowujemy tablicę; konflikt (ktoś zmienił wcześniej) pokazuje komunikat.
  private run(req: import('rxjs').Observable<unknown>) {
    this.busy = true; this.message = '';
    req.subscribe({
      next: () => { this.busy = false; this.load(); },
      error: e => { this.busy = false; this.message = e?.error?.detail ?? 'Nie udało się zapisać zmiany.'; this.load(); },
    });
  }

  // ---------- Format ----------
  label(s: string) { return STATUS[s] ?? s; }
  act(a: string) { return ACTION[a] ?? a; }
  pillStatus(s: string) { return s === 'InTransit' ? 'InDelivery' : s === 'Assigned' ? 'Confirmed' : s; }
  hm(t?: string) { return (t ?? '').substring(0, 5); }
  win(d: BoardDelivery) { return d.windowStart ? `${this.hm(d.windowStart)}–${this.hm(d.windowEnd)}` : 'bez terminu'; }
  tel(p?: string) { return telHref(p); }
}
