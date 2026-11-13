import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { apiError } from '../../services/studio.service';
import { StudioNav } from './studio-nav';

@Component({
  selector: 'app-studio-account',
  imports: [FormsModule, RouterLink, StudioNav],
  template: `
    <div class="container py-5" style="max-width: 720px">
      <app-studio-nav />
      @if (auth.user(); as u) {
        <h1 class="h3">My account</h1>
        <p class="mb-1">Username: <strong>{{ u.username }}</strong> · role: <strong>{{ u.role }}</strong></p>
        <p class="small text-muted">Your game save is linked to this account: open the game from
          <a routerLink="/play" [queryParams]="{ p: u.player_id }">this link</a> and every night you finish appears in your playtests.</p>
      }
      <form class="card mt-4" (ngSubmit)="change()"><div class="card-body">
        <h2 class="h6">Change the password</h2>
        @if (message()) { <div class="alert alert-success py-2">{{ message() }}</div> }
        @if (error()) { <div class="alert alert-danger py-2">{{ error() }}</div> }
        <label class="form-label small" for="cur">Current password</label>
        <input id="cur" name="cur" type="password" class="form-control mb-2" autocomplete="current-password" required [(ngModel)]="current">
        <label class="form-label small" for="new">New password (8+ characters, a letter and a number)</label>
        <input id="new" name="new" type="password" class="form-control mb-3" autocomplete="new-password" required [(ngModel)]="next">
        <button type="submit" class="btn btn-primary btn-sm">Change</button>
      </div></form>
    </div>
  `,
})
export class StudioAccount {
  protected readonly auth = inject(AuthService);
  protected readonly message = signal('');
  protected readonly error = signal('');
  protected current = '';
  protected next = '';

  protected change(): void {
    this.message.set('');
    this.error.set('');
    this.auth.changePassword(this.current, this.next).subscribe({
      next: () => {
        this.message.set('Password changed.');
        this.current = '';
        this.next = '';
      },
      error: (e) => this.error.set(apiError(e)),
    });
  }
}
