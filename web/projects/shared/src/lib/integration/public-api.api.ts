import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { INTEGRATION_API } from './integration.api';

/** What a public endpoint does: read rows built in the query builder, or call a read-only system function (ADR-0042). */
export type ApiEndpointKind = 'QUERY' | 'FUNCTION';
export type ApiParameterLocation = 'PATH' | 'QUERY';
export type ApiParameterType = 'TEXT' | 'NUMBER' | 'DATE' | 'BOOLEAN';

/** A parameter of a public endpoint: a `{placeholder}` of its path or a query string value. */
export interface ApiEndpointParameter {
  name: string;
  location: ApiParameterLocation;
  type: ApiParameterType;
  isRequired: boolean;
  /** Personal data: written as *** in the call log. */
  isMasked: boolean;
  descriptionAr?: string | null;
  descriptionEn?: string | null;
  sample?: string | null;
}

/** A field of a read-data endpoint's rows: its name in the response and the column (alias.column) it reads. */
export interface ApiEndpointField {
  name: string;
  column: string;
  descriptionAr?: string | null;
  descriptionEn?: string | null;
}

/** What a system function's input gets: one of the endpoint's parameters, or a fixed value. */
export interface ApiFunctionBinding {
  input: string;
  parameter?: string | null;
  value?: string | null;
}

/** A public endpoint as stored (the change payload of integ.api-endpoint). */
export interface ApiEndpointDefinition {
  code?: string | null;
  nameAr: string;
  nameEn: string;
  isActive?: boolean;
  path: string;
  kind: ApiEndpointKind;
  descriptionAr?: string | null;
  descriptionEn?: string | null;
  parameters: ApiEndpointParameter[];
  query: unknown;
  fields: ApiEndpointField[];
  singleRow: boolean;
  pageSize: number;
  function: string | null;
  functionInputs: ApiFunctionBinding[];
  /** A client may name a registered person (X-Acting-User) whose roles and data then apply (ADR-0042). */
  actsForUser?: boolean;
  [key: string]: unknown;
}

export interface ApiFunctionInput {
  name: string;
  required: boolean;
  descriptionAr: string;
  descriptionEn: string;
}

/** A read-only system function an endpoint may call, with an example of what it returns. */
export interface ApiFunctionInfo {
  code: string;
  nameAr: string;
  nameEn: string;
  descriptionAr: string;
  descriptionEn: string;
  inputs: ApiFunctionInput[];
  sample: unknown;
}

/** Where partners call the API and get tokens, and whether sign-in clients are maintained from here. */
export interface PublicApiInfo {
  apiUrl: string;
  audience: string;
  tokenUrl: string | null;
  managed: boolean;
}

/** What an endpoint answered when tried, as a partner would get it. */
export interface TryEndpointResult {
  status: number;
  body: unknown;
  rows: number | null;
  durationMs: number;
  url: string;
}

/** An API client's sign-in client as it stands. */
export interface ApiClientSignIn {
  code: string;
  clientId: string;
  managed: boolean;
  exists: boolean;
  enabled: boolean;
  inSync: boolean;
  tokenUrl: string | null;
  audience: string;
  apiUrl: string;
}

/** Setting up sign-in or replacing the secret: the secret is shown this once and never kept. */
export interface ApiClientCredentials {
  signIn: ApiClientSignIn;
  secret: string | null;
}

export interface ApiCallSummary {
  id: string;
  clientCode: string;
  clientId: string;
  endpointCode: string | null;
  method: string;
  path: string;
  startedAt: string;
  durationMs: number;
  status: number;
  rows: number | null;
  error: string | null;
  clientIp: string | null;
  isTry: boolean;
  /** The registered person the client acted for, whose access applied (ADR-0042). */
  actingFor?: string | null;
}

export interface ApiCallQuery {
  client?: string | null;
  endpoint?: string | null;
  /** A status class (4 → 4xx) or an exact status. */
  status?: number | null;
  from?: string | null;
  to?: string | null;
  skip?: number;
  take?: number;
}

/** The public API's administration (internal host): functions, trying endpoints, clients' sign-in, the call log. */
@Injectable({ providedIn: 'root' })
export class PublicApiAdmin {
  private readonly http = inject(HttpClient);
  private readonly base = `${INTEGRATION_API}/public-api`;

  info(): Observable<PublicApiInfo> {
    return this.http.get<PublicApiInfo>(`${this.base}/info`);
  }

  functions(): Observable<ApiFunctionInfo[]> {
    return this.http.get<ApiFunctionInfo[]>(`${this.base}/functions`);
  }

  /** Runs an endpoint that is not saved yet as a partner would call it (limited to a client's companies when given). */
  tryEndpoint(
    endpoint: ApiEndpointDefinition,
    inputs: Record<string, string | null>,
    client: string | null,
    limit: number | null,
    offset: number | null,
    company: string | null = null,
    actingUser: string | null = null,
  ): Observable<TryEndpointResult> {
    return this.http.post<TryEndpointResult>(`${this.base}/endpoints/try`, {
      endpoint,
      inputs,
      client,
      limit,
      offset,
      company,
      actingUser,
    });
  }

  signIn(client: string): Observable<ApiClientSignIn> {
    return this.http.get<ApiClientSignIn>(
      `${this.base}/clients/${encodeURIComponent(client)}/sign-in`,
    );
  }

  setUpSignIn(client: string): Observable<ApiClientCredentials> {
    return this.http.post<ApiClientCredentials>(
      `${this.base}/clients/${encodeURIComponent(client)}/sign-in`,
      null,
    );
  }

  newSecret(client: string): Observable<ApiClientCredentials> {
    return this.http.post<ApiClientCredentials>(
      `${this.base}/clients/${encodeURIComponent(client)}/secret`,
      null,
    );
  }

  /** The OpenAPI document of the endpoints a client may call, to hand to the partner. */
  description(client: string): Observable<unknown> {
    return this.http.get<unknown>(
      `${this.base}/clients/${encodeURIComponent(client)}/openapi.json`,
    );
  }

  calls(query: ApiCallQuery): Observable<{ items: ApiCallSummary[]; total: number }> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '')
        params = params.set(key, String(value));
    }
    return this.http.get<{ items: ApiCallSummary[]; total: number }>(`${this.base}/calls`, {
      params,
    });
  }
}
