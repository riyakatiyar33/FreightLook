/** Matches Models.Dtos.DashboardStatsDto. */
export interface DashboardStats {
  airlineCount: number;
  routeCount: number;
  rateCount: number;
  lastUploadFileName: string | null;
  /** Parsed to a Date by DashboardService (the API sends UTC without an offset). */
  lastUploadAt: Date | null;
  lastUploadSavedCount: number | null;
}
