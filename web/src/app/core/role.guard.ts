import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';
import { AuthService, Role } from './auth.service';

/**
 * UX-only route guard: not logged in → /login (with returnUrl); wrong role → no match (falls through to '**' → home).
 * No roles = any logged-in user. The API still checks every request.
 */
export function roleGuard(...roles: Role[]): CanMatchFn {
  return (_route, segments) => {
    const auth = inject(AuthService);
    const router = inject(Router);

    if (!auth.accessToken()) {
      const returnUrl = '/' + segments.map((s) => s.path).join('/');
      return router.createUrlTree(['/login'], { queryParams: { returnUrl } });
    }

    // Wrong role: false (not a redirect) so the router keeps trying other routes; the final '**' route
    // sends the user to their home page. A UrlTree here would hijack URLs meant for other routes.
    return roles.length === 0 || auth.hasRole(...roles);
  };
}
