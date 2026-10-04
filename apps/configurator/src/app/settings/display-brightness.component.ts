import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({ selector: 'pd-display-brightness', standalone: true, imports: [FormsModule],
  templateUrl: './display-brightness.component.html', styleUrl: './display-brightness.component.scss' })
export class DisplayBrightnessComponent {
  readonly percent = input<number | null>(null);
  readonly percentChange = output<number | null>();
  readonly busy = input(false);
  private manualPercent = 50;

  setInherited(inherited: boolean) {
    this.manualPercent = this.percent() ?? this.manualPercent;
    this.percentChange.emit(inherited ? null : this.manualPercent);
  }
  setPercent(percent: number) {
    this.manualPercent = percent;
    this.percentChange.emit(percent);
  }
}
