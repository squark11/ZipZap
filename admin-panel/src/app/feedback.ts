import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api } from './api';

interface FeedbackItem {
  id: string; type: string; message: string; status: string;
  screen?: string; appVersion?: string; platform?: string;
  createdAtUtc: string; contactEmail?: string; storeId?: string;
}

@Component({
  selector: 'app-feedback',
  imports: [CommonModule, FormsModule],
  styles: [`
    .statuscell select { padding:4px 8px; border:1px solid #E5E7EB; border-radius:6px; font-size:13px; background:#fff; }
    td.msg { max-width:440px; white-space:pre-wrap; }
  `],
  template: `
  <div class="page-head">
    <h1>Uwagi</h1>
    <div class="controls">
      <select [(ngModel)]="statusFilter" (ngModelChange)="load()" style="padding:6px 10px;border:1px solid #E5E7EB;border-radius:8px">
        <option value="">Wszystkie</option>
        <option value="New">Nowe</option>
        <option value="InProgress">W toku</option>
        <option value="Closed">Zamknięte</option>
      </select>
      <button class="btn ghost sm" (click)="load()">Odśwież</button>
    </div>
  </div>

  <div class="card">
    @if (loading) { <div class="pad muted">Ładowanie…</div> }
    @else {
      <table>
        <thead><tr><th>Typ</th><th>Treść</th><th>Kontekst</th><th>Data</th><th>Status</th></tr></thead>
        <tbody>
          @for (f of items; track f.id) {
            <tr>
              <td><span class="pill" [attr.data-status]="typeStatus(f.type)">{{ typeLabel(f.type) }}</span></td>
              <td class="msg">{{ f.message }}@if (f.contactEmail) { <div class="muted" style="font-size:12px">✉ {{ f.contactEmail }}</div> }</td>
              <td class="muted" style="font-size:12px">
                {{ f.platform || '—' }}@if (f.screen) { · {{ f.screen }} }@if (f.appVersion) { · v{{ f.appVersion }} }
              </td>
              <td class="muted">{{ f.createdAtUtc | date:'dd.MM HH:mm' }}</td>
              <td class="statuscell">
                <select [ngModel]="f.status" (ngModelChange)="setStatus(f, $event)">
                  <option value="New">Nowe</option>
                  <option value="InProgress">W toku</option>
                  <option value="Closed">Zamknięte</option>
                </select>
              </td>
            </tr>
          }
          @if (items.length === 0) { <tr><td colspan="5" class="muted pad">Brak uwag.</td></tr> }
        </tbody>
      </table>
    }
  </div>
  `,
})
export class FeedbackComponent implements OnInit {
  private api = inject(Api);
  items: FeedbackItem[] = [];
  loading = false;
  statusFilter = '';

  ngOnInit() { this.load(); }

  load() {
    this.loading = true;
    const q = this.statusFilter ? `?status=${this.statusFilter}&take=300` : '?take=300';
    this.api.get<FeedbackItem[]>(`/feedback${q}`).subscribe({
      next: i => { this.items = i; this.loading = false; },
      error: () => { this.items = []; this.loading = false; },
    });
  }

  setStatus(f: FeedbackItem, status: string) {
    if (status === f.status) return;
    this.api.patch(`/feedback/${f.id}`, { status }).subscribe({
      next: () => { f.status = status; if (this.statusFilter && this.statusFilter !== status) this.load(); },
      error: () => this.load(),
    });
  }

  typeLabel(t: string) { return t === 'Bug' ? 'Błąd' : t === 'Idea' ? 'Pomysł' : 'Inne'; }
  typeStatus(t: string) { return t === 'Bug' ? 'Cancelled' : t === 'Idea' ? 'Completed' : 'Processing'; }
}
