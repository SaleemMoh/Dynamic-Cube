import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/** Adds the access token to same-origin API calls and starts sign-in when the API answers 401. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const isApi = request.url.startsWith('/api/');
  const token = isApi ? auth.accessToken() : null;
  const authorized = token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;

  return next(authorized).pipe(
    catchError((error: unknown) => {
      if (isApi && error instanceof HttpErrorResponse && error.status === 401) {
        void auth.login();
      }
      return throwError(() => error);
    }),
  );
};
