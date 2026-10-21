import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Results, Runs } from '../models/results.model';

@Injectable({ providedIn: 'root' })
export class ResultsService {
  private readonly http = inject(HttpClient);

  getResults(): Observable<Results> {
    return this.http.get<Results>('/api/results');
  }

  getRuns(): Observable<Runs> {
    return this.http.get<Runs>('/api/runs');
  }

  getGameStatus(): Observable<{ available: boolean; version: string }> {
    return this.http.get<{ available: boolean; version: string }>('/api/game/status');
  }
}
