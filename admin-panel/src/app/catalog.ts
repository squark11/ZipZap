import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, CategoryDto, ProductDto } from './api';

interface ImportRow { row: number; name: string; action: string; error?: string; }
interface ImportReport { committed: boolean; total: number; created: number; updated: number; failed: number; rows: ImportRow[]; }

@Component({
  selector: 'app-catalog',
  imports: [CommonModule, FormsModule],
  template: `
  <div class="page-head">
    <h1>Oferta</h1>
    <div class="controls">
      <button class="btn ghost sm" (click)="exportCsv()" [disabled]="exporting || products.length === 0">Eksportuj CSV</button>
      <button class="btn ghost sm" (click)="load()">Odśwież</button>
    </div>
  </div>

  <div class="card pad" style="margin-bottom:16px">
    <div class="inline-form" style="margin-bottom:14px">
      <label>Nowa kategoria<input [(ngModel)]="newCategory" placeholder="np. Pieczywo" /></label>
      <button class="btn" (click)="addCategory()">Dodaj kategorię</button>
    </div>
    <div class="formgrid">
      <label>Nazwa produktu<input [(ngModel)]="np.name" placeholder="np. Chleb żytni" /></label>
      <label>Cena<input [(ngModel)]="np.price" type="number" step="0.01" /></label>
      <label>Jednostka<input [(ngModel)]="np.unit" placeholder="szt" /></label>
      <label>Kategoria
        <select [(ngModel)]="np.categoryId">
          <option [ngValue]="undefined">—</option>
          @for (c of categories; track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
        </select>
      </label>
      <button class="btn primary" (click)="addProduct()">Dodaj produkt</button>
    </div>
  </div>

  <div class="card pad" style="margin-bottom:16px">
    <div style="display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap">
      <div>
        <strong>Import asortymentu z pliku</strong>
        <div class="muted" style="font-size:13px">Plik CSV (separator „;"), nagłówek: <code>nazwa;kategoria;cena;jednostka;dostepny</code>. Aktualizacja po nazwie.</div>
      </div>
      <div class="actions">
        <button class="btn ghost sm" (click)="downloadTemplate()">Pobierz szablon</button>
        <label class="btn sm" style="cursor:pointer;margin:0">
          Wybierz plik…
          <input type="file" accept=".csv,text/csv" (change)="onFile($event)" hidden />
        </label>
      </div>
    </div>

    @if (importFileName) {
      <div style="margin-top:12px;display:flex;align-items:center;gap:10px;flex-wrap:wrap">
        <span class="pill" data-status="Processing">{{ importFileName }}</span>
        <button class="btn ghost sm" (click)="preview()" [disabled]="importing || !importText">Podgląd</button>
        <button class="btn ghost sm" (click)="clearImport()" [disabled]="importing">Anuluj</button>
      </div>
    }

    @if (importReport) {
      <div style="margin-top:14px">
        <div class="muted" style="margin-bottom:8px">
          Wierszy: <strong>{{ importReport.total }}</strong> ·
          nowych: <strong>{{ importReport.created }}</strong> ·
          aktualizacji: <strong>{{ importReport.updated }}</strong> ·
          błędów: <strong [style.color]="importReport.failed ? 'var(--danger, #c0392b)' : 'inherit'">{{ importReport.failed }}</strong>
          @if (importReport.committed) { · <span style="color:var(--ok, #1e8e3e)">zaimportowano ✓</span> }
        </div>
        <table>
          <thead><tr><th>#</th><th>Nazwa</th><th>Wynik</th><th>Uwagi</th></tr></thead>
          <tbody>
            @for (r of importReport.rows; track r.row) {
              <tr>
                <td class="muted">{{ r.row }}</td>
                <td>{{ r.name || '—' }}</td>
                <td><span class="pill" [attr.data-status]="rowStatus(r.action)">{{ r.action }}</span></td>
                <td class="muted">{{ r.error || '' }}</td>
              </tr>
            }
          </tbody>
        </table>
        @if (!importReport.committed) {
          <div style="margin-top:12px">
            <button class="btn primary" (click)="confirmImport()"
              [disabled]="importing || (importReport.created + importReport.updated) === 0">
              Zatwierdź import ({{ importReport.created }} nowych, {{ importReport.updated }} aktualizacji)
            </button>
            @if (importReport.failed) { <span class="muted" style="margin-left:10px">Błędne wiersze zostaną pominięte.</span> }
          </div>
        }
      </div>
    }
  </div>

  <div class="card">
    <table>
      <thead><tr><th>Produkt</th><th>Kategoria</th><th class="right">Cena</th><th>Dostępność</th><th>Akcje</th></tr></thead>
      <tbody>
        @for (p of products; track p.id) {
          <tr>
            <td><strong>{{ p.name }}</strong></td>
            <td class="muted">{{ categoryName(p.categoryId) }}</td>
            <td class="right">{{ p.price | number:'1.2-2' }} {{ p.currency }}/{{ p.unit }}</td>
            <td><span class="pill" [attr.data-status]="p.isAvailable ? 'Completed' : 'Cancelled'">{{ p.isAvailable ? 'dostępny' : 'ukryty' }}</span></td>
            <td>
              <div class="actions">
                <button class="btn ghost sm" (click)="toggleAvailability(p)">{{ p.isAvailable ? 'Ukryj' : 'Pokaż' }}</button>
                <button class="btn ghost sm" (click)="changePrice(p)">Zmień cenę</button>
              </div>
            </td>
          </tr>
        }
        @if (products.length === 0) { <tr><td colspan="5" class="muted pad">Brak produktów.</td></tr> }
      </tbody>
    </table>
  </div>
  `,
})
export class CatalogComponent {
  private api = inject(Api);
  storeId = input<string>('');

