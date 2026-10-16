import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { RateSearchQuery, RateSearchResponse } from '../models/rate';

/** Calls RatesController (Backend). Never cached, so newly uploaded rates show up immediately. */
@Injectable({ providedIn: 'root' })
export class RateService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/rates`;

  /** GET /api/rates/search?origin=&destination=&weight= */
  search(query: RateSearchQuery): Observable<RateSearchResponse> {
    let params = new HttpParams().set('origin', query.origin).set('destination', query.destination);
    if (query.weight != null) {
      params = params.set('weight', query.weight);
    }
    return this.http.get<RateSearchResponse>(`${this.baseUrl}/search`, { params });
  }
}
