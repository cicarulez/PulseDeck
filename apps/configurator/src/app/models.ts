export interface NewsChannel { name: string; url: string; enabled: boolean; }
export interface NewsOptions { enabled: boolean; rotationSeconds: number; fontSize: number; channels: NewsChannel[]; }
export interface NewsItem { source: string; title: string; url: string; publishedAt: string | null; }
export interface NewsSnapshot { status: string; channels: { name: string; url: string; status: string; items: NewsItem[] }[]; items: NewsItem[]; fetchedAt: string | null; }
export interface WeatherLocation { name: string; latitude: number; longitude: number; }
export interface WeatherPlace extends WeatherLocation { label: string; }
export interface WidgetConfig { slot: string; source: 'metric' | 'sensor' | 'network' | 'none'; metricId: string; sensorId: string; sensorName: string; label: string; maximum: number; style: 'auto' | 'value' | 'bar' | 'ring'; }
export interface WidgetSlot { id: string; name: string; isBar: boolean; }
export interface WidgetCatalog { slots: WidgetSlot[]; defaults: WidgetConfig[]; }
export interface GameTheme { processName: string; backgroundPath: string; }
export interface DeckConfig {
  widgets: WidgetConfig[];
  layout: 'classic' | 'compact' | 'weather';
  weatherLocation: WeatherLocation | null; news: NewsOptions;
  gamingLayout: boolean; gamingVoiceActivity: boolean; gameThemes: GameTheme[];
  schemaVersion: number; profileMode: string; gameProcesses: string[]; profileDelaySeconds: number;
  discordMode: string; discordBaseUrl: string; trackedMemberId: string; displayPort: string; backgroundPath: string; accentColor: string;
}
export interface Metric { id: string; label: string; value: number | null; unit: string; }
export interface SensorReading { id: string; name: string; hardwareId: string; hardwareName: string; hardwareType: string; sensorType: string; value: number | null; minimum: number | null; maximum: number | null; unit: string; }
export interface HardwareState { metrics: Metric[]; sensors: SensorReading[]; status: string; detail: string | null; isAdministrator: boolean; pawnIoInstalled: boolean; }
export interface VoiceMember { id: string; name: string; mute: boolean; deaf: boolean; speaking: boolean | null; }
export interface ForegroundState { processId: number; processName: string; displayName: string; isGame: boolean; iconId: string | null; iconStatus: string; }
export interface DisplayState {
  connected: boolean; port: string; deviceId: string | null; status: string; error: string | null;
  recoveryAttempts: number; recoveries: number; acknowledgedFrames: number;
  lastAcknowledgedAt: string | null; lastTransportError: string | null; userDisconnected: boolean;
}
export interface DeckState {
  volume: { status: string; percent: number | null; muted: boolean };
  fps: { status: string; framesPerSecond: number | null; frameTimeMs: number | null; detail: string | null };
  gameSession: { processId: number; startedAt: string; elapsedSeconds: number } | null;
  gameArtwork: { status: string; source: string | null };
  news: NewsSnapshot;
  timestamp: string; profile: string; foregroundApp: string;
  hardware: HardwareState;
  media: { playing: boolean; title: string; artist: string; app: string; positionSeconds: number; durationSeconds: number; status: string; artworkId: string | null };
  discord: { speakingStatus: string; members: VoiceMember[]; tracked: VoiceMember | null; status: string; detail: string | null };
  display: DisplayState; fpsStatus: string; foreground: ForegroundState; game: ForegroundState | null;
}
