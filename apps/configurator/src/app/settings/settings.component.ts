import { Component, effect, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DeckConfig } from '../models';

@Component({ selector: 'pd-settings', standalone: true, imports: [FormsModule], templateUrl: './settings.component.html', styleUrl: './settings.component.scss' })
export class SettingsComponent {
  config = input.required<DeckConfig>(); busy = input(false); save = output<DeckConfig>();
  draft!: DeckConfig; processes = '';
  constructor() { effect(() => { this.draft = { ...this.config(), gameProcesses: [...this.config().gameProcesses] }; this.processes = this.draft.gameProcesses.join(', '); }); }
  submit() { this.save.emit({ ...this.draft, gameProcesses: this.processes.split(',').map(p => p.trim()).filter(Boolean) }); }
}
