import { Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Playtest } from '../../models/studio.model';
import { AuthService } from '../../services/auth.service';
import { apiError, StudioService } from '../../services/studio.service';
import { StudioNav } from './studio-nav';

const EMPTY: Partial<Playtest> = {
  night: 1, mode: 'Story', outcome: 'Caught', cause: '', room: '', seconds: 120, objectives_done: 0, objectives_total: 3,
  heard: 0, seen: 0, searches: 0, sense_uses: 0, sense_hits: 0, decisions: 0, notes: '',
};

@Component({
  selector: 'app-studio-playtests',
  imports: [FormsModule, RouterLink, DecimalPipe, StudioNav],
  templateUrl: './playtests.html',
})
export class StudioPlaytests {
  private readonly studio = inject(StudioService);
  protected readonly auth = inject(AuthService);
  protected readonly rows = signal<Playtest[]>([]);
  protected readonly error = signal('');
  protected readonly message = signal('');
  protected readonly editing = signal<Partial<Playtest> | null>(null);
  protected readonly outcomes = ['Caught', 'Escaped', 'TimeUp', 'Quit'];
  protected readonly nights = [1, 2, 3, 4, 5, 6, 7];
  protected mine = !this.auth.can('designer');
  protected night = '';
  protected outcome = '';

  constructor() {
    this.load();
  }

  protected load(): void {
    const filters: Record<string, string> = {};
    if (this.mine) filters['mine'] = '1';
    if (this.night) filters['night'] = this.night;
    if (this.outcome) filters['outcome'] = this.outcome;
    this.studio.playtests(filters).subscribe({
      next: (r) => this.rows.set(r.playtests),
      error: (e) => this.error.set(apiError(e)),
    });
  }

  protected canEdit(p: Playtest): boolean {
    return p.user_id === this.auth.user()?.id || this.auth.can('admin');
  }

  protected startNew(): void {
    this.message.set('');
    this.editing.set({ ...EMPTY });
  }

  protected edit(p: Playtest): void {
    this.message.set('');
    this.editing.set({ ...p });
  }

  protected save(): void {
    const draft = this.editing();
    if (!draft) return;
    this.error.set('');
    const call = draft.id ? this.studio.updatePlaytest(draft.id, draft) : this.studio.createPlaytest(draft);
    call.subscribe({
      next: () => {
        this.message.set(draft.id ? 'Playtest updated.' : 'Playtest recorded.');
        this.editing.set(null);
        this.load();
      },
      error: (e) => this.error.set(apiError(e)),
    });
  }

  protected remove(p: Playtest): void {
    if (!confirm(`Delete the playtest of night ${p.night} (${p.outcome})? Reports linked to it keep their text.`)) return;
    this.studio.deletePlaytest(p.id).subscribe({
      next: () => {
        this.message.set('Playtest deleted.');
        this.load();
      },
      error: (e) => this.error.set(apiError(e)),
    });
  }
}
