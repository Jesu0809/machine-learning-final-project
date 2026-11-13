import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom, Observable, tap } from 'rxjs';
import { Role, User } from '../models/studio.model';

const RANK: Record<Role, number> = { playtester: 0, designer: 1, admin: 2 };

/** The signed-in user. The session lives in an HttpOnly cookie set by Flask; this only mirrors it. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  readonly user = signal<User | null>(null);
  readonly loaded = signal(false);
  readonly signedIn = computed(() => this.user() !== null);
  private pending: Promise<User | null> | null = null;

  /** Asks the server who is signed in (once; later calls reuse the answer). */
  load(force = false): Promise<User | null> {
    if (this.loaded() && !force) {
      return Promise.resolve(this.user());
    }
    if (!this.pending || force) {
      this.pending = firstValueFrom(this.http.get<{ user: User | null }>('/api/auth/me'))
        .then((r) => {
          this.user.set(r.user);
          this.loaded.set(true);
          return r.user;
        })
        .catch(() => {
          this.user.set(null);
          this.loaded.set(true);
          return null;
        });
    }
    return this.pending;
  }

  can(role: Role): boolean {
    const user = this.user();
    return user !== null && RANK[user.role] >= RANK[role];
  }

  login(username: string, password: string): Observable<{ user: User }> {
    return this.http.post<{ user: User }>('/api/auth/login', { username, password }).pipe(tap((r) => this.set(r.user)));
  }

  register(username: string, password: string): Observable<{ user: User }> {
    return this.http.post<{ user: User }>('/api/auth/register', { username, password }).pipe(tap((r) => this.set(r.user)));
  }

  logout(): Observable<unknown> {
    return this.http.post('/api/auth/logout', {}).pipe(tap(() => this.set(null)));
  }

  changePassword(current: string, next: string): Observable<unknown> {
    return this.http.post('/api/auth/password', { current, new: next });
  }

  private set(user: User | null): void {
    this.user.set(user);
    this.loaded.set(true);
    this.pending = Promise.resolve(user);
  }
}
