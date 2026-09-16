import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api } from './api';
import { ConfigModalComponent } from './config-modal';

interface TestCredential {
  id: string; label: string; role: string; email: string; password: string; note?: string; updatedAtUtc: string;
}
interface EditModel { id?: string; label: string; role: string; email: string; password: string; note: string; }

/**
 * Konta testowe: zapisane dane logowania kont testowych (klient/dostawca/sklep) w koncie admina.
 * ADMIN-ONLY; hasła jawne (to konta testowe, nie realni użytkownicy). Samodzielny modal.
 */
@Component({
  selector: 'app-test-accounts',
  imports: [CommonModule, FormsModule, ConfigModalComponent],
  template: `
  <app-config-modal [open]="open" title="Konta testowe" [showFooter]="false" (close)="onClose()">
    <p class="muted" style="margin-top:0">Zapisane dane logowania kont testowych (klient, dostawca, sklep) — do szybkiego logowania i pomocy. Widoczne tylko dla administratora.</p>

    @if (loading) { <p class="muted">Ładowanie…</p> }
    @else {
      @if (items.length === 0) { <p class="muted">Brak zapisanych kont. Dodaj pierwsze poniżej.</p> }
      <div class="list">
        @for (c of items; track c.id) {
          <div class="acc">
            <div class="acc-top">
              <span class="pill" [attr.data-status]="roleStatus(c.role)">{{ c.role || '—' }}</span>
              <b class="lbl">{{ c.label }}</b>
              <div class="acc-actions">
                <button class="ic" title="Edytuj" (click)="edit(c)">✎</button>
                <button class="ic danger" title="Usuń" (click)="remove(c)">🗑</button>
              </div>
            </div>
            <div class="fields">
              <div class="f"><span>E-mail</span><code>{{ c.email }}</code><button class="ic" (click)="copy(c.email)" title="Kopiuj">⧉</button></div>
              <div class="f"><span>Hasło</span><code>{{ revealed.has(c.id) ? c.password : '••••••••' }}</code>
                <button class="ic" (click)="toggle(c.id)" title="Pokaż/ukryj">{{ revealed.has(c.id) ? '🙈' : '👁' }}</button>
                <button class="ic" (click)="copy(c.password)" title="Kopiuj">⧉</button>
              </div>
              @if (c.note) { <div class="note">{{ c.note }}</div> }
            </div>
          </div>
        }
      </div>
    }

    <div class="form">
      <h3>{{ model.id ? 'Edytuj konto' : 'Dodaj konto testowe' }}</h3>
      <div class="grid2">
        <div><label>Etykieta</label><input name="lbl" [(ngModel)]="model.label" placeholder="np. Klient demo" /></div>
        <div><label>Rola</label>
          <select name="role" [(ngModel)]="model.role">
            <option value="Klient">Klient</option>
            <option value="Dostawca">Dostawca</option>
            <option value="Sklep">Sklep</option>
            <option value="Administrator">Administrator</option>
            <option value="Inne">Inne</option>
          </select>
        </div>
      </div>
      <div class="grid2">
        <div><label>E-mail (login)</label><input name="em" [(ngModel)]="model.email" placeholder="test@dowozka.pl" /></div>
        <div><label>Hasło</label><input name="pw" [(ngModel)]="model.password" placeholder="hasło konta testowego" /></div>
      </div>
      <label>Notatka (opcjonalnie)</label>
      <input name="nt" [(ngModel)]="model.note" placeholder="np. sklep Rapacz, konto właściciela" />
      @if (error) { <p class="warn">{{ error }}</p> }
      <div class="form-actions">
        @if (model.id) { <button class="btn ghost sm" (click)="resetForm()">Anuluj edycję</button> }
        <button class="btn primary sm" (click)="save()" [disabled]="saving">{{ model.id ? 'Zapisz zmiany' : 'Dodaj' }}</button>
      </div>
    </div>
  </app-config-modal>
  `,
  styles: [`
    .list { display:flex; flex-direction:column; gap:10px; margin-bottom:8px; }
    .acc { border:1px solid #E5E7EB; border-radius:12px; padding:12px 14px; }
    .acc-top { display:flex; align-items:center; gap:10px; }
    .acc-top .lbl { color:#0F2A2A; font-size:14.5px; }
    .acc-actions { margin-left:auto; display:flex; gap:4px; }
    .ic { border:1px solid #E5E7EB; background:#fff; border-radius:7px; width:30px; height:30px; cursor:pointer; font-size:14px; line-height:1; color:#3A3F4B; }
    .ic:hover { border-color:#14B9BA; }
    .ic.danger:hover { border-color:#B4232A; color:#B4232A; }
    .fields { margin-top:10px; display:flex; flex-direction:column; gap:7px; }
    .f { display:flex; align-items:center; gap:8px; }
    .f > span { font-size:12px; color:#6B7280; width:56px; flex:none; }
    .f code { font-family:'JetBrains Mono',monospace; font-size:13.5px; background:#F1F3F5; padding:5px 9px; border-radius:7px; color:#0F2A2A; flex:1; overflow:auto; word-break:break-all; }
    .note { font-size:12.5px; color:#6B7280; background:#F7F9FA; padding:7px 10px; border-radius:7px; }
    .form { margin-top:16px; border-top:1px solid #eef0f3; padding-top:14px; }
    .form h3 { font-size:14px; color:#3A3F4B; margin:0 0 10px; }
    .form label { display:block; font-size:13px; font-weight:600; color:#3A3F4B; margin:12px 0 6px; }
    .form input, .form select { border:1px solid #E5E7EB; border-radius:8px; padding:9px 11px; font-size:14px; width:100%; box-sizing:border-box; background:#fff; }
    .form input:focus, .form select:focus { outline:none; border-color:#14B9BA; }
    .grid2 { display:grid; grid-template-columns:1fr 1fr; gap:14px; }
    @media (max-width:520px){ .grid2 { grid-template-columns:1fr; } }
    .form-actions { display:flex; justify-content:flex-end; gap:10px; margin-top:16px; }
    .warn { margin-top:12px; background:#FEECEC; color:#B4232A; padding:10px 12px; border-radius:8px; font-size:13px; }
  `],
})
export class TestAccountsComponent {
  private api = inject(Api);

