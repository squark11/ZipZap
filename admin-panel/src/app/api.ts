import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface StoreDto {
  id: string; name: string; city: string; slug: string; commissionRate: number; isActive: boolean;
  status: string; minimumOrderValue: number; isAcceptingOrders: boolean;
  description?: string; address?: string; phone?: string;
}
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
export interface DeliveryDto {
  id: string; orderId: string; storeId: string; driverId?: string; status: string;
  createdAtUtc: string; pickedUpAtUtc?: string; deliveredAtUtc?: string;
}
export interface PaymentDto {
  orderId: string; storeId: string; amount: number; deliveryFee: number; commissionAmount: number;
  status: string; provider?: string; redirectUrl?: string; providerRef?: string;
}
export interface CommissionDto { storeId: string; totalCommission: number; entries: number; }
export interface PaymentsSummaryDto {
  storeId: string; pending: number; paid: number; settled: number; failed: number;
  grossPaid: number; settledCommission: number;
}
export interface LedgerEntryDto { orderId: string; amount: number; createdAtUtc: string; }
export interface TeamMemberDto {
  id: string; email: string; fullName: string; phone?: string;
  isActive: boolean; isEmailVerified: boolean; role: string; storeId: string;
}

@Injectable({ providedIn: 'root' })
export class Api {
  private http = inject(HttpClient);
  readonly base = 'http://localhost:5080/api';

  readonly token = signal<string | null>(null);
  readonly userEmail = signal<string>('');
  readonly roles = signal<string[]>([]);
  readonly storeIds = signal<string[]>([]);
  readonly isLoggedIn = computed(() => !!this.token());
  readonly isAdmin = computed(() => this.roles().includes('Admin'));

  login(email: string, password: string): Observable<{ accessToken: string }> {
    return this.http.post<{ accessToken: string; user: { email: string; roles: string[]; storeIds: string[] } }>(
      `${this.base}/identity/login`, { email, password }
    ).pipe(tap(r => {
      this.token.set(r.accessToken);
      this.userEmail.set(r.user?.email ?? email);
      this.roles.set(r.user?.roles ?? []);
      this.storeIds.set(r.user?.storeIds ?? []);
    }));
  }

  logout() { this.token.set(null); this.userEmail.set(''); this.roles.set([]); this.storeIds.set([]); }

  private opts() { return { headers: { Authorization: `Bearer ${this.token()}` } }; }

  get<T>(path: string) { return this.http.get<T>(`${this.base}${path}`, this.opts()); }
  getPublic<T>(path: string) { return this.http.get<T>(`${this.base}${path}`); }
  post<T>(path: string, body: unknown) { return this.http.post<T>(`${this.base}${path}`, body, this.opts()); }
  patch<T>(path: string, body: unknown) { return this.http.patch<T>(`${this.base}${path}`, body, this.opts()); }
}
