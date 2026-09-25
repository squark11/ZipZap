import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, ProductDto } from './api';

// ---- Kontrakty API (moduł Ordering: rundy zakupowe i kompletacja) ----
export interface RoundInfo {
  localDate: string; localTime: string; cutoffLocalDate: string; cutoffLocalTime: string;
  timeZone: string; startsAtUtc: string; cutoffAtUtc: string;
}
export interface RoundProgress { lines: number; pending: number; bought: number; unavailable: number; substituted: number; }
export interface RoundSummary {
  id: string | null; round: RoundInfo; state: string; stateLabel: string;
  orderCount: number; excludedOrderCount: number; progress: RoundProgress;
}
export interface Pick {
  orderItemId: string; status: PickStatus; pickedQuantity: number;
  substituteProductId?: string; substituteProductName?: string; substituteUnit?: string; substituteQuantity?: number;
  note?: string; version: number; updatedBy?: string; updatedAtUtc?: string;
}
export interface PickLine {
  orderId: string; orderCode: string; customerCode: string; orderItemId: string;
  productId: string; productName: string; unit: string; quantity: number; editable: boolean; pick: Pick;
}
export interface ShoppingRow {
  productId: string; productName: string; unit: string; orderedQuantity: number; boughtQuantity: number;
  pendingLines: number; boughtLines: number; unavailableLines: number; substitutedLines: number; lines: PickLine[];
}
export interface RoundOrder {
  orderId: string; orderCode: string; customerCode: string; status: string; isTestOrder: boolean;
  included: boolean; excludedReason?: string; editable: boolean;
  deliveryDate?: string; deliveryStartTime?: string; deliveryEndTime?: string; items: PickLine[];
}
export interface RoundDetail { summary: RoundSummary; shoppingList: ShoppingRow[]; orders: RoundOrder[]; }
export interface PickChange {
  version: number; fromStatus: PickStatus; status: PickStatus; pickedQuantity: number;
  substituteProductName?: string; substituteUnit?: string; substituteQuantity?: number;
  note?: string; changedBy?: string; changedAtUtc: string;
}
export type PickStatus = 'Pending' | 'Bought' | 'Unavailable' | 'Substituted';

interface Editor {
  status: PickStatus; qty: number; subId: string; subQty: number; note: string;
  saving: boolean; error: string; history?: PickChange[]; historyOpen: boolean;
}

const PICK_LABEL: Record<PickStatus, string> = {
  Pending: 'Oczekuje', Bought: 'Kupiono', Unavailable: 'Niedostępne', Substituted: 'Zastąpiono',
};
const ORDER_LABEL: Record<string, string> = {
  Placed: 'Złożone', Confirmed: 'Potwierdzone', Picking: 'Kompletowane', ReadyForPickup: 'Gotowe do odbioru',
  InDelivery: 'W dostawie', Delivered: 'Dostarczone', Completed: 'Zakończone', Cancelled: 'Anulowane',
};
const WEEKDAY = ['nd', 'pn', 'wt', 'śr', 'cz', 'pt', 'sb'];

