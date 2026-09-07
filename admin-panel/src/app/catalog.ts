import { Component, effect, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, CategoryDto, ProductDto } from './api';

@Component({
  selector: 'app-catalog',
  imports: [CommonModule, FormsModule],
  template: `
  <div class="row">
    <h2>Oferta sklepu</h2>
    <div class="controls"><button class="ghost sm" (click)="load()">Odśwież</button></div>
  </div>

  <div class="detail">
    <strong>Dodaj kategorię:</strong>
    <div class="actions" style="margin-top:8px">
      <input [(ngModel)]="newCategory" placeholder="Nazwa kategorii" />
      <button class="ghost sm" (click)="addCategory()">Dodaj kategorię</button>
    </div>
  </div>

  <div class="detail" style="margin-top:10px">
    <strong>Dodaj produkt:</strong>
    <div class="formgrid" style="margin-top:8px">
      <label>Nazwa<input [(ngModel)]="np.name" placeholder="np. Chleb żytni" /></label>
      <label>Cena<input [(ngModel)]="np.price" type="number" step="0.01" /></label>
      <label>Jednostka<input [(ngModel)]="np.unit" placeholder="szt" /></label>
      <label>Kategoria
        <select [(ngModel)]="np.categoryId">
          <option [ngValue]="undefined">—</option>
          @for (c of categories; track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
        </select>
      </label>
      <button class="primary" (click)="addProduct()">Dodaj</button>
    </div>
  </div>

  <table style="margin-top:16px">
    <thead>
      <tr><th>Produkt</th><th>Kategoria</th><th class="right">Cena</th><th>Dostępność</th><th>Akcje</th></tr>
    </thead>
    <tbody>
      @for (p of products; track p.id) {
        <tr>
          <td>{{ p.name }}</td>
          <td class="muted">{{ categoryName(p.categoryId) }}</td>
          <td class="right">{{ p.price | number:'1.2-2' }} {{ p.currency }}/{{ p.unit }}</td>
          <td>
            <span class="badge" [attr.data-status]="p.isAvailable ? 'Completed' : 'Cancelled'">
              {{ p.isAvailable ? 'dostępny' : 'niedostępny' }}
            </span>
          </td>
          <td>
            <div class="actions">
              <button class="ghost sm" (click)="toggleAvailability(p)">{{ p.isAvailable ? 'Ukryj' : 'Pokaż' }}</button>
              <button class="ghost sm" (click)="changePrice(p)">Zmień cenę</button>
            </div>
          </td>
        </tr>
      }
      @if (products.length === 0) { <tr><td colspan="5" class="muted">Brak produktów.</td></tr> }
    </tbody>
  </table>
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
    this.api.patch(`/catalog/products/${p.id}`, { price })
      .subscribe({ next: () => this.load(), error: e => alert(err(e)) });
  }

  categoryName(id?: string) { return this.categories.find(c => c.id === id)?.name ?? '—'; }
}

function err(e: any): string { return 'Błąd: ' + (e?.error?.detail ?? e?.message ?? 'nieznany'); }
