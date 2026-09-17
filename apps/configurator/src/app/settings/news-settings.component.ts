import { Component, input, model } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NewsChannel, NewsOptions, NewsSnapshot } from '../models';

@Component({ selector: 'pd-news-settings', standalone: true, imports: [FormsModule, DatePipe],
  templateUrl: './news-settings.component.html', styleUrl: './news-settings.component.scss' })
export class NewsSettingsComponent {
  options = model.required<NewsOptions>(); state = input<NewsSnapshot | null>(null); busy = input(false);
  readonly presets: NewsChannel[] = [
    { name: 'ANSA · Ultime notizie', url: 'https://www.ansa.it/sito/notizie/topnews/topnews_rss.xml', enabled: true },
    { name: 'ANSA · Tecnologia', url: 'https://www.ansa.it/canale_tecnologia/notizie/tecnologia_rss.xml', enabled: true },
    { name: 'ANSA · Sport', url: 'https://www.ansa.it/sito/notizie/sport/sport_rss.xml', enabled: true },
    { name: 'ANSA · Lazio', url: 'https://www.ansa.it/lazio/notizie/lazio_rss.xml', enabled: true },
    { name: 'Multiplayer.it · PC', url: 'https://multiplayer.it/feed/rss/news/pc/', enabled: true }
  ];
  customName = ''; customUrl = '';
  update(patch: Partial<NewsOptions>) { this.options.update(current => ({ ...current, ...patch })); }
  has(url: string) { return this.options().channels.some(c => c.url === url); }
  add(channel: NewsChannel) {
    if (this.options().channels.length >= 8 || this.has(channel.url)) return;
    this.update({ channels: [...this.options().channels, { ...channel }] });
  }
  customValid() {
    try { const u = new URL(this.customUrl.trim()); return this.customName.trim().length > 0 && u.protocol === 'https:' && !u.username && !u.password; }
    catch { return false; }
  }
  addCustom() {
    if (!this.customValid()) return;
    this.add({ name: this.customName.trim(), url: new URL(this.customUrl.trim()).href, enabled: true });
    this.customName = ''; this.customUrl = '';
  }
  change(index: number, patch: Partial<NewsChannel>) { this.update({ channels: this.options().channels.map((c, i) => i === index ? { ...c, ...patch } : c) }); }
  remove(index: number) { this.update({ channels: this.options().channels.filter((_, i) => i !== index) }); }
  status(url: string) { const s = this.state()?.channels.find(c => c.url === url)?.status; return s === 'connected' ? 'Collegato' : s === 'empty' ? 'Nessuna notizia recente' : s === 'unavailable' ? 'Feed non disponibile' : 'In attesa / non attivo'; }
}
