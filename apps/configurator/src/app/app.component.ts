import { Component, computed, inject, signal } from '@angular/core';
import { DeckService } from './deck.service';
import { StatusBadgeComponent } from './shared/status-badge.component';
import { MetricCardComponent } from './shared/metric-card.component';
import { SettingsComponent } from './settings/settings.component';
import { SensorsComponent } from './sensors/sensors.component';
import { WidgetsComponent } from './widgets/widgets.component';
import { DeckConfig } from './models';
import { DisplayControlsComponent } from './display/display-controls.component';

@Component({ selector: 'pd-root', standalone: true, imports: [StatusBadgeComponent, MetricCardComponent, SettingsComponent, SensorsComponent, WidgetsComponent, DisplayControlsComponent], templateUrl: './app.component.html', styleUrl: './app.component.scss' })
export class AppComponent {
  readonly deck = inject(DeckService);
  readonly tab = signal<'overview' | 'sensors' | 'widgets' | 'settings'>('overview');
  readonly pages = [
    { id: 'overview', icon: '◫', label: 'Panoramica', title: 'Tutto sotto controllo.', subtitle: 'Prestazioni, musica e squadra. Un unico punto di vista.' },
    { id: 'sensors', icon: '≋', label: 'Sensori', title: 'Ogni lettura, in chiaro.', subtitle: 'Esplora tutti i sensori rilevati sul tuo PC.' },
    { id: 'widgets', icon: '⊡', label: 'Widget', title: 'Il tuo display, i tuoi dati.', subtitle: 'Scegli quali letture mostrare nelle otto posizioni del pannello.' },
    { id: 'settings', icon: '⊞', label: 'Configurazione', title: 'Un pannello, le tue regole.', subtitle: 'Scegli cosa mostrare e quando cambiare profilo.' }
  ] as const;
  readonly page = computed(() => this.pages.find(p => p.id === this.tab())!);
  readonly saved = signal(false);
  readonly mediaSource = computed(() => {
    const source = this.deck.state()?.media.app.toLowerCase() ?? '';
    return source.includes('spotify') ? 'Spotify' : source.includes('chrome') ? 'Google Chrome' : source.includes('msedge') ? 'Microsoft Edge' : 'Sessione multimediale Windows';
  });
  readonly preview = computed(() => '/api/preview.png?v=' + encodeURIComponent(this.deck.state()?.timestamp ?? 'initial'));
  readonly profile = computed(() => ({ desktop: 'Desktop', music: 'Musica', gaming: 'Gaming' }[this.deck.state()?.profile ?? 'desktop'] ?? 'Desktop'));
  constructor() { void this.deck.start(); }
  async save(config: DeckConfig) { this.saved.set(await this.deck.save(config)); setTimeout(() => this.saved.set(false), 4000); }
}
