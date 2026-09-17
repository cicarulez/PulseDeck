import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DeckService } from '../deck.service';

@Component({ selector: 'pd-steam-grid', standalone: true, imports: [FormsModule],
  templateUrl: './steam-grid.component.html', styleUrl: './game-themes.component.scss' })
export class SteamGridComponent implements OnInit {
  private readonly deck = inject(DeckService);
  readonly configured = signal(false);
  readonly busy = signal(false);
  readonly message = signal('');
  key = '';
  async ngOnInit() {
    try { this.configured.set((await this.deck.steamGridStatus()).configured); }
    catch { this.message.set('Stato del collegamento non disponibile.'); }
  }
  async connect() {
    this.busy.set(true); this.message.set('Verifica della chiave…');
    const key = this.key.trim(); this.key = '';
    try {
      this.configured.set((await this.deck.connectSteamGrid(key)).configured);
      this.message.set('Collegato. Copertine e sfondi saranno cercati automaticamente quando apri un gioco.');
    } catch (e) { this.message.set(e instanceof Error ? e.message : 'Collegamento non riuscito.'); }
    finally { this.busy.set(false); }
  }
  async disconnect() {
    this.busy.set(true);
    try { this.configured.set((await this.deck.disconnectSteamGrid()).configured); this.message.set('Chiave rimossa. Resta disponibile la ricerca su Steam.'); }
    catch (e) { this.message.set(e instanceof Error ? e.message : 'Rimozione non riuscita.'); }
    finally { this.busy.set(false); }
  }
}
