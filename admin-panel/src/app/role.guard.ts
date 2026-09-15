import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Api } from './api';
import { PANEL_MODULES, PanelRole } from './modules';

/// Guard tras per rola: przepuszcza, gdy zalogowany użytkownik ma którąkolwiek z ról
/// modułu; w przeciwnym razie przekierowuje do pierwszej dostępnej dla niego sekcji.
export function roleGuard(roles: PanelRole[]): CanActivateFn {
  return () => {
    const api = inject(Api);
    const router = inject(Router);
    if (!api.isLoggedIn() || api.hasAnyRole(roles)) return true;
    const first = PANEL_MODULES.find(m => api.hasAnyRole(m.roles));
    return router.parseUrl('/' + (first?.id ?? 'dashboard'));
  };
}
