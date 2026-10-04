import { Component, computed, inject, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DeckService } from '../deck.service';
import { StatusBadgeComponent } from '../shared/status-badge.component';

@Component({ selector: 'pd-aura-settings', standalone: true, imports: [FormsModule, StatusBadgeComponent],
  templateUrl: './aura-settings.component.html', styleUrl: './news-settings.component.scss' })
export class AuraSettingsComponent {
  private readonly deck = inject(DeckService);
  readonly enabled = input(false);
  readonly enabledChange = output<boolean>();
  readonly busy = input(false);
  readonly state = computed(() => this.deck.connected() ? this.deck.state()?.aura : undefined);
  readonly active = computed(() => this.state()?.status === 'connected');
  readonly label = computed(() => {
    if (!this.deck.connected()) return 'Agent non disponibile';
    if (!this.deck.config()?.auraEnabled) return 'Colore manuale';
    return this.active() ? 'Aura collegata' : 'Aura non disponibile';
  });
}
