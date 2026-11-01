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

export type ParameterLocation = 'PATH' | 'QUERY' | 'HEADER' | 'BODY';
export type AuthType = 'NONE' | 'BASIC' | 'API_KEY' | 'OAUTH2_CLIENT_CREDENTIALS' | 'WS_SECURITY';

/** How an operation talks to the provider: JSON over HTTP, or SOAP (ADR-0044). */
export type OperationProtocol = 'REST' | 'SOAP';

/** The SOAP call of an operation: envelope version, SOAPAction, the body's operation element and its namespace. */
export interface SoapSettings {
  version: '1.1' | '1.2';
  action: string | null;
  operationName: string;
  namespace: string | null;
  /** Child elements in the operation's namespace (elementFormDefault="qualified"). */
  qualifiedElements: boolean;
}

/** An input of an operation: where it goes (path, query, header or a dot path in the JSON body) and how it is logged. */
export interface OperationParameterDef {
  name: string;
  location: ParameterLocation;
  /** The name at the provider (query, header) or the dot path in the body, when different from the name. */
  target?: string | null;
  isRequired: boolean;
  isMasked: boolean;
  sampleValue?: string | null;
}

/** An output: a name and the dot path of its value in the provider's response (e.g. Licenses[*].Commercial_NO). */
export interface OperationOutputDef {
  name: string;
  path: string;
}

export interface ResponseCodeDef {
  code: string;
  outcome: IntegrationOutcome;
  messageAr?: string | null;
  messageEn?: string | null;
}

/** An operation as stored (the change payload of integ.operation). */
export interface OperationDefinition {
  code?: string | null;
  nameAr: string;
  nameEn: string;
  isActive?: boolean;
  connectionCode: string | null;
  envelopeProfileCode: string | null;
  descriptionAr?: string | null;
  descriptionEn?: string | null;
  /** REST when absent. */
  protocol?: OperationProtocol;
  soap?: SoapSettings | null;
  method: string;
  pathTemplate: string;
  parameters: OperationParameterDef[];
  outputs: OperationOutputDef[];
  responseCodes: ResponseCodeDef[];
  maskFields: string[];
  cacheSeconds: number;
  isIdempotent?: boolean | null;
  [key: string]: unknown;
}

/** A security scheme of an API description and the connection settings it suggests. */
export interface OpenApiAuthSuggestion {
  name: string;
  authType: AuthType;
  tokenUrl: string | null;
  apiKeyHeader: string | null;
  note: string | null;
}

/** A field of an operation's success response, as an output it may become. */
export interface ImportedField {
  name: string;
  path: string;
  type: string;
}

export interface ImportedOperation {
  code: string;
  method: string;
  pathTemplate: string;
  name: string;
  description: string | null;
  tag: string | null;
  parameters: OperationParameterDef[];
  fields: ImportedField[];
  warnings: string[];
  /** SOAP operations read from a WSDL carry their SOAP call. */
  protocol?: OperationProtocol;
  soap?: SoapSettings | null;
}

/** What an OpenAPI / Swagger description (ADR-0041) or a WSDL (ADR-0044) holds, ready to review. */
export interface OpenApiReadResult {
  title: string;
  version: string | null;
  description: string | null;
  suggestedProviderCode: string;
  servers: string[];
  authentication: OpenApiAuthSuggestion[];
  operations: ImportedOperation[];
  warnings: string[];
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

  /** Runs an operation that is not saved yet on its published connection (the operation designer's "Try it"). */
  tryOperation(operation: OperationDefinition, inputs: Record<string, string | null>): Observable<TestOperationResult> {
    return this.http.post<TestOperationResult>(`${INTEGRATION_API}/operations/try`, { operation, inputs });
  }

  /** Reads an OpenAPI 3.x or Swagger 2.0 description (JSON or YAML); nothing is saved. */
  readOpenApi(text: string): Observable<OpenApiReadResult> {
    return this.http.post<OpenApiReadResult>(`${INTEGRATION_API}/openapi/read`, { text });
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
