import { Injectable, signal } from '@angular/core';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { SpotifyConnection, GmailStatus, DiscordOptions, GameLibraryStatus, DeckConfig, DeckState, DisplayState, WidgetCatalog } from './models';

@Injectable({ providedIn: 'root' })
export class DeckService {
  readonly state = signal<DeckState | null>(null);
  readonly widgetCatalog = signal<WidgetCatalog | null>(null);
  readonly config = signal<DeckConfig | null>(null);
  readonly version = signal<string | null>(null);
  readonly connected = signal(false);
  readonly error = signal('');
  readonly busy = signal(false);
  private lastState = 0;
  private started = false;
  private connection = new HubConnectionBuilder().withUrl('/live').withAutomaticReconnect([0, 1000, 3000, 5000]).configureLogging(LogLevel.Warning).build();

  async start() {
    if (this.started) return;
    this.started = true;
    this.connection.on('state', (state: DeckState) => { this.state.set(state); this.lastState = Date.now(); this.connected.set(true); });
    this.connection.onreconnecting(() => this.connected.set(false));
    this.connection.onclose(() => { this.connected.set(false); setTimeout(() => void this.open(), 3000); });
    setInterval(() => { if (Date.now() - this.lastState > 8000) this.connected.set(false); }, 2000);
    await this.open();
  }
  private async open() {
    try {
      const [config, state, catalog, health] = await Promise.all([
        this.request<DeckConfig>('/api/config'), this.request<DeckState>('/api/state'),
        this.request<WidgetCatalog>('/api/widget-slots'),
        this.request<{ version: string | null }>('/api/health').catch(() => ({ version: null }))
      ]);
      this.widgetCatalog.set(catalog);
      this.version.set(health.version);
      if (!this.config()) this.config.set(config);
      this.state.set(state);
      this.lastState = new Date(state.timestamp).getTime();
      await this.connection.start();
      this.connected.set(Date.now() - this.lastState < 8000);
    } catch { this.connected.set(false); setTimeout(() => void this.open(), 3000); }
  }
  private async request<T>(url: string, method = 'GET', body?: unknown): Promise<T> {
    const response = await fetch(url, { method, headers: { 'Content-Type': 'application/json', 'X-PulseDeck-Client': 'configurator' }, body: body === undefined ? undefined : JSON.stringify(body) });
    if (!response.ok) {
      const data = await response.json().catch(() => ({})) as { error?: string; detail?: string };
      throw new Error(data.error || data.detail || `Richiesta non riuscita (${response.status})`);
    }
    return response.json() as Promise<T>;
  }
  discordOptions() { return this.request<DiscordOptions>('/api/discord/options'); }
  spotifyStatus() { return this.request<SpotifyConnection>('/api/spotify'); }
  configureSpotify(clientId: string) { return this.request<SpotifyConnection>('/api/spotify/client', 'POST', { clientId }); }
  connectSpotify() { return this.request<{url: string}>('/api/spotify/connect', 'POST'); }
  disconnectSpotify() { return this.request<SpotifyConnection>('/api/spotify', 'DELETE'); }
  gmailStatus() { return this.request<GmailStatus>('/api/notifications/gmail'); }
  importGmailClient(json: string) { return this.request<GmailStatus>('/api/notifications/gmail/client', 'POST', { json }); }
  connectGmail() { return this.request<{url: string}>('/api/notifications/gmail/connect', 'POST'); }
  disconnectGmail() { return this.request<GmailStatus>('/api/notifications/gmail', 'DELETE'); }
  testNotification(kind: 'mail' | 'calendar' = 'mail') { return this.request(`/api/notifications/test?kind=${kind}`, 'POST'); }
  calendarStatus() { return this.request<{configured: boolean}>('/api/calendar'); }
  connectCalendar(url: string) { return this.request<{configured: boolean}>('/api/calendar', 'POST', {url}); }
  disconnectCalendar() { return this.request<{configured: boolean}>('/api/calendar', 'DELETE'); }
  gameLibraries() { return this.request<GameLibraryStatus>('/api/games'); }
  scanGames() { return this.request<{ requested: boolean }>('/api/games/scan', 'POST'); }
  async previewGameImage(path: string, kind: 'cover' | 'background'): Promise<Blob> {
    const response = await fetch('/api/games/artwork/custom-preview', {
      method: 'POST', headers: { 'Content-Type': 'application/json', 'X-PulseDeck-Client': 'configurator' }, body: JSON.stringify({ path, kind })
    });
    if (!response.ok) throw new Error('Immagine non disponibile');
    return response.blob();
  }
  async uploadGameImage(file: File): Promise<string> {
    const form = new FormData(); form.append('image', file);
    const response = await fetch('/api/games/artwork/upload', {
      method: 'POST', headers: { 'X-PulseDeck-Client': 'configurator' }, body: form
    });
    if (!response.ok) {
      const data = await response.json().catch(() => ({})) as { error?: string; detail?: string };
      throw new Error(data.error || data.detail || `Caricamento non riuscito (${response.status})`);
    }
    return (await response.json() as { path: string }).path;
  }
  steamGridStatus() { return this.request<{ configured: boolean }>('/api/steamgriddb'); }
  connectSteamGrid(key: string) { return this.request<{ configured: boolean }>('/api/steamgriddb', 'POST', { key }); }
  disconnectSteamGrid() { return this.request<{ configured: boolean }>('/api/steamgriddb', 'DELETE'); }
  async save(config: DeckConfig) {
    this.busy.set(true); this.error.set('');
    try { this.config.set(await this.request<DeckConfig>('/api/config', 'PUT', config)); return true; }
    catch (e) { this.error.set(e instanceof Error ? e.message : 'Salvataggio non riuscito'); return false; }
    finally { this.busy.set(false); }
  }
  async display(action: 'connect' | 'disconnect') {
    this.busy.set(true); this.error.set('');
    try {
      const display = await this.request<DisplayState>(`/api/display/${action}`, 'POST');
      this.state.update(state => state ? { ...state, display } : state);
    } catch (e) { this.error.set(e instanceof Error ? e.message : 'Connessione non riuscita'); }
    finally { this.busy.set(false); }
  }
  async stop() {
    this.error.set('');
    try { await this.request('/api/stop', 'POST'); this.connected.set(false); }
    catch (e) { this.error.set(e instanceof Error ? e.message : 'Arresto non riuscito'); }
  }
}
