import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

// Tymczasowy ekran dostawcy — pełny interfejs kierowcy (dostępne/moje dostawy,
// online/offline, mapa/ETA, zarobki) w R8. Zapobiega trafianiu dostawcy na
// sklepowe komponenty (Pulpit/Dostawy) bez wybranego sklepu.
@Component({
  selector: 'app-driver-home',
  imports: [CommonModule],
  template: `
  <div class="page-head"><h1>Panel dostawcy</h1></div>
  <div class="card pad">
    <p class="muted" style="margin-top:0">
      Twoje konto dostawcy jest aktywne. Panel z <strong>dostawami do odbioru</strong>,
      dostępnością <strong>online/offline</strong>, nawigacją i <strong>zarobkami</strong>
      jest w przygotowaniu — damy znać, gdy będzie gotowy.
    </p>
  </div>
  `,
})
export class DriverHomeComponent {}
