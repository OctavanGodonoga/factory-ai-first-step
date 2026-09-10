import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

export interface MapPointDetails {
  id: string;
  name: string;
  description: string | null;
  longitude: number;
  latitude: number;
}

@Injectable({ providedIn: 'root' })
export class MapPointsService {
  // No environment.ts/fileReplacements setup exists yet in this project (angular.json has no
  // configurations for it) — hardcoded here consistent with `map.ts`. Matches the API's
  // local dev port from launchSettings.json (http profile), paired with the `ng serve` origin
  // (4200) already whitelisted in appsettings.Development.json's Cors:AllowedOrigins.
  private readonly apiBaseUrl = 'http://localhost:5205';

  private readonly http = inject(HttpClient);

  getById(id: string): Observable<MapPointDetails> {
    console.debug('[MapPointsService] fetching point details', { id });
    return this.http.get<MapPointDetails>(`${this.apiBaseUrl}/api/mappoints/${id}`);
  }
}
