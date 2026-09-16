import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api } from './api';
import { ConfigModalComponent } from './config-modal';

interface AdminUser {
  id: string;
  email: string;
  fullName: string;
  phone?: string;
  isActive: boolean;
  isEmailVerified: boolean;
  roles: string;
  createdAtUtc: string;
}

@Component({
  selector: 'app-users',
  imports: [CommonModule, FormsModule, ConfigModalComponent],
  template: `
  <div class="page-head">
    <h1>Użytkownicy</h1>
    <div class="controls">
      <input class="search-in" placeholder="Szukaj (e-mail, imię, rola)…" [(ngModel)]="q" />
      <button class="btn ghost sm" (click)="load()">Odśwież</button>
    </div>
  </div>

  <div class="card">
    <table>
      <thead><tr><th>Użytkownik</th><th>Rola</th><th>E-mail</th><th>Utworzono</th><th>Status</th><th></th></tr></thead>
      <tbody>
        @for (u of filtered; track u.id) {
          <tr class="rowlink" (click)="open(u)">
            <td><strong class="link">{{ u.fullName || '—' }}</strong><div class="muted" style="font-size:12px">{{ u.email }}</div></td>
            <td>{{ rolesLabel(u.roles) }}</td>
            <td>@if (u.isEmailVerified) { <span style="color:#128040">✓</span> } @else { <span class="muted">—</span> }</td>
            <td class="muted">{{ u.createdAtUtc | date:'yyyy-MM-dd' }}</td>
            <td><span class="pill" [attr.data-status]="u.isActive ? 'Completed' : 'Cancelled'">{{ u.isActive ? 'aktywny' : 'nieaktywny' }}</span></td>
            <td class="right"><span class="chev">›</span></td>
          </tr>
        }
        @if (filtered.length === 0) { <tr><td colspan="6" class="muted pad">Brak użytkowników.</td></tr> }
      </tbody>
    </table>
  </div>
  @if (error) { <p class="warn">{{ error }}</p> }

  <!-- Szczegóły użytkownika -->
  <app-config-modal [open]="!!selected" [title]="selected?.fullName || selected?.email || 'Użytkownik'"
      [showFooter]="false" (close)="selected = null">
    @if (selected; as u) {
      <div class="kv">
        <div><span>E-mail</span><b>{{ u.email }}</b></div>
        <div><span>Telefon</span><b>{{ u.phone || '—' }}</b></div>
        <div><span>Role</span><b>{{ rolesLabel(u.roles) }}</b></div>
        <div><span>E-mail potwierdzony</span><b [style.color]="u.isEmailVerified ? '#128040' : '#6B7280'">{{ u.isEmailVerified ? 'tak' : 'nie' }}</b></div>
        <div><span>Konto</span><b [style.color]="u.isActive ? '#128040' : '#B4232A'">{{ u.isActive ? 'aktywne' : 'nieaktywne' }}</b></div>
        <div><span>Utworzono</span><b>{{ u.createdAtUtc | date:'yyyy-MM-dd HH:mm' }}</b></div>
      </div>

      <div class="act">
        <button class="btn" [class.primary]="!u.isActive" (click)="toggle(u)" [disabled]="busy">
          {{ u.isActive ? 'Dezaktywuj konto' : 'Aktywuj konto' }}
        </button>
      </div>

      <div class="rolebox">
        <label class="lbl">Nadaj rolę</label>
        <div style="display:flex;gap:10px;flex-wrap:wrap">
          <select [(ngModel)]="newRole">
            <option value="Admin">Administrator serwisu</option>
            <option value="StoreEmployee">Zarządca sklepu</option>
            <option value="Driver">Dostawca</option>
            <option value="Customer">Klient</option>
          </select>
          <button class="btn ghost sm" (click)="assignRole(u)" [disabled]="busy">Nadaj rolę</button>
        </div>
        <p class="note">Nadanie roli nie usuwa istniejących — konto może mieć kilka ról. Zmiana zadziała po ponownym zalogowaniu użytkownika.</p>
      </div>
      @if (modalError) { <p class="warn">{{ modalError }}</p> }
    }
  </app-config-modal>
  `,
  styles: [`
    .search-in { border:1px solid #E5E7EB; border-radius:8px; padding:8px 11px; font-size:14px; min-width:230px; }
    .search-in:focus { outline:none; border-color:#14B9BA; }
    .warn { margin-top:12px; background:#FEECEC; color:#B4232A; padding:10px 12px; border-radius:8px; font-size:13px; }
    .rowlink { cursor:pointer; transition:background .12s; }
    .rowlink:hover { background:#f4faf9; }
    .rowlink:hover .link { color:#0C7D7E; }
    .link { color:#0F2A2A; }
    .right { text-align:right; } .chev { color:#9aa8a8; font-size:18px; }
    .kv { display:grid; grid-template-columns:1fr 1fr; gap:12px 20px; }
    @media (max-width:520px){ .kv { grid-template-columns:1fr; } }
    .kv > div { display:flex; flex-direction:column; }
    .kv span { font-size:12px; color:#6B7280; } .kv b { color:#0F2A2A; }
    .act { margin:18px 0 6px; }
    .rolebox { border-top:1px solid #eef0f3; margin-top:16px; padding-top:14px; }
    .lbl { display:block; font-size:13px; font-weight:600; color:#3A3F4B; margin:0 0 8px; }
    select { border:1px solid #E5E7EB; border-radius:8px; padding:9px 11px; font-size:14px; background:#fff; }
    .note { background:#F1F3F5; color:#6B7280; padding:10px 12px; border-radius:8px; font-size:12.5px; margin-top:12px; }
  `],
})
export class UsersComponent implements OnInit {
  private api = inject(Api);
  users: AdminUser[] = [];
  q = '';
  busy = false;
  error = '';
  selected: AdminUser | null = null;
  newRole = 'StoreEmployee';
  modalError = '';

