import { Component, inject, signal } from '@angular/core';
import { DecimalPipe, PercentPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Dashboard } from '../../models/studio.model';
import { AuthService } from '../../services/auth.service';
import { apiError, StudioService } from '../../services/studio.service';
import { StudioNav } from './studio-nav';

@Component({
  selector: 'app-studio-dashboard',
  imports: [RouterLink, PercentPipe, DecimalPipe, StudioNav],
  templateUrl: './dashboard.html',
})
export class StudioDashboard {
  private readonly studio = inject(StudioService);
  protected readonly auth = inject(AuthService);
  protected readonly data = signal<Dashboard | null>(null);
  protected readonly error = signal('');
  protected readonly denied = inject(ActivatedRoute).snapshot.queryParamMap.get('denied');
  protected simulated = false;

  constructor() {
    this.load();
  }

  protected load(): void {
    this.studio.dashboard(this.simulated).subscribe({
      next: (d) => this.data.set(d),
      error: (e) => this.error.set(apiError(e)),
    });
  }

  protected toggleSimulated(): void {
    this.simulated = !this.simulated;
    this.load();
  }

  protected maxPlays(): number {
    return Math.max(1, ...(this.data()?.studio?.by_night ?? []).map((n) => n.plays));
  }
}
