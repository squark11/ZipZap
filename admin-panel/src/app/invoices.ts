import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, StoreDto } from './api';
import { ConfigModalComponent } from './config-modal';

interface InvoiceRow {
  storeId: string; storeName: string; city: string; isActive: boolean;
  plan: string; basis: string; orderCount: number; total: number;
}
interface InvoicesResp {
  period: string; currency: string; unitFee: number; grandTotal: number; stores: InvoiceRow[];
}

@Component({
  selector: 'app-invoices',
  imports: [CommonModule, FormsModule, ConfigModalComponent, RouterLink],
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
            <tr class="clickable" (click)="open(r)">
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

  <app-config-modal [open]="!!selected" [title]="selected ? 'Faktura — ' + selected.storeName : ''"
      [showFooter]="false" (close)="selected = null">
    @if (selected && data) {
      <div class="kv">
        <div><span>Sklep</span><b>{{ selected.storeName }}</b></div>
        <div><span>Miasto</span><b>{{ selected.city }}</b></div>
        <div><span>Status konta</span><b [style.color]="selected.isActive ? '#128040' : '#B4232A'">{{ selected.isActive ? 'aktywny' : 'nieaktywny' }}</b></div>
        <div><span>NIP</span><b>{{ store?.nip || '—' }}</b></div>
        <div><span>Adres</span><b>{{ store?.address || '—' }}</b></div>
        <div><span>Telefon</span><b>{{ store?.phone || '—' }}</b></div>
        <div><span>Plan</span><b>Plan {{ selected.plan }} ({{ selected.basis === 'delivery' ? 'za dostawę' : 'prowizja' }})</b></div>
        <div><span>Okres</span><b>{{ data.period }}</b></div>
      </div>

      <div class="calc">
        <div class="crow"><span>{{ selected.basis === 'delivery' ? 'Liczba dostaw' : 'Zamówień z prowizją' }}</span><b>{{ selected.orderCount }}</b></div>
        @if (selected.plan === 'A') {
          <div class="crow"><span>Opłata za dostawę</span><b>{{ data.unitFee | number:'1.2-2' }} {{ data.currency }}</b></div>
          <div class="crow"><span>{{ selected.orderCount }} × {{ data.unitFee | number:'1.2-2' }}</span><b>{{ selected.total | number:'1.2-2' }} {{ data.currency }}</b></div>
        } @else {
          <div class="crow"><span>Suma prowizji</span><b>{{ selected.total | number:'1.2-2' }} {{ data.currency }}</b></div>
        }
        <div class="crow total"><span>Do zapłaty (Dowózka.pl → sklep)</span><b>{{ selected.total | number:'1.2-2' }} {{ data.currency }}</b></div>
      </div>

      <div style="margin-top:16px;display:flex;gap:10px;flex-wrap:wrap">
        <a class="btn ghost sm" routerLink="/stores" (click)="selected = null" style="text-decoration:none">Przejdź do sklepu →</a>
        @if (store?.phone) { <a class="btn ghost sm" [href]="'tel:' + store!.phone" style="text-decoration:none">Zadzwoń</a> }
      </div>
      <p class="note" style="margin-top:14px">Faktura naliczana automatycznie na koniec okresu. Klient płaci bramką sklepu — kwota powyżej to opłata sklepu za usługę Dowózka.pl.</p>
    }
  </app-config-modal>
  `,
  styles: [`
    input[type=month]{ border:1px solid #E5E7EB; border-radius:8px; padding:7px 10px; font-size:14px; }
    input[type=month]:focus{ outline:none; border-color:#14B9BA; }
    .sum{ display:flex; flex-wrap:wrap; gap:14px; margin-bottom:16px; }
    .sum>div{ background:#fff; border:1px solid #E5E7EB; border-radius:12px; padding:14px 18px; display:flex; flex-direction:column; min-width:190px; }
    .sum span{ font-size:12px; } .sum b{ font-size:20px; color:#0F2A2A; }
    .right{ text-align:right; }
    .note{ margin-top:14px; background:#F1F3F5; color:#6B7280; padding:10px 12px; border-radius:8px; font-size:13px; }
    tr.clickable{ cursor:pointer; }
    tr.clickable:hover td{ background:#F7FBFB; }
    .kv{ display:grid; grid-template-columns:1fr 1fr; gap:12px 20px; margin-bottom:16px; }
    @media (max-width:520px){ .kv{ grid-template-columns:1fr; } }
    .kv > div{ display:flex; flex-direction:column; }
    .kv span{ font-size:12px; color:#6B7280; } .kv b{ color:#0F2A2A; word-break:break-word; }
    .calc{ border:1px solid #EAEEF0; border-radius:10px; overflow:hidden; }
    .crow{ display:flex; justify-content:space-between; align-items:center; padding:10px 14px; border-bottom:1px solid #EEF0F3; font-size:14px; }
    .crow:last-child{ border-bottom:0; }
    .crow span{ color:#6B7280; } .crow b{ color:#0F2A2A; }
    .crow.total{ background:#EAF9F9; } .crow.total b{ color:#0C7D7E; font-size:16px; }
  `],
})
export class InvoicesComponent implements OnInit {
  private api = inject(Api);
  data: InvoicesResp | null = null;
  month = new Date().toISOString().slice(0, 7);
  selected: InvoiceRow | null = null;
  store: StoreDto | null = null;

  ngOnInit() { this.load(); }

  load() {
    this.api.get<InvoicesResp>(`/admin/invoices?month=${this.month}`).subscribe({
      next: d => this.data = d,
      error: () => this.data = null,
    });
  }

  open(r: InvoiceRow) {
    this.selected = r;
    this.store = null;
    // Dociągamy dane sklepu (NIP/adres/telefon) do widoku faktury.
    this.api.getPublic<StoreDto>(`/catalog/stores/${r.storeId}`).subscribe({
      next: s => { if (this.selected?.storeId === r.storeId) this.store = s; },
      error: () => this.store = null,
    });
  }
}
