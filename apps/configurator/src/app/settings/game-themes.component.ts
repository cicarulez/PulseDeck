import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { GameTheme } from '../models';

@Component({
  selector: 'pd-game-themes', standalone: true, imports: [FormsModule],
  templateUrl: './game-themes.component.html', styleUrl: './game-themes.component.scss'
})
export class GameThemesComponent {
  themes = input.required<GameTheme[]>(); processes = input.required<string[]>(); busy = input(false);
  themesChange = output<GameTheme[]>();
  private key(process: string) { return process.replace(/\.[^.]*$/, '').toLowerCase(); }
  available() { return this.processes().filter(p => !this.themes().some(t => this.key(t.processName) === this.key(p))); }
  options(theme: GameTheme) { return [...new Set([theme.processName, ...this.processes()])]; }
  add() {
    const processName = this.available()[0];
    if (processName && this.themes().length < 50) this.themesChange.emit([...this.themes(), { processName, backgroundPath: '' }]);
  }
  update(index: number, values: Partial<GameTheme>) { this.themesChange.emit(this.themes().map((t, i) => i === index ? { ...t, ...values } : t)); }
  remove(index: number) { this.themesChange.emit(this.themes().filter((_, i) => i !== index)); }
}
