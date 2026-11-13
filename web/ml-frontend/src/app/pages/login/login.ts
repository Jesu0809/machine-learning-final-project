import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { apiError } from '../../services/studio.service';

/** Sign in (/login) and create an account (/register): the same form, two modes. */
@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink],
  templateUrl: './login.html',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly mode: 'login' | 'register' = this.route.snapshot.data['mode'] ?? 'login';
  protected username = '';
  protected password = '';
  protected confirm = '';
  protected readonly error = signal('');
  protected readonly busy = signal(false);

  protected submit(): void {
    this.error.set('');
    if (this.mode === 'register' && this.password !== this.confirm) {
      this.error.set('The two passwords are different.');
      return;
    }
    this.busy.set(true);
    const call = this.mode === 'login' ? this.auth.login(this.username, this.password) : this.auth.register(this.username, this.password);
    call.subscribe({
      next: () => {
        const next = this.route.snapshot.queryParamMap.get('next');
        this.router.navigateByUrl(next && next.startsWith('/') ? next : '/studio');
      },
      error: (e) => {
        this.error.set(apiError(e));
        this.busy.set(false);
      },
    });
  }
}
