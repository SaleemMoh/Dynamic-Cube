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

/** What importing a classification guide gives: a matrix configuration, what the matrix does not hold, and notes. */
export interface MatrixImport {
  configuration: Record<string, unknown>;
  extras: Record<string, unknown>;
  warnings: string[];
}

/** A matrix tried on sample values without saving it. */
export interface MatrixSimulationResult {
  /** Problems in the configuration, by where they are (`cells[0].scale`); nothing is worked out while there are any. */
  errors: Record<string, string>;
  output: Record<string, unknown> | null;
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

  /** Converts a classification guide export (sectors, tracks, criteria, bands) to a scoring matrix. */
  importGuide(guide: unknown): Observable<MatrixImport> {
    return this.http.post<MatrixImport>(`${DECISIONS_API}/matrix/import`, guide);
  }

  /** Works out a matrix that is being designed, on sample values. */
  simulateMatrix(configuration: unknown, input: unknown): Observable<MatrixSimulationResult> {
    return this.http.post<MatrixSimulationResult>(`${DECISIONS_API}/matrix/simulate`, { configuration, input });
  }

  evaluations(filter: { subjectType?: string | null; subjectId?: string | null; ruleSet?: string | null; take?: number }): Observable<EvaluationView[]> {
    let params = new HttpParams().set('take', filter.take ?? 50);
    if (filter.subjectType) params = params.set('subjectType', filter.subjectType);
    if (filter.subjectId) params = params.set('subjectId', filter.subjectId);
    if (filter.ruleSet) params = params.set('ruleSet', filter.ruleSet);
    return this.http.get<EvaluationView[]>(`${DECISIONS_API}/evaluations`, { params });
  }
}
