import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Wspólny modal konfiguracji: nagłówek + (opcjonalnie) zakładki + treść (ng-content) + stopka z „Zapisz".
 * Zakładki: rodzic trzyma stan aktywnej zakładki (`active`) i pokazuje odpowiednią część treści.
 */
@Component({
  selector: 'app-config-modal',
  imports: [CommonModule],
  template: `
    @if (open) {
      <div class="mback" (click)="close.emit()">
        <div class="mbox" (click)="$event.stopPropagation()">
          <div class="mhead">
            <h2>{{ title }}</h2>
            <button class="mx" (click)="close.emit()" aria-label="Zamknij">✕</button>
          </div>
          @if (tabs.length > 1) {
            <div class="mtabs">
              @for (t of tabs; track t) {
                <button [class.act]="t === active" (click)="tabChange.emit(t)">{{ t }}</button>
              }
            </div>
          }
          <div class="mbody"><ng-content></ng-content></div>
          @if (showFooter) {
            <div class="mfoot">
              @if (saved) { <span class="ok-msg">✓ Zapisano</span> }
              <button class="btn ghost sm" (click)="close.emit()">Zamknij</button>
              <button class="btn primary sm" (click)="save.emit()" [disabled]="saving">Zapisz</button>
            </div>
          }
        </div>
      </div>
    }
  `,
  styles: [`
    .mback { position:fixed; inset:0; background:rgba(15,42,42,.45); display:flex; align-items:flex-start; justify-content:center; padding:40px 16px; z-index:1000; overflow:auto; }
    .mbox { background:#fff; border-radius:16px; width:100%; max-width:640px; box-shadow:0 30px 80px -20px rgba(0,0,0,.45); overflow:hidden; animation:pop .16s ease-out; }
    @keyframes pop { from { transform:translateY(8px); opacity:0; } to { transform:translateY(0); opacity:1; } }
    .mhead { display:flex; align-items:center; justify-content:space-between; padding:18px 20px; border-bottom:1px solid #eef0f3; }
    .mhead h2 { font-size:17px; margin:0; color:#0F2A2A; }
    .mx { border:none; background:transparent; font-size:18px; cursor:pointer; color:#6B7280; line-height:1; padding:4px; }
    .mx:hover { color:#0F2A2A; }
    .mtabs { display:flex; gap:4px; padding:10px 16px 0; border-bottom:1px solid #eef0f3; }
    .mtabs button { border:none; background:transparent; padding:8px 14px; font:inherit; font-size:13.5px; font-weight:600; color:#6B7280; cursor:pointer; border-bottom:2px solid transparent; margin-bottom:-1px; }
    .mtabs button.act { color:#0C7D7E; border-bottom-color:#14B9BA; }
    .mbody { padding:20px; max-height:calc(100vh - 260px); overflow:auto; }
    .mfoot { display:flex; align-items:center; justify-content:flex-end; gap:10px; padding:14px 20px; border-top:1px solid #eef0f3; background:#FAFBFC; }
    .ok-msg { color:#128040; font-size:13px; font-weight:600; margin-right:auto; }
  `],
})
export class ConfigModalComponent {
  @Input() open = false;
  @Input() title = '';
  @Input() tabs: string[] = [];
  @Input() active = '';
  @Input() showFooter = true;
  @Input() saving = false;
  @Input() saved = false;
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<void>();
  @Output() tabChange = new EventEmitter<string>();
}
