import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../services/auth.service';

/** The studio's tabs; each one appears only for the roles that may open it. */
@Component({
  selector: 'app-studio-nav',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-4">
      <ul class="nav nav-pills flex-wrap gap-1">
        <li class="nav-item"><a class="nav-link" routerLink="/studio" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Dashboard</a></li>
        <li class="nav-item"><a class="nav-link" routerLink="/studio/playtests" routerLinkActive="active">Playtests</a></li>
        <li class="nav-item"><a class="nav-link" routerLink="/studio/reports" routerLinkActive="active">Behaviour reports</a></li>
        @if (auth.can('designer')) {
          <li class="nav-item"><a class="nav-link" routerLink="/studio/model" routerLinkActive="active">Model &amp; history</a></li>
        }
        @if (auth.can('admin')) {
          <li class="nav-item"><a class="nav-link" routerLink="/studio/users" routerLinkActive="active">Users</a></li>
          <li class="nav-item"><a class="nav-link" routerLink="/studio/monitoring" routerLinkActive="active">Monitoring</a></li>
        }
      </ul>
      @if (auth.user(); as u) {
        <div class="small text-muted">
          <a routerLink="/studio/account">{{ u.username }}</a>
          <span class="badge text-bg-secondary ms-1">{{ u.role }}</span>
          <button type="button" class="btn btn-link btn-sm" (click)="logout()">Sign out</button>
        </div>
      }
    </div>
  `,
})
export class StudioNav {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected logout(): void {
    this.auth.logout().subscribe(() => this.router.navigateByUrl('/'));
  }
}
