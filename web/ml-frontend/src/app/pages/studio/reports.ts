import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Ticket } from '../../models/studio.model';
import { AuthService } from '../../services/auth.service';
import { apiError, StudioService } from '../../services/studio.service';
import { StudioNav } from './studio-nav';

export const STATUS_LABELS: Record<string, string> = {
  open: 'Open: waiting for the model review',
  reviewed: 'Reviewed by the model: waiting for a designer',
  expected: 'Closed: expected behaviour',
  bug: 'Confirmed bug: to fix',
  resolved: 'Resolved',
};

export const VERDICT_LABELS: Record<string, string> = {
  expected: 'The brain would do this',
  unlikely: 'Unlikely for the brain',
  impossible: 'Impossible for the brain',
};

@Component({
  selector: 'app-studio-reports',
  imports: [FormsModule, RouterLink, DatePipe, StudioNav],
  template: `
    <div class="container py-5">
      <app-studio-nav />
      <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
        <div>
          <h1 class="h3 mb-1">Behaviour reports</h1>
          <p class="text-muted small mb-0">Something Pockets did that looked wrong, a bug, or feedback. Behaviour reports are checked
            against the trained network before a designer decides.</p>
        </div>
        <a class="btn btn-primary" routerLink="/studio/reports/new">New report</a>
      </div>
      @if (error()) { <div class="alert alert-danger py-2">{{ error() }}</div> }

      <div class="d-flex flex-wrap gap-2 align-items-end mb-3">
        @if (auth.can('designer')) {
          <div class="form-check form-switch me-3">
            <input class="form-check-input" type="checkbox" id="mine" [(ngModel)]="mine" (ngModelChange)="load()">
            <label class="form-check-label small" for="mine">Only mine</label>
          </div>
        }
        <select class="form-select form-select-sm w-auto" aria-label="Status" [(ngModel)]="status" (ngModelChange)="load()">
          <option value="">Any status</option>
          @for (s of statuses; track s) { <option [value]="s">{{ labels[s] }}</option> }
        </select>
        <select class="form-select form-select-sm w-auto" aria-label="Kind" [(ngModel)]="kind" (ngModelChange)="load()">
          <option value="">Any kind</option>
          <option value="behaviour">Behaviour</option><option value="bug">Bug</option><option value="feedback">Feedback</option>
        </select>
      </div>

      <div class="list-group">
        @for (t of rows(); track t.id) {
          <a class="list-group-item list-group-item-action" [routerLink]="['/studio/reports', t.id]">
            <div class="d-flex flex-wrap justify-content-between gap-2">
              <strong>#{{ t.id }} {{ t.title }}</strong>
              <span class="small text-muted">{{ t.updated_at * 1000 | date: 'yyyy-MM-dd HH:mm' }}</span>
            </div>
            <div class="small d-flex flex-wrap gap-2 mt-1">
              <span class="badge text-bg-dark border">{{ t.kind }}</span>
              <span class="badge" [class.text-bg-info]="t.status === 'open' || t.status === 'reviewed'"
                    [class.text-bg-success]="t.status === 'expected' || t.status === 'resolved'" [class.text-bg-danger]="t.status === 'bug'">{{ labels[t.status] }}</span>
              @if (t.ai_verdict) { <span class="badge text-bg-secondary">model: {{ verdicts[t.ai_verdict] }}</span> }
              @if (t.simulated) { <span class="badge text-bg-warning">SIMULATED</span> }
              <span class="text-muted">by {{ t.reporter }}@if (t.night) { · night {{ t.night }} }</span>
            </div>
          </a>
        } @empty {
          <div class="list-group-item text-muted small">No reports match.</div>
        }
      </div>
    </div>
  `,
})
export class StudioReports {
  private readonly studio = inject(StudioService);
  protected readonly auth = inject(AuthService);
  protected readonly rows = signal<Ticket[]>([]);
  protected readonly error = signal('');
  protected readonly labels = STATUS_LABELS;
  protected readonly verdicts = VERDICT_LABELS;
  protected readonly statuses = Object.keys(STATUS_LABELS);
  protected mine = !this.auth.can('designer');
  protected status = inject(ActivatedRoute).snapshot.queryParamMap.get('status') ?? '';
  protected kind = '';

  constructor() {
    this.load();
  }

  protected load(): void {
    const filters: Record<string, string> = {};
    if (this.mine) filters['mine'] = '1';
    if (this.status) filters['status'] = this.status;
    if (this.kind) filters['kind'] = this.kind;
    this.studio.tickets(filters).subscribe({ next: (r) => this.rows.set(r.tickets), error: (e) => this.error.set(apiError(e)) });
  }
}
