import { Component, effect, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DeckConfig } from '../models';
import { GameThemesComponent } from './game-themes.component';

@Component({ selector: 'pd-settings', standalone: true, imports: [FormsModule, GameThemesComponent], templateUrl: './settings.component.html', styleUrl: './settings.component.scss' })
export class SettingsComponent {
  config = input.required<DeckConfig>(); busy = input(false); save = output<DeckConfig>();
  draft!: DeckConfig; processes = '';
  constructor() { effect(() => { this.draft = { ...this.config(), gameProcesses: [...this.config().gameProcesses], gameThemes: this.config().gameThemes.map(t => ({ ...t })) }; this.processes = this.draft.gameProcesses.join(', '); }); }
  gameProcesses() { return [...new Set(this.processes.split(',').map(p => p.trim()).filter(Boolean))]; }
  submit() { this.save.emit({ ...this.draft, gameProcesses: this.gameProcesses() }); }
}