@Component({
  selector: 'app-rounds',
  imports: [CommonModule, FormsModule],
  styles: [`
    .rounds-grid { display:grid; grid-template-columns:268px 1fr; gap:20px; align-items:start; }
    @media (max-width: 860px) { .rounds-grid { grid-template-columns:1fr; } }
    .rlist { padding:8px; display:flex; flex-direction:column; gap:2px; }
    .rlist h3 { margin:10px 10px 4px; font-size:11px; text-transform:uppercase; letter-spacing:.06em; color:var(--muted); }
    .rbtn { width:100%; text-align:left; background:transparent; border-radius:11px; padding:10px 12px; color:var(--text);
            display:grid; grid-template-columns:1fr auto; gap:2px 8px; }
    .rbtn:hover { background:#F3F5F9; }
    .rbtn.active { background:rgba(20,185,186,.1); }
    .rbtn .when { font-weight:700; font-variant-numeric:tabular-nums; white-space:nowrap; }
    .ml { margin-left:6px; }
    .rbtn .meta { grid-column:1 / -1; font-size:12px; color:var(--muted); }
    .rbtn:focus-visible, .seg button:focus-visible, .linkbtn:focus-visible { outline:2px solid var(--zz-orange); outline-offset:2px; }

    .rhead { padding:18px 22px; border-bottom:1px solid var(--border); display:flex; flex-wrap:wrap; gap:10px 18px; align-items:center; }
    .rhead h2 { margin:0; font-size:18px; font-variant-numeric:tabular-nums; }
    .rhead .sub { color:var(--muted); font-size:13px; }
    .grow { flex:1; }
    .bar { height:8px; border-radius:999px; background:#EEF2F8; overflow:hidden; min-width:160px; flex:1; max-width:320px; }
    .bar > span { display:block; height:100%; background:var(--zz-orange); }
    .progress { display:flex; align-items:center; gap:10px; font-size:13px; color:var(--muted); font-variant-numeric:tabular-nums; }

    .tabs { display:flex; gap:4px; padding:10px 16px 0; border-bottom:1px solid var(--border); }
    .tabs button { background:transparent; padding:9px 14px; border-radius:10px 10px 0 0; font-weight:600; color:var(--muted); border-bottom:2px solid transparent; }
    .tabs button.on { color:var(--zz-orange); border-bottom-color:var(--zz-orange); }

    .qty { font-variant-numeric:tabular-nums; font-weight:700; }
    .expand { background:transparent; color:var(--muted); width:28px; }

    .line { display:grid; grid-template-columns:minmax(150px,1.1fr) minmax(160px,1.4fr) auto; gap:10px 14px; align-items:center;
            padding:10px 14px; border-top:1px solid var(--border); background:#FBFCFE; }
    .line.nested { padding-left:44px; }
    .who { font-size:12px; color:var(--muted); }
    .code { font-family:ui-monospace, monospace; font-weight:700; color:var(--blue); }
    .quick { display:flex; gap:6px; flex-wrap:wrap; justify-content:flex-end; }
    .quick .btn { white-space:nowrap; }
    @media (max-width: 600px) { .line { grid-template-columns:1fr; } .line.nested { padding-left:14px; } .quick { justify-content:flex-start; } }
    .sub { color:var(--muted); font-size:12px; }
    .editor { grid-column:1 / -1; background:#fff; border:1px solid var(--border); border-radius:12px; padding:12px 14px;
              display:flex; flex-direction:column; gap:10px; }
    .seg { display:inline-flex; border:1px solid var(--border); border-radius:10px; overflow:hidden; flex-wrap:wrap; }
    .seg button { background:#fff; padding:7px 12px; font-size:13px; font-weight:600; color:var(--text); border-right:1px solid var(--border); }
    .seg button:last-child { border-right:0; }
    .seg button.on { background:var(--zz-orange); color:#fff; }
    .row { display:flex; gap:10px; flex-wrap:wrap; align-items:end; }
    .row label { display:flex; flex-direction:column; gap:4px; font-size:12px; color:var(--muted); font-weight:600; }
    .row input, .row select { padding:8px 10px; border:1px solid var(--border); border-radius:9px; font-size:14px; background:#fff; color:var(--text); }
    .row input[type=number] { width:96px; }
    .row .wide { min-width:220px; flex:1; }
    .err { color:#B4232A; background:#FEECEC; border-radius:8px; padding:8px 10px; font-size:13px; }
    .note { background:#F1F3F5; color:#6B7280; border-radius:8px; padding:8px 10px; font-size:12.5px; }
    .linkbtn { background:transparent; color:var(--zz-orange-600); font-weight:600; font-size:12.5px; padding:0; }
    .hist { margin:0; padding-left:18px; font-size:12.5px; color:var(--text); }
    .hist li { margin:3px 0; }

    .ocard { border-top:1px solid var(--border); }
    .ohead { display:flex; flex-wrap:wrap; gap:8px 14px; align-items:center; padding:12px 16px; }
    .excluded { opacity:.62; }
    .badge-test { background:#EAF1FE; color:#2563EB; font-size:11px; font-weight:700; padding:2px 7px; border-radius:6px; }
    .empty { padding:28px 22px; color:var(--muted); }
    .empty b { color:var(--text); display:block; margin-bottom:4px; }
  `],
  template: `
  <div class="page-head">
    <h1>Zakupy w rundach</h1>
    <div class="controls">
      <button class="btn ghost sm" (click)="loadList()">Odśwież</button>
    </div>
  </div>

  @if (!storeId()) {
    <div class="card empty"><b>Wybierz sklep</b>Rundy i listy zakupów są prowadzone osobno dla każdego sklepu.</div>
  } @else if (listError) {
    <div class="card empty"><b>Nie udało się wczytać rund</b>{{ listError }}</div>
  } @else if (loadingList && !rounds.length) {
    <div class="card empty">Wczytywanie rund…</div>
  } @else if (!rounds.length) {
    <div class="card empty"><b>Brak rund</b>Harmonogram nie ma najbliższej rundy (sprawdź Konfiguracja → Rundy zakupowe) i sklep nie ma zamówień z ostatnich dni.</div>
  } @else {
    <div class="rounds-grid">
      <nav class="card rlist" aria-label="Rundy zakupowe">
        @for (g of groups(); track g.label) {
          <h3>{{ g.label }}</h3>
          @for (r of g.items; track key(r)) {
            <button class="rbtn" [class.active]="key(r) === selectedKey" (click)="select(r)">
              <span class="when">{{ dayLabel(r.round.localDate) }}, {{ hm(r.round.localTime) }}</span>
              <span class="state" [class]="'state st-' + r.state">{{ r.stateLabel }}</span>
              <span class="meta">
                {{ r.orderCount }} {{ plural(r.orderCount, 'zamówienie', 'zamówienia', 'zamówień') }}
                @if (r.progress.lines) { · {{ r.progress.lines - r.progress.pending }}/{{ r.progress.lines }} poz. }
                @if (r.excludedOrderCount) { · {{ r.excludedOrderCount }} poza zakupami }
              </span>
            </button>
          }
        }
      </nav>

      <section class="card">
        @if (selected; as s) {
          <div class="rhead">
            <div>
              <h2>Runda {{ dayLabel(s.round.localDate) }}, {{ hm(s.round.localTime) }}</h2>
              <div class="sub">Zamówienia przyjmowane do {{ cutoffLabel(s.round) }} · czas {{ s.round.timeZone }}</div>
            </div>
            <span class="state" [class]="'state st-' + s.state">{{ s.stateLabel }}</span>
            <span class="grow"></span>
            @if (s.progress.lines) {
              <div class="progress" [attr.aria-label]="'Skompletowano ' + (s.progress.lines - s.progress.pending) + ' z ' + s.progress.lines">
                <div class="bar"><span [style.width.%]="100 * (s.progress.lines - s.progress.pending) / s.progress.lines"></span></div>
                {{ s.progress.lines - s.progress.pending }}/{{ s.progress.lines }} pozycji
              </div>
            }
          </div>

          @if (!s.id) {
            <div class="empty"><b>Brak zamówień w tej rundzie</b>Zamówienia złożone przed {{ cutoffLabel(s.round) }} pojawią się tutaj automatycznie.</div>
          } @else if (detailError) {
            <div class="empty"><b>Nie udało się wczytać rundy</b>{{ detailError }}</div>
          } @else if (!detail) {
            <div class="empty">Wczytywanie rundy…</div>
          } @else {
            <div class="tabs" role="tablist">
              <button role="tab" [class.on]="tab === 'list'" [attr.aria-selected]="tab === 'list'" (click)="tab = 'list'">Lista zakupów ({{ detail.shoppingList.length }})</button>
              <button role="tab" [class.on]="tab === 'orders'" [attr.aria-selected]="tab === 'orders'" (click)="tab = 'orders'">Zamówienia ({{ detail.orders.length }})</button>
            </div>

            @if (tab === 'list') {
              @if (!detail.shoppingList.length) {
                <div class="empty"><b>Nic do kupienia</b>
                  @if (detail.orders.length) { Zamówienia tej rundy czekają na płatność albo zostały anulowane — zobacz zakładkę „Zamówienia”. }
                  @else { Ta runda nie ma zamówień. }
                </div>
              } @else {
                <table>
                  <thead><tr><th></th><th>Produkt</th><th class="right">Do kupienia</th><th class="right">Kupiono</th><th>Stan pozycji</th></tr></thead>
                  <tbody>
                    @for (row of detail.shoppingList; track row.productId + row.unit) {
                      <tr>
                        <td><button class="expand" (click)="toggleRow(row)" [attr.aria-expanded]="openRows.has(rowKey(row))"
                              [attr.aria-label]="'Pokaż zamówienia: ' + row.productName">{{ openRows.has(rowKey(row)) ? '▾' : '▸' }}</button></td>
                        <td><b>{{ row.productName }}</b><span class="sub ml">({{ row.lines.length }} {{ plural(row.lines.length, 'zamówienie', 'zamówienia', 'zamówień') }})</span></td>
                        <td class="right qty">{{ qu(row.orderedQuantity, row.unit) }}</td>
                        <td class="right qty">{{ qu(row.boughtQuantity, row.unit) }}</td>
                        <td><span class="chips">
                          @if (row.pendingLines) { <span class="chip c-Pending">{{ row.pendingLines }} oczekuje</span> }
                          @if (row.boughtLines) { <span class="chip c-Bought">{{ row.boughtLines }} kupiono</span> }
                          @if (row.unavailableLines) { <span class="chip c-Unavailable">{{ row.unavailableLines }} brak</span> }
                          @if (row.substitutedLines) { <span class="chip c-Substituted">{{ row.substitutedLines }} zamiana</span> }
                        </span></td>
                      </tr>
                      @if (openRows.has(rowKey(row))) {
                        <tr><td colspan="5" style="padding:0">
                          @for (l of row.lines; track l.orderItemId) {
                            <ng-container *ngTemplateOutlet="lineTpl; context: { $implicit: l, nested: true }"></ng-container>
                          }
                        </td></tr>
                      }
                    }
                  </tbody>
                </table>
              }
            } @else {
              @if (!detail.orders.length) {
                <div class="empty"><b>Brak zamówień</b>Ta runda nie ma przypisanych zamówień.</div>
              }
              @for (o of detail.orders; track o.orderId) {
                <div class="ocard" [class.excluded]="!o.included">
                  <div class="ohead">
                    <span class="code">#{{ o.orderCode }}</span>
                    <span class="who">Klient {{ o.customerCode }}</span>
                    <span class="pill" [attr.data-status]="o.status">{{ orderLabel(o.status) }}</span>
                    @if (o.isTestOrder) { <span class="badge-test">testowe — bez opłaty</span> }
                    <span class="grow"></span>
                    @if (o.deliveryDate) { <span class="sub">Dostawa {{ dayLabel(o.deliveryDate) }}, {{ hm(o.deliveryStartTime) }}–{{ hm(o.deliveryEndTime) }}</span> }
                  </div>
                  @if (!o.included) {
                    <div class="note" style="margin:0 16px 12px">Poza listą zakupów: {{ o.excludedReason }}.</div>
                  } @else {
                    @for (l of o.items; track l.orderItemId) {
                      <ng-container *ngTemplateOutlet="lineTpl; context: { $implicit: l, nested: false }"></ng-container>
                    }
                  }
                </div>
              }
            }
          }
        }
      </section>
    </div>
  }

  <!-- Jedna pozycja zamówienia: stan, szybkie akcje i edytor (ten sam w obu zakładkach). -->
  <ng-template #lineTpl let-l let-nested="nested">
    <div class="line" [class.nested]="nested">
      <div>
        @if (nested) { <span class="code">#{{ l.orderCode }}</span><span class="who ml">Klient {{ l.customerCode }}</span> }
        @else { <b>{{ l.productName }}</b> }
        <div class="sub">zamówiono <span class="qty">{{ qu(l.quantity, l.unit) }}</span></div>
      </div>
      <div>
        <span class="chip" [class]="'chip c-' + l.pick.status">{{ pickLabel(l.pick.status) }}</span>
        @if (l.pick.status === 'Bought') { <span class="qty ml">{{ qu(l.pick.pickedQuantity, l.unit) }}</span>
          @if (l.pick.pickedQuantity !== l.quantity) { <span class="sub ml">(inna ilość niż zamówiona)</span> } }
        @if (l.pick.status === 'Substituted') { <span class="sub ml">→ {{ l.pick.substituteProductName }}: {{ qu(l.pick.substituteQuantity ?? 0, l.pick.substituteUnit ?? 'szt') }}</span> }
        @if (l.pick.note) { <div class="sub">„{{ l.pick.note }}”</div> }
        @if (l.pick.version) { <div class="who">{{ l.pick.updatedBy || 'operator' }} · {{ l.pick.updatedAtUtc | date:'dd.MM HH:mm' }} · zmiana {{ l.pick.version }}</div> }
      </div>
      <div class="quick">
        @if (l.editable) {
          <button class="btn sm" [disabled]="ed(l).saving" (click)="quick(l, 'Bought')">Kupiono {{ l.quantity }}</button>
          <button class="btn sm" [disabled]="ed(l).saving" (click)="quick(l, 'Unavailable')">Brak</button>
          <button class="btn ghost sm" (click)="toggleEditor(l)" [attr.aria-expanded]="openEditors.has(l.orderItemId)">Więcej…</button>
        } @else {
          <span class="sub">tylko podgląd</span>
        }
      </div>
      @if (ed(l).error && !openEditors.has(l.orderItemId)) { <div class="err" style="grid-column:1 / -1">{{ ed(l).error }}</div> }
      @if (openEditors.has(l.orderItemId)) {
        <div class="editor">
          <div class="seg" role="radiogroup" aria-label="Stan pozycji">
            @for (st of statuses; track st) {
              <button role="radio" [class.on]="ed(l).status === st" [attr.aria-checked]="ed(l).status === st" (click)="ed(l).status = st">{{ pickLabel(st) }}</button>
            }
          </div>
          @if (ed(l).status === 'Bought') {
            <div class="row"><label [for]="'q-' + l.orderItemId">Kupiona ilość ({{ l.unit }})
              <input type="number" min="1" max="1000" [id]="'q-' + l.orderItemId" [(ngModel)]="ed(l).qty" /></label></div>
          }
          @if (ed(l).status === 'Substituted') {
            <div class="row">
              <label class="wide" [for]="'s-' + l.orderItemId">Produkt zastępczy
                <select [id]="'s-' + l.orderItemId" [(ngModel)]="ed(l).subId">
                  <option value="">— wybierz —</option>
                  @for (p of substitutes(l); track p.id) { <option [value]="p.id">{{ p.name }} ({{ p.unit }}, {{ p.price | number:'1.2-2' }} zł)</option> }
                </select></label>
              <label [for]="'sq-' + l.orderItemId">Ilość
                <input type="number" min="1" max="1000" [id]="'sq-' + l.orderItemId" [(ngModel)]="ed(l).subQty" /></label>
            </div>
            <div class="note">Zamiana nie zmienia cen ani kwoty zamówienia. Zasady zgody klienta i rozliczenia zamienników nie są jeszcze ustalone — opisz w notatce, jak uzgodniono zamianę.</div>
          }
          <div class="row"><label class="wide" [for]="'n-' + l.orderItemId">Notatka (opcjonalnie, maks. 300 znaków)
            <input [id]="'n-' + l.orderItemId" maxlength="300" [(ngModel)]="ed(l).note" placeholder="np. ostatnia sztuka, klient potwierdził telefonicznie" /></label></div>
          @if (ed(l).error) { <div class="err">{{ ed(l).error }}</div> }
          <div class="row" style="justify-content:space-between;align-items:center">
            <button class="linkbtn" (click)="toggleHistory(l)">{{ ed(l).historyOpen ? 'Ukryj historię' : 'Historia zmian' }}</button>
            <span>
              <button class="btn ghost sm" (click)="toggleEditor(l)">Anuluj</button>
              <button class="btn primary sm" [disabled]="ed(l).saving" (click)="save(l)">{{ ed(l).saving ? 'Zapisywanie…' : 'Zapisz' }}</button>
            </span>
          </div>
          @if (ed(l).historyOpen) {
            @if (!ed(l).history) { <div class="sub">Wczytywanie…</div> }
            @else if (!ed(l).history!.length) { <div class="sub">Brak zmian — pozycja oczekuje od złożenia zamówienia.</div> }
            @else {
              <ol class="hist">
                @for (h of ed(l).history!; track h.version) {
                  <li>{{ h.changedAtUtc | date:'dd.MM HH:mm' }} · {{ h.changedBy || 'operator' }}: {{ pickLabel(h.fromStatus) }} → <b>{{ pickLabel(h.status) }}</b>
                    @if (h.status === 'Bought') { ({{ h.pickedQuantity }}) }
                    @if (h.status === 'Substituted') { ({{ h.substituteProductName }} × {{ h.substituteQuantity }}) }
                    @if (h.note) { — „{{ h.note }}” }
                  </li>
                }
              </ol>
            }
          }
        </div>
      }
    </div>
  </ng-template>
  `,
})
export class RoundsComponent {
  private api = inject(Api);
  storeId = input<string>('');

