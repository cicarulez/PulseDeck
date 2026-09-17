import { Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HardwareState, SensorReading, WidgetConfig, WidgetSlot } from '../models';

@Component({ selector: 'pd-widget-slot-editor', standalone: true, imports: [FormsModule], templateUrl: './widget-slot-editor.component.html', styleUrl: './widget-slot-editor.component.scss' })
export class WidgetSlotEditorComponent {
  readonly widget = input.required<WidgetConfig>();
  readonly slot = input.required<WidgetSlot>();
  readonly compact = input(false);
  readonly usesScale = computed(() => this.widget().source !== 'network' && (this.compact() ? this.widget().style === 'ring' || this.widget().style === 'bar' || this.widget().style === 'auto' && this.slot().isBar : this.slot().isBar));
  readonly hardware = input<HardwareState | null>(null);
  readonly change = output<WidgetConfig>();
  readonly query = signal('');
  readonly selection = computed(() => this.key(this.widget()));
  readonly groups = computed(() => {
    const query = this.query().trim().toLocaleLowerCase();
    const selected = this.widget();
    const groups = new Map<string, { id: string; name: string; sensors: SensorReading[] }>();
    for (const sensor of this.hardware()?.sensors ?? []) {
      const isSelected = selected.source === 'sensor' && sensor.id === selected.sensorId && sensor.name === selected.sensorName;
      if (!isSelected && query && !`${sensor.hardwareName} ${sensor.name} ${sensor.sensorType} ${sensor.unit}`.toLocaleLowerCase().includes(query)) continue;
      if (!groups.has(sensor.hardwareId)) groups.set(sensor.hardwareId, { id: sensor.hardwareId, name: sensor.hardwareName, sensors: [] });
      groups.get(sensor.hardwareId)!.sensors.push(sensor);
    }
    return [...groups.values()];
  });
  readonly networks = computed(() => [...new Map((this.hardware()?.sensors ?? [])
    .filter(s => s.hardwareType === 'Network' && s.sensorType === 'Throughput')
    .map(s => [s.hardwareId, s])).values()]);
  networkKey(sensor: SensorReading) { return JSON.stringify(['network', sensor.hardwareId, sensor.hardwareName]); }
  readonly missing = computed(() => {
    const widget = this.widget();
    return widget.source === 'network' ? !this.networks().some(s => s.hardwareId === widget.sensorId && s.hardwareName === widget.sensorName) : widget.source === 'sensor' && !(this.hardware()?.sensors ?? []).some(s => s.id === widget.sensorId && s.name === widget.sensorName);
  });
  readonly reading = computed(() => {
    const widget = this.widget();
    if (widget.source === 'none') return 'Posizione nascosta';
    if (widget.source === 'network') {
      const read = (name: string) => this.hardware()?.sensors.find(s => s.hardwareId === widget.sensorId && s.hardwareName === widget.sensorName && s.name === name && s.sensorType === 'Throughput');
      const format = (name: string) => { const value = read(name)?.value; return value == null ? '—' : `${(value / 1024).toLocaleString('it-IT', { maximumFractionDigits: 1 })} KiB/s`; };
      return `↓ ${format('Download Speed')} · ↑ ${format('Upload Speed')}`;
    }
    const reading = widget.source === 'metric'
      ? this.hardware()?.metrics.find(m => m.id === widget.metricId)
      : this.hardware()?.sensors.find(s => s.id === widget.sensorId && s.name === widget.sensorName);
    return reading?.value == null ? 'Nessuna lettura disponibile' : `${reading.value.toLocaleString('it-IT', { maximumFractionDigits: 2 })} ${reading.unit}`;
  });
  key(widget: WidgetConfig) { return widget.source === 'sensor' || widget.source === 'network' ? JSON.stringify([widget.source, widget.sensorId, widget.sensorName]) : widget.source === 'metric' ? JSON.stringify(['metric', widget.metricId]) : 'none'; }
  metricKey(id: string) { return JSON.stringify(['metric', id]); }
  sensorKey(sensor: SensorReading) { return JSON.stringify(['sensor', sensor.id, sensor.name]); }
  patch(value: Partial<WidgetConfig>) { this.change.emit({ ...this.widget(), ...value }); }
  select(key: string) {
    if (key === 'none') { this.patch({ source: 'none' }); return; }
    const [source, id, name] = JSON.parse(key) as string[];
    if (source === 'network') {
      this.patch({ source, metricId: '', sensorId: id, sensorName: name, label: 'RETE', style: 'value' });
    } else if (source === 'metric') {
      const metric = this.hardware()?.metrics.find(m => m.id === id);
      this.patch({ source, metricId: id, sensorId: '', sensorName: '', label: (metric?.label ?? id).slice(0, 24), maximum: id === 'gpu.power' ? 500 : id === 'gpu.memory' ? 32768 : 100 });
    } else {
      const sensor = this.hardware()?.sensors.find(s => s.id === id && s.name === name);
      if (!sensor) return;
      const scales: Record<string, number> = { Fan: 3000, Power: 500, Clock: 6000, Frequency: 6000, Voltage: 12, Data: 128, SmallData: 32768, Throughput: 1000000000 };
      this.patch({ source: 'sensor', metricId: '', sensorId: id, sensorName: name, label: name.slice(0, 24), maximum: scales[sensor.sensorType] ?? 100 });
    }
  }
}
