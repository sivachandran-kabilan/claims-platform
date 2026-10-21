import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';
import { NoticeService } from './notice.service';

/**
 * Adds the bearer token to API calls. 401 → clear the session and go to login (except the login call
 * itself, whose 401 is "invalid credentials"). 403 → a clear message; the API is the enforcement point.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const notices = inject(NoticeService);
  const router = inject(Router);

  const token = auth.accessToken();
  const outgoing =
    token && request.url.startsWith(environment.apiBaseUrl)
      ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : request;

  return next(outgoing).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        if (error.status === 401 && !request.url.endsWith('/auth/login')) {
          notices.error('Your session has ended. Please log in again.');
          auth.logout(router.url);
        } else if (error.status === 403) {
          notices.error('You do not have permission to do that.');
        }
      }
      return throwError(() => error);
    }),
  );
};