  @Input() set open(v: boolean) { this._open = v; if (v) { this.resetForm(); this.load(); } }
  get open() { return this._open; }
  private _open = false;
  @Output() close = new EventEmitter<void>();

  items: TestCredential[] = [];
  loading = false;
  saving = false;
  error = '';
  revealed = new Set<string>();
  model: EditModel = this.blank();

  private blank(): EditModel { return { label: '', role: 'Klient', email: '', password: '', note: '' }; }
  onClose() { this.close.emit(); }
  toggle(id: string) { this.revealed.has(id) ? this.revealed.delete(id) : this.revealed.add(id); }
  copy(v: string) { navigator.clipboard?.writeText(v).catch(() => {}); }

  load() {
    this.loading = true;
    this.api.get<TestCredential[]>('/identity/admin/test-accounts').subscribe({
      next: i => { this.items = i; this.loading = false; },
      error: () => { this.items = []; this.loading = false; },
    });
  }

  edit(c: TestCredential) {
    this.model = { id: c.id, label: c.label, role: c.role || 'Inne', email: c.email, password: c.password, note: c.note ?? '' };
    this.error = '';
  }

  resetForm() { this.model = this.blank(); this.error = ''; }

  save() {
    this.error = '';
    if (!this.model.label.trim() || !this.model.email.trim() || !this.model.password.trim()) {
      this.error = 'Etykieta, e-mail i hasło są wymagane.'; return;
    }
    this.saving = true;
    this.api.put<TestCredential>('/identity/admin/test-accounts', {
      id: this.model.id ?? null,
      label: this.model.label.trim(), role: this.model.role, email: this.model.email.trim(),
      password: this.model.password, note: this.model.note.trim() || null,
    }).subscribe({
      next: () => { this.saving = false; this.resetForm(); this.load(); },
      error: e => { this.saving = false; this.error = e?.error?.detail ?? 'Nie udało się zapisać.'; },
    });
  }

  remove(c: TestCredential) {
    if (!confirm(`Usunąć zapisane dane „${c.label}"?`)) return;
    this.api.delete(`/identity/admin/test-accounts/${c.id}`).subscribe({
      next: () => { if (this.model.id === c.id) this.resetForm(); this.load(); },
      error: () => this.load(),
    });
  }

  roleStatus(role: string) {
    const r = (role || '').toLowerCase();
    if (r.includes('klient')) return 'Completed';
    if (r.includes('dostaw')) return 'Processing';
    if (r.includes('sklep')) return 'Pending';
    return 'Cancelled';
  }
}
