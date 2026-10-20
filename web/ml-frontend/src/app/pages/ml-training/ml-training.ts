import { Component, inject, signal } from '@angular/core';
import { DecimalPipe, PercentPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ResultsService } from '../../services/results.service';
import { Runs } from '../../models/results.model';

@Component({
  selector: 'app-ml-training',
  imports: [RouterLink, DecimalPipe, PercentPipe],
  templateUrl: './ml-training.html',
})
export class MlTraining {
  private readonly resultsService = inject(ResultsService);
  protected readonly runs = signal<Runs | null>(null);
  protected readonly selected = signal('freddy_v6');

  protected readonly config = [
    ['Trainer', 'PPO'],
    ['Network', '2 hidden layers × 256 (Swish)'],
    ['Batch / buffer', '1024 / 20480'],
    ['Learning rate', '3e-4, linear decay'],
    ['β (entropy) / ε (clip)', '0.005 / 0.2'],
    ['γ / λ (GAE)', '0.99 / 0.95'],
    ['Epochs per update', '3'],
    ['Max steps', '5,000,000 (3,000,000 for v1 to v3)'],
    ['Parallel environments', '32 hunts in one Unity scene, no graphics'],
  ];

  constructor() {
    this.resultsService.getRuns().subscribe({ next: (r) => this.runs.set(r) });
  }

  protected plotUrl(): string {
    return '/api/plots/training?run=' + this.selected();
  }
}