  categories: CategoryDto[] = [];
  products: ProductDto[] = [];
  newCategory = '';
  np: { name: string; price: number; unit: string; categoryId?: string } = { name: '', price: 0, unit: 'szt', categoryId: undefined };

  importText: string | null = null;
  importFileName = '';
  importReport: ImportReport | null = null;
  importing = false;
  exporting = false;

  constructor() {
    effect(() => { const id = this.storeId(); if (id) { this.load(); this.clearImport(); } });
  }

  load() {
    const id = this.storeId();
    this.api.getPublic<CategoryDto[]>(`/catalog/stores/${id}/categories`).subscribe(c => this.categories = c);
    this.api.getPublic<ProductDto[]>(`/catalog/stores/${id}/products`).subscribe(p => this.products = p);
  }

  addCategory() {
    if (!this.newCategory.trim()) return;
    this.api.post(`/catalog/stores/${this.storeId()}/categories`, { name: this.newCategory, sortOrder: this.categories.length })
      .subscribe({ next: () => { this.newCategory = ''; this.load(); }, error: e => alert(err(e)) });
  }

  addProduct() {
    if (!this.np.name.trim()) return;
    this.api.post(`/catalog/stores/${this.storeId()}/products`, this.np)
      .subscribe({ next: () => { this.np = { name: '', price: 0, unit: 'szt', categoryId: undefined }; this.load(); }, error: e => alert(err(e)) });
  }

  toggleAvailability(p: ProductDto) {
    this.api.patch(`/catalog/products/${p.id}`, { isAvailable: !p.isAvailable })
      .subscribe({ next: () => this.load(), error: e => alert(err(e)) });
  }

  changePrice(p: ProductDto) {
    const val = prompt(`Nowa cena dla "${p.name}"`, String(p.price));
    if (val === null) return;
    const price = Number(val.replace(',', '.'));
    if (Number.isNaN(price)) { alert('Nieprawidłowa cena'); return; }
    this.api.patch(`/catalog/products/${p.id}`, { price }).subscribe({ next: () => this.load(), error: e => alert(err(e)) });
  }

  categoryName(id?: string) { return this.categories.find(c => c.id === id)?.name ?? '—'; }

  // ---------- Import z pliku ----------

  onFile(ev: Event) {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    this.importReport = null;
    if (!file) { this.importText = null; this.importFileName = ''; return; }
    this.importFileName = file.name;
    const reader = new FileReader();
    reader.onload = () => { this.importText = String(reader.result ?? ''); };
    reader.onerror = () => { alert('Nie udało się odczytać pliku.'); this.clearImport(); };
    reader.readAsText(file, 'UTF-8');
    input.value = ''; // pozwól wybrać ten sam plik ponownie
  }

  preview() { this.send(false); }
  confirmImport() { this.send(true); }

  private send(commit: boolean) {
    if (!this.importText) return;
    this.importing = true;
    this.api.post<ImportReport>(`/catalog/stores/${this.storeId()}/products/import?commit=${commit}`, { content: this.importText })
      .subscribe({
        next: r => {
          this.importReport = r;
          this.importing = false;
          if (commit) { this.load(); }
        },
        error: e => { this.importing = false; alert(err(e)); },
      });
  }

  clearImport() { this.importText = null; this.importFileName = ''; this.importReport = null; }

  exportCsv() {
    this.exporting = true;
    this.api.getBlob(`/catalog/stores/${this.storeId()}/products/export`).subscribe({
      next: blob => {
        this.exporting = false;
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = `asortyment-${new Date().toISOString().slice(0, 10)}.csv`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: e => { this.exporting = false; alert(err(e)); },
    });
  }

  rowStatus(action: string) {
    return action === 'nowy' ? 'Completed' : action === 'aktualizacja' ? 'Processing' : 'Cancelled';
  }

  downloadTemplate() {
    const csv = [
      'nazwa;kategoria;cena;jednostka;dostepny',
      'Chleb żytni;Pieczywo;6,00;szt;tak',
      'Mleko 2%;Nabiał;3,49;szt;tak',
      'Jabłka;Owoce i warzywa;4,99;kg;tak',
      'Sok pomarańczowy 1L;Napoje;6,49;szt;nie',
    ].join('\r\n');
    const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = 'asortyment-import-szablon.csv';
    a.click();
    URL.revokeObjectURL(url);
  }
}

function err(e: any): string { return 'Błąd: ' + (e?.error?.detail ?? e?.message ?? 'nieznany'); }
