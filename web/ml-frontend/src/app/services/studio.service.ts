import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Dashboard, Inference, ModelCard, Monitoring, Playtest, Prediction, Role, Situation, Ticket, User,
} from '../models/studio.model';

const API = '/api/studio';

@Injectable({ providedIn: 'root' })
export class StudioService {
  private readonly http = inject(HttpClient);

  dashboard(simulated = false): Observable<Dashboard> {
    return this.http.get<Dashboard>(`${API}/dashboard`, { params: simulated ? { simulated: '1' } : {} });
  }

  model(): Observable<ModelCard> {
    return this.http.get<ModelCard>(`${API}/model`);
  }

  playtests(filters: Record<string, string> = {}): Observable<{ playtests: Playtest[] }> {
    return this.http.get<{ playtests: Playtest[] }>(`${API}/playtests`, { params: new HttpParams({ fromObject: filters }) });
  }

  playtest(id: number): Observable<{ playtest: Playtest; tickets: Partial<Ticket>[] }> {
    return this.http.get<{ playtest: Playtest; tickets: Partial<Ticket>[] }>(`${API}/playtests/${id}`);
  }

  createPlaytest(body: Partial<Playtest>): Observable<{ playtest: Playtest }> {
    return this.http.post<{ playtest: Playtest }>(`${API}/playtests`, body);
  }

  updatePlaytest(id: number, body: Partial<Playtest>): Observable<{ playtest: Playtest }> {
    return this.http.put<{ playtest: Playtest }>(`${API}/playtests/${id}`, body);
  }

  deletePlaytest(id: number): Observable<unknown> {
    return this.http.delete(`${API}/playtests/${id}`);
  }

  tickets(filters: Record<string, string> = {}): Observable<{ tickets: Ticket[] }> {
    return this.http.get<{ tickets: Ticket[] }>(`${API}/tickets`, { params: new HttpParams({ fromObject: filters }) });
  }

  ticket(id: number): Observable<{ ticket: Ticket }> {
    return this.http.get<{ ticket: Ticket }>(`${API}/tickets/${id}`);
  }

  createTicket(body: Record<string, unknown>): Observable<{ ticket: Ticket }> {
    return this.http.post<{ ticket: Ticket }>(`${API}/tickets`, body);
  }

  updateTicket(id: number, body: Record<string, unknown>): Observable<{ ticket: Ticket }> {
    return this.http.put<{ ticket: Ticket }>(`${API}/tickets/${id}`, body);
  }

  deleteTicket(id: number): Observable<unknown> {
    return this.http.delete(`${API}/tickets/${id}`);
  }

  review(id: number): Observable<{ ticket: Ticket }> {
    return this.http.post<{ ticket: Ticket }>(`${API}/tickets/${id}/review`, {});
  }

  decide(id: number, decision: 'expected' | 'bug', note: string): Observable<{ ticket: Ticket }> {
    return this.http.post<{ ticket: Ticket }>(`${API}/tickets/${id}/decision`, { decision, note });
  }

  resolve(id: number, note: string): Observable<{ ticket: Ticket }> {
    return this.http.post<{ ticket: Ticket }>(`${API}/tickets/${id}/resolve`, { note });
  }

  predict(situation: Situation, observed: number | null): Observable<Prediction> {
    return this.http.post<Prediction>(`${API}/predict`, { situation, observed_action: observed });
  }

  inferences(simulated = false): Observable<{ inferences: Inference[]; model_version: string; threshold: number }> {
    return this.http.get<{ inferences: Inference[]; model_version: string; threshold: number }>(`${API}/inferences`, {
      params: simulated ? { simulated: '1' } : {},
    });
  }

  users(): Observable<{ users: User[]; roles: Role[] }> {
    return this.http.get<{ users: User[]; roles: Role[] }>(`${API}/users`);
  }

  updateUser(id: number, body: { role?: Role; active?: boolean }): Observable<{ user: User }> {
    return this.http.put<{ user: User }>(`${API}/users/${id}`, body);
  }

  monitoring(simulated = false): Observable<Monitoring> {
    return this.http.get<Monitoring>(`${API}/monitoring`, { params: simulated ? { simulated: '1' } : {} });
  }

  simulate(): Observable<{ note: string }> {
    return this.http.post<{ note: string }>(`${API}/monitoring/simulate`, { seed: Date.now() % 1000 });
  }

  clearSimulated(): Observable<unknown> {
    return this.http.delete(`${API}/monitoring/simulate`);
  }
}

/** The message the API sent with an error, or a generic one. */
export function apiError(error: unknown, fallback = 'Something went wrong.'): string {
  const e = error as { error?: { error?: string }; status?: number };
  if (e?.status === 0) {
    return 'The server did not answer (it may be waking up: try again in a few seconds).';
  }
  return e?.error?.error ?? fallback;
}
