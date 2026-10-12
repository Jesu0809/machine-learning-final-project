import { Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe, PercentPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ResultsService } from '../../services/results.service';
import { EvaluationRun, Results, Runs } from '../../models/results.model';

const POLICY_ORDER = ['trained', 'scripted', 'random'];
const SURVIVOR_ORDER = ['Novice', 'Normal', 'Expert'];

@Component({
  selector: 'app-ml-metrics',
  imports: [RouterLink, DecimalPipe, PercentPipe],
  templateUrl: './ml-metrics.html',
})
export class MlMetrics {
  private readonly resultsService = inject(ResultsService);
  protected readonly results = signal<Results | null>(null);
  protected readonly runs = signal<Runs | null>(null);

  protected readonly rows = computed<EvaluationRun[]>(() =>
    [...(this.results()?.runs ?? [])].sort(
      (a, b) =>
        SURVIVOR_ORDER.indexOf(a.survivor) - SURVIVOR_ORDER.indexOf(b.survivor) ||
        POLICY_ORDER.indexOf(a.policy) - POLICY_ORDER.indexOf(b.policy),
    ),
  );

  protected readonly policyLabels: Record<string, string> = {
    trained: 'Trained (PPO)',
    scripted: 'Scripted rules',
    random: 'Random',
  };

  constructor() {
    this.resultsService.getResults().subscribe({ next: (r) => this.results.set(r) });
    this.resultsService.getRuns().subscribe({ next: (r) => this.runs.set(r) });
  }
}
