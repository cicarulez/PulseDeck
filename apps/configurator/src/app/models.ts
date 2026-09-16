export interface WidgetConfig { slot: string; source: 'metric' | 'sensor' | 'none'; metricId: string; sensorId: string; sensorName: string; label: string; maximum: number; }
export interface WidgetSlot { id: string; name: string; isBar: boolean; }
export interface WidgetCatalog { slots: WidgetSlot[]; defaults: WidgetConfig[]; }
export interface DeckConfig {
  widgets: WidgetConfig[];
  schemaVersion: number; profileMode: string; gameProcesses: string[]; profileDelaySeconds: number;
  discordMode: string; discordBaseUrl: string; trackedMemberId: string; displayPort: string; backgroundPath: string; accentColor: string;
}
export interface Metric { id: string; label: string; value: number | null; unit: string; }
export interface SensorReading { id: string; name: string; hardwareId: string; hardwareName: string; hardwareType: string; sensorType: string; value: number | null; minimum: number | null; maximum: number | null; unit: string; }
export interface HardwareState { metrics: Metric[]; sensors: SensorReading[]; status: string; detail: string | null; isAdministrator: boolean; pawnIoInstalled: boolean; }
export interface VoiceMember { id: string; name: string; mute: boolean; deaf: boolean; }
export interface DisplayState { connected: boolean; port: string; deviceId: string | null; status: string; error: string | null; }
export interface DeckState {
  timestamp: string; profile: string; foregroundApp: string;
  hardware: HardwareState;
  media: { playing: boolean; title: string; artist: string; app: string; positionSeconds: number; durationSeconds: number; status: string };
  discord: { members: VoiceMember[]; tracked: VoiceMember | null; status: string; detail: string | null };
  display: DisplayState; fpsStatus: string;
}
