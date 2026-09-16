// Rejestr modułów panelu (web). Modularny dashboard: DODANIE FORMULARZA = jeden wpis
// tutaj (+ ikona w app.ts `@switch (m.icon)` + komponent w `@switch (tab)`). Nawigacja
// jest generowana z tej listy i filtrowana po rolach zalogowanego użytkownika.
//
// Role (nazwa właściciela → enum Identity):
//   Admin         = administrator serwisu (platforma)
//   StoreEmployee = administrator sklepu (scope: StoreId[])
//   Driver        = dostawca / kierowca (scope: StoreId[])
//   Customer      = klient (kanał: aplikacja mobilna/PWA — nie panel web)

export type PanelRole = 'Admin' | 'StoreEmployee' | 'Driver' | 'Customer';

export type PanelTab =
  | 'onboarding' | 'dashboard' | 'orders' | 'deliveries' | 'catalog'
  | 'integrations' | 'team' | 'drivers' | 'driver-home' | 'finance' | 'stores' | 'feedback' | 'settings'
  | 'admin-home' | 'users' | 'invoices' | 'support' | 'compliance';

export interface PanelModule {
  id: PanelTab;
  label: string;
  icon: string;         // klucz ikony (SVG w app.ts)
  roles: PanelRole[];   // role, które widzą moduł
  storeScoped: boolean; // wymaga wybranego sklepu (pusty stan „brak sklepów")
}

export const PANEL_MODULES: readonly PanelModule[] = [
  // Dostawca (kierowca)
  { id: 'driver-home',  label: 'Moje dostawy', icon: 'deliveries',   roles: ['Driver'],           storeScoped: false },

  // Administrator serwisu (platforma) — nadzór, nie prowadzenie sklepu
  { id: 'admin-home',   label: 'Statystyki',   icon: 'stats',        roles: ['Admin'],            storeScoped: false },
  { id: 'stores',       label: 'Sklepy',       icon: 'stores',       roles: ['Admin'],            storeScoped: false },
  { id: 'users',        label: 'Użytkownicy',  icon: 'users',        roles: ['Admin'],            storeScoped: false },
  { id: 'invoices',     label: 'Faktury',      icon: 'finance',      roles: ['Admin'],            storeScoped: false },
  { id: 'drivers',      label: 'Dostawcy',     icon: 'drivers',      roles: ['Admin'],            storeScoped: false },
  { id: 'support',      label: 'Wsparcie',     icon: 'support',      roles: ['Admin'],            storeScoped: false },
  { id: 'compliance',   label: 'Nadzór',       icon: 'compliance',   roles: ['Admin'],            storeScoped: false },
  { id: 'feedback',     label: 'Uwagi',        icon: 'feedback',     roles: ['Admin'],            storeScoped: false },
  { id: 'settings',     label: 'Konfiguracja', icon: 'settings',     roles: ['Admin'],            storeScoped: false },

  // Administrator sklepu (właściciel) — narzędzia do prowadzenia sklepu
  { id: 'onboarding',   label: 'Start',        icon: 'start',        roles: ['StoreEmployee'],    storeScoped: true },
  { id: 'dashboard',    label: 'Pulpit',       icon: 'dashboard',    roles: ['StoreEmployee'],    storeScoped: true },
  { id: 'orders',       label: 'Zamówienia',   icon: 'orders',       roles: ['StoreEmployee'],    storeScoped: true },
  { id: 'deliveries',   label: 'Dostawy',      icon: 'deliveries',   roles: ['StoreEmployee'],    storeScoped: true },
  { id: 'catalog',      label: 'Oferta',       icon: 'catalog',      roles: ['StoreEmployee'],    storeScoped: true },
  { id: 'integrations', label: 'Integracje',   icon: 'integrations', roles: ['StoreEmployee'],    storeScoped: true },
  { id: 'finance',      label: 'Rozliczenia',  icon: 'finance',      roles: ['StoreEmployee'],    storeScoped: true },
  { id: 'team',         label: 'Zespół',       icon: 'team',         roles: ['StoreEmployee'],    storeScoped: true },
];
