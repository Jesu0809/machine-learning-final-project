export type Role = 'playtester' | 'designer' | 'admin';

export interface User {
  id: number;
  username: string;
  role: Role;
  active: boolean;
  player_id: string;
  created_at: number;
  last_login: number | null;
  playtests?: number;
}

/** What Pockets knew at the moment of a report: the same input structure the model was trained on. */
export interface Situation {
  freddy_room: number;
  noises: Record<string, number>;
  last_seen_room?: number | null;
  seconds_since_seen?: number;
  rooms_not_checked: number[];
  sees_player: boolean;
  objectives: number;
  night_progress: number;
  searched_here: number[];
  sense_ready: boolean;
  sensed_room?: number | null;
  seconds_since_sensed?: number;
}

export interface Playtest {
  id: number;
  user_id: number;
  username?: string;
  source: 'game' | 'manual' | 'simulated';
  played_at: string;
  night: number;
  mode: string;
  outcome: 'Caught' | 'Escaped' | 'TimeUp' | 'Quit';
  cause: string;
  room: string;
  seconds: number;
  objectives_done: number;
  objectives_total: number;
  heard: number;
  seen: number;
  searches: number;
  sense_uses: number;
  sense_hits: number;
  decisions: number;
  threat_before: number | null;
  threat_after: number | null;
  device: string;
  notes: string;
  simulated: boolean;
}

export interface RankedAction {
  index: number;
  name: string;
  probability: number;
}

export interface Inference {
  id: number;
  user_id: number | null;
  username?: string | null;
  ticket_id: number | null;
  model_version: string;
  situation: Situation;
  result: RankedAction[];
  top_action: string;
  top_probability: number;
  observed_action: number | null;
  observed_probability: number | null;
  verdict: 'expected' | 'unlikely' | 'impossible' | null;
  latency_ms: number;
  simulated: boolean;
  created_at: number;
}

export type TicketStatus = 'open' | 'reviewed' | 'expected' | 'bug' | 'resolved';
export type TicketKind = 'behaviour' | 'bug' | 'feedback';

export interface Ticket {
  id: number;
  reporter_id: number;
  reporter: string;
  designer: string | null;
  playtest_id: number | null;
  kind: TicketKind;
  title: string;
  description: string;
  night: number | null;
  situation: Situation | null;
  observed_action: number | null;
  observed_label: string | null;
  status: TicketStatus;
  ai_verdict: 'expected' | 'unlikely' | 'impossible' | null;
  suggestion: 'expected' | 'bug' | null;
  decision: 'expected' | 'bug' | null;
  decision_note: string | null;
  resolution_note: string | null;
  inference?: Inference;
  simulated: boolean;
  created_at: number;
  updated_at: number;
}

export interface NightIndicator {
  night: number;
  plays: number;
  caught: number;
  catch_rate: number | null;
  avg_seconds: number | null;
}

export interface StudioIndicators {
  playtests: number;
  catch_rate: number | null;
  by_night: NightIndicator[];
  tickets: number;
  tickets_by_status: Record<TicketStatus, number>;
  tickets_by_kind: Record<TicketKind, number>;
  ai_verdicts: Record<string, number>;
  decided_with_model: number;
  agreement: number | null;
  inferences: number;
  users: number;
}

export interface Dashboard {
  user: User;
  model_version: string;
  mine: { playtests: number; caught: number; tickets: number; open_tickets: number };
  studio?: StudioIndicators;
  queue?: number;
}

export interface ModelCard {
  version: string;
  algorithm: string;
  trained: string;
  evaluation: string;
  threshold: number;
  rooms: string[];
  neighbors: number[][];
  spots: string[][];
}

export interface Monitoring {
  include_simulated: boolean;
  window_days: number;
  requests: number;
  errors: number;
  error_rate: number | null;
  p50_ms: number | null;
  p95_ms: number | null;
  endpoints: { endpoint: string; count: number; p50_ms: number; p95_ms: number }[];
  days: { day: string; requests: number; errors: number }[];
  inference_count: number;
  inference_p50_ms: number | null;
  inference_p95_ms: number | null;
  drift: { reference_size: number; recent_size: number; psi: Record<string, number | null> };
  recent_catch_rate: number | null;
  recent_playtests: number;
  agreement: number | null;
  decided_with_model: number;
  expected_catch_rate: Record<string, number>;
  thresholds: Record<string, number>;
  alerts: string[];
  recent_errors: { at: number; path: string; message: string }[];
  simulated_rows: Record<string, number>;
}

export interface Prediction {
  inference_id: number;
  actions: RankedAction[];
  observed_probability: number | null;
  verdict: 'expected' | 'unlikely' | 'impossible' | null;
  latency_ms: number;
  model_version: string;
}
