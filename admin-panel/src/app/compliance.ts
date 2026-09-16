import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api } from './api';

interface Flag {
  id: string; subjectType: string; subjectId: string; subjectLabel: string;
  category: string; severity: string; note?: string; status: string;
  createdAtUtc: string; resolvedAtUtc?: string; resolution?: string;
}
interface Counts { open: number; resolved: number; highOpen: number; }

@Component({
  selector: 'app-compliance',
  imports: [CommonModule, FormsModule],
  template: `
  <div class="page-head">
    <h1>Nadzór regulaminu</h1>
    <div class="controls">
      <select [(ngModel)]="statusFilter" (ngModelChange)="load()" style="padding:6px 10px;border:1px solid #E5E7EB;border-radius:8px">
        <option value="Open">Otwarte</option>
        <option value="Resolved">Rozwiązane</option>
        <option value="">Wszystkie</option>
      </select>
      <button class="btn ghost sm" (click)="load()">Odśwież</button>
    </div>
  </div>

  @if (counts) {
    <div class="sum">
      <div><span class="muted">Otwarte</span><b>{{ counts.open }}</b></div>
      <div><span class="muted">Krytyczne (otwarte)</span><b [style.color]="counts.highOpen ? '#B4232A' : '#0F2A2A'">{{ counts.highOpen }}</b></div>
      <div><span class="muted">Rozwiązane</span><b>{{ counts.resolved }}</b></div>
    </div>
  }

  @if (loading) { <p class="muted">Ładowanie…</p> }
  @else if (items.length === 0) {
    <div class="card pad muted">Brak zgłoszeń w tym widoku. Zgłoszenia dodajesz z karty sklepu lub użytkownika (przycisk „Zgłoś naruszenie").</div>
  }
  @else {
    <div class="list">
      @for (f of items; track f.id) {
        <div class="flag" [class.resolved]="f.status === 'Resolved'">
          <div class="ftop">
            <span class="pill" [attr.data-status]="f.subjectType === 'store' ? 'Processing' : 'Pending'">{{ f.subjectType === 'store' ? 'Sklep' : 'Użytkownik' }}</span>
            <b class="subj">{{ f.subjectLabel }}</b>
            <span class="sev" [attr.data-sev]="f.severity">{{ sevLabel(f.severity) }}</span>
            <span class="cat">{{ catLabel(f.category) }}</span>
            <span class="date muted">{{ f.createdAtUtc | date:'dd.MM HH:mm' }}</span>
          </div>
          @if (f.note) { <div class="note">{{ f.note }}</div> }
          @if (f.status === 'Resolved') {
            <div class="res">✓ Rozwiązane {{ f.resolvedAtUtc | date:'dd.MM.yyyy' }}@if (f.resolution) { — {{ f.resolution }} }</div>
          }
          <div class="factions">
            @if (f.status === 'Open') {
              <button class="btn primary sm" (click)="resolve(f)">Rozwiąż</button>
            } @else {
              <button class="btn ghost sm" (click)="reopen(f)">Otwórz ponownie</button>
            }
            <button class="btn ghost sm danger" (click)="remove(f)">Usuń</button>
          </div>
        </div>
      }
    </div>
  }
  `,
  styles: [`
    .sum{ display:flex; flex-wrap:wrap; gap:14px; margin-bottom:16px; }
    .sum>div{ background:#fff; border:1px solid #E5E7EB; border-radius:12px; padding:14px 18px; display:flex; flex-direction:column; min-width:150px; }
    .sum span{ font-size:12px; } .sum b{ font-size:22px; color:#0F2A2A; }
    .list{ display:flex; flex-direction:column; gap:10px; }
    .flag{ background:#fff; border:1px solid #E5E7EB; border-radius:12px; padding:14px 16px; }
    .flag.resolved{ opacity:.72; }
    .ftop{ display:flex; align-items:center; gap:10px; flex-wrap:wrap; }
    .ftop .subj{ color:#0F2A2A; font-size:14.5px; }
    .cat{ font-size:12px; color:#6B7280; background:#F1F3F5; padding:3px 8px; border-radius:20px; }
    .date{ margin-left:auto; font-size:12px; }
    .sev{ font-size:11px; font-weight:700; padding:3px 9px; border-radius:20px; text-transform:uppercase; letter-spacing:.03em; }
    .sev[data-sev=low]{ background:#EAF7EE; color:#128040; }
    .sev[data-sev=medium]{ background:#FFF3EA; color:#EA6A0C; }
    .sev[data-sev=high]{ background:#FEECEC; color:#B4232A; }
    .note{ margin-top:10px; background:#F7F9FA; border:1px solid #EEF2F4; border-radius:8px; padding:10px 12px; font-size:13.5px; color:#1F2A2A; white-space:pre-wrap; }
    .res{ margin-top:10px; color:#128040; font-size:13px; font-weight:600; }
    .factions{ margin-top:12px; display:flex; gap:8px; flex-wrap:wrap; }
    .btn.danger{ color:#B4232A; } .btn.danger:hover{ border-color:#B4232A; }
  `],
})
export class ComplianceComponent implements OnInit {
  private api = inject(Api);
  items: Flag[] = [];
  counts: Counts | null = null;
  loading = false;
  statusFilter = 'Open';

  ngOnInit() { this.load(); this.loadCounts(); }

  load() {
    this.loading = true;
    const q = this.statusFilter ? `?status=${this.statusFilter}` : '';
    this.api.get<Flag[]>(`/identity/admin/compliance${q}`).subscribe({
      next: i => { this.items = i; this.loading = false; },
      error: () => { this.items = []; this.loading = false; },
    });
  }

  loadCounts() {
    this.api.get<Counts>('/identity/admin/compliance/counts').subscribe({
      next: c => this.counts = c, error: () => this.counts = null,
    });
  }

  resolve(f: Flag) {
    const res = prompt('Jak rozwiązano zgłoszenie? (opcjonalny opis)', '') ?? '';
    this.api.post(`/identity/admin/compliance/${f.id}/resolve`, { resolution: res, reopen: false }).subscribe({
      next: () => { this.load(); this.loadCounts(); }, error: () => this.load(),
    });
  }

  reopen(f: Flag) {
    this.api.post(`/identity/admin/compliance/${f.id}/resolve`, { reopen: true }).subscribe({
      next: () => { this.load(); this.loadCounts(); }, error: () => this.load(),
    });
  }

  remove(f: Flag) {
    if (!confirm('Usunąć zgłoszenie na trwałe?')) return;
    this.api.delete(`/identity/admin/compliance/${f.id}`).subscribe({
      next: () => { this.load(); this.loadCounts(); }, error: () => this.load(),
    });
  }

  sevLabel(s: string) { return s === 'high' ? 'Krytyczne' : s === 'low' ? 'Niskie' : 'Średnie'; }
  catLabel(c: string) {
    const m: Record<string, string> = { regulamin: 'Regulamin', platnosci: 'Płatności', tresci: 'Treści', dostawa: 'Dostawa', inne: 'Inne' };
    return m[c] ?? c;
  }
}
