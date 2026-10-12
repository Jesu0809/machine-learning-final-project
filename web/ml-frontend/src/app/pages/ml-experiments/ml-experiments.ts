import { Component, inject, signal } from '@angular/core';
import { PercentPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ResultsService } from '../../services/results.service';
import { Runs } from '../../models/results.model';

@Component({
  selector: 'app-ml-experiments',
  imports: [RouterLink, PercentPipe],
  templateUrl: './ml-experiments.html',
})
export class MlExperiments {
  private readonly resultsService = inject(ResultsService);
  protected readonly runs = signal<Runs | null>(null);

  constructor() {
    this.resultsService.getRuns().subscribe({ next: (r) => this.runs.set(r) });
  }
}
