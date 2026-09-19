import { SectionTabsComponent, SectionTab } from '../shared/section-tabs.component';
import { Component, computed, input, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HardwareState, SensorReading } from '../models';
import { StatusBadgeComponent } from '../shared/status-badge.component';

@Component({ selector: 'pd-sensors', standalone: true, imports: [SectionTabsComponent, DecimalPipe, FormsModule, StatusBadgeComponent], templateUrl: './sensors.component.html', styleUrl: './sensors.component.scss' })
export class SensorsComponent {
  readonly hardware = input<HardwareState | null>(null);
  readonly online = input(false);
  readonly category = signal('all');
  readonly categories: readonly SectionTab[] = [
    { id: 'cpu', label: 'CPU' }, { id: 'gpu', label: 'GPU' }, { id: 'memory', label: 'Memoria' },
    { id: 'board', label: 'Scheda madre' }, { id: 'storage', label: 'Dischi' },
    { id: 'network', label: 'Rete' }, { id: 'other', label: 'Altri' }
  ];
  categoryOf(sensor: SensorReading) {
    const type = sensor.hardwareType.toLowerCase();
    if (type === 'cpu') return 'cpu';
    if (type.startsWith('gpu')) return 'gpu';
    if (type === 'memory') return 'memory';
    if (['motherboard', 'superio', 'embeddedcontroller'].includes(type)) return 'board';
    if (type === 'storage') return 'storage';
    if (type === 'network') return 'network';
    return 'other';
  }
  readonly categoryTabs = computed<SectionTab[]>(() => {
    const counts = new Map<string, number>();
    for (const sensor of this.sensors()) { const id = this.categoryOf(sensor); counts.set(id, (counts.get(id) ?? 0) + 1); }
    return [{ id: 'all', label: 'Tutti', count: this.sensors().length }, ...this.categories
      .filter(c => counts.has(c.id)).map(c => ({ ...c, count: counts.get(c.id) }))];
  });
  readonly selectedCategory = computed(() => this.categoryTabs().some(c => c.id === this.category()) ? this.category() : 'all');
  readonly query = signal('');
  readonly type = signal('');
  readonly availability = signal('all');
  readonly labels: Record<string, string> = { Temperature: 'Temperatura', Load: 'Utilizzo', Fan: 'Ventole', Voltage: 'Tensione', Current: 'Corrente', Power: 'Potenza', Clock: 'Clock', Frequency: 'Frequenza', Flow: 'Flusso', Control: 'Controllo', Level: 'Livello', Factor: 'Fattore', Data: 'Dati', SmallData: 'Memoria', Throughput: 'Trasferimento', TimeSpan: 'Durata', Timing: 'Latenza', Energy: 'Energia', Noise: 'Rumore', Conductivity: 'Conduttività', Humidity: 'Umidità' };
  readonly sensors = computed(() => this.hardware()?.sensors ?? []);
  readonly available = computed(() => this.sensors().filter(s => s.value !== null).length);
  readonly types = computed(() => [...new Set(this.sensors().map(s => s.sensorType))].sort((a, b) => this.label(a).localeCompare(this.label(b))));
  readonly filtered = computed(() => {
    const query = this.query().trim().toLocaleLowerCase();
    return this.sensors().filter(s => (this.selectedCategory() === 'all' || this.categoryOf(s) === this.selectedCategory())
      && (!this.type() || s.sensorType === this.type())
      && (this.availability() === 'all' || (this.availability() === 'available') === (s.value !== null))
      && (!query || `${s.name} ${s.hardwareName} ${s.hardwareType} ${this.label(s.sensorType)} ${s.id}`.toLocaleLowerCase().includes(query)));
  });
  readonly groups = computed(() => {
    const groups = new Map<string, { id: string; name: string; type: string; sensors: SensorReading[] }>();
    for (const sensor of this.filtered()) {
      if (!groups.has(sensor.hardwareId)) groups.set(sensor.hardwareId, { id: sensor.hardwareId, name: sensor.hardwareName, type: sensor.hardwareType, sensors: [] });
      groups.get(sensor.hardwareId)!.sensors.push(sensor);
    }
    return [...groups.values()];
  });
  label(type: string) { return this.labels[type] ?? type; }
}
