import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Api } from './api';
import { RouteLink, routeLinks, telHref } from './maps';

/** Dostawa kierowcy — adres/telefon są tylko w dostawach przypisanych mu i w realizacji (decyduje serwer). */
interface MyDelivery {
  id: string; orderCode: string; status: string; stopSequence?: number; version: number;
  deliveryDate?: string; windowStart?: string; windowEnd?: string;
  address?: string; phone?: string; itemCount: number; isTestOrder: boolean;
  pickedUpAtUtc?: string; deliveredAtUtc?: string;
}
interface Group { key: string; label: string; stops: MyDelivery[]; }

@Component({
  selector: 'app-driver-home',
  imports: [CommonModule],
  styles: [`
    .grp { margin-bottom:16px; }
    .grp h2 { font-size:15px; margin:0; padding:14px 18px; border-bottom:1px solid var(--border); display:flex; gap:10px; flex-wrap:wrap; align-items:center; }
    .grp h2 .grow { flex:1; }
    .stop { display:grid; grid-template-columns:34px 1fr auto; gap:6px 14px; align-items:center; padding:12px 18px; border-bottom:1px solid var(--border); }
    .stop:last-child { border-bottom:0; }
    .no { width:28px; height:28px; border-radius:50%; background:var(--zz-orange); color:#fff; font-weight:700; display:grid; place-items:center; }
    .no.off { background:#CBD2DD; }
    .code { font-family:ui-monospace,monospace; font-weight:700; color:var(--blue); }
    .addr { font-weight:600; }
    .sub { color:var(--muted); font-size:12.5px; }
    .maplink { font-weight:600; font-size:13px; color:var(--zz-orange-600); }
    .err { background:#FEECEC; color:#B4232A; border-radius:10px; padding:10px 14px; font-size:13px; margin-bottom:14px; }
    .badge-test { background:#EAF1FE; color:var(--blue); font-size:11px; font-weight:700; padding:2px 7px; border-radius:6px; }
    .empty { padding:22px 18px; color:var(--muted); }
    @media (max-width: 600px) { .stop { grid-template-columns:34px 1fr; } .stop .btn { grid-column:1 / -1; } }
  `],
  template: `
  <div class="page-head">
    <h1>Moje dostawy</h1>
    <div class="controls"><button class="btn ghost sm" (click)="load()">Odśwież</button></div>
  </div>

  @if (message) { <div class="err">{{ message }}</div> }
  @if (error) { <div class="err">{{ error }} <button class="btn sm" (click)="load()">Spróbuj ponownie</button></div> }
  @else if (loading && !items) { <div class="card empty">Wczytywanie…</div> }
  @else if (items && !active().length && !done().length) {
    <div class="card empty">Nie masz przypisanych dostaw. Operator sklepu przydzieli Ci dostawy — pojawią się tutaj z adresem i kolejnością przystanków.</div>
  } @else if (items) {
    @for (g of groups(); track g.key) {
      <section class="card grp">
        <h2>{{ g.label }} <span class="sub">{{ g.stops.length }} przyst.</span><span class="grow"></span>
          @for (l of links(g); track l.url) { <a class="maplink" [href]="l.url" target="_blank" rel="noopener noreferrer">{{ l.label }} ↗</a> }
        </h2>
        @for (d of g.stops; track d.id; let i = $index) {
          <div class="stop">
            <span class="no">{{ d.stopSequence ?? i + 1 }}</span>
            <div>
              <div class="addr">{{ d.address }}</div>
              <div class="sub"><span class="code">#{{ d.orderCode }}</span> · {{ d.itemCount }} poz. ·
                <a [href]="tel(d.phone)">{{ d.phone }}</a>
                @if (d.isTestOrder) { · <span class="badge-test">testowe — bez pobierania płatności</span> }</div>
            </div>
            @if (d.status === 'Assigned') { <button class="btn primary sm" [disabled]="busy" (click)="step(d, 'pick-up', 'Potwierdzasz odbiór zamówienia #' + d.orderCode + '?')">Odebrano</button> }
            @else if (d.status === 'InTransit') { <button class="btn success sm" [disabled]="busy" (click)="step(d, 'delivered', 'Potwierdzasz dostarczenie zamówienia #' + d.orderCode + '?')">Dostarczono</button> }
          </div>
        }
      </section>
    }
    @if (done().length) {
      <section class="card grp">
        <h2>Dostarczone (ostatnia doba)</h2>
        @for (d of done(); track d.id) {
          <div class="stop"><span class="no off">✓</span>
            <div><span class="code">#{{ d.orderCode }}</span> <span class="sub">· {{ d.deliveredAtUtc | date:'dd.MM HH:mm' }}</span></div></div>
        }
      </section>
    }
  }
  `,
})
export class DriverHomeComponent implements OnInit {
  private api = inject(Api);
  items: MyDelivery[] | null = null;
  loading = false;
  busy = false;
  error = '';
  message = '';

  ngOnInit() { this.load(); }

  load() {
    this.loading = true; this.error = '';
    this.api.get<MyDelivery[]>('/delivery/mine').subscribe({
      next: d => { this.items = d; this.loading = false; },
      error: e => { this.loading = false; this.error = e?.error?.detail ?? 'Nie udało się wczytać dostaw.'; },
    });
  }

  active() { return (this.items ?? []).filter(d => d.status === 'Assigned' || d.status === 'InTransit'); }
  done() { return (this.items ?? []).filter(d => d.status === 'Delivered'); }

  groups(): Group[] {
    const map = new Map<string, Group>();
    for (const d of this.active()) {
      const key = `${d.deliveryDate}|${d.windowStart}|${d.windowEnd}`;
      if (!map.has(key)) map.set(key, { key, label: this.windowLabel(d), stops: [] });
      map.get(key)!.stops.push(d);
    }
    for (const g of map.values()) g.stops.sort((a, b) => (a.stopSequence ?? 999) - (b.stopSequence ?? 999));
    return [...map.values()];
  }

  links(g: Group): RouteLink[] { return routeLinks(g.stops.map(s => s.address ?? '')); }

  step(d: MyDelivery, action: 'pick-up' | 'delivered', question: string) {
    if (!confirm(question)) return;
    this.busy = true; this.message = '';
    this.api.post(`/delivery/${d.id}/${action}`, {}).subscribe({
      next: () => { this.busy = false; this.load(); },
      error: e => { this.busy = false; this.message = e?.error?.detail ?? 'Nie udało się zapisać.'; this.load(); },
    });
  }

  private windowLabel(d: MyDelivery): string {
    const hm = (t?: string) => (t ?? '').substring(0, 5);
    const date = d.deliveryDate ? new Date(d.deliveryDate + 'T00:00:00').toLocaleDateString('pl-PL', { weekday: 'short', day: '2-digit', month: '2-digit' }) : 'bez daty';
    return d.windowStart ? `${date}, okno ${hm(d.windowStart)}–${hm(d.windowEnd)}` : date;
  }

  tel(p?: string) { return telHref(p); }
}
