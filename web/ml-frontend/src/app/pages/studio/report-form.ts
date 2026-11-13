import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ModelCard, Playtest, Situation, TicketKind } from '../../models/studio.model';
import { apiError, StudioService } from '../../services/studio.service';
import { actionsFor, emptySituation, SituationForm } from './situation-form';
import { StudioNav } from './studio-nav';

/** New report (/studio/reports/new) and edit while open (/studio/reports/:id/edit). */
@Component({
  selector: 'app-studio-report-form',
  imports: [FormsModule, RouterLink, SituationForm, StudioNav],
  template: `
    <div class="container py-5">
      <app-studio-nav />
      <h1 class="h3 mb-1">{{ id ? 'Edit report #' + id : 'Report what happened' }}</h1>
      <p class="text-muted small mb-4">For a <strong>behaviour</strong> report, describe what Pockets knew and what he did. A designer
        will ask the trained network whether it would do the same: that tells a learned decision apart from a bug.</p>
      @if (error()) { <div class="alert alert-danger py-2">{{ error() }}</div> }

      @if (card(); as m) {
        <form (ngSubmit)="save()">
          <div class="card mb-3"><div class="card-body">
            <div class="row g-3">
              <div class="col-md-4">
                <label class="form-label small" for="kind">Kind</label>
                <select id="kind" name="kind" class="form-select" [(ngModel)]="kind">
                  <option value="behaviour">Pockets' behaviour (checked by the model)</option>
                  <option value="bug">Bug (anything else broken)</option>
                  <option value="feedback">Feedback or suggestion</option>
                </select>
              </div>
              <div class="col-md-5">
                <label class="form-label small" for="title">Title</label>
                <input id="title" name="title" class="form-control" required minlength="5" maxlength="120" [(ngModel)]="title"
                       placeholder="He walked past my noise">
              </div>
              <div class="col-6 col-md-1">
                <label class="form-label small" for="night">Night</label>
                <select id="night" name="night" class="form-select" [(ngModel)]="night">
                  <option [ngValue]="null">—</option>
                  @for (n of [1,2,3,4,5,6,7]; track n) { <option [ngValue]="n">{{ n }}</option> }
                </select>
              </div>
              <div class="col-6 col-md-2">
                <label class="form-label small" for="playtest">Playtest</label>
                <select id="playtest" name="playtest" class="form-select" [(ngModel)]="playtestId">
                  <option [ngValue]="null">none</option>
                  @for (p of playtests(); track p.id) { <option [ngValue]="p.id">#{{ p.id }} night {{ p.night }} {{ p.outcome }}</option> }
                </select>
              </div>
              <div class="col-12">
                <label class="form-label small" for="desc">What happened</label>
                <textarea id="desc" name="desc" class="form-control" rows="3" required minlength="10" maxlength="2000" [(ngModel)]="description"></textarea>
              </div>
            </div>
          </div></div>

          @if (kind === 'behaviour') {
            <div class="card mb-3"><div class="card-body">
              <h2 class="h6 mb-3">The situation (what Pockets knew)</h2>
              <app-situation-form [situation]="situation" [card]="m" (changed)="refresh()" />
              <hr>
              <label class="form-label small" for="observed">What Pockets did next</label>
              <select id="observed" name="observed" class="form-select" required [(ngModel)]="observed">
                <option [ngValue]="null" disabled>choose…</option>
                @for (a of actions(); track a.index) {
                  <option [ngValue]="a.index">{{ a.name }}{{ a.legal ? '' : '  (not possible from this room)' }}</option>
                }
              </select>
              <p class="small text-muted mt-2 mb-0">If he did something the brain cannot even choose from here (for example going to a room that is
                not connected), pick it anyway: the review will flag it as impossible for the brain.</p>
            </div></div>
          }

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="busy()">{{ id ? 'Save changes' : 'Send report' }}</button>
            <a class="btn btn-outline-secondary" [routerLink]="id ? ['/studio/reports', id] : '/studio/reports'">Cancel</a>
          </div>
        </form>
      }
    </div>
  `,
})
export class StudioReportForm {
  private readonly studio = inject(StudioService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly id = Number(this.route.snapshot.paramMap.get('id')) || null;
  protected readonly card = signal<ModelCard | null>(null);
  protected readonly playtests = signal<Playtest[]>([]);
  protected readonly actions = signal<{ index: number; name: string; legal: boolean }[]>([]);
  protected readonly error = signal('');
  protected readonly busy = signal(false);
  protected kind: TicketKind = 'behaviour';
  protected title = '';
  protected description = '';
  protected night: number | null = Number(this.route.snapshot.queryParamMap.get('night')) || null;
  protected playtestId: number | null = Number(this.route.snapshot.queryParamMap.get('playtest')) || null;
  protected situation: Situation = emptySituation();
  protected observed: number | null = null;

  constructor() {
    this.studio.model().subscribe({
      next: (m) => {
        this.card.set(m);
        this.refresh();
      },
      error: (e) => this.error.set(apiError(e)),
    });
    this.studio.playtests({ mine: '1' }).subscribe({ next: (r) => this.playtests.set(r.playtests) });
    if (this.id) {
      this.studio.ticket(this.id).subscribe({
        next: ({ ticket }) => {
          this.kind = ticket.kind;
          this.title = ticket.title;
          this.description = ticket.description;
          this.night = ticket.night;
          this.playtestId = ticket.playtest_id;
          if (ticket.situation) this.situation = { ...emptySituation(), ...ticket.situation };
          this.observed = ticket.observed_action;
          this.refresh();
        },
        error: (e) => this.error.set(apiError(e)),
      });
    }
  }

  protected refresh(): void {
    const m = this.card();
    if (m) this.actions.set(actionsFor(m, this.situation.freddy_room));
  }

  protected save(): void {
    this.error.set('');
    if (this.kind === 'behaviour' && this.observed === null) {
      this.error.set('Choose what Pockets did next.');
      return;
    }
    const body: Record<string, unknown> = {
      kind: this.kind, title: this.title, description: this.description, night: this.night, playtest_id: this.playtestId,
    };
    if (this.kind === 'behaviour') {
      body['situation'] = this.situation;
      body['observed_action'] = this.observed;
    }
    this.busy.set(true);
    const call = this.id ? this.studio.updateTicket(this.id, body) : this.studio.createTicket(body);
    call.subscribe({
      next: ({ ticket }) => this.router.navigate(['/studio/reports', ticket.id]),
      error: (e) => {
        this.error.set(apiError(e));
        this.busy.set(false);
      },
    });
  }
}
