import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api } from './api';
import { ConfigModalComponent } from './config-modal';

interface FeedbackItem {
  id: string; type: string; message: string; status: string;
  screen?: string; appVersion?: string; platform?: string;
  createdAtUtc: string; contactEmail?: string; storeId?: string;
}

@Component({
  selector: 'app-feedback',
  imports: [CommonModule, FormsModule, ConfigModalComponent],
  styles: [`
    .statuscell select { padding:4px 8px; border:1px solid #E5E7EB; border-radius:6px; font-size:13px; background:#fff; }
    td.msg { max-width:440px; white-space:pre-wrap; }
    tr.clickable { cursor:pointer; }
    tr.clickable:hover td { background:#F7FBFB; }
    .kv { display:grid; grid-template-columns:1fr 1fr; gap:12px 20px; margin-bottom:14px; }
    @media (max-width:520px){ .kv { grid-template-columns:1fr; } }
    .kv > div { display:flex; flex-direction:column; }
    .kv span { font-size:12px; color:#6B7280; } .kv b { color:#0F2A2A; word-break:break-word; }
    .fullmsg { background:#F7F9FA; border:1px solid #EAEEF0; border-radius:10px; padding:12px 14px; white-space:pre-wrap; color:#1F2A2A; font-size:14px; line-height:1.5; }
    .dstatus { display:flex; align-items:center; gap:10px; margin-top:16px; flex-wrap:wrap; }
    .dstatus select { padding:8px 11px; border:1px solid #E5E7EB; border-radius:8px; font-size:14px; background:#fff; }
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
            <tr class="clickable" (click)="open(f)">
              <td><span class="pill" [attr.data-status]="typeStatus(f.type)">{{ typeLabel(f.type) }}</span></td>
              <td class="msg">{{ f.message }}@if (f.contactEmail) { <div class="muted" style="font-size:12px">✉ {{ f.contactEmail }}</div> }</td>
              <td class="muted" style="font-size:12px">
                {{ f.platform || '—' }}@if (f.screen) { · {{ f.screen }} }@if (f.appVersion) { · v{{ f.appVersion }} }
              </td>
              <td class="muted">{{ f.createdAtUtc | date:'dd.MM HH:mm' }}</td>
              <td class="statuscell" (click)="$event.stopPropagation()">
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

  <app-config-modal [open]="!!selected" [title]="selected ? typeLabel(selected.type) + ' — szczegóły' : ''"
      [showFooter]="false" (close)="selected = null">
    @if (selected) {
      <div class="kv">
        <div><span>Typ</span><b>{{ typeLabel(selected.type) }}</b></div>
        <div><span>Status</span><b>{{ statusLabel(selected.status) }}</b></div>
        <div><span>Data zgłoszenia</span><b>{{ selected.createdAtUtc | date:'dd.MM.yyyy HH:mm' }}</b></div>
        <div><span>Platforma</span><b>{{ selected.platform || '—' }}</b></div>
        <div><span>Ekran</span><b>{{ selected.screen || '—' }}</b></div>
        <div><span>Wersja aplikacji</span><b>{{ selected.appVersion || '—' }}</b></div>
        <div><span>Kontakt</span><b>{{ selected.contactEmail || '— (anonimowo)' }}</b></div>
        <div><span>Sklep</span><b>{{ selected.storeId || '—' }}</b></div>
      </div>
      <span class="muted" style="font-size:12px">Treść</span>
      <div class="fullmsg">{{ selected.message }}</div>
      <div class="dstatus">
        <span class="muted" style="font-size:13px">Zmień status:</span>
        <select [ngModel]="selected.status" (ngModelChange)="setStatus(selected, $event)">
          <option value="New">Nowe</option>
          <option value="InProgress">W toku</option>
          <option value="Closed">Zamknięte</option>
        </select>
        @if (selected.contactEmail) {
          <a class="btn ghost sm" [href]="'mailto:' + selected.contactEmail + '?subject=' + mailSubject(selected)" style="margin-left:auto;text-decoration:none">✉ Odpisz</a>
        }
      </div>
    }
  </app-config-modal>
  `,
})
export class FeedbackComponent implements OnInit {
  private api = inject(Api);
  items: FeedbackItem[] = [];
  loading = false;
  statusFilter = '';
  selected: FeedbackItem | null = null;

  ngOnInit() { this.load(); }

  open(f: FeedbackItem) { this.selected = f; }

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
  statusLabel(s: string) { return s === 'New' ? 'Nowe' : s === 'InProgress' ? 'W toku' : s === 'Closed' ? 'Zamknięte' : s; }
  mailSubject(f: FeedbackItem) { return encodeURIComponent('Dowózka.pl — odpowiedź na Twoje zgłoszenie (' + this.typeLabel(f.type) + ')'); }
}
