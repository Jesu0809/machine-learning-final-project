import { Component, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe, PercentPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ModelCard, Ticket } from '../../models/studio.model';
import { AuthService } from '../../services/auth.service';
import { apiError, StudioService } from '../../services/studio.service';
import { STATUS_LABELS, VERDICT_LABELS } from './reports';
import { StudioNav } from './studio-nav';

@Component({
  selector: 'app-studio-report-detail',
  imports: [FormsModule, RouterLink, DatePipe, DecimalPipe, PercentPipe, StudioNav],
  templateUrl: './report-detail.html',
})
export class StudioReportDetail {
  private readonly studio = inject(StudioService);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);
  protected readonly id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));
  protected readonly ticket = signal<Ticket | null>(null);
  protected readonly card = signal<ModelCard | null>(null);
  protected readonly error = signal('');
  protected readonly busy = signal(false);
  protected readonly labels = STATUS_LABELS;
  protected readonly verdicts = VERDICT_LABELS;
  protected decision: 'expected' | 'bug' = 'expected';
  protected note = '';
  protected resolution = '';

  constructor() {
    this.studio.model().subscribe({ next: (m) => this.card.set(m) });
    this.load();
  }

  private load(): void {
    this.studio.ticket(this.id).subscribe({ next: ({ ticket }) => this.show(ticket), error: (e) => this.error.set(apiError(e)) });
  }

  private show(ticket: Ticket): void {
    this.ticket.set(ticket);
    this.decision = ticket.suggestion ?? 'expected';
    this.busy.set(false);
  }

  protected isMine(t: Ticket): boolean {
    return t.reporter_id === this.auth.user()?.id;
  }

  protected roomName(i: number | null | undefined): string {
    return i === null || i === undefined ? '—' : (this.card()?.rooms[i] ?? String(i));
  }

  protected noiseText(t: Ticket): string {
    const noises = Object.entries(t.situation?.noises ?? {});
    const loud = ['', 'footsteps', 'running', 'a loud noise'];
    return noises.length ? noises.map(([r, l]) => `${loud[Math.round(l)] ?? 'noise'} in ${this.roomName(Number(r))}`).join(', ') : 'nothing';
  }

  private act(call: ReturnType<StudioService['review']>): void {
    this.error.set('');
    this.busy.set(true);
    call.subscribe({
      next: ({ ticket }) => this.show(ticket),
      error: (e) => {
        this.error.set(apiError(e));
        this.busy.set(false);
      },
    });
  }

  protected review(): void {
    this.act(this.studio.review(this.id));
  }

  protected decide(): void {
    this.act(this.studio.decide(this.id, this.decision, this.note));
  }

  protected resolve(): void {
    this.act(this.studio.resolve(this.id, this.resolution));
  }

  protected remove(): void {
    if (!confirm('Delete this report?')) return;
    this.studio.deleteTicket(this.id).subscribe({
      next: () => this.router.navigateByUrl('/studio/reports'),
      error: (e) => this.error.set(apiError(e)),
    });
  }
}
