import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Api } from './api';

/** Metadane zdarzenia z API — celowo BEZ treści zdarzenia i bez treści błędu (tylko kategoria). */
interface OutboxItem {
  module: string; id: string; type: string;
  occurredAtUtc: string; deadLetteredAtUtc?: string | null; nextAttemptAtUtc?: string | null;
  attempts: number; errorCategory: string; errorDescription: string;
  lastManualRetryAtUtc?: string | null; lastManualRetryByUserId?: string | null;
}
interface ModuleCounts { module: string; deadLettered: number; retrying: number; longRetrying: number; }
interface OutboxOverview {
  longRetryAttempts: number; maxRetryDelayMinutes: number;
  summary: { deadLettered: number; retrying: number; longRetrying: number };
  modules: ModuleCounts[];
  deadLettered: OutboxItem[];
  longRetrying: OutboxItem[];
}

const MODULE_LABELS: Record<string, string> = {
  ordering: 'Zamówienia', delivery: 'Dostawy', payments: 'Płatności', identity: 'Konta', catalog: 'Oferta',
};

@Component({
  selector: 'app-outbox',
  imports: [CommonModule],
  template: `
  <div class="page-head">
    <h1>Kolejka zdarzeń</h1>
    <div class="controls">
      <button class="btn ghost sm" [disabled]="loading" (click)="load()">Odśwież</button>
    </div>
  </div>

  <p class="lead muted">
    Zdarzenia przekazują zmiany między modułami (np. „zamówienie gotowe" → nowa dostawa).
    Błędy przejściowe są ponawiane automatycznie, bez limitu, najrzadziej co {{ data?.maxRetryDelayMinutes ?? 30 }} min.
    Do odłożonych trafiają tylko błędy trwałe — tych system sam już nie wyśle.
  </p>

  @if (notice) { <div class="notice" [attr.data-kind]="notice.kind" role="status">{{ notice.text }}</div> }

  @if (loading && !data) { <p class="muted">Ładowanie…</p> }
  @else if (loadError) { <div class="card pad muted">{{ loadError }}</div> }
  @else if (data) {
    <div class="sum">
      <div [class.alert]="data.summary.deadLettered > 0">
        <span>Odłożone</span><b>{{ data.summary.deadLettered }}</b><small>wymagają decyzji</small>
      </div>
      <div [class.warn]="data.summary.longRetrying > 0">
        <span>Ponawiane długo</span><b>{{ data.summary.longRetrying }}</b><small>{{ data.longRetryAttempts }}+ nieudanych prób</small>
      </div>
      <div>
        <span>Ponawiane teraz</span><b>{{ data.summary.retrying }}</b><small>ponawiane automatycznie</small>
      </div>
    </div>

    <section>
      <h2>Odłożone <span class="muted">— błąd trwały, bez automatycznych ponowień</span></h2>
      <div class="warning" role="note">
        <b>Ponów dopiero po usunięciu przyczyny</b> (np. po wdrożeniu poprawki). Ponowienie może ponownie
        uruchomić obsługę zdarzenia w modułach — np. drugi raz wysłać powiadomienie. Ponawiasz jedno zdarzenie naraz;
        każde ponowienie zapisuje się w audycie z Twoim kontem i czasem.
      </div>
      <div class="card scroll">
        <table>
          <thead><tr>
            <th>Moduł</th><th>Typ zdarzenia</th><th>Identyfikator</th><th>Utworzone</th><th>Odłożone</th>
            <th class="count">Próby</th><th>Przyczyna</th><th></th>
          </tr></thead>
          <tbody>
            @for (e of data.deadLettered; track e.module + e.id) {
              <tr>
                <td>{{ moduleLabel(e.module) }}</td>
                <td class="type" [title]="e.type">{{ shortType(e.type) }}</td>
                <td class="id" [title]="e.id">{{ e.id }}</td>
                <td class="nowrap">{{ e.occurredAtUtc | date:'dd.MM.yyyy HH:mm' }}</td>
                <td class="nowrap">{{ e.deadLetteredAtUtc | date:'dd.MM.yyyy HH:mm' }}</td>
                <td class="count">{{ e.attempts }}</td>
                <td class="why">
                  <span class="cat">{{ categoryLabel(e.errorCategory) }}</span>
                  <div class="muted">{{ e.errorDescription }}</div>
                  @if (e.lastManualRetryAtUtc) {
                    <div class="muted small">Ostatnio ponowione ręcznie {{ e.lastManualRetryAtUtc | date:'dd.MM HH:mm' }}</div>
                  }
                </td>
                <td class="act">
                  <button class="btn sm" [disabled]="busy.has(key(e))" (click)="retry(e)">
                    {{ busy.has(key(e)) ? 'Ponawiam…' : 'Ponów' }}
                  </button>
                </td>
              </tr>
            }
            @if (data.deadLettered.length === 0) {
              <tr><td colspan="8" class="muted pad">Brak odłożonych zdarzeń.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </section>

    <section>
      <h2>Ponawiane długo <span class="muted">— {{ data.longRetryAttempts }}+ prób, system nadal ponawia sam</span></h2>
      <div class="card scroll">
        <table>
          <thead><tr>
            <th>Moduł</th><th>Typ zdarzenia</th><th>Identyfikator</th><th>Utworzone</th>
            <th class="count">Próby</th><th>Następna próba</th><th>Przyczyna</th>
          </tr></thead>
          <tbody>
            @for (e of data.longRetrying; track e.module + e.id) {
              <tr>
                <td>{{ moduleLabel(e.module) }}</td>
                <td class="type" [title]="e.type">{{ shortType(e.type) }}</td>
                <td class="id" [title]="e.id">{{ e.id }}</td>
                <td class="nowrap">{{ e.occurredAtUtc | date:'dd.MM.yyyy HH:mm' }}</td>
                <td class="count">{{ e.attempts }}</td>
                <td class="nowrap">{{ e.nextAttemptAtUtc | date:'dd.MM HH:mm' }}</td>
                <td class="why"><span class="cat">{{ categoryLabel(e.errorCategory) }}</span>
                  <div class="muted">{{ e.errorDescription }}</div></td>
              </tr>
            }
            @if (data.longRetrying.length === 0) {
              <tr><td colspan="7" class="muted pad">Nic nie jest ponawiane długo.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </section>

    @if (data.modules.length) {
      <p class="modules muted">
        @for (m of data.modules; track m.module) {
          <span>{{ moduleLabel(m.module) }}: odłożone {{ m.deadLettered }} · ponawiane {{ m.retrying }}</span>
        }
      </p>
    }
  }
  `,
  styles: [`
    .lead{ max-width:78ch; margin:-6px 0 16px; line-height:1.55; }
    .sum{ display:flex; flex-wrap:wrap; gap:14px; margin-bottom:22px; }
    .sum>div{ background:#fff; border:1px solid #E5E7EB; border-radius:12px; padding:14px 18px; display:flex; flex-direction:column; min-width:170px; }
    .sum span{ font-size:12px; color:#6B7280; } .sum b{ font-size:24px; color:#0F2A2A; font-variant-numeric:tabular-nums; }
    .sum small{ font-size:11.5px; color:#8A929E; }
    .sum>div.alert{ border-color:#F3B4B4; background:#FFF7F7; } .sum>div.alert b{ color:#B4232A; }
    .sum>div.warn{ border-color:#F7D9B9; background:#FFFAF3; } .sum>div.warn b{ color:#B45309; }
    section{ margin-bottom:26px; }
    h2{ font-size:16px; margin:0 0 10px; } h2 .muted{ font-weight:400; font-size:13px; }
    .warning{ background:#FFF8EB; border:1px solid #F7D9B9; color:#6B3E09; border-radius:10px; padding:10px 14px; font-size:13px; line-height:1.5; margin-bottom:10px; max-width:100ch; }
    .notice{ border-radius:10px; padding:10px 14px; margin-bottom:14px; font-size:13.5px; border:1px solid; }
    .notice[data-kind=ok]{ background:#EAF7EE; border-color:#BFE6CB; color:#12603A; }
    .notice[data-kind=error]{ background:#FEECEC; border-color:#F3B4B4; color:#8F1D22; }
    .scroll{ overflow-x:auto; }
    td.id{ font-family:ui-monospace, 'Cascadia Mono', Consolas, monospace; font-size:12px; color:#4B5563; white-space:nowrap; }
    td.type{ font-weight:600; white-space:nowrap; }
    td.count, th.count{ text-align:right; font-variant-numeric:tabular-nums; }
    td.nowrap{ white-space:nowrap; }
    td.why{ min-width:220px; font-size:13px; } td.why .muted{ margin-top:3px; }
    td.act{ text-align:right; }
    .cat{ display:inline-block; font-size:11.5px; font-weight:700; padding:2px 8px; border-radius:20px; background:#F1F3F5; color:#374151; }
    .small{ font-size:11.5px; }
    .modules{ display:flex; flex-wrap:wrap; gap:6px 18px; font-size:12.5px; }
  `],
})
export class OutboxComponent implements OnInit {
  private api = inject(Api);
  data: OutboxOverview | null = null;
  loading = false;
  loadError = '';
  notice: { kind: 'ok' | 'error'; text: string } | null = null;
  /** Zdarzenia z trwającym żądaniem ponowienia — przycisk zablokowany (ochrona przed podwójnym kliknięciem). */
  readonly busy = new Set<string>();

