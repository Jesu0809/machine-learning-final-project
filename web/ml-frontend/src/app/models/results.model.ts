export interface EvaluationRun {
  policy: 'trained' | 'scripted' | 'random';
  survivor: 'Novice' | 'Normal' | 'Expert';
  nights: number;
  catch_rate: number;
  catch_ci_low: number;
  catch_ci_high: number;
  escape_rate: number;
  timeup_rate: number;
  avg_ticks: number;
  avg_ticks_to_catch: number;
  avg_searches: number;
  failed_search_rate: number;
  search_spot0_share: number;
}

export interface Results {
  generated_at: string;
  model: string;
  nights_per_run: number;
  runs: EvaluationRun[];
}

export interface TrainingRun {
  id: string;
  date: string;
  steps: number;
  minutes: number;
  observations: number;
  actions: number;
  reward_start: number;
  reward_end: number;
  change: string;
  catch_rate: Record<string, number> | null;
  finding: string;
  decision: string;
}

export interface SenseRow {
  survivor: string;
  trained_uses: number;
  trained_hit: number;
  scripted_uses: number;
  scripted_hit: number;
}

export interface Runs {
  runs: TrainingRun[];
  sense: SenseRow[];
  checks: string[];
}
