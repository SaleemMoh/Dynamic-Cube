import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export const DECISIONS_API = '/api/decisions/v1';

export interface EngineInfo {
  code: string;
  nameAr: string;
  nameEn: string;
}

export interface TraceStep {
  step: string;
  value?: unknown;
  [key: string]: unknown;
}

export interface DecisionResult {
  evaluationId: string;
  ruleSetCode: string;
  version: number;
  engine: string;
  output: Record<string, unknown>;
  trace: TraceStep[];
}

export interface EvaluationView {
  id: string;
  ruleSetCode: string;
  version: number;
  engine: string;
  subjectType: string;
  subjectId: string;
  subjectNumber: string | null;
  at: string;
  by: string;
  durationMs: number;
  input: Record<string, unknown>;
  output: Record<string, unknown>;
  trace: TraceStep[];
}

/** Rule-set engines, the try console and the evaluation log (internal host). */
@Injectable({ providedIn: 'root' })
export class DecisionsApi {
  private readonly http = inject(HttpClient);

  engines(): Observable<EngineInfo[]> {
    return this.http.get<EngineInfo[]>(`${DECISIONS_API}/engines`);
  }

  try(ruleSet: string, input: unknown, version: number | null): Observable<DecisionResult> {
    return this.http.post<DecisionResult>(`${DECISIONS_API}/rule-sets/${encodeURIComponent(ruleSet)}/try`, { input, version });
  }

  evaluations(filter: { subjectType?: string | null; subjectId?: string | null; ruleSet?: string | null; take?: number }): Observable<EvaluationView[]> {
    let params = new HttpParams().set('take', filter.take ?? 50);
    if (filter.subjectType) params = params.set('subjectType', filter.subjectType);
    if (filter.subjectId) params = params.set('subjectId', filter.subjectId);
    if (filter.ruleSet) params = params.set('ruleSet', filter.ruleSet);
    return this.http.get<EvaluationView[]>(`${DECISIONS_API}/evaluations`, { params });
  }
}