  readonly statuses: PickStatus[] = ['Pending', 'Bought', 'Unavailable', 'Substituted'];
  rounds: RoundSummary[] = [];
  loadingList = false;
  listError = '';
  selectedKey = '';
  selected: RoundSummary | null = null;
  detail: RoundDetail | null = null;
  detailError = '';
  tab: 'list' | 'orders' = 'list';
  openRows = new Set<string>();
  openEditors = new Set<string>();
  private editors = new Map<string, Editor>();
  private products: ProductDto[] = [];

  constructor() {
    effect(() => {
      const id = this.storeId();
      this.rounds = []; this.selected = null; this.detail = null; this.selectedKey = '';
      if (id) { this.loadList(); this.loadProducts(id); }
    });
  }

  // ---------- Dane ----------

  loadList() {
    const id = this.storeId();
    if (!id) return;
    this.loadingList = true; this.listError = '';
    this.api.get<RoundSummary[]>(`/ordering/stores/${id}/purchasing-rounds?pastDays=3`).subscribe({
      next: list => {
        this.loadingList = false;
        this.rounds = list;
        const keep = list.find(r => this.key(r) === this.selectedKey);
        this.select(keep ?? this.defaultRound(list));
      },
      error: e => { this.loadingList = false; this.listError = e?.error?.detail ?? 'Brak dostępu lub błąd serwera.'; },
    });
  }

