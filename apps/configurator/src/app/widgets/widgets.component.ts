import { Component, computed, effect, input, output, signal } from '@angular/core';
import { DeckConfig, HardwareState, WidgetCatalog, WidgetConfig } from '../models';
import { WidgetSlotEditorComponent } from './widget-slot-editor.component';
import { FormsModule } from '@angular/forms';

@Component({ selector: 'pd-widgets', standalone: true, imports: [WidgetSlotEditorComponent, FormsModule], templateUrl: './widgets.component.html', styleUrl: './widgets.component.scss' })
export class WidgetsComponent {
  readonly config = input.required<DeckConfig>();
  readonly catalog = input.required<WidgetCatalog>();
  readonly hardware = input<HardwareState | null>(null);
  readonly preview = input('');
  readonly online = input(false);
  readonly busy = input(false);
  readonly save = output<DeckConfig>();
  readonly draft = signal<WidgetConfig[]>([]);
  readonly selected = signal('bar1');
  readonly layout = signal<DeckConfig['layout']>('compact');
  readonly slots = computed(() => this.catalog().slots.slice(0, this.layout() === 'classic' ? 8 : this.layout() === 'weather' ? 12 : 16));
  readonly visibleCount = computed(() => this.draft().filter(w => w.source !== 'none' && this.slots().some(s => s.id === w.slot)).length);
  readonly current = computed(() => this.draft().find(w => w.slot === this.selected())!);
  readonly slot = computed(() => this.catalog().slots.find(s => s.id === this.selected())!);
  readonly dirty = computed(() => this.layout() !== this.config().layout || JSON.stringify(this.draft()) !== JSON.stringify(this.config().widgets));
  readonly valid = computed(() => this.draft().every(w => typeof w.maximum === 'number' && Number.isFinite(w.maximum) && w.maximum > 0 && w.maximum <= 1e15 && w.label.length <= 24 && !/[\u0000-\u001f\u007f]/.test(w.label)));
  constructor() { effect(() => { this.draft.set(this.config().widgets.map(w => ({ ...w }))); this.layout.set(this.config().layout); }); }
  chooseLayout(layout: DeckConfig['layout']) { this.layout.set(layout); if (!this.slots().some(s => s.id === this.selected())) this.selected.set('bar1'); }
  positionName(index: number, name: string) { const columns = this.layout() === 'weather' ? 3 : 4; return this.layout() !== 'classic' ? `Riga ${Math.floor(index / columns) + 1} · Colonna ${index % columns + 1}` : name; }
  update(widget: WidgetConfig) { this.draft.update(all => all.map(w => w.slot === widget.slot ? widget : w)); }
  description(slot: string) { const widget = this.draft().find(w => w.slot === slot); return widget?.source === 'none' ? 'Nascosto' : widget?.label || widget?.sensorName || 'Scegli un dato'; }
  reset() { this.draft.set(this.catalog().defaults.map(w => ({ ...w }))); }
  discard() { this.draft.set(this.config().widgets.map(w => ({ ...w }))); this.chooseLayout(this.config().layout); }
  submit() { if (this.valid()) this.save.emit({ ...this.config(), layout: this.layout(), widgets: this.draft() }); }
}