  ngOnInit() { this.load(); }

  load() {
    this.loading = true;
    this.api.get<OutboxOverview>('/admin/outbox').subscribe({
      next: d => { this.data = d; this.loadError = ''; this.loading = false; },
      error: () => { this.loadError = 'Nie udało się pobrać kolejki zdarzeń.'; this.loading = false; },
    });
  }

  retry(e: OutboxItem) {
    const k = this.key(e);
    if (this.busy.has(k)) return;
    const ok = confirm(
      `Ponowić zdarzenie ${this.shortType(e.type)} (${this.moduleLabel(e.module)})?\n\n` +
      'Rób to dopiero po usunięciu przyczyny błędu. Ponowienie może ponownie uruchomić obsługę zdarzenia ' +
      '(np. drugi raz wysłać powiadomienie).');
    if (!ok) return;

    this.busy.add(k);
    this.notice = null;
    this.api.post<{ message: string }>(`/admin/outbox/${e.module}/${e.id}/retry`, { expectedAttempts: e.attempts })
      .subscribe({
        next: r => { this.busy.delete(k); this.notice = { kind: 'ok', text: r.message }; this.load(); },
        error: (err: HttpErrorResponse) => {
          this.busy.delete(k);
          this.notice = { kind: 'error', text: err.status === 409
            ? 'Zdarzenie zmieniło stan (np. ponowił je ktoś inny) — lista została odświeżona.'
            : err.error?.detail ?? 'Nie udało się ponowić zdarzenia.' };
          this.load();
        },
      });
  }

  key(e: OutboxItem) { return `${e.module}/${e.id}`; }
  moduleLabel(m: string) { return MODULE_LABELS[m] ?? m; }
  shortType(t: string) { const i = t.lastIndexOf('.'); return i >= 0 ? t.slice(i + 1) : t; }
  categoryLabel(c: string) {
    const m: Record<string, string> = {
      unknown_type: 'Nieznany typ', unreadable_payload: 'Nieczytelna treść', database: 'Baza danych',
      conflict: 'Konflikt zmian', external_service: 'Usługa zewnętrzna', timeout: 'Limit czasu',
      handler_error: 'Błąd obsługi', legacy: 'Starszy błąd',
    };
    return m[c] ?? c;
  }
}
