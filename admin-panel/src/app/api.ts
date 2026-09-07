import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface StoreDto { id: string; name: string; city: string; slug: string; commissionRate: number; isActive: boolean; }
export interface CategoryDto { id: string; storeId: string; name: string; sortOrder: number; }
export interface ProductDto { id: string; storeId: string; categoryId?: string; name: string; price: number; currency: string; unit: string; isAvailable: boolean; }
export interface OrderItemDto { productId: string; productName: string; unitPrice: number; quantity: number; lineTotal: number; }
export interface OrderStatusChangeDto { fromStatus?: string; toStatus: string; changedAtUtc: string; }
export interface OrderDto {
  id: string; storeId: string; status: string;
  subtotal: number; commissionAmount: number; deliveryFee: number; total: number;
  contactPhone: string; deliveryAddress: string; placedAtUtc: string;
  items: OrderItemDto[]; history: OrderStatusChangeDto[];
}
export interface DeliveryDto { id: string; orderId: string; storeId: string; status: string; createdAtUtc: string; }
export interface CommissionDto { storeId: string; totalCommission: number; entries: number; }

@Injectable({ providedIn: 'root' })
export class Api {
  private http = inject(HttpClient);
  readonly base = 'http://localhost:5080/api';

  readonly token = signal<string | null>(null);
  readonly userEmail = signal<string>('');
  readonly isLoggedIn = computed(() => !!this.token());

  login(email: string, password: string): Observable<{ accessToken: string }> {
    return this.http.post<{ accessToken: string; user: { email: string } }>(
      `${this.base}/identity/login`, { email, password }
    ).pipe(tap(r => { this.token.set(r.accessToken); this.userEmail.set(r.user?.email ?? email); }));
  }

  logout() { this.token.set(null); this.userEmail.set(''); }

  private opts() { return { headers: { Authorization: `Bearer ${this.token()}` } }; }

  get<T>(path: string) { return this.http.get<T>(`${this.base}${path}`, this.opts()); }
  getPublic<T>(path: string) { return this.http.get<T>(`${this.base}${path}`); }
  post<T>(path: string, body: unknown) { return this.http.post<T>(`${this.base}${path}`, body, this.opts()); }
  patch<T>(path: string, body: unknown) { return this.http.patch<T>(`${this.base}${path}`, body, this.opts()); }
}
