import { Component, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe, PercentPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Inference, ModelCard, Prediction, Situation } from '../../models/studio.model';
import { apiError, StudioService } from '../../services/studio.service';
import { VERDICT_LABELS } from './reports';
import { actionsFor, emptySituation, SituationForm } from './situation-form';
import { StudioNav } from './studio-nav';

@Component({
  selector: 'app-studio-model',
  imports: [FormsModule, RouterLink, DatePipe, DecimalPipe, PercentPipe, SituationForm, StudioNav],
  template: `
    <div class="container py-5">
      <app-studio-nav />
      <h1 class="h3 mb-1">The model in service</h1>
      @if (error()) { <div class="alert alert-danger py-2">{{ error() }}</div> }
      @if (card(); as m) {
        <div class="card mb-4"><div class="card-body small">
          <div class="row g-2">
            <div class="col-md-6"><strong>Version:</strong> <code>{{ m.version }}</code></div>
            <div class="col-md-6"><strong>Algorithm:</strong> {{ m.algorithm }}</div>
            <div class="col-md-6"><strong>Training:</strong> {{ m.trained }}</div>
            <div class="col-md-6"><strong>Evaluation:</strong> {{ m.evaluation }}</div>
            <div class="col-12"><strong>Decision rule:</strong> a reported move the network gives at least {{ m.threshold | percent }} is "the brain would do this";
              below it is "unlikely"; a move it cannot choose there is "impossible". The evidence is on the
              <a routerLink="/crisp-ml/evaluation">Evaluation</a> and <a routerLink="/crisp-ml/deployment">Deployment</a> pages.</div>
          </div>
        </div></div>

        <div class="row g-4 mb-4">
          <div class="col-lg-7"><div class="card h-100"><div class="card-body">
            <h2 class="h6 mb-3">Ask the network about a situation</h2>
            <app-situation-form [situation]="situation" [card]="m" (changed)="refresh()" />
            <label class="form-label small mt-3" for="obs">Compare with a move (optional)</label>
            <select id="obs" class="form-select mb-3" [(ngModel)]="observed">
              <option [ngValue]="null">no move to compare</option>
              @for (a of actions(); track a.index) { <option [ngValue]="a.index">{{ a.name }}{{ a.legal ? '' : ' (not possible here)' }}</option> }
            </select>
            <button type="button" class="btn btn-info" (click)="ask()">Run inference</button>
          </div></div></div>
          <div class="col-lg-5"><div class="card h-100"><div class="card-body">
            <h2 class="h6">Answer</h2>
            @if (prediction(); as p) {
              <p class="small text-muted">{{ p.model_version }} · {{ p.latency_ms | number: '1.1-1' }} ms · saved as inference #{{ p.inference_id }}</p>
              @if (p.verdict) { <p><strong>{{ verdicts[p.verdict] }}</strong> ({{ (p.observed_probability ?? 0) | percent: '1.1-1' }})</p> }
              @for (a of p.actions.slice(0, 8); track a.index) {
                <div class="d-flex justify-content-between small"><span>{{ a.name }}</span><span>{{ a.probability | percent: '1.1-1' }}</span></div>
                <div class="progress mb-2" style="height: 6px" role="progressbar" [attr.aria-label]="a.name"><div class="progress-bar" [style.width.%]="a.probability * 100"></div></div>
              }
            } @else { <p class="small text-muted">Describe a situation and run the inference.</p> }
          </div></div></div>
        </div>
      }

      <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">
        <h2 class="h5 mb-0">Inference history</h2>
        <div class="form-check form-switch mb-0">
          <input class="form-check-input" type="checkbox" id="sim" [(ngModel)]="simulated" (ngModelChange)="load()">
          <label class="form-check-label small" for="sim">Include simulated rows</label>
        </div>
      </div>
      <div class="table-responsive">
        <table class="table table-sm align-middle small">
          <thead><tr><th>#</th><th>When</th><th>User</th><th>Report</th><th>Top action</th><th>Observed p</th><th>Verdict</th><th>ms</th><th class="d-none d-lg-table-cell">Model</th></tr></thead>
          <tbody>
            @for (i of history(); track i.id) {
              <tr>
                <td>{{ i.id }} @if (i.simulated) { <span class="badge text-bg-warning">SIM</span> }</td>
                <td>{{ i.created_at * 1000 | date: 'MM-dd HH:mm' }}</td>
                <td>{{ i.username ?? 'public demo' }}</td>
                <td>@if (i.ticket_id) { <a [routerLink]="['/studio/reports', i.ticket_id]">#{{ i.ticket_id }}</a> } @else { — }</td>
                <td>{{ i.top_action }} ({{ i.top_probability | percent: '1.0-0' }})</td>
                <td>{{ i.observed_probability === null ? '—' : (i.observed_probability | percent: '1.1-1') }}</td>
                <td>{{ i.verdict ? verdicts[i.verdict] : '—' }}</td>
                <td>{{ i.latency_ms | number: '1.1-1' }}</td>
                <td class="d-none d-lg-table-cell"><code>{{ i.model_version }}</code></td>
              </tr>
            } @empty { <tr><td colspan="9" class="text-muted">No inferences yet.</td></tr> }
          </tbody>
        </table>
      </div>
    </div>
  `,
})
export class StudioModel {
  private readonly studio = inject(StudioService);
  protected readonly card = signal<ModelCard | null>(null);
  protected readonly history = signal<Inference[]>([]);
  protected readonly prediction = signal<Prediction | null>(null);
  protected readonly actions = signal<{ index: number; name: string; legal: boolean }[]>([]);
  protected readonly error = signal('');
  protected readonly verdicts: Record<string, string> = VERDICT_LABELS;
  protected situation: Situation = emptySituation();
  protected observed: number | null = null;
  protected simulated = false;

  constructor() {
    this.studio.model().subscribe({
      next: (m) => {
        this.card.set(m);
        this.refresh();
      },
      error: (e) => this.error.set(apiError(e)),
    });
    this.load();
  }

  protected load(): void {
    this.studio.inferences(this.simulated).subscribe({ next: (r) => this.history.set(r.inferences), error: (e) => this.error.set(apiError(e)) });
  }

  protected refresh(): void {
    const m = this.card();
    if (m) this.actions.set(actionsFor(m, this.situation.freddy_room));
  }

  protected ask(): void {
    this.studio.predict(this.situation, this.observed).subscribe({
      next: (p) => {
        this.prediction.set(p);
        this.load();
      },
      error: (e) => this.error.set(apiError(e)),
    });
  }
}
