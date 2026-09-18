import { Component, inject, input, OnInit, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DeckConfig, DiscordOptions } from '../models';
import { DeckService } from '../deck.service';

@Component({ selector: 'pd-discord-settings', standalone: true, imports: [FormsModule],
  templateUrl: './discord-settings.component.html', styleUrl: './news-settings.component.scss' })
export class DiscordSettingsComponent implements OnInit {
  config = input.required<DeckConfig>(); busy = input(false); configChange = output<DeckConfig>();
  private readonly deck = inject(DeckService);
  readonly choices = signal<DiscordOptions | null>(null); readonly loading = signal(false); readonly message = signal('');
  ngOnInit() { void this.refresh(); }
  update(patch: Partial<DeckConfig>) { this.configChange.emit({ ...this.config(), ...patch }); }
  async refresh() {
    this.loading.set(true); this.message.set('');
    try { this.choices.set(await this.deck.discordOptions()); }
    catch { this.message.set('Elenco non disponibile. Puoi inserire gli ID manualmente.'); }
    finally { this.loading.set(false); }
  }
  knownMember() { return this.choices()?.members.some(member => member.id === this.config().trackedMemberId) ?? false; }
  knownChannel() { return this.choices()?.channels.some(channel => channel.id === this.config().discordVoiceChannelId) ?? false; }
}
