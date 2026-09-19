import { Component, input, output } from '@angular/core';

export interface SectionTab { id: string; label: string; count?: number; }

@Component({
  selector: 'pd-section-tabs', standalone: true,
  template: `<div class="tabs" role="tablist" [attr.aria-label]="label()">
    @for (item of items(); track item.id) {
      <button type="button" role="tab" [id]="prefix() + '-tab-' + item.id"
        [attr.aria-controls]="panelId() ?? prefix() + '-panel-' + item.id"
        [attr.aria-selected]="selected() === item.id" [tabIndex]="selected() === item.id ? 0 : -1"
        [class.active]="selected() === item.id" (click)="choose(item.id, $event)" (keydown)="navigate($event, $index)">
        {{ item.label }} @if (item.count !== undefined) { <span>{{ item.count }}</span> }
      </button>
    }
  </div>`,
  styleUrl: './section-tabs.component.scss'
})
export class SectionTabsComponent {
  readonly items = input.required<readonly SectionTab[]>();
  readonly selected = input.required<string>();
  readonly label = input.required<string>();
  readonly prefix = input.required<string>();
  readonly panelId = input<string | null>(null);
  readonly selectedChange = output<string>();
  choose(id: string, event: Event) {
    this.selectedChange.emit(id);
    (event.currentTarget as HTMLElement).scrollIntoView({ block: 'nearest', inline: 'nearest' });
  }
  navigate(event: KeyboardEvent, index: number) {
    const count = this.items().length;
    const target = event.key === 'ArrowRight' ? (index + 1) % count
      : event.key === 'ArrowLeft' ? (index + count - 1) % count
      : event.key === 'Home' ? 0 : event.key === 'End' ? count - 1 : -1;
    if (target < 0) return;
    event.preventDefault();
    this.selectedChange.emit(this.items()[target].id);
    const buttons = (event.currentTarget as HTMLElement).parentElement?.querySelectorAll<HTMLButtonElement>('button');
    buttons?.[target].focus({ preventScroll: true });
    buttons?.[target].scrollIntoView({ block: 'nearest', inline: 'nearest' });
  }
}
