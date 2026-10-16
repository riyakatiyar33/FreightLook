import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, throwError } from 'rxjs';
import { ApiError } from '../models/api-error';

/** Set on a request to handle errors in the page instead of showing the global snackbar. */
export const SKIP_ERROR_SNACKBAR = new HttpContextToken<boolean>(() => false);

interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

/**
 * Converts any HTTP failure into an {@link ApiError} (using the API's ProblemDetails body)
 * and shows it in an error snackbar.
 */
export const httpErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const snackBar = inject(MatSnackBar);

  return next(req).pipe(
    catchError((error: unknown) => {
      const apiError = toApiError(error);
      if (!req.context.get(SKIP_ERROR_SNACKBAR)) {
        snackBar.open(apiError.message, 'Dismiss', { duration: 6000, panelClass: 'fl-snack-error' });
      }
      return throwError(() => apiError);
    }),
  );
};

function toApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: -1, title: 'Error', message: 'An unexpected error occurred.' };
  }

  if (error.status === 0) {
    return {
      status: 0,
      title: 'Network error',
      message: 'Cannot reach the FreightLook API. Make sure the Backend is running.',
    };
  }

  const body = (typeof error.error === 'object' ? error.error : null) as ProblemDetails | null;
  const fieldErrors = body?.errors;
  const fieldMessage = fieldErrors ? Object.values(fieldErrors).flat().join(' ') : '';

  return {
    status: error.status,
    title: body?.title ?? error.statusText,
    message: fieldMessage || body?.detail || body?.title || `Request failed (${error.status}).`,
    fieldErrors,
  };
}
