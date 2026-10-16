/** Matches Models.Responses.UploadResultResponse. */
export interface UploadResult {
  fileName: string;
  parsed: number;
  saved: number;
  rejected: number;
  /** Reasons for rejected rows. */
  errors: string[];
  /** Non-fatal notes (call-carrier rows, surcharges in another currency, ...). */
  warnings: string[];
  /** Saved rates, e.g. "QR DEL-DOH DGRCAO". */
  savedRoutes: string[];
}
