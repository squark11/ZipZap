import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Api } from './api';

interface AdminStats {
  stores: { total: number; active: number };
  users: { total: number; customers: number; stores: number; drivers: number; inactive: number };
  pendingDrivers: number;
  orders: { count: number; gmv: number };
}

@Component({
  selector: 'app-admin-home',
  imports: [CommonModule, RouterLink],
  template: `
  <div class="page-head">
    <h1>Statystyki platformy</h1>
    <div class="controls"><button class="btn ghost sm" (click)="load()">Odśwież</button></div>
  </div>

  @if (stats) {
    <div class="sgrid">
      <a class="stat hero" routerLink="/invoices">
        <div class="sic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M12 1v22"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg></div>
        <div class="sval"><b>{{ stats.orders.gmv | number:'1.2-2' }} zł</b><span>GMV (bez anulowanych) · zobacz faktury ›</span></div>
      </a>
      <a class="stat" routerLink="/invoices">
        <div class="sic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M6 2h9l5 5v13a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2z"/><path d="M14 2v6h6M8 13h8M8 17h6"/></svg></div>
        <div class="sval"><b>{{ stats.orders.count }}</b><span>zamówień</span></div>
      </a>
      <a class="stat" routerLink="/stores">
        <div class="sic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M3 9l1.5-5h15L21 9M4 9h16v10a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V9z"/></svg></div>
        <div class="sval"><b>{{ stats.stores.active }} / {{ stats.stores.total }}</b><span>aktywne sklepy ›</span></div>
      </a>
      <a class="stat" routerLink="/users">
        <div class="sic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/></svg></div>
        <div class="sval"><b>{{ stats.users.total }}</b><span>użytkowników ›</span></div>
      </a>
      <a class="stat" routerLink="/users">
        <div class="sic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/></svg></div>
        <div class="sval"><b>{{ stats.users.customers }}</b><span>klientów</span></div>
      </a>
      <a class="stat" routerLink="/users">
        <div class="sic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><rect x="1" y="3" width="15" height="13" rx="1"/><path d="M16 8h4l3 3v5h-7V8z"/><circle cx="5.5" cy="18.5" r="2.5"/><circle cx="18.5" cy="18.5" r="2.5"/></svg></div>
        <div class="sval"><b>{{ stats.users.drivers }}</b><span>dostawców</span></div>
      </a>
      <a class="stat" [class.warn]="stats.pendingDrivers > 0" routerLink="/drivers">
        <div class="sic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M12 9v4M12 17h.01"/><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/></svg></div>
        <div class="sval"><b>{{ stats.pendingDrivers }}</b><span>dostawców do zatwierdzenia ›</span></div>
      </a>
      <a class="stat" [class.warn]="stats.users.inactive > 0" routerLink="/users">
        <div class="sic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><path d="M4.9 4.9l14.2 14.2"/></svg></div>
        <div class="sval"><b>{{ stats.users.inactive }}</b><span>nieaktywnych kont ›</span></div>
      </a>
    </div>
  } @else {
    <p class="muted">Ładowanie statystyk…</p>
  }
  `,
  styles: [`
    .sgrid { display:grid; grid-template-columns:repeat(auto-fill,minmax(220px,1fr)); gap:14px; }
    .stat { display:flex; align-items:center; gap:14px; background:#fff; border:1px solid #E5E7EB; border-radius:14px; padding:18px; text-decoration:none; color:inherit; transition:border-color .15s, box-shadow .15s, transform .15s; }
    .stat:hover { border-color:#14B9BA; box-shadow:0 12px 28px -20px rgba(12,125,126,.5); transform:translateY(-2px); }
    .stat.hero { grid-column:span 2; background:linear-gradient(135deg,#e7f7f7,#fff); border-color:#bfe9e8; }
    @media (max-width:520px){ .stat.hero { grid-column:span 1; } }
    .stat .sic { width:46px; height:46px; flex:none; border-radius:12px; background:#E7F7F7; color:#0C7D7E; display:flex; align-items:center; justify-content:center; }
    .stat .sic svg { width:24px; height:24px; }
    .stat.warn .sic { background:#FFF3EA; color:#EA6A0C; }
    .stat .sval { display:flex; flex-direction:column; min-width:0; }
    .stat .sval b { font-size:22px; color:#0F2A2A; line-height:1.15; }
    .stat.hero .sval b { font-size:26px; }
    .stat .sval span { font-size:12.5px; color:#6B7280; }
  `],
})
export class AdminHomeComponent implements OnInit {
  private api = inject(Api);
  stats: AdminStats | null = null;

  ngOnInit() { this.load(); }

  load() {
    this.api.get<AdminStats>('/admin/stats').subscribe({
      next: s => this.stats = s,
      error: () => this.stats = null,
    });
  }
}
