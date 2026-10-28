import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export const INTEGRATION_API = '/api/integration/v1';

export type IntegrationOutcome = 'SUCCESS' | 'NOT_FOUND' | 'BUSINESS_ERROR' | 'TECHNICAL_ERROR';

export interface IntegrationTrace {
  method: string;
  url: string;
  requestBody: string | null;
  httpStatus: number | null;
  responseBody: string | null;
  attempts: number;
  durationMs: number;
  error: string | null;
}

export interface TestOperationResult {
  outcome: IntegrationOutcome;
  code: string | null;
  messageAr: string | null;
  messageEn: string | null;
  outputs: Record<string, unknown>;
  data: unknown;
  callId: string;
  trace: IntegrationTrace;
}

export interface CallLogSummary {
  id: string;
  operationCode: string;
  connectionCode: string;
  startedAt: string;
  durationMs: number;
  outcome: IntegrationOutcome;
  httpStatus: number | null;
  code: string | null;
  fromCache: boolean;
  userId: string;
  appCode: string;
}

export interface CallLogEntry extends CallLogSummary {
  correlationId: string | null;
  message: string | null;
  attempts: number;
  method: string;
  url: string;
  requestBody: string | null;
  responseBody: string | null;
  error: string | null;
}

export interface CallLogQuery {
  operation?: string | null;
  outcome?: IntegrationOutcome | null;
  from?: string | null;
  to?: string | null;
  skip?: number;
  take?: number;
}

/** Integration Manager test console and call log (internal host). Configuration goes through Apps Control change sets. */
@Injectable({ providedIn: 'root' })
export class IntegrationApi {
  private readonly http = inject(HttpClient);

  test(operationCode: string, inputs: Record<string, string | null>): Observable<TestOperationResult> {
    return this.http.post<TestOperationResult>(`${INTEGRATION_API}/operations/${encodeURIComponent(operationCode)}/test`, { inputs });
  }

  callLogs(query: CallLogQuery): Observable<{ items: CallLogSummary[]; total: number }> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<{ items: CallLogSummary[]; total: number }>(`${INTEGRATION_API}/call-logs`, { params });
  }

  callLog(id: string): Observable<CallLogEntry> {
    return this.http.get<CallLogEntry>(`${INTEGRATION_API}/call-logs/${id}`);
  }
}
