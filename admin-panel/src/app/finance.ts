import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Api, LedgerEntryDto, PaymentsSummaryDto } from './api';

@Component({
  selector: 'app-finance',
  imports: [CommonModule],
  template: `
  <div class="page-head">
    <h1>Rozliczenia</h1>
    <div class="controls"><button class="btn ghost sm" (click)="load()">Odśwież</button></div>
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
})
export class FinanceComponent {
  private api = inject(Api);
  storeId = input.required<string>();

  summary: PaymentsSummaryDto | null = null;
  ledger: LedgerEntryDto[] = [];
  loading = false;

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
  }
}
