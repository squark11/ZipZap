import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, CategoryDto, ProductDto } from './api';

@Component({
  selector: 'app-catalog',
  imports: [CommonModule, FormsModule],
  template: `
  <div class="page-head">
    <h1>Oferta</h1>
    <div class="controls"><button class="btn ghost sm" (click)="load()">Odśwież</button></div>
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
  storeId = input.required<string>();

  categories: CategoryDto[] = [];
  products: ProductDto[] = [];
  newCategory = '';
  np: { name: string; price: number; unit: string; categoryId?: string } = { name: '', price: 0, unit: 'szt', categoryId: undefined };

  constructor() {
    effect(() => { const id = this.storeId(); if (id) this.load(); });
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
}

function err(e: any): string { return 'Błąd: ' + (e?.error?.detail ?? e?.message ?? 'nieznany'); }
