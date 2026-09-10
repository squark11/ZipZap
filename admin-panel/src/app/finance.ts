import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, LedgerEntryDto, PaymentsSummaryDto } from './api';

interface InvoiceDto {
  period: string; plan: string; basis: string; unitFee?: number;
  orderCount: number; total: number; currency: string;
  lines: { orderId: string; createdAtUtc: string; amount: number }[];
}
interface BillingDto { plan: string; }

@Component({
  selector: 'app-finance',
  imports: [CommonModule, FormsModule],
  template: `
  <div class="page-head">
    <h1>Rozliczenia</h1>
    <div class="controls"><button class="btn ghost sm" (click)="load()">Odśwież</button></div>
  </div>

  <div class="card pad" style="margin-bottom:16px">
    <div class="inv-head">
      <div>
        <div class="k">Faktura miesięczna ZipZap → sklep</div>
        <div class="muted" style="font-size:13px">
          Plan
          <select [(ngModel)]="plan" (ngModelChange)="savePlan()" style="margin:0 6px">
            <option value="A">A — dostawa ZipZap ({{ invoice?.unitFee ?? 25 | number:'1.2-2' }} zł/dostawę)</option>
            <option value="B">B — kurier sklepu (prowizja)</option>
          </select>
          · miesiąc
          <input type="month" [(ngModel)]="month" (ngModelChange)="loadInvoice()" style="margin-left:6px" />
        </div>
      </div>
      <div class="inv-total">
        <div class="muted" style="font-size:12px">Do zafakturowania</div>
        <div class="v accent">{{ (invoice?.total ?? 0) | number:'1.2-2' }} zł</div>
        <div class="muted" style="font-size:12px">
          {{ invoice?.orderCount ?? 0 }} {{ invoice?.basis === 'delivery' ? 'dostaw' : 'zamówień z prowizją' }}
        </div>
      </div>
    </div>
  </div>

  <div class="tiles">
    <div class="tile">
      <div class="k">Prowizja ZipZap (rozliczona)</div>
      <div class="v accent">{{ (summary?.settledCommission ?? 0) | number:'1.2-2' }} zł</div>
      <div class="muted">{{ ledger.length }} rozliczonych zamówień</div>
    </div>
    <div class="tile">
      <div class="k">Obrót opłacony</div>
      <div class="v">{{ (summary?.grossPaid ?? 0) | number:'1.2-2' }} zł</div>
      <div class="muted">płatności potwierdzone i rozliczone</div>
    </div>
    <div class="tile">
      <div class="k">Płatności opłacone</div>
      <div class="v green">{{ summary?.paid ?? 0 }}</div>
      <div class="muted">rozliczone: {{ summary?.settled ?? 0 }}</div>
    </div>
    <div class="tile">
      <div class="k">Oczekujące / nieudane</div>
      <div class="v blue">{{ summary?.pending ?? 0 }}</div>
      <div class="muted">nieudane: {{ summary?.failed ?? 0 }}</div>
    </div>
  </div>

  <div class="card">
    <div class="page-head"><h1 style="font-size:16px">Księga prowizji</h1></div>
    @if (loading) { <div class="pad muted">Ładowanie…</div> }
    @else if (ledger.length === 0) { <p class="pad muted">Brak rozliczonych zamówień. Prowizja księgowana jest po dostarczeniu.</p> }
    @else {
      <table>
        <thead><tr><th>Zamówienie</th><th class="right">Prowizja</th><th>Rozliczono</th></tr></thead>
        <tbody>
          @for (e of ledger; track e.orderId) {
            <tr>
              <td class="mono num">{{ e.orderId.substring(0,8) }}</td>
              <td class="right">{{ e.amount | number:'1.2-2' }} zł</td>
              <td class="muted">{{ e.createdAtUtc | date:'dd.MM HH:mm' }}</td>
            </tr>
          }
        </tbody>
      </table>
    }
  </div>
  `,
  styles: [`
    .inv-head { display:flex; justify-content:space-between; align-items:center; gap:16px; flex-wrap:wrap; }
    .inv-total { text-align:right; }
    select, input[type=month] { border:1px solid #E5E7EB; border-radius:6px; padding:4px 8px; font-size:13px; background:#fff; }
  `],
})
export class FinanceComponent {
  private api = inject(Api);
  storeId = input.required<string>();

  summary: PaymentsSummaryDto | null = null;
  ledger: LedgerEntryDto[] = [];
  loading = false;

  invoice: InvoiceDto | null = null;
  plan = 'A';
  month = new Date().toISOString().slice(0, 7);

  constructor() {
    effect(() => { const id = this.storeId(); if (id) this.load(); });
  }

  load() {
    const id = this.storeId();
    this.loading = true;
    this.api.get<PaymentsSummaryDto>(`/payments/stores/${id}/summary`).subscribe({ next: s => this.summary = s, error: () => this.summary = null });
    this.api.get<LedgerEntryDto[]>(`/payments/stores/${id}/ledger`).subscribe({
      next: l => { this.ledger = l; this.loading = false; },
      error: () => { this.ledger = []; this.loading = false; },
    });
    this.api.get<BillingDto>(`/payments/stores/${id}/billing`).subscribe({ next: b => this.plan = b.plan || 'A', error: () => {} });
    this.loadInvoice();
  }

  loadInvoice() {
    const id = this.storeId();
    if (!id) return;
    this.api.get<InvoiceDto>(`/payments/stores/${id}/invoice?month=${this.month}`).subscribe({
      next: inv => this.invoice = inv,
      error: () => this.invoice = null,
    });
  }

  savePlan() {
    const id = this.storeId();
    if (!id) return;
    this.api.put<BillingDto>(`/payments/stores/${id}/billing`, { plan: this.plan }).subscribe({
      next: () => this.loadInvoice(),
      error: () => {},
    });
  }
}
