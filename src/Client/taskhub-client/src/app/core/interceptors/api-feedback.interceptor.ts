import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, tap, throwError } from 'rxjs';
import { ApiFeedbackService } from '../services/api-feedback.service';
import { AuthService } from '../services/auth.service';
import { isApiRequest } from '../utils/api-request';
import { serverMessageKey } from '../utils/server-messages';

const FORBIDDEN = 403;
const TOO_MANY_REQUESTS = 429;
const MAX_DETAILS_LENGTH = 1000;

// Signing in and out have their own screens, and starting the 2FA setup saves nothing yet, so they do not report "saved".
const SILENT_REQUESTS = /\/auth\/(login|register|2fa\/verify|2fa\/setup|google\/login|sessions\/logout)(\?|$)/;

export const apiFeedbackInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isApiRequest(request.url)) {
    return next(request);
  }

  const feedback = inject(ApiFeedbackService);
  const authService = inject(AuthService);
  const translate = inject(TranslateService);

  feedback.pending.update((count) => count + 1);

  return next(request).pipe(
    tap((event) => {
      if (event instanceof HttpResponse && request.method !== 'GET' && !SILENT_REQUESTS.test(request.url)) {
        feedback.show('success', 'feedback.saved');
      }
    }),
    catchError((error) => {
      // Until the password is changed the server refuses everything else; the profile page already says so.
      const expected = error.status === FORBIDDEN && authService.currentUser()?.mustChangePassword === true;

      if (!expected) {
        feedback.show('error', errorKey(error.status), errorDetails(error.error?.errors, translate));
      }

      return throwError(() => error);
    }),
    finalize(() => feedback.pending.update((count) => Math.max(0, count - 1)))
  );
};

function errorKey(status: number): string {
  if (status === 0) {
    return 'feedback.offline';
  }

  return status === TOO_MANY_REQUESTS ? 'feedback.rateLimit' : 'feedback.failed';
}

function errorDetails(errors: unknown, translate: TranslateService): string | undefined {
  if (!Array.isArray(errors)) {
    return undefined;
  }

  return errors
    .filter((value): value is string => typeof value === 'string')
    .map((message) => {
      const key = serverMessageKey(message);

      return key ? translate.instant(key) : message;
    })
    .join(' ')
    .slice(0, MAX_DETAILS_LENGTH);
}
