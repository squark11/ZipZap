import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { registerLocaleData } from '@angular/common';
import localePl from '@angular/common/locales/pl';
import { routes } from './app.routes';

// Polska lokalizacja: przecinek dziesiętny (23,50), format dat pl-PL.
registerLocaleData(localePl);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideHttpClient(),
    // withComponentInputBinding: query-paramy (storeId, query) wiązane do @Input komponentów sekcji.
    provideRouter(routes, withComponentInputBinding()),
    { provide: LOCALE_ID, useValue: 'pl-PL' },
  ]
};
