import { Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { WeatherLocation, WeatherPlace } from '../models';

@Component({ selector: 'pd-weather-settings', standalone: true, imports: [FormsModule],
  templateUrl: './weather-settings.component.html', styleUrl: './weather-settings.component.scss' })
export class WeatherSettingsComponent {
  location = input<WeatherLocation | null>(null); busy = input(false);
  locationChange = output<WeatherLocation | null>();
  query = ''; searching = signal(false); error = signal(''); places = signal<WeatherPlace[]>([]);
  async search() {
    if (this.searching() || this.query.trim().length < 2) return;
    this.searching.set(true); this.error.set(''); this.places.set([]);
    try {
      const response = await fetch('/api/weather/locations?query=' + encodeURIComponent(this.query.trim()));
      if (!response.ok) throw new Error('Ricerca non disponibile. Riprova tra poco.');
      const places = await response.json() as WeatherPlace[];
      this.places.set(places);
      if (!places.length) this.error.set('Nessuna località trovata. Prova il nome del comune e il paese.');
    } catch (error) { this.error.set(error instanceof Error ? error.message : 'Ricerca non disponibile.'); }
    finally { this.searching.set(false); }
  }
  choose(place: WeatherPlace) {
    this.locationChange.emit({ name: place.name, latitude: place.latitude, longitude: place.longitude });
    this.places.set([]);
  }
}
