import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Airport } from '../models/airport';

/** Calls AirportsController (Backend). */
@Injectable({ providedIn: 'root' })
export class AirportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/airports`;

  /** GET /api/airports — all airports ordered by IATA code. */
  getAll(): Observable<Airport[]> {
    return this.http.get<Airport[]>(this.baseUrl);
  }
}
