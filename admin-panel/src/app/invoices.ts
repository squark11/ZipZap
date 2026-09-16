import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api } from './api';

interface InvoiceRow {
  storeId: string; storeName: string; city: string; isActive: boolean;
  plan: string; basis: string; orderCount: number; total: number;
}
interface InvoicesResp {
  period: string; currency: string; unitFee: number; grandTotal: number; stores: InvoiceRow[];
}

@Component({
  selector: 'app-invoices',
  imports: [CommonModule, FormsModule],
  template: `
  <div class="page-head">
    <h1>Faktury</h1>
    <div class="controls">
      <input type="month" [(ngModel)]="month" (ngModelChange)="load()" />
      <button class="btn ghost sm" (click)="load()">Odśwież</button>
    </div>
  </div>

  @if (data) {
    <div class="sum">
      <div><span class="muted">Miesiąc</span><b>{{ data.period }}</b></div>
      <div><span class="muted">Suma faktur (ZipZap → sklepy)</span><b>{{ data.grandTotal | number:'1.2-2' }} {{ data.currency }}</b></div>
      <div><span class="muted">Opłata za dostawę (plan A)</span><b>{{ data.unitFee | number:'1.2-2' }} {{ data.currency }}</b></div>
    </div>

    <div class="card">
      <table>
        <thead><tr><th>Sklep</th><th>Plan</th><th>Podstawa</th><th class="right">Zamówień</th><th class="right">Kwota</th></tr></thead>
        <tbody>
          @for (r of data.stores; track r.storeId) {
            <tr>
              <td>
                <strong>{{ r.storeName }}</strong> <span class="muted">— {{ r.city }}</span>
                @if (!r.isActive) { <span class="pill" data-status="Cancelled" style="margin-left:6px">nieaktywny</span> }
              </td>
              <td><span class="pill" [attr.data-status]="r.plan === 'A' ? 'Processing' : 'Completed'">Plan {{ r.plan }}</span></td>
              <td class="muted">{{ r.basis === 'delivery' ? 'dostawa ZipZap' : 'prowizja' }}</td>
              <td class="right">{{ r.orderCount }}</td>
              <td class="right"><b>{{ r.total | number:'1.2-2' }} {{ data.currency }}</b></td>
            </tr>
          }
          @if (data.stores.length === 0) { <tr><td colspan="5" class="muted pad">Brak sklepów.</td></tr> }
        </tbody>
      </table>
    </div>
    <p class="note">Plan <b>A</b> = liczba dostaw × stała opłata za dostawę; Plan <b>B</b> = suma prowizji. Klient płaci bramką sklepu — Dowózka.pl fakturuje sklep za usługę (nie jest płatnikiem).</p>
  } @else {
    <p class="muted">Ładowanie faktur…</p>
  }
  `,
  styles: [`
    input[type=month]{ border:1px solid #E5E7EB; border-radius:8px; padding:7px 10px; font-size:14px; }
    input[type=month]:focus{ outline:none; border-color:#14B9BA; }
    .sum{ display:flex; flex-wrap:wrap; gap:14px; margin-bottom:16px; }
    .sum>div{ background:#fff; border:1px solid #E5E7EB; border-radius:12px; padding:14px 18px; display:flex; flex-direction:column; min-width:190px; }
    .sum span{ font-size:12px; } .sum b{ font-size:20px; color:#0F2A2A; }
    .right{ text-align:right; }
    .note{ margin-top:14px; background:#F1F3F5; color:#6B7280; padding:10px 12px; border-radius:8px; font-size:13px; }
  `],
})
export class InvoicesComponent implements OnInit {
  private api = inject(Api);
  data: InvoicesResp | null = null;
  month = new Date().toISOString().slice(0, 7);

  ngOnInit() { this.load(); }

  load() {
    this.api.get<InvoicesResp>(`/admin/invoices?month=${this.month}`).subscribe({
      next: d => this.data = d,
      error: () => this.data = null,
    });
  }
}