  private loadProducts(storeId: string) {
    this.api.getPublic<ProductDto[]>(`/catalog/stores/${storeId}/products`).subscribe({
      next: p => this.products = p, error: () => this.products = [],
    });
  }

  private loadDetail() {
    const s = this.selected;
    if (!s?.id) { this.detail = null; return; }
    this.detailError = '';
    this.api.get<RoundDetail>(`/ordering/stores/${this.storeId()}/purchasing-rounds/${s.id}`).subscribe({
      next: d => { this.detail = d; this.selected = d.summary; this.syncEditors(d); },
      error: e => this.detailError = e?.error?.detail ?? 'Błąd serwera.',
    });
  }

  /// Bieżąca runda: pierwsza z zamówieniami, która nie jest jeszcze skompletowana; inaczej najbliższa przyszła.
  private defaultRound(list: RoundSummary[]): RoundSummary | null {
    const now = Date.now();
    return list.find(r => r.id && (r.state === 'to_shop' || r.state === 'shopping'))
      ?? list.find(r => Date.parse(r.round.startsAtUtc) >= now - 2 * 3600_000)
      ?? list[list.length - 1] ?? null;
  }

  select(r: RoundSummary | null) {
    this.selected = r; this.selectedKey = r ? this.key(r) : '';
    this.detail = null; this.openRows.clear(); this.openEditors.clear(); this.editors.clear();
    this.loadDetail();
  }

