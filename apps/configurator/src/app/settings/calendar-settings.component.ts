import { Component, inject, input, model, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CalendarOptions, CalendarSnapshot } from '../models';
import { DeckService } from '../deck.service';

@Component({ selector: 'pd-calendar-settings', standalone: true, imports: [FormsModule],
  templateUrl: './calendar-settings.component.html', styleUrl: './news-settings.component.scss' })
export class CalendarSettingsComponent implements OnInit {
  options = model.required<CalendarOptions>(); state = input<CalendarSnapshot | null>(null); busy = input(false);
  private readonly deck = inject(DeckService);
  readonly configured = signal(false); readonly connecting = signal(false); readonly message = signal('');
  url = '';
  async ngOnInit() {
    try { this.configured.set((await this.deck.calendarStatus()).configured); }
    catch { this.message.set('Stato del collegamento non disponibile.'); }
  }
  update(patch: Partial<CalendarOptions>) { this.options.update(current => ({ ...current, ...patch })); }
  async connect() {
    this.connecting.set(true); this.message.set('Verifica del calendario…');
    const url = this.url.trim(); this.url = '';
    try { this.configured.set((await this.deck.connectCalendar(url)).configured); this.message.set('Calendario collegato. Gli appuntamenti vengono aggiornati ogni 5 minuti.'); }
    catch (e) { this.message.set(e instanceof Error ? e.message : 'Collegamento non riuscito.'); }
    finally { this.connecting.set(false); }
  }
  async disconnect() {
    this.connecting.set(true);
    try { this.configured.set((await this.deck.disconnectCalendar()).configured); this.message.set('Collegamento rimosso.'); }
    catch (e) { this.message.set(e instanceof Error ? e.message : 'Rimozione non riuscita.'); }
    finally { this.connecting.set(false); }
  }
  status() {
    if (!this.configured()) return 'Non collegato';
    switch (this.state()?.status) {
      case 'disabled': return 'Disattivato'; case 'connected': return 'Aggiornato'; case 'empty': return 'Nessun impegno nei prossimi 7 giorni';
      case 'unavailable': return 'Calendario non disponibile'; default: return 'In attesa di aggiornamento';
    }
  }
}
