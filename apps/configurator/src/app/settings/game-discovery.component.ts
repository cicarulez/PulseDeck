import { Component, effect, inject, input, OnDestroy, OnInit, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DeckService } from '../deck.service';
import { DiscoveredGame, GameDiscoveryOptions, GameLibraryStatus } from '../models';

@Component({ selector: 'pd-game-discovery', standalone: true, imports: [FormsModule],
  templateUrl: './game-discovery.component.html', styleUrl: './game-discovery.component.scss' })
export class GameDiscoveryComponent implements OnInit, OnDestroy {
  options = input.required<GameDiscoveryOptions>(); busy = input(false);
  optionsChange = output<GameDiscoveryOptions>(); processesChange = output<string[]>();
  readonly state = signal<GameLibraryStatus | null>(null); readonly message = signal('');
  readonly requesting = signal(false); readonly failedImages = signal<Set<string>>(new Set());
  private readonly deck = inject(DeckService);
  private timer?: ReturnType<typeof setInterval>;
  constructor() {
    effect(() => this.processesChange.emit(this.options().enabled ? [...new Set((this.state()?.games ?? [])
      .filter(g => this.status(g) === 'recognized').map(g => g.executable.split(/[\\/]/).pop()!.replace(/\.exe$/i, '')))] : []));
  }
  ngOnInit() { void this.refresh(); this.timer = setInterval(() => void this.refresh(), 5000); }
  ngOnDestroy() { if (this.timer) clearInterval(this.timer); }
  update(values: Partial<GameDiscoveryOptions>) { this.optionsChange.emit({ ...this.options(), ...values }); }
  folders(value: string) { this.update({ folders: value.split('\n').map(p => p.trim()).filter(Boolean) }); }
  status(game: DiscoveredGame) {
    const contains = (paths: string[]) => paths.some(p => p.toLowerCase() === game.executable.toLowerCase());
    return contains(this.options().ignoredExecutables) ? 'ignored' : game.automatic || contains(this.options().confirmedExecutables) ? 'recognized' : 'review';
  }
  imageFailed(game: DiscoveredGame) { this.failedImages.update(ids => new Set([...ids, game.thumbnailId])); }
  label(game: DiscoveredGame) { return { recognized: 'Riconosciuto', review: 'Da confermare', ignored: 'Ignorato' }[this.status(game)]; }
  decide(game: DiscoveredGame, decision: 'confirm' | 'ignore' | 'reset') {
    const without = (paths: string[]) => paths.filter(p => p.toLowerCase() !== game.executable.toLowerCase());
    this.update({ confirmedExecutables: [...without(this.options().confirmedExecutables), ...(decision === 'confirm' ? [game.executable] : [])],
      ignoredExecutables: [...without(this.options().ignoredExecutables), ...(decision === 'ignore' ? [game.executable] : [])] });
  }
  async refresh() {
    try { this.state.set(await this.deck.gameLibraries()); }
    catch { this.message.set('Catalogo non disponibile.'); }
  }
  async scan() {
    this.requesting.set(true); this.message.set(''); this.failedImages.set(new Set());
    try { await this.deck.scanGames(); this.message.set('Scansione richiesta sulle cartelle salvate. I risultati si aggiornano automaticamente.'); await this.refresh(); }
    catch (e) { this.message.set(e instanceof Error ? e.message : 'Scansione non riuscita.'); }
    finally { this.requesting.set(false); }
  }
}
