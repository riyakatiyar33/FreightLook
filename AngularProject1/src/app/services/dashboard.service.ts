import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { DashboardStats } from '../models/dashboard-stats';

type DashboardStatsResponse = Omit<DashboardStats, 'lastUploadAt'> & { lastUploadAt: string | null };

/** Calls DashboardController (Backend). */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/dashboard`;

  /** GET /api/dashboard/stats */
  getStats(): Observable<DashboardStats> {
    return this.http.get<DashboardStatsResponse>(`${this.baseUrl}/stats`).pipe(
      map(stats => ({ ...stats, lastUploadAt: parseUtc(stats.lastUploadAt) })),
    );
  }
}

/** SQL Server returns UTC timestamps without an offset; treat them as UTC. */
function parseUtc(value: string | null): Date | null {
  if (!value) {
    return null;
  }
  return new Date(/[zZ]|[+-]\d{2}:\d{2}$/.test(value) ? value : `${value}Z`);
}
