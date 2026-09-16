import { Component, input } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Metric } from '../models';

@Component({
  selector: 'pd-metric', standalone: true, imports: [DecimalPipe],
  template: `<article class="metric"><span>{{ metric().label }}</span><p>@if(metric().value !== null){ {{ metric().value | number:'1.0-1' }} <small>{{ metric().unit }}</small> }@else{ — }</p><div>{{ metric().value !== null ? 'Lettura hardware' : 'Sensore non disponibile' }}</div></article>`,
  styles: [`.metric{padding:20px;border:1px solid var(--line);border-radius:10px;background:var(--panel)}span{font-size:12px;color:var(--muted)}p{font:500 30px var(--mono);margin:15px 0 12px;color:var(--text)}small{font-size:12px;color:var(--muted)}.metric div{font:10px var(--mono);color:var(--muted)}`]
})
export class MetricCardComponent { metric = input.required<Metric>(); }