  // ---------- Kompletacja ----------

  ed(l: PickLine): Editor {
    let e = this.editors.get(l.orderItemId);
    if (!e) {
      e = { status: l.pick.status, qty: l.pick.status === 'Bought' ? l.pick.pickedQuantity : l.quantity,
            subId: l.pick.substituteProductId ?? '', subQty: l.pick.substituteQuantity ?? l.quantity,
            note: l.pick.note ?? '', saving: false, error: '', historyOpen: false };
      this.editors.set(l.orderItemId, e);
    }
    return e;
  }

  /// Po odświeżeniu rundy zamknięte edytory przyjmują stan z serwera (otwarte zostają nietknięte).
  private syncEditors(d: RoundDetail) {
    for (const l of this.allLines(d)) if (!this.openEditors.has(l.orderItemId)) this.editors.delete(l.orderItemId);
  }

  toggleEditor(l: PickLine) {
    if (this.openEditors.has(l.orderItemId)) { this.openEditors.delete(l.orderItemId); this.editors.delete(l.orderItemId); }
    else { this.editors.delete(l.orderItemId); this.openEditors.add(l.orderItemId); }
  }

  quick(l: PickLine, status: PickStatus) {
    this.send(l, { status, pickedQuantity: status === 'Bought' ? l.quantity : 0, note: l.pick.note ?? null });
  }

