import { Component, DestroyRef, effect, inject, input, signal } from '@angular/core';

@Component({ selector: 'pd-live-preview', standalone: true,
  template: '@if (image(); as url) { <img [src]="url" [alt]="description()" width="1920" height="480" (load)="displayed()"> }',
  styles: ':host{display:block}img{display:block;width:100%;height:auto}' })
export class LivePreviewComponent {
  readonly source = input.required<string>();
  readonly description = input('Anteprima live del pannello PulseDeck');
  readonly image = signal('');
  private pending = '';
  private loading = false;
  private destroyed = false;
  private readonly abort = new AbortController();
  private readonly retired = new Set<string>();
  private candidate = '';

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.destroyed = true;
      this.abort.abort();
      for (const url of [this.image(), this.candidate, ...this.retired]) if (url) URL.revokeObjectURL(url);
    });
    effect(() => { this.pending = this.source(); void this.refresh(); });
  }

  displayed() {
    for (const url of this.retired) URL.revokeObjectURL(url);
    this.retired.clear();
  }

  private async refresh() {
    if (this.loading || this.destroyed) return;
    this.loading = true;
    try {
      while (this.pending && !this.destroyed) {
        const source = this.pending;
        this.pending = '';
        try {
          // Keep one request/decode in flight. New revisions replace pending
          // work rather than cancelling the image already being loaded.
          const response = await fetch(source, { cache: 'no-store',
            signal: AbortSignal.any([this.abort.signal, AbortSignal.timeout(2000)]) });
          if (!response.ok) continue;
          const blob = await response.blob();
          if (this.destroyed) break;
          this.candidate = URL.createObjectURL(blob);
          const decoder = new Image();
          decoder.src = this.candidate;
          await decoder.decode();
          if (this.destroyed) break;
          const previous = this.image();
          if (previous) this.retired.add(previous);
          this.image.set(this.candidate);
          this.candidate = '';
        } catch {
          if (this.candidate) URL.revokeObjectURL(this.candidate);
          this.candidate = '';
          // Retain the last valid picture and retry on the next revision.
        }
      }
    } finally { this.loading = false; }
  }
}
