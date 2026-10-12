export interface TableRow {
  index: string;
  name: string;
  encoding?: string;
  detail?: string;
}

export interface RewardRow {
  event: string;
  reward: string;
  kind: 'terminal' | 'shaping';
}

export interface NightRow {
  night: number;
  objectives: string;
  freddy: string;
  other: string;
}

export interface ModelInfo {
  network: number[];
  rooms: string[];
  neighbors: number[][];
  spots: string[][];
  observations: TableRow[];
  actions: TableRow[];
  rewards: RewardRow[];
  nights: NightRow[];
  settings: {
    seconds_per_decision: number;
    max_decisions: number;
    sense_cooldown: number;
    gamma: number;
    algorithm: string;
  };
}

/** What Freddy knows, sent to /api/brain/decide. */
export interface BrainState {
  freddy_room: number;
  noises: Record<string, number>;
  last_seen_room: number | null;
  seconds_since_seen: number;
  sees_player: boolean;
  fuses: number;
  night_progress: number;
  searched_here: number[];
  /** Rooms he has not checked for a long time (the rest were checked recently). */
  rooms_not_checked: number[];
  sense_ready: boolean;
  sensed_room: number | null;
  seconds_since_sensed: number;
}

export interface BrainAction {
  index: number;
  name: string;
  probability: number;
}

export interface BrainDecision {
  actions: BrainAction[];
  chosen: BrainAction;
  inputs: number[];
}