  save(l: PickLine) {
    const e = this.ed(l);
    if (e.status === 'Substituted' && !e.subId) { e.error = 'Wybierz produkt zastępczy.'; return; }
    this.send(l, {
      status: e.status,
      pickedQuantity: e.status === 'Bought' ? Number(e.qty) : 0,
      substituteProductId: e.status === 'Substituted' ? e.subId : null,
      substituteQuantity: e.status === 'Substituted' ? Number(e.subQty) : null,
      note: e.note.trim() || null,
    });
  }

  private send(l: PickLine, body: Record<string, unknown>) {
    const e = this.ed(l);
    e.saving = true; e.error = '';
    const url = `/ordering/stores/${this.storeId()}/purchasing-rounds/${this.selected!.id}/items/${l.orderItemId}/pick`;
    this.api.put<Pick>(url, { ...body, expectedVersion: l.pick.version }).subscribe({
      next: () => { e.saving = false; this.openEditors.delete(l.orderItemId); this.editors.delete(l.orderItemId); this.refresh(); },
      error: err => {
        e.saving = false;
        e.error = err?.error?.detail ?? 'Nie udało się zapisać.';
        if (err?.status === 409) this.refresh(); // ktoś zmienił pozycję — pokaż aktualny stan, komunikat zostaje
      },
    });
  }

