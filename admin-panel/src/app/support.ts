import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, StoreDto } from './api';

@Component({
  selector: 'app-support',
  imports: [CommonModule, FormsModule],
  template: `
  <div class="page-head"><h1>Wsparcie sklepów</h1></div>

  <div class="card pad">
    <p class="muted" style="margin-top:0">Domyślnie <b>nie masz wglądu</b> w dane sklepu. Sklep proszący o pomoc podaje swój <b>kod wsparcia</b> — wpisz go, aby zobaczyć jego dane i pomóc.</p>
    <div class="row">
      <input class="code-in" [(ngModel)]="code" placeholder="np. K7X9PQ" maxlength="12"
             (keyup.enter)="lookup()" (ngModelChange)="onType()" />
      <button class="btn primary" (click)="lookup()" [disabled]="busy || code.trim().length < 4">Sprawdź kod</button>
    </div>
    @if (error) { <p class="warn">{{ error }}</p> }
  </div>

  @if (store) {
    <div class="card pad" style="margin-top:16px">
      <div style="display:flex;align-items:center;gap:12px;margin-bottom:12px">
        @if (store.logoUrl) { <img [src]="store.logoUrl" alt="logo" style="width:44px;height:44px;border-radius:10px;object-fit:cover;border:1px solid #E5E7EB" /> }
        <div>
          <h2 style="margin:0;font-size:18px">{{ store.name }}</h2>
          <span class="muted mono" style="font-size:12px">{{ store.slug }}</span>
        </div>
        <span class="pill" style="margin-left:auto" [attr.data-status]="store.status === 'Open' ? 'Completed' : 'Cancelled'">{{ statusLabel(store.status) }}</span>
      </div>
      <div class="kv">
        <div><span>Miasto</span><b>{{ store.city }}</b></div>
        <div><span>Adres</span><b>{{ store.address || '—' }}</b></div>
        <div><span>Telefon</span><b>{{ store.phone || '—' }}</b></div>
        <div><span>NIP</span><b>{{ store.nip || '—' }}</b></div>
        <div><span>Przyjmuje zamówienia</span><b [style.color]="store.isAcceptingOrders ? '#128040' : '#B4232A'">{{ store.isAcceptingOrders ? 'tak' : 'nie' }}</b></div>
        <div><span>Konto</span><b [style.color]="store.isActive ? '#128040' : '#B4232A'">{{ store.isActive ? 'aktywny' : 'nieaktywny' }}</b></div>
        <div><span>Min. zamówienie</span><b>{{ store.minimumOrderValue | number:'1.2-2' }} zł</b></div>
        <div><span>Prowizja</span><b>{{ (store.commissionRate * 100) | number:'1.0-1' }}%</b></div>
      </div>
      <p class="note">Wgląd przyznany na podstawie kodu wsparcia. Zakres pełnych funkcji pomocy (podgląd oferty/zamówień) dojdzie w kolejnym kroku.</p>
    </div>
  }
  `,
  styles: [`
    .row { display:flex; gap:10px; flex-wrap:wrap; align-items:center; }
    .code-in { border:1px solid #E5E7EB; border-radius:8px; padding:10px 12px; font-size:16px; letter-spacing:.12em; text-transform:uppercase; font-family:'JetBrains Mono',monospace; min-width:200px; }
    .code-in:focus { outline:none; border-color:#14B9BA; }
    .warn { margin-top:12px; background:#FEECEC; color:#B4232A; padding:10px 12px; border-radius:8px; font-size:13px; }
    .kv { display:grid; grid-template-columns:1fr 1fr; gap:12px 20px; }
    @media (max-width:520px){ .kv { grid-template-columns:1fr; } }
    .kv > div { display:flex; flex-direction:column; }
    .kv span { font-size:12px; color:#6B7280; } .kv b { color:#0F2A2A; }
    .note { margin-top:14px; background:#F1F3F5; color:#6B7280; padding:10px 12px; border-radius:8px; font-size:12.5px; }
  `],
})
export class SupportComponent {
  private api = inject(Api);
  code = '';
  store: StoreDto | null = null;
  busy = false;
  error = '';

  onType() { if (this.error) this.error = ''; }
  statusLabel(st: string) { return st === 'Open' ? 'Otwarty' : st === 'Closed' ? 'Zamknięty' : 'Niedostępny'; }

  lookup() {
    const c = this.code.trim().toUpperCase();
    if (c.length < 4) return;
    this.busy = true; this.error = ''; this.store = null;
    this.api.get<StoreDto>(`/admin/support?code=${encodeURIComponent(c)}`).subscribe({
      next: s => { this.store = s; this.busy = false; },
      error: e => { this.busy = false; this.error = e?.error?.detail ?? 'Nie znaleziono sklepu dla tego kodu.'; },
    });
  }
}
