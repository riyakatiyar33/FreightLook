import { HttpClient, HttpContext, HttpEvent } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { SKIP_ERROR_SNACKBAR } from '../interceptors/http-error.interceptor';
import { UploadResult } from '../models/upload-result';

/** Calls RateSheetsController (Backend). */
@Injectable({ providedIn: 'root' })
export class RateSheetService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/ratesheets`;

  /** File types the API accepts (see UploadService on the backend). */
  readonly allowedExtensions = ['.pdf', '.xlsx', '.xls', '.csv'] as const;
  readonly maxFileBytes = 10 * 1024 * 1024;

  /**
   * POST /api/ratesheets/upload (multipart, field "file").
   * Emits progress events while uploading and the UploadResult as the final HttpResponse.
   * Errors are shown inline by the upload page, so the global snackbar is skipped.
   */
  upload(file: File): Observable<HttpEvent<UploadResult>> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<UploadResult>(`${this.baseUrl}/upload`, body, {
      reportProgress: true,
      observe: 'events',
      context: new HttpContext().set(SKIP_ERROR_SNACKBAR, true),
    });
  }
}
