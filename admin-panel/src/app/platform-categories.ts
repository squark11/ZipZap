import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, PlatformCategoryDto } from './api';

/**
 * Wspólne kategorie platformy (F-6) — chipy na Starcie aplikacji klienta („Owoce", „Pieczywo"…).
 * Administrator zmienia nazwę, kolejność i widoczność. Klucz to nazwa ikony w aplikacji, więc nowych kategorii
 * nie dodaje się tutaj (nowa kategoria = nowa ikona w aplikacji).
 */
@Component({
  selector: 'app-platform-categories',
  imports: [CommonModule, FormsModule],
  template: `
  <div class="page-head">
    <h1>Kategorie platformy</h1>
    <div class="controls">
      <button class="btn ghost sm" [disabled]="loading" (click)="load()">Odśwież</button>
    </div>
  </div>
  <p class="lead muted">
    Wspólne kategorie widoczne w aplikacji klienta na ekranie Start. Sklepy przypinają do nich swoje kategorie
    w zakładce „Oferta"; klient widzi pod kategorią tylko sklepy, które mają w niej dostępny produkt.
    Ukryta kategoria znika z aplikacji, ale przypięcia sklepów zostają.
  </p>

  <div class="card">
    <table>
      <thead><tr><th>Ikona (klucz)</th><th>Nazwa w aplikacji</th><th class="right">Kolejność</th><th>Widoczna</th><th></th></tr></thead>
      <tbody>
        @for (c of rows; track c.key) {
          <tr>
            <td class="muted"><code>{{ c.key }}</code></td>
            <td><input [(ngModel)]="c.name" maxlength="60" [attr.aria-label]="'Nazwa kategorii ' + c.key" /></td>
            <td class="right"><input [(ngModel)]="c.sortOrder" type="number" step="10" style="width:90px"
                [attr.aria-label]="'Kolejność kategorii ' + c.key" /></td>
            <td><input type="checkbox" [(ngModel)]="c.isActive" [attr.aria-label]="'Widoczna: ' + c.key" /></td>
            <td>
              <div class="actions">
                <button class="btn sm" [disabled]="!changed(c) || saving === c.key" (click)="save(c)">Zapisz</button>
                @if (saved === c.key) { <span class="ok-msg">✓ Zapisano</span> }
              </div>
            </td>
          </tr>
        }
        @if (!loading && rows.length === 0) { <tr><td colspan="5" class="muted pad">Brak kategorii.</td></tr> }
      </tbody>
    </table>
  </div>
  @if (error) { <p class="error" role="alert">{{ error }}</p> }
  `,
})
export class PlatformCategoriesComponent implements OnInit {
  private api = inject(Api);
  rows: PlatformCategoryDto[] = [];
  private original = new Map<string, PlatformCategoryDto>();
  loading = false;
  saving: string | null = null;
  saved: string | null = null;
  error = '';

  ngOnInit() { this.load(); }

  load() {
    this.loading = true; this.error = '';
    this.api.get<PlatformCategoryDto[]>('/catalog/admin/platform-categories').subscribe({
      next: list => {
        this.rows = list.map(c => ({ ...c }));
        this.original = new Map(list.map(c => [c.key, { ...c }]));
        this.loading = false;
      },
      error: e => { this.loading = false; this.error = message(e); },
    });
  }

  changed(c: PlatformCategoryDto) {
    const o = this.original.get(c.key);
    return !o || o.name !== c.name || Number(o.sortOrder) !== Number(c.sortOrder) || o.isActive !== c.isActive;
  }

  save(c: PlatformCategoryDto) {
    const name = c.name.trim();
    if (!name) { this.error = 'Nazwa nie może być pusta.'; return; }
    this.saving = c.key; this.error = '';
    this.api.patch<PlatformCategoryDto>(`/catalog/admin/platform-categories/${c.key}`,
      { name, sortOrder: Number(c.sortOrder) || 0, isActive: c.isActive }).subscribe({
      next: r => {
        this.saving = null; this.saved = c.key;
        this.original.set(r.key, { ...r });
        Object.assign(c, r);
        setTimeout(() => { if (this.saved === c.key) this.saved = null; }, 2000);
      },
      error: e => { this.saving = null; this.error = message(e); },
    });
  }
}

function message(e: any): string {
  return e?.error?.detail || e?.error?.title || e?.message || 'Nie udało się zapisać.';
}
