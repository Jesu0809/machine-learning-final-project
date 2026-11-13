import { Component, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe, KeyValuePipe, PercentPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Monitoring } from '../../models/studio.model';
import { apiError, StudioService } from '../../services/studio.service';
import { StudioNav } from './studio-nav';

@Component({
  selector: 'app-studio-monitoring',
  imports: [FormsModule, RouterLink, DatePipe, DecimalPipe, PercentPipe, KeyValuePipe, StudioNav],
  template: `
    <div class="container py-5">
      <app-studio-nav />
      <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
        <div>
          <h1 class="h3 mb-1">Monitoring</h1>
          <p class="text-muted small mb-0">Last 7 days of API requests, errors, response times, model inferences, input drift and outcomes.
            The procedure is on the <a routerLink="/crisp-ml/monitoring">Monitoring and maintenance</a> page.</p>
        </div>
        <div class="d-flex flex-wrap gap-2 align-items-center">
          <div class="form-check form-switch mb-0">
            <input class="form-check-input" type="checkbox" id="sim" [(ngModel)]="simulated" (ngModelChange)="load()">
            <label class="form-check-label small" for="sim">Include SIMULATED data</label>
          </div>
          <button type="button" class="btn btn-sm btn-outline-warning" (click)="simulate()">Run a simulated logging test</button>
          <button type="button" class="btn btn-sm btn-outline-secondary" (click)="clear()">Delete simulated rows</button>
        </div>
      </div>
      @if (message()) { <div class="alert alert-warning py-2">{{ message() }}</div> }
      @if (error()) { <div class="alert alert-danger py-2">{{ error() }}</div> }

      @if (data(); as m) {
        @if (m.include_simulated) {
          <div class="alert alert-warning py-2 small">Showing real AND <strong>SIMULATED</strong> operational data
            ({{ m.simulated_rows['requests'] }} requests, {{ m.simulated_rows['inferences'] }} inferences, {{ m.simulated_rows['playtests'] }} playtests are simulated).</div>
        }
        <div class="card mb-3" [class.border-danger]="m.alerts.length" [class.border-success]="!m.alerts.length"><div class="card-body">
          <h2 class="h6">Alerts</h2>
          @for (a of m.alerts; track a) { <div class="small text-danger">⚠ {{ a }}</div> }
          @empty { <div class="small text-success">No indicator is outside its threshold.</div> }
        </div></div>

        <div class="row g-3 mb-3">
          <div class="col-6 col-lg-3"><div class="card h-100"><div class="card-body"><div class="small text-muted">API requests</div>
            <div class="display-6">{{ m.requests }}</div><div class="small">{{ m.errors }} server errors ({{ m.error_rate === null ? '—' : (m.error_rate | percent: '1.1-1') }})</div></div></div></div>
          <div class="col-6 col-lg-3"><div class="card h-100"><div class="card-body"><div class="small text-muted">Response time</div>
            <div class="display-6">{{ m.p95_ms ?? '—' }}<small class="fs-6"> ms p95</small></div><div class="small">median {{ m.p50_ms ?? '—' }} ms (alert over {{ m.thresholds['p95_ms'] }} ms)</div></div></div></div>
          <div class="col-6 col-lg-3"><div class="card h-100"><div class="card-body"><div class="small text-muted">Model inferences</div>
            <div class="display-6">{{ m.inference_count }}</div><div class="small">inference p95 {{ m.inference_p95_ms ?? '—' }} ms</div></div></div></div>
          <div class="col-6 col-lg-3"><div class="card h-100"><div class="card-body"><div class="small text-muted">Real catch rate (last {{ m.recent_playtests }})</div>
            <div class="display-6">{{ m.recent_catch_rate === null ? '—' : (m.recent_catch_rate | percent: '1.0-0') }}</div>
            <div class="small">expected band {{ m.thresholds['catch_low'] | percent }}–{{ m.thresholds['catch_high'] | percent }}</div></div></div></div>
        </div>

        <div class="row g-3 mb-3">
          <div class="col-lg-6"><div class="card h-100"><div class="card-body">
            <h2 class="h6">Input drift (population stability index)</h2>
            <p class="small text-muted">Oldest half of the inferences ({{ m.drift.reference_size }}) against the newest half ({{ m.drift.recent_size }}).
              PSI under 0.1 is stable, 0.1–0.25 worth watching, over {{ m.thresholds['psi'] }} an alert.</p>
            <table class="table table-sm small mb-0"><tbody>
              @for (f of m.drift.psi | keyvalue; track f.key) {
                <tr><td>{{ f.key }}</td><td class="text-end" [class.text-danger]="(f.value ?? 0) > m.thresholds['psi']">{{ f.value ?? 'not enough data' }}</td></tr>
              }
            </tbody></table>
          </div></div></div>
          <div class="col-lg-6"><div class="card h-100"><div class="card-body">
            <h2 class="h6">Model quality in operation</h2>
            <p class="small mb-1">Designers agreed with the model's suggestion on
              <strong>{{ m.agreement === null ? '—' : (m.agreement | percent: '1.0-0') }}</strong> of {{ m.decided_with_model }} decided behaviour reports
              (alert under {{ m.thresholds['agreement'] | percent }} once there are 10).</p>
            <p class="small mb-0">Offline evaluation, for comparison: caught {{ m.expected_catch_rate['Novice'] | percent }} of novices,
              {{ m.expected_catch_rate['Normal'] | percent: '1.1-1' }} of normal and {{ m.expected_catch_rate['Expert'] | percent: '1.1-1' }} of expert simulated survivors.</p>
          </div></div></div>
        </div>

        <div class="row g-3">
          <div class="col-lg-7"><div class="card h-100"><div class="card-body">
            <h2 class="h6">Endpoints</h2>
            <div class="table-responsive"><table class="table table-sm small mb-0">
              <thead><tr><th>Endpoint</th><th>Calls</th><th>p50 ms</th><th>p95 ms</th></tr></thead>
              <tbody>@for (e of m.endpoints; track e.endpoint) { <tr><td><code>{{ e.endpoint }}</code></td><td>{{ e.count }}</td><td>{{ e.p50_ms }}</td><td>{{ e.p95_ms }}</td></tr> }</tbody>
            </table></div>
          </div></div></div>
          <div class="col-lg-5"><div class="card h-100"><div class="card-body">
            <h2 class="h6">Requests per day</h2>
            <table class="table table-sm small"><tbody>
              @for (d of m.days; track d.day) { <tr><td>{{ d.day }}</td><td class="text-end">{{ d.requests }}</td><td class="text-end text-danger">{{ d.errors || '' }}</td></tr> }
            </tbody></table>
            <h2 class="h6">Latest errors</h2>
            @for (e of m.recent_errors; track e.at) { <div class="small"><span class="text-muted">{{ e.at * 1000 | date: 'MM-dd HH:mm' }}</span> <code>{{ e.path }}</code> {{ e.message }}</div> }
            @empty { <div class="small text-muted">No errors logged.</div> }
          </div></div></div>
        </div>
        <p class="small text-muted mt-3">Rows marked simulated are generated by the test button above and are never mixed with real data unless the switch is on.
          Inference averages: p50 {{ m.inference_p50_ms | number: '1.1-2' }} ms.</p>
      }
    </div>
  `,
})
export class StudioMonitoring {
  private readonly studio = inject(StudioService);
  protected readonly data = signal<Monitoring | null>(null);
  protected readonly error = signal('');
  protected readonly message = signal('');
  protected simulated = false;

  constructor() {
    this.load();
  }

  protected load(): void {
    this.studio.monitoring(this.simulated).subscribe({ next: (m) => this.data.set(m), error: (e) => this.error.set(apiError(e)) });
  }

  protected simulate(): void {
    this.studio.simulate().subscribe({
      next: (r) => {
        this.message.set('SIMULATED test: ' + r.note);
        this.simulated = true;
        this.load();
      },
      error: (e) => this.error.set(apiError(e)),
    });
  }

  protected clear(): void {
    this.studio.clearSimulated().subscribe({
      next: () => {
        this.message.set('Simulated rows deleted.');
        this.simulated = false;
        this.load();
      },
      error: (e) => this.error.set(apiError(e)),
    });
  }
}
