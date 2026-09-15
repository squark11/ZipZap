import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface StoreDto {
  id: string; name: string; city: string; slug: string; commissionRate: number; isActive: boolean;
  status: string; minimumOrderValue: number; isAcceptingOrders: boolean;
  description?: string; address?: string; phone?: string;
  logoUrl?: string; latitude?: number; longitude?: number;
}
export interface CategoryDto { id: string; storeId: string; name: string; sortOrder: number; }
export interface ProductUnitOption { unit: string; price: number; }
export interface ProductDto { id: string; storeId: string; categoryId?: string; name: string; price: number; currency: string; unit: string; isAvailable: boolean; unitOptions?: ProductUnitOption[]; }
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

export interface AuthResponse {
  accessToken: string;
  refreshToken?: string;
  user?: { email: string; roles: string[]; storeIds: string[] };
}

@Injectable({ providedIn: 'root' })
export class Api {
  private http = inject(HttpClient);
  // Dev (localhost) → lokalne API; wdrożony panel → API na Render. (Docelowo: konfiguracja env.)
  readonly base = (typeof location !== 'undefined'
      && location.hostname !== 'localhost' && location.hostname !== '127.0.0.1')
    ? 'https://dowozka-api.onrender.com/api'
    : 'http://localhost:5080/api';

  readonly token = signal<string | null>(null);
  private readonly refreshToken = signal<string | null>(null);
  readonly userEmail = signal<string>('');
  readonly roles = signal<string[]>([]);
  readonly storeIds = signal<string[]>([]);
  readonly isLoggedIn = computed(() => !!this.token());
  readonly isAdmin = computed(() => this.roles().includes('Admin'));
  readonly isStoreAdmin = computed(() => this.roles().includes('StoreEmployee'));
  readonly isDriver = computed(() => this.roles().includes('Driver'));
  readonly isCustomer = computed(() => this.roles().includes('Customer'));
  /// Etykieta roli do UI (nazwy wg właściciela). Admin serwisu ma pierwszeństwo.
  readonly roleLabel = computed(() => {
    const r = this.roles();
    if (r.includes('Admin')) return 'Administrator serwisu';
    if (r.includes('StoreEmployee')) return 'Administrator sklepu';
    if (r.includes('Driver')) return 'Dostawca';
    if (r.includes('Customer')) return 'Klient';
    return 'Użytkownik';
  });
  /// Czy zalogowany user ma którąkolwiek z podanych ról (dla rejestru modułów).
  hasAnyRole(roles: readonly string[]): boolean {
    const mine = this.roles();
    return roles.some(r => mine.includes(r));
  }

  private apply(r: AuthResponse, fallbackEmail?: string) {
    this.token.set(r.accessToken);
    if (r.refreshToken) this.refreshToken.set(r.refreshToken);
    this.userEmail.set(r.user?.email ?? fallbackEmail ?? this.userEmail());
    this.roles.set(r.user?.roles ?? []);
    this.storeIds.set(r.user?.storeIds ?? []);
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/identity/login`, { email, password })
      .pipe(tap(r => this.apply(r, email)));
  }

  /// Self-service „Załóż sklep" — tworzy sklep + konto administratora sklepu i loguje.
  registerStore(body: {
    email: string; password: string; fullName: string; phone?: string;
    storeName: string; city: string; nip: string;
  }): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/register/store`, body)
      .pipe(tap(r => this.apply(r, body.email)));
  }

  /// Self-service „Zostań dostawcą" — konto do weryfikacji przez administratora (nie loguje).
  registerDriver(body: { email: string; password: string; fullName: string; phone?: string; }):
    Observable<{ status: string; message: string }> {
    return this.http.post<{ status: string; message: string }>(`${this.base}/register/driver`, body);
  }

  /// Odświeża token (np. po dodaniu lokalizacji, by nowy sklep trafił do claimów).
  refresh(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/identity/refresh`, { refreshToken: this.refreshToken() })
      .pipe(tap(r => this.apply(r)));
  }

  logout() { this.token.set(null); this.refreshToken.set(null); this.userEmail.set(''); this.roles.set([]); this.storeIds.set([]); }

  private opts() { return { headers: { Authorization: `Bearer ${this.token()}` } }; }

  get<T>(path: string) { return this.http.get<T>(`${this.base}${path}`, this.opts()); }
  getBlob(path: string) { return this.http.get(`${this.base}${path}`, { ...this.opts(), responseType: 'blob' as const }); }
  getPublic<T>(path: string) { return this.http.get<T>(`${this.base}${path}`); }
  post<T>(path: string, body: unknown) { return this.http.post<T>(`${this.base}${path}`, body, this.opts()); }
  put<T>(path: string, body: unknown) { return this.http.put<T>(`${this.base}${path}`, body, this.opts()); }
  patch<T>(path: string, body: unknown) { return this.http.patch<T>(`${this.base}${path}`, body, this.opts()); }
}
