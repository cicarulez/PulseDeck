import { Component, computed, effect, inject, input, OnDestroy, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DeckService } from '../deck.service';
import { DiscoveredGame, GameTheme } from '../models';

type ImageKind = 'cover' | 'background';
type PreviewState = 'loading' | 'available' | 'unavailable';

@Component({
  selector: 'pd-game-themes', standalone: true, imports: [FormsModule],
  templateUrl: './game-themes.component.html', styleUrl: './game-themes.component.scss'
})
export class GameThemesComponent implements OnDestroy {
  themes = input.required<GameTheme[]>(); processes = input.required<string[]>(); games = input<DiscoveredGame[]>([]); busy = input(false);
  themesChange = output<GameTheme[]>();
  readonly kinds: { id: ImageKind; title: string; description: string; automatic: 'cover' | 'hero' }[] = [
    { id: 'cover', title: 'Copertina del widget', description: 'Immagine nel riquadro con sessione e FPS', automatic: 'cover' },
    { id: 'background', title: 'Sfondo panoramico', description: 'Immagine dietro a tutto il deck', automatic: 'hero' }
  ];
  readonly selected = signal(''); readonly uploading = signal<ImageKind | null>(null); readonly message = signal('');
  readonly automaticState = signal<Record<ImageKind, PreviewState>>({ cover: 'loading', background: 'loading' });
  readonly manualState = signal<Record<ImageKind, PreviewState | 'none'>>({ cover: 'none', background: 'none' });
  readonly manualUrls = signal<Record<ImageKind, string | null>>({ cover: null, background: null });
  private previewVersion = 0;
  private readonly deck = inject(DeckService);
  readonly choices = computed(() => [...new Set([...this.processes(), ...this.themes().map(t => t.processName)])].sort((a, b) => a.localeCompare(b)));
  readonly selectedGame = computed(() => this.games().find(g => this.key(g.executable.split(/[\\/]/).pop() ?? '') === this.key(this.selected())));
  readonly selectedTheme = computed(() => this.themes().find(t => this.key(t.processName) === this.key(this.selected())));

  constructor() {
    effect(() => { const choices = this.choices(); if (!choices.includes(this.selected())) this.select(choices[0] ?? ''); });
    effect(() => {
      const cover = this.manualPath('cover'), background = this.manualPath('background');
      untracked(() => void this.loadManualPreviews(cover, background));
    });
  }
  private key(process: string) { return process.replace(/\.exe$/i, '').toLowerCase(); }
  manualPath(kind: ImageKind) {
    const theme = this.selectedTheme();
    return kind === 'cover' ? theme?.coverPath ?? theme?.backgroundPath ?? '' : theme?.backgroundPath ?? '';
  }
  fileName(kind: ImageKind) { return this.manualPath(kind).split(/[\\/]/).pop() ?? ''; }
  select(process: string) {
    this.selected.set(process);
    this.automaticState.set({ cover: 'loading', background: 'loading' });
    this.message.set('');
  }
  automaticLoaded(kind: ImageKind) { this.automaticState.update(state => ({ ...state, [kind]: 'available' })); }
  automaticFailed(kind: ImageKind) { this.automaticState.update(state => ({ ...state, [kind]: 'unavailable' })); }
  private clearManualUrls() {
    for (const url of Object.values(this.manualUrls())) if (url) URL.revokeObjectURL(url);
    this.manualUrls.set({ cover: null, background: null });
  }
  private async loadManualPreviews(cover: string, background: string) {
    const version = ++this.previewVersion;
    this.clearManualUrls();
    this.manualState.set({ cover: cover ? 'loading' : 'none', background: background ? 'loading' : 'none' });
    await Promise.all(([['cover', cover], ['background', background]] as const).map(async ([kind, path]) => {
      if (!path) return;
      try {
        const url = URL.createObjectURL(await this.deck.previewGameImage(path));
        if (version !== this.previewVersion) { URL.revokeObjectURL(url); return; }
        this.manualUrls.update(urls => ({ ...urls, [kind]: url }));
        this.manualState.update(state => ({ ...state, [kind]: 'available' }));
      } catch {
        if (version === this.previewVersion) this.manualState.update(state => ({ ...state, [kind]: 'unavailable' }));
      }
    }));
  }
  private updateTheme(kind: ImageKind, path: string, processName: string) {
    const existing = this.themes().findIndex(t => this.key(t.processName) === this.key(processName));
    const previous = existing >= 0 ? this.themes()[existing] : null;
    // Materialize the old shared image when an older configuration is edited.
    const coverPath = kind === 'cover' ? path : previous?.coverPath ?? previous?.backgroundPath ?? '';
    const backgroundPath = kind === 'background' ? path : previous?.backgroundPath ?? '';
    const next = this.themes().filter((_, i) => i !== existing);
    if (coverPath || backgroundPath) next.push({ processName, coverPath, backgroundPath });
    this.themesChange.emit(next);
  }
  useAutomatic(kind: ImageKind) { this.updateTheme(kind, '', this.selected()); this.message.set('Scelta aggiornata. Premi Salva configurazione.'); }
  async chooseFile(event: Event, kind: ImageKind) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0]; input.value = '';
    if (!file) return;
    if (!this.selectedTheme() && this.themes().length >= 50) { this.message.set('Limite di 50 giochi con immagini personalizzate raggiunto.'); return; }
    const process = this.selected();
    this.uploading.set(kind); this.message.set('Caricamento e verifica dell’immagine in corso…');
    try {
      const path = await this.deck.uploadGameImage(file);
      this.updateTheme(kind, path, process);
      this.message.set('Immagine pronta. Premi Salva configurazione per applicarla.');
    } catch (error) { this.message.set(error instanceof Error ? error.message : 'Caricamento non riuscito.'); }
    finally { this.uploading.set(null); }
  }
  ngOnDestroy() { ++this.previewVersion; this.clearManualUrls(); }
}
