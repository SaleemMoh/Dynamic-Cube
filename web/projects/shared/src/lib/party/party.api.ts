import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ContextsResult, LinkEstablishmentRequest, PartyDetail, PartyKind, PartySummary, PersonLinks, ProfileRefreshResult } from './party.models';

export const PARTY_API = '/api/party/v1';

/** Party Registry: registry operations (internal host) and the applicant's acting contexts (external host). */
@Injectable({ providedIn: 'root' })
export class PartyApi {
  private readonly http = inject(HttpClient);

  /** `app`: only parties with a request for a service of that business app. */
  search(search: string | null, kind: PartyKind | null, skip = 0, take = 50, app: string | null = null): Observable<{ items: PartySummary[]; total: number }> {
    let params = new HttpParams().set('skip', skip).set('take', take);
    if (search) params = params.set('search', search);
    if (kind) params = params.set('kind', kind);
    if (app) params = params.set('app', app);
    return this.http.get<{ items: PartySummary[]; total: number }>(`${PARTY_API}/parties`, { params });
  }

  get(id: string): Observable<PartyDetail> {
    return this.http.get<PartyDetail>(`${PARTY_API}/parties/${id}`);
  }

  register(request: { kind: PartyKind; identifierType: string; identifier: string; nameAr: string; nameEn: string }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${PARTY_API}/parties`, request);
  }

  refreshProfile(id: string): Observable<ProfileRefreshResult[]> {
    return this.http.post<ProfileRefreshResult[]>(`${PARTY_API}/parties/${id}/profile/refresh`, {});
  }

  refreshContexts(id: string): Observable<ContextsResult> {
    return this.http.post<ContextsResult>(`${PARTY_API}/parties/${id}/contexts/refresh`, {});
  }

  addRelationship(id: string, request: { individualIdentifier: string; relationshipCode: string; services: string[]; validFrom: string | null; validTo: string | null }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${PARTY_API}/parties/${id}/relationships`, request);
  }

  endRelationship(id: string, relationshipId: string): Observable<void> {
    return this.http.post<void>(`${PARTY_API}/parties/${id}/relationships/${relationshipId}/end`, {});
  }

  /** The establishments a person (national ID or iqama number) may act for, before or after the first sign-in. */
  personLinks(number: string): Observable<PersonLinks> {
    return this.http.get<PersonLinks>(`${PARTY_API}/people/${number}/links`);
  }

  linkEstablishment(number: string, request: LinkEstablishmentRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${PARTY_API}/people/${number}/links`, request);
  }

  endPersonLink(number: string, relationshipId: string): Observable<void> {
    return this.http.post<void>(`${PARTY_API}/people/${number}/links/${relationshipId}/end`, {});
  }

  addSanction(id: string, request: { typeCode: string; reason: string; startsOn: string | null; endsOn: string | null }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${PARTY_API}/parties/${id}/sanctions`, request);
  }

  liftSanction(id: string, sanctionId: string, reason: string): Observable<void> {
    return this.http.post<void>(`${PARTY_API}/parties/${id}/sanctions/${sanctionId}/lift`, { reason });
  }

  contexts(refresh = false): Observable<ContextsResult> {
    return this.http.get<ContextsResult>(`${PARTY_API}/me/contexts`, { params: refresh ? { refresh: true } : {} });
  }
}