  toggleHistory(l: PickLine) {
    const e = this.ed(l);
    e.historyOpen = !e.historyOpen;
    if (!e.historyOpen) return;
    e.history = undefined;
    this.api.get<PickChange[]>(`/ordering/stores/${this.storeId()}/purchasing-rounds/${this.selected!.id}/items/${l.orderItemId}/pick-history`)
      .subscribe({ next: h => e.history = h, error: () => e.history = [] });
  }

  private refresh() { this.loadDetail(); this.refreshListQuietly(); }

  private refreshListQuietly() {
    this.api.get<RoundSummary[]>(`/ordering/stores/${this.storeId()}/purchasing-rounds?pastDays=3`)
      .subscribe({ next: list => this.rounds = list, error: () => {} });
  }

  substitutes(l: PickLine): ProductDto[] {
    return this.products.filter(p => p.id !== l.productId && p.isAvailable)
      .sort((a, b) => a.name.localeCompare(b.name, 'pl'));
  }

  // ---------- Widok ----------

  groups(): { label: string; items: RoundSummary[] }[] {
    const cut = Date.now() - 2 * 3600_000;
    const current = this.rounds.filter(r => Date.parse(r.round.startsAtUtc) >= cut || r.state === 'to_shop' || r.state === 'shopping');
    const past = this.rounds.filter(r => !current.includes(r)).reverse();
    return [
      ...(current.length ? [{ label: 'Bieżące i nadchodzące', items: current }] : []),
      ...(past.length ? [{ label: 'Ostatnie', items: past }] : []),
    ];
  }

  toggleRow(row: ShoppingRow) {
    const k = this.rowKey(row);
    this.openRows.has(k) ? this.openRows.delete(k) : this.openRows.add(k);
  }

  private allLines(d: RoundDetail): PickLine[] { return d.orders.flatMap(o => o.items); }
  key(r: RoundSummary) { return r.id ?? 'next-' + r.round.startsAtUtc; }
  rowKey(r: ShoppingRow) { return r.productId + '|' + r.unit; }
  pickLabel(s: PickStatus) { return PICK_LABEL[s] ?? s; }
  orderLabel(s: string) { return ORDER_LABEL[s] ?? s; }
  hm(t?: string) { return (t ?? '').substring(0, 5); }

  /// Ilość z jednostką: „3 kg", a dla jednostek-opakowań („100g") „2 × 100g" (nie „2 100g").
  qu(n: number, unit: string): string { return /^\d/.test(unit) ? `${n} × ${unit}` : `${n} ${unit}`; }

  dayLabel(iso: string): string {
    const [y, m, d] = iso.split('-').map(Number);
    const that = new Date(y, m - 1, d);
    const today = new Date(); today.setHours(0, 0, 0, 0);
    const diff = Math.round((that.getTime() - today.getTime()) / 86400_000);
    if (diff === 0) return 'Dziś';
    if (diff === 1) return 'Jutro';
    if (diff === -1) return 'Wczoraj';
    return `${WEEKDAY[that.getDay()]} ${String(d).padStart(2, '0')}.${String(m).padStart(2, '0')}`;
  }

  cutoffLabel(r: RoundInfo): string {
    const t = this.hm(r.cutoffLocalTime);
    return r.cutoffLocalDate === r.localDate ? t : `${this.dayLabel(r.cutoffLocalDate)}, ${t}`;
  }

  plural(n: number, one: string, few: string, many: string): string {
    if (n === 1) return one;
    const d = n % 10, dd = n % 100;
    return d >= 2 && d <= 4 && (dd < 12 || dd > 14) ? few : many;
  }
}
