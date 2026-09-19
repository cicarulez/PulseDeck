import { NotificationSettingsComponent } from './notification-settings.component';
import { Component, effect, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DeckConfig, NewsSnapshot, CalendarSnapshot } from '../models';
import { NewsSettingsComponent } from './news-settings.component';
import { WeatherSettingsComponent } from './weather-settings.component';
import { GameDiscoveryComponent } from './game-discovery.component';
import { SteamGridComponent } from './steam-grid.component';
import { CalendarSettingsComponent } from './calendar-settings.component';
import { DiscordSettingsComponent } from './discord-settings.component';
import { GameThemesComponent } from './game-themes.component';

@Component({ selector: 'pd-settings', standalone: true, imports: [NotificationSettingsComponent, FormsModule, CalendarSettingsComponent, DiscordSettingsComponent, GameDiscoveryComponent, SteamGridComponent, GameThemesComponent, WeatherSettingsComponent, NewsSettingsComponent], templateUrl: './settings.component.html', styleUrl: './settings.component.scss' })
export class SettingsComponent {
  calendarState = input<CalendarSnapshot | null>(null);
  newsState = input<NewsSnapshot | null>(null);
  config = input.required<DeckConfig>(); busy = input(false); save = output<DeckConfig>();
  draft!: DeckConfig; processes = ''; discoveredProcesses: string[] = [];
  constructor() { effect(() => { this.draft = { ...this.config(), gameProcesses: [...this.config().gameProcesses], gameThemes: this.config().gameThemes.map(t => ({ ...t })) }; this.processes = this.draft.gameProcesses.join(', '); }); }
  gameProcesses() { return [...new Set(this.processes.split(',').map(p => p.trim()).filter(Boolean))]; }
  themeProcesses() { return [...new Set([...this.gameProcesses(), ...this.discoveredProcesses])]; }
  validationError() {
    const gmail = this.draft.notifications.pollSeconds, calendar = this.draft.calendar.pollMinutes;
    if (!Number.isInteger(gmail) || gmail < 15 || gmail > 300) return 'Inserisci un intervallo Gmail intero tra 15 e 300 secondi.';
    if (!Number.isInteger(calendar) || calendar < 1 || calendar > 60) return 'Inserisci un intervallo calendario intero tra 1 e 60 minuti.';
    const advance = this.draft.lyricsAdvanceMilliseconds;
    if (!Number.isInteger(advance) || advance < -5000 || advance > 5000) return 'Inserisci una regolazione testi intera tra -5000 e +5000 ms.';
    return '';
  }
  submit() { if (this.validationError()) return; this.save.emit({ ...this.draft, gameProcesses: this.gameProcesses() }); }
}
