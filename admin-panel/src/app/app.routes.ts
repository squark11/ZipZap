import { Routes } from '@angular/router';
import { PANEL_MODULES } from './modules';
import { roleGuard } from './role.guard';

// Leniwe ładowanie komponentów sekcji (mniejszy bundle startowy).
const loaders: Record<string, () => Promise<any>> = {
  'admin-home': () => import('./admin-home').then(m => m.AdminHomeComponent),
  users:        () => import('./users').then(m => m.UsersComponent),
  invoices:     () => import('./invoices').then(m => m.InvoicesComponent),
  support:      () => import('./support').then(m => m.SupportComponent),
  onboarding:   () => import('./onboarding').then(m => m.OnboardingComponent),
  'driver-home': () => import('./driver-home').then(m => m.DriverHomeComponent),
  dashboard:    () => import('./dashboard').then(m => m.DashboardComponent),
  orders:       () => import('./orders').then(m => m.OrdersComponent),
  deliveries:   () => import('./deliveries').then(m => m.DeliveriesComponent),
  catalog:      () => import('./catalog').then(m => m.CatalogComponent),
  integrations: () => import('./integrations').then(m => m.IntegrationsComponent),
  finance:      () => import('./finance').then(m => m.FinanceComponent),
  team:         () => import('./team').then(m => m.TeamComponent),
  drivers:      () => import('./drivers').then(m => m.DriversComponent),
  stores:       () => import('./stores').then(m => m.StoresComponent),
  feedback:     () => import('./feedback').then(m => m.FeedbackComponent),
  settings:     () => import('./settings').then(m => m.SettingsComponent),
};

// Trasy generowane z rejestru modułów — dodanie formularza = wpis w PANEL_MODULES + loader.
// „dashboard" jest bezpiecznym domyślnym celem (widoczny dla wszystkich ról panelu).
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  ...PANEL_MODULES.map(m => ({
    path: m.id,
    canActivate: [roleGuard(m.roles)],
    loadComponent: loaders[m.id],
  })),
  { path: '**', redirectTo: 'dashboard' },
];
