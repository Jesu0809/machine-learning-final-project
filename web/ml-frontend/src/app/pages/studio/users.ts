import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Role, User } from '../../models/studio.model';
import { AuthService } from '../../services/auth.service';
import { apiError, StudioService } from '../../services/studio.service';
import { StudioNav } from './studio-nav';

@Component({
  selector: 'app-studio-users',
  imports: [FormsModule, DatePipe, StudioNav],
  template: `
    <div class="container py-5">
      <app-studio-nav />
      <h1 class="h3 mb-1">Users and roles</h1>
      <p class="text-muted small">Playtesters play and report · designers review reports with the model and decide · admins manage users and
        monitoring. Nobody can change their own role, and the system always keeps one active admin.</p>
      @if (message()) { <div class="alert alert-success py-2">{{ message() }}</div> }
      @if (error()) { <div class="alert alert-danger py-2">{{ error() }}</div> }
      <div class="table-responsive">
        <table class="table table-sm align-middle">
          <thead><tr><th>#</th><th>User</th><th>Role</th><th>Active</th><th>Playtests</th><th>Created</th><th>Last sign-in</th></tr></thead>
          <tbody>
            @for (u of users(); track u.id) {
              <tr>
                <td>{{ u.id }}</td>
                <td>{{ u.username }} @if (u.id === auth.user()?.id) { <span class="badge text-bg-secondary">you</span> }</td>
                <td>
                  <select class="form-select form-select-sm w-auto" [attr.aria-label]="'Role of ' + u.username" [ngModel]="u.role"
                          [disabled]="u.id === auth.user()?.id" (ngModelChange)="update(u, { role: $event })">
                    @for (r of roles(); track r) { <option [value]="r">{{ r }}</option> }
                  </select>
                </td>
                <td>
                  <div class="form-check form-switch">
                    <input class="form-check-input" type="checkbox" [attr.aria-label]="'Active ' + u.username" [checked]="u.active"
                           [disabled]="u.id === auth.user()?.id" (change)="update(u, { active: !u.active })">
                  </div>
                </td>
                <td>{{ u.playtests }}</td>
                <td class="small">{{ u.created_at * 1000 | date: 'yyyy-MM-dd' }}</td>
                <td class="small">{{ u.last_login ? (u.last_login * 1000 | date: 'yyyy-MM-dd HH:mm') : '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
})
export class StudioUsers {
  private readonly studio = inject(StudioService);
  protected readonly auth = inject(AuthService);
  protected readonly users = signal<User[]>([]);
  protected readonly roles = signal<Role[]>([]);
  protected readonly error = signal('');
  protected readonly message = signal('');

  constructor() {
    this.load();
  }

  private load(): void {
    this.studio.users().subscribe({
      next: (r) => {
        this.users.set(r.users);
        this.roles.set(r.roles);
      },
      error: (e) => this.error.set(apiError(e)),
    });
  }

  protected update(user: User, change: { role?: Role; active?: boolean }): void {
    this.error.set('');
    this.studio.updateUser(user.id, change).subscribe({
      next: (r) => {
        this.message.set(`${r.user.username}: ${r.user.role}, ${r.user.active ? 'active' : 'disabled'}.`);
        this.load();
      },
      error: (e) => {
        this.error.set(apiError(e));
        this.load();
      },
    });
  }
}
