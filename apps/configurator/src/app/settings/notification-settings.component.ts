import { Component, inject, input, model, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DeckService } from '../deck.service';
import { GmailStatus, NotificationOptions, NotificationProfile } from '../models';

@Component({ selector: 'pd-notification-settings', standalone: true, imports: [FormsModule],
  templateUrl: './notification-settings.component.html', styleUrl: './news-settings.component.scss' })
export class NotificationSettingsComponent implements OnInit, OnDestroy {
  options = model.required<NotificationOptions>(); busy = input(false);
  private readonly deck = inject(DeckService);
  readonly status = signal<GmailStatus | null>(null); readonly working = signal(false); readonly message = signal('');
  private timer?: ReturnType<typeof setInterval>;
  ngOnInit() { void this.refresh(); this.timer = setInterval(() => void this.refresh(), 3000); }
  ngOnDestroy() { clearInterval(this.timer); }
  async refresh() {
    try {
      const previous = this.status();
      const status = await this.deck.gmailStatus();
      this.status.set(status);
      if (previous?.authorizationStatus === 'waiting' && status.authorizationStatus !== 'waiting') this.message.set('');
    } catch { this.message.set('Stato Gmail non disponibile.'); }
  }
  connected() { return this.status()?.connected === true; }
  awaitingConsent() { return this.status()?.authorizationStatus === 'waiting'; }
  connectionLabel() {
    if (this.awaitingConsent() || this.working()) return 'Collegamento in corso…';
    if (this.connected()) return 'Collegato';
    return this.status()?.authorizationStatus === 'reauthorize' ? 'Ricollega Gmail con Google' : 'Collega Gmail con Google';
  }
  update(patch: Partial<NotificationOptions>) { this.options.update(value => ({ ...value, ...patch })); }
  readonly profiles: readonly { id: NotificationProfile; label: string }[] = [
    { id: 'desktop', label: 'Desktop' }, { id: 'gaming', label: 'Gaming' }, { id: 'music', label: 'Musica' }
  ];
  profileSelected(profile: NotificationProfile) { return (this.options().profiles ?? ['desktop']).includes(profile); }
  setProfile(profile: NotificationProfile, selected: boolean) {
    const profiles = this.profiles.map(item => item.id).filter(id => id === profile ? selected : this.profileSelected(id));
    this.update({ profiles });
  }
  async importClient(event: Event) {
    const input = event.target as HTMLInputElement; const file = input.files?.[0]; input.value = '';
    if (!file) return;
    if (file.size > 16384) { this.message.set('Scegli il piccolo file JSON del client OAuth Desktop.'); return; }
    this.working.set(true);
    try { this.status.set(await this.deck.importGmailClient(await file.text())); this.message.set('Client importato. Ora collega Gmail.'); }
    catch (e) { this.message.set(e instanceof Error ? e.message : 'Importazione non riuscita.'); }
    finally { this.working.set(false); }
  }
  async connect() {
    const popup = window.open('about:blank', '_blank');
    if (!popup) { this.message.set('Consenti l’apertura della finestra Google e riprova.'); return; }
    popup.opener = null; this.working.set(true);
    try { const result = await this.deck.connectGmail(); popup.location.href = result.url; this.message.set('Completa il consenso nella finestra Google.'); await this.refresh(); }
    catch (e) { popup.close(); this.message.set(e instanceof Error ? e.message : 'Collegamento non riuscito.'); }
    finally { this.working.set(false); }
  }
  async disconnect() {
    this.working.set(true);
    try { this.status.set(await this.deck.disconnectGmail()); this.message.set('Credenziali rimosse da questo PC. Puoi revocare anche il consenso nelle connessioni del tuo account Google.'); }
    catch (e) { this.message.set(e instanceof Error ? e.message : 'Rimozione non riuscita.'); }
    finally { this.working.set(false); }
  }
  async test() { try { await this.deck.testNotification(); this.message.set('Prova di 8 secondi: arrivo, conteggio 1 → 3 → 0, poi ripristino del badge reale.'); } catch (e) { this.message.set(e instanceof Error ? e.message : 'Prova non riuscita.'); } }
  description() {
    const status = this.status();
    if (status?.authorizationStatus === 'waiting') return 'In attesa del consenso Google (massimo 5 minuti).';
    if (status?.authorizationStatus === 'reauthorize') return 'Consenso scaduto o revocato: collega nuovamente Gmail.';
    if (status?.authorizationStatus === 'failed') return 'Collegamento non riuscito. Riprova o contatta l’assistenza.';
    if (status?.authorizationStatus === 'cancelled') return 'Autorizzazione annullata o scaduta.';
    if (status?.authorizationStatus === 'credentials-unavailable') return 'Collegamento non leggibile per questo utente Windows. Collega nuovamente Gmail.';
    if (status?.source.status === 'disabled') return 'Notifiche Gmail disattivate.';
    if (status?.source.status === 'connected') {
      const count = status.source.unreadCount;
      return `${count} ${count === 1 ? 'messaggio non letto' : 'messaggi non letti'} in Posta in arrivo · ultimo controllo ${new Date(status.source.updatedAt!).toLocaleTimeString()}`;
    }
    if (status?.source.status === 'unavailable') return 'Gmail temporaneamente non disponibile. Nuovo tentativo automatico.';
    if (!status) return 'Controllo del collegamento Gmail…';
    if (!status.clientConfigured) return 'Collegamento Google non configurato in questa installazione. Contatta l’assistenza o usa la modalità sviluppatore.';
    return status.connected ? 'Collegato, in attesa del primo conteggio.' : 'Gmail non collegato. Premi Collega Gmail con Google per iniziare.';
  }
}
