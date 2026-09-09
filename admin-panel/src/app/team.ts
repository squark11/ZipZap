import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, TeamMemberDto } from './api';

@Component({
  selector: 'app-team',
  imports: [CommonModule, FormsModule],
  styles: [`
    .rolebadge { display:inline-block; padding:2px 8px; border-radius:6px; font-size:12px; font-weight:600; }
    .role-emp { background:#EFF6FF; color:#2563EB; }
    .role-drv { background:#FFF3EA; color:#C2560A; }
  `],
  template: `
  <div class="page-head">
    <h1>Zespół</h1>
    <div class="controls"><button class="btn ghost sm" (click)="load()">Odśwież</button></div>
  </div>

  <div class="card pad" style="margin-bottom:16px">
    <div class="formgrid">
      <label>Rola
        <select [(ngModel)]="nu.role">
          <option value="StoreEmployee">Pracownik sklepu</option>
          <option value="Driver">Kierowca</option>
        </select>
      </label>
      <label>Imię i nazwisko<input [(ngModel)]="nu.fullName" placeholder="np. Anna Kowalska" /></label>
      <label>E-mail<input [(ngModel)]="nu.email" type="email" placeholder="anna@sklep.pl" /></label>
      <label>Telefon<input [(ngModel)]="nu.phone" placeholder="600 100 200" /></label>
      <label>Hasło startowe<input [(ngModel)]="nu.password" type="password" placeholder="min. 6 znaków" /></label>
      <button class="btn primary" (click)="create()">Dodaj do zespołu</button>
    </div>
    <p class="muted" style="margin:8px 0 0; font-size:12px">Konto zostaje przypisane do wybranego w nagłówku sklepu. Hasło przekaż pracownikowi bezpiecznym kanałem.</p>
  </div>

  <div class="card">
    @if (loading) { <div class="pad muted">Ładowanie…</div> }
    @else {
      <table>
        <thead><tr><th>Osoba</th><th>Rola</th><th>Telefon</th><th>Status</th><th>Akcje</th></tr></thead>
        <tbody>
          @for (m of team; track m.id + m.role) {
            <tr>
              <td><strong>{{ m.fullName }}</strong><br><span class="muted mono">{{ m.email }}</span></td>
              <td><span class="rolebadge" [class]="m.role === 'Driver' ? 'role-drv' : 'role-emp'">{{ roleLabel(m.role) }}</span></td>
              <td class="muted">{{ m.phone || '—' }}</td>
              <td><span class="pill" [attr.data-status]="m.isActive ? 'Completed' : 'Cancelled'">{{ m.isActive ? 'Aktywny' : 'Zablokowany' }}</span></td>
              <td>
                <div class="actions">
                  <button class="btn ghost sm" (click)="toggleActive(m)">{{ m.isActive ? 'Zablokuj' : 'Odblokuj' }}</button>
                </div>
              </td>
            </tr>
          }
          @if (team.length === 0) { <tr><td colspan="5" class="muted pad">Brak pracowników i kierowców przypisanych do tego sklepu.</td></tr> }
        </tbody>
      </table>
    }
  </div>
  `,
})
export class TeamComponent {
  private api = inject(Api);
  storeId = input.required<string>();

  team: TeamMemberDto[] = [];
  loading = false;
  nu = { role: 'StoreEmployee', fullName: '', email: '', phone: '', password: '' };

  constructor() {
    effect(() => { const id = this.storeId(); if (id) this.load(); });
  }

  load() {
    this.loading = true;
    this.api.get<TeamMemberDto[]>(`/identity/admin/stores/${this.storeId()}/team`).subscribe({
      next: t => { this.team = t; this.loading = false; },
      error: () => { this.team = []; this.loading = false; },
    });
  }

  create() {
    if (!this.nu.fullName.trim() || !this.nu.email.trim()) { alert('Podaj imię i e-mail.'); return; }
    if ((this.nu.password ?? '').length < 6) { alert('Hasło musi mieć co najmniej 6 znaków.'); return; }
    const body = {
      email: this.nu.email.trim(),
      password: this.nu.password,
      fullName: this.nu.fullName.trim(),
      phone: this.nu.phone.trim() || null,
      role: this.nu.role,
      storeId: this.storeId(),
    };
    this.api.post('/identity/admin/users', body).subscribe({
      next: () => { this.nu = { role: this.nu.role, fullName: '', email: '', phone: '', password: '' }; this.load(); },
      error: e => alert(err(e)),
    });
  }

  toggleActive(m: TeamMemberDto) {
    this.api.post(`/identity/admin/users/${m.id}/active`, { isActive: !m.isActive })
      .subscribe({ next: () => this.load(), error: e => alert(err(e)) });
  }

  roleLabel(role: string): string {
    return role === 'Driver' ? 'Kierowca' : role === 'StoreEmployee' ? 'Pracownik' : role;
  }
}

function err(e: any): string { return 'Błąd: ' + (e?.error?.detail ?? e?.message ?? 'nieznany'); }
