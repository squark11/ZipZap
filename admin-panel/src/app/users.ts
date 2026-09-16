import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api } from './api';

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
  imports: [CommonModule, FormsModule],
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
      <thead><tr><th>Użytkownik</th><th>Rola</th><th>E-mail</th><th>Utworzono</th><th>Status</th><th>Akcje</th></tr></thead>
      <tbody>
        @for (u of filtered; track u.id) {
          <tr>
            <td><strong>{{ u.fullName || '—' }}</strong><div class="muted" style="font-size:12px">{{ u.email }}</div></td>
            <td>{{ rolesLabel(u.roles) }}</td>
            <td>@if (u.isEmailVerified) { <span style="color:#128040">✓ potwierdzony</span> } @else { <span class="muted">— niepotwierdzony</span> }</td>
            <td class="muted">{{ u.createdAtUtc | date:'yyyy-MM-dd' }}</td>
            <td><span class="pill" [attr.data-status]="u.isActive ? 'Completed' : 'Cancelled'">{{ u.isActive ? 'aktywny' : 'nieaktywny' }}</span></td>
            <td>
              <button class="btn ghost sm" (click)="toggle(u)" [disabled]="busyId === u.id">
                {{ u.isActive ? 'Dezaktywuj' : 'Aktywuj' }}
              </button>
            </td>
          </tr>
        }
        @if (filtered.length === 0) { <tr><td colspan="6" class="muted pad">Brak użytkowników.</td></tr> }
      </tbody>
    </table>
  </div>
  @if (error) { <p class="warn">{{ error }}</p> }
  `,
  styles: [`
    .search-in { border:1px solid #E5E7EB; border-radius:8px; padding:8px 11px; font-size:14px; min-width:230px; }
    .search-in:focus { outline:none; border-color:#14B9BA; }
    .warn { margin-top:12px; background:#FEECEC; color:#B4232A; padding:10px 12px; border-radius:8px; font-size:13px; }
  `],
})
export class UsersComponent implements OnInit {
  private api = inject(Api);
  users: AdminUser[] = [];
  q = '';
  busyId = '';
  error = '';

  ngOnInit() { this.load(); }

  get filtered(): AdminUser[] {
    const q = this.q.trim().toLowerCase();
    if (!q) return this.users;
    return this.users.filter(u =>
      (u.email || '').toLowerCase().includes(q) ||
      (u.fullName || '').toLowerCase().includes(q) ||
      (u.roles || '').toLowerCase().includes(q));
  }

  load() {
    this.error = '';
    this.api.get<AdminUser[]>('/admin/users').subscribe({
      next: u => this.users = u,
      error: e => this.error = 'Nie udało się pobrać użytkowników: ' + (e?.error?.detail ?? 'błąd'),
    });
  }

  rolesLabel(roles: string): string {
    return (roles || '')
      .split(',').map(r => r.trim())
      .map(r => r === 'Admin' ? 'Administrator serwisu'
        : r === 'StoreEmployee' ? 'Sklep'
        : r === 'Driver' ? 'Dostawca'
        : r === 'Customer' ? 'Klient' : r)
      .filter(Boolean).join(', ');
  }

  toggle(u: AdminUser) {
    this.busyId = u.id;
    this.error = '';
    const value = !u.isActive;
    this.api.post(`/admin/users/${u.id}/active?value=${value}`, {}).subscribe({
      next: () => { u.isActive = value; this.busyId = ''; },
      error: e => { this.busyId = ''; this.error = 'Nie udało się zmienić statusu: ' + (e?.error?.detail ?? 'błąd'); },
    });
  }
}
