import { Component, inject, input, model, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DeckService } from '../deck.service';
import { GmailStatus, NotificationOptions } from '../models';

@Component({ selector: 'pd-notification-settings', standalone: true, imports: [FormsModule],
  templateUrl: './notification-settings.component.html', styleUrl: './news-settings.component.scss' })
export class NotificationSettingsComponent implements OnInit, OnDestroy {
  options = model.required<NotificationOptions>(); busy = input(false);
  private readonly deck = inject(DeckService);
  readonly status = signal<GmailStatus | null>(null); readonly working = signal(false); readonly message = signal('');
  private timer?: ReturnType<typeof setInterval>;
  ngOnInit() { void this.refresh(); this.timer = setInterval(() => void this.refresh(), 3000); }
  ngOnDestroy() { clearInterval(this.timer); }
  async refresh() { try { this.status.set(await this.deck.gmailStatus()); } catch { this.message.set('Stato Gmail non disponibile.'); } }
  update(patch: Partial<NotificationOptions>) { this.options.update(value => ({ ...value, ...patch })); }
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
    if (status?.authorizationStatus === 'failed') return 'Autorizzazione non riuscita. Verifica client, utente di test e permesso Gmail metadata.';
    if (status?.authorizationStatus === 'cancelled') return 'Autorizzazione annullata o scaduta.';
    if (status?.authorizationStatus === 'credentials-unavailable') return 'Credenziali non leggibili per questo utente Windows. Importa nuovamente il client.';
    if (status?.source.status === 'disabled') return 'Notifiche Gmail disattivate.';
    if (status?.source.status === 'connected') return `${status.source.unreadCount} messaggi non letti in Posta in arrivo · ultimo controllo ${new Date(status.source.updatedAt!).toLocaleTimeString()}`;
    if (status?.source.status === 'unavailable') return 'Gmail temporaneamente non disponibile. Nuovo tentativo automatico.';
    return status?.connected ? 'Collegato, in attesa del primo conteggio.' : 'Gmail non collegato.';
  }
}
