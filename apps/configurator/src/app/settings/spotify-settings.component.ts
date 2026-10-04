import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DeckService } from '../deck.service';
import { SpotifyConnection } from '../models';
import { StatusBadgeComponent } from '../shared/status-badge.component';

@Component({ selector: 'pd-spotify-settings', standalone: true, imports: [FormsModule, StatusBadgeComponent],
  templateUrl: './spotify-settings.component.html', styleUrl: './news-settings.component.scss' })
export class SpotifySettingsComponent implements OnInit, OnDestroy {
  private readonly deck = inject(DeckService);
  readonly status = signal<SpotifyConnection | null>(null);
  readonly busy = signal(false);
  readonly message = signal('');
  readonly unavailable = signal(false);
  readonly authorizationUrl = signal('');
  readonly extras = computed(() => this.deck.connected() ? this.deck.state()?.spotify : undefined);
  clientId = '';
  private timer?: ReturnType<typeof setInterval>;
  private refreshing = false;
  private destroyed = false;
  readonly redirectUri = 'http://127.0.0.1:5179/spotify/callback/';
  ngOnInit() { void this.refresh(); this.timer = setInterval(() => void this.refresh(), 3000); }
  ngOnDestroy() { this.destroyed = true; clearInterval(this.timer); }
  async refresh() {
    if (this.refreshing) return;
    this.refreshing = true;
    try {
      const value = await this.deck.spotifyStatus();
      if (!this.destroyed) { this.status.set(value); this.unavailable.set(false); if (value.authorizationStatus !== 'waiting') this.authorizationUrl.set(''); }
    } catch { if (!this.destroyed) this.unavailable.set(true); }
    finally { this.refreshing = false; }
  }
  waiting() { return this.status()?.authorizationStatus === 'waiting'; }
  active() { return !this.unavailable() && this.extras()?.status === 'connected'; }
  label() {
    if (this.unavailable()) return 'Agent non disponibile';
    if (this.waiting()) return 'In attesa di Spotify';
    if (this.active()) return 'Extra attivi';
    return this.status()?.connected ? 'Spotify collegato' : 'Modalità base';
  }
  detail() {
    if (this.unavailable()) return 'Impossibile leggere lo stato del collegamento. Controlla che PulseDeck sia avviato.';
    const auth = this.status()?.authorizationStatus;
    const messages: Record<string, string> = {
      waiting: 'Completa il consenso nella finestra Spotify. Il collegamento scade dopo cinque minuti.',
      denied: 'Consenso annullato. Puoi riprovare quando vuoi.', expired: 'Tempo scaduto. Premi Collega Spotify per riprovare.',
      failed: 'Collegamento non riuscito. Controlla Client ID e indirizzo di ritorno nelle impostazioni dell’app Spotify.',
      reauthorize: 'L’autorizzazione non è più valida. Ricollega Spotify per riattivare gli extra.',
      'credentials-unavailable': 'Il collegamento salvato non è leggibile da questo utente Windows. Configuralo di nuovo.'
    };
    if (auth && messages[auth]) return messages[auth];
    const data = this.status()?.dataStatus;
    if (data === 'forbidden') return 'Accesso negato da Spotify: verifica Premium del proprietario dell’app e utenti autorizzati nel Developer Dashboard.';
    if (data === 'rate-limited') return 'Spotify ha raggiunto il limite di richieste. PulseDeck riproverà automaticamente; i dati locali restano attivi.';
    if (data === 'private') return 'Sessione privata Spotify: gli extra non vengono mostrati.';
    if (data === 'unavailable' || data === 'unauthorized') return 'Spotify non è disponibile. PulseDeck usa i dati locali e riproverà automaticamente.';
    if (this.status()?.connected && !this.active()) return 'In attesa di un brano Spotify corrispondente alla riproduzione sul PC.';
    return 'Il collegamento è facoltativo e si applica subito, senza salvare la configurazione.';
  }
  validId() { return /^[a-fA-F0-9]{32}$/.test(this.clientId.trim()); }
  async saveClient() {
    this.busy.set(true); this.message.set('');
    try { this.status.set(await this.deck.configureSpotify(this.clientId.trim())); this.clientId = ''; this.message.set('App configurata. Ora puoi collegare il tuo account Spotify.'); }
    catch (e) { this.message.set(e instanceof Error ? e.message : 'Configurazione non riuscita.'); }
    finally { this.busy.set(false); }
  }
  async connect() {
    this.busy.set(true); this.message.set('');
    try {
      const result = await this.deck.connectSpotify();
      this.authorizationUrl.set(result.url);
      await this.refresh();
      // Same-window navigation also works in the desktop shell, which opens HTTPS links in the browser.
      window.location.assign(result.url);
    }
    catch (e) { this.message.set(e instanceof Error ? e.message : 'Collegamento non riuscito.'); }
    finally { this.busy.set(false); }
  }
  async disconnect() {
    this.busy.set(true); this.message.set('');
    try { this.status.set(await this.deck.disconnectSpotify()); this.authorizationUrl.set(''); this.message.set('Account scollegato da questo PC. La modalità base continua a funzionare.'); }
    catch (e) { this.message.set(e instanceof Error ? e.message : 'Rimozione non riuscita.'); }
    finally { this.busy.set(false); }
  }
}
