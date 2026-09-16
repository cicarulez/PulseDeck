import { Component, input } from '@angular/core';

@Component({
  selector: 'pd-status', standalone: true,
  template: '<span class="status" [class.status--live]="active()"><i></i>{{ label() }}</span>',
  styles: [`.status{display:inline-flex;align-items:center;gap:7px;color:var(--muted);font:11px var(--mono);text-transform:uppercase;letter-spacing:.07em;white-space:nowrap}.status i{height:6px;width:6px;border-radius:50%;background:currentColor}.status--live{color:var(--accent)}`]
})
export class StatusBadgeComponent { label = input.required<string>(); active = input(false); }
