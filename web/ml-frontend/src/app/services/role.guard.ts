import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Role } from '../models/studio.model';
import { AuthService } from './auth.service';

/**
 * Keeps the studio pages behind sign-in in the browser. The real protection is on the server:
 * every studio API call checks the session and the role again.
 */
export function roleGuard(role: Role = 'playtester'): CanActivateFn {
  return async (_route, state) => {
    const auth = inject(AuthService);
    const router = inject(Router);
    const user = await auth.load();
    if (!user) {
      return router.createUrlTree(['/login'], { queryParams: { next: state.url } });
    }
    return auth.can(role) ? true : router.createUrlTree(['/studio'], { queryParams: { denied: role } });
  };
}
