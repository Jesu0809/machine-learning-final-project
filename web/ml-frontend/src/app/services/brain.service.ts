import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BrainDecision, BrainState, ModelInfo } from '../models/brain.model';

@Injectable({ providedIn: 'root' })
export class BrainService {
  private readonly http = inject(HttpClient);

  getModelInfo(): Observable<ModelInfo> {
    return this.http.get<ModelInfo>('/api/model/info');
  }

  decide(state: BrainState): Observable<BrainDecision> {
    return this.http.post<BrainDecision>('/api/brain/decide', state);
  }
}