  ngOnInit() { this.load(); }

  get filtered(): AdminUser[] {
    const q = this.q.trim().toLowerCase();
    if (!q) return this.users;
    return this.users.filter(u =>
      (u.email || '').toLowerCase().includes(q) ||
      (u.fullName || '').toLowerCase().includes(q) ||
      (u.roles || '').toLowerCase().includes(q));
  }

  open(u: AdminUser) { this.selected = u; this.modalError = ''; this.newRole = 'StoreEmployee'; }

  load() {
    this.error = '';
    this.api.get<AdminUser[]>('/admin/users').subscribe({
      next: u => {
        this.users = u;
        if (this.selected) this.selected = u.find(x => x.id === this.selected!.id) ?? null;
      },
      error: e => this.error = 'Nie udało się pobrać użytkowników: ' + (e?.error?.detail ?? 'błąd'),
    });
  }

  rolesLabel(roles: string): string {
    return (roles || '')
      .split(',').map(r => r.trim())
      .map(r => r === 'Admin' ? 'Administrator serwisu'
        : r === 'StoreEmployee' ? 'Zarządca sklepu'
        : r === 'Driver' ? 'Dostawca'
        : r === 'Customer' ? 'Klient' : r)
      .filter(Boolean).join(', ');
  }

  toggle(u: AdminUser) {
    this.busy = true; this.modalError = '';
    const value = !u.isActive;
    this.api.post(`/admin/users/${u.id}/active?value=${value}`, {}).subscribe({
      next: () => { u.isActive = value; this.busy = false; },
      error: e => { this.busy = false; this.modalError = 'Nie udało się zmienić statusu: ' + (e?.error?.detail ?? 'błąd'); },
    });
  }

  assignRole(u: AdminUser) {
    this.busy = true; this.modalError = '';
    this.api.post(`/identity/admin/users/${u.id}/role`, { role: this.newRole }).subscribe({
      next: () => { this.busy = false; this.load(); },
      error: e => { this.busy = false; this.modalError = 'Nie udało się nadać roli: ' + (e?.error?.detail ?? 'błąd'); },
    });
  }
}
