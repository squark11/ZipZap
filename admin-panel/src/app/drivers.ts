import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, TeamMemberDto, StoreDto } from './api';

@Component({
  selector: 'app-drivers',
  imports: [CommonModule, FormsModule],
  template: `
  <div class="page-head">
    <h1>Dostawcy — do weryfikacji</h1>
    <div class="controls"><button class="btn ghost sm" (click)="load()">Odśwież</button></div>
  </div>

  <div class="card">
    @if (loading) { <div class="pad muted">Ładowanie…</div> }
    @else {
      <table>
        <thead><tr><th>Osoba</th><th>Kontakt</th><th>Przypisz do sklepu</th><th>Akcja</th></tr></thead>
        <tbody>
          @for (d of pending; track d.id) {
            <tr>
              <td><strong>{{ d.fullName }}</strong></td>
              <td><span class="muted mono">{{ d.email }}</span><br><span class="muted">{{ d.phone || '—' }}</span></td>
              <td>
                <select [(ngModel)]="assign[d.id]">
                  <option value="">— wybierz sklep —</option>
                  @for (s of stores; track s.id) { <option [value]="s.id">{{ s.name }} — {{ s.city }}</option> }
                </select>
              </td>
              <td>
                <button class="btn primary sm" [disabled]="!assign[d.id] || busy === d.id" (click)="approve(d)">
                  {{ busy === d.id ? 'Zatwierdzam…' : 'Zatwierdź' }}
                </button>
              </td>
            </tr>
          }
          @if (pending.length === 0) { <tr><td colspan="4" class="muted pad">Brak oczekujących zgłoszeń dostawców.</td></tr> }
        </tbody>
      </table>
    }
  </div>
  <p class="muted" style="margin:10px 2px 0; font-size:12px">Zatwierdzenie aktywuje konto dostawcy i przypisuje go do wybranego sklepu.</p>
  `,
})
export class DriversComponent implements OnInit {
  private api = inject(Api);
  pending: TeamMemberDto[] = [];
  stores: StoreDto[] = [];
  assign: Record<string, string> = {};
  loading = false;
  busy = '';

  ngOnInit() { this.load(); this.loadStores(); }

  load() {
    this.loading = true;
    this.api.get<TeamMemberDto[]>('/admin/drivers/pending').subscribe({
      next: d => { this.pending = d; this.loading = false; },
      error: () => { this.pending = []; this.loading = false; },
    });
  }

  loadStores() {
    this.api.getPublic<StoreDto[]>('/catalog/stores?onlyActive=false').subscribe({
      next: s => this.stores = s,
      error: () => this.stores = [],
    });
  }

  approve(d: TeamMemberDto) {
    const storeId = this.assign[d.id];
    if (!storeId) return;
    this.busy = d.id;
    this.api.post(`/admin/drivers/${d.id}/approve`, { storeId }).subscribe({
      next: () => { this.busy = ''; delete this.assign[d.id]; this.load(); },
      error: e => { this.busy = ''; alert('Błąd: ' + (e?.error?.detail ?? 'nieznany')); },
    });
  }
}
