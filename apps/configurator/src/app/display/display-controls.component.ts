import { Component, computed, input, output } from '@angular/core';
import { DisplayState } from '../models';
import { StatusBadgeComponent } from '../shared/status-badge.component';

@Component({
  selector: 'pd-display-controls', standalone: true, imports: [StatusBadgeComponent],
  template: `
    <div class="actions">
      <pd-status [label]="label()" [active]="display()?.connected ?? false" />
      <button class="button" [disabled]="!online() || busy()"
        (click)="action.emit(canDisconnect() ? 'disconnect' : 'connect')">
        {{ busy() ? 'Attendi…' : canDisconnect() ? 'Scollega display' : 'Collega display' }}
      </button>
    </div>
    @if (recovering()) {
      <p class="detail" role="status">Recupero USB in corso. Tentativi eseguiti: {{ display()?.recoveryAttempts ?? 0 }}/2. Puoi scollegare per annullare.</p>
    } @else if (display()?.status === 'error') {
      <p class="detail" role="status">Collegamento interrotto. Premi Collega display per riprovare.</p>
    }
  `,
  styles: [`:host{display:block;max-width:55%}.actions{display:flex;align-items:center;justify-content:flex-end;flex-wrap:wrap;gap:12px}.detail{color:var(--muted);font-size:12px;line-height:1.5;margin:10px 0 0;max-width:340px}@media(max-width:1050px){pd-status{display:none}}`]
})
export class DisplayControlsComponent {
  readonly display = input<DisplayState | null>(null);
  readonly online = input(false);
  readonly busy = input(false);
  readonly action = output<'connect' | 'disconnect'>();
  readonly recovering = computed(() => this.display()?.status === 'recovering');
  readonly canDisconnect = computed(() => this.display()?.connected || this.recovering());
  readonly label = computed(() => this.recovering() ? 'Recupero USB' : this.display()?.connected ? 'Output USB attivo' : 'Solo anteprima');
}
