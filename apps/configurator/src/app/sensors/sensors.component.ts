import { Component, computed, input, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HardwareState, SensorReading } from '../models';
import { StatusBadgeComponent } from '../shared/status-badge.component';

@Component({ selector: 'pd-sensors', standalone: true, imports: [DecimalPipe, FormsModule, StatusBadgeComponent], templateUrl: './sensors.component.html', styleUrl: './sensors.component.scss' })
export class SensorsComponent {
  readonly hardware = input<HardwareState | null>(null);
  readonly online = input(false);
  readonly query = signal('');
  readonly type = signal('');
  readonly availability = signal('all');
  readonly labels: Record<string, string> = { Temperature: 'Temperatura', Load: 'Utilizzo', Fan: 'Ventole', Voltage: 'Tensione', Current: 'Corrente', Power: 'Potenza', Clock: 'Clock', Frequency: 'Frequenza', Flow: 'Flusso', Control: 'Controllo', Level: 'Livello', Factor: 'Fattore', Data: 'Dati', SmallData: 'Memoria', Throughput: 'Trasferimento', TimeSpan: 'Durata', Timing: 'Latenza', Energy: 'Energia', Noise: 'Rumore', Conductivity: 'Conduttività', Humidity: 'Umidità' };
  readonly sensors = computed(() => this.hardware()?.sensors ?? []);
  readonly available = computed(() => this.sensors().filter(s => s.value !== null).length);
  readonly types = computed(() => [...new Set(this.sensors().map(s => s.sensorType))].sort((a, b) => this.label(a).localeCompare(this.label(b))));
  readonly filtered = computed(() => {
    const query = this.query().trim().toLocaleLowerCase();
    return this.sensors().filter(s => (!this.type() || s.sensorType === this.type())
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
