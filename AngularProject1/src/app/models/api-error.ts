/** Normalized error produced by the HTTP error interceptor from ProblemDetails responses. */
export interface ApiError {
  /** HTTP status; 0 when the API could not be reached. */
  status: number;
  title: string;
  /** Human-readable message suitable for display. */
  message: string;
  /** Field validation errors (ValidationProblemDetails.errors), if any. */
  fieldErrors?: Record<string, string[]>;
}
