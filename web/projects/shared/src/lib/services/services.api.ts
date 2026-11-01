import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CatalogEntry,
  FollowUpService,
  NafathChallenge,
  NafathCheck,
  PortalCatalog,
  PortalStatistics,
  RequestDetail,
  RequestFileInfo,
  ServiceCard,
  RequestSummary,
  RequestView,
  ResolvedVersion,
  ServiceView,
  StatusCategory,
  VersionDefinition,
} from './service.models';
import { DecisionStepInfo, LiveResultView } from './panel/panel.models';

export const SERVICES_API = '/api/services/v1';

/** Services: the applicant's catalog and requests (external host) and the employees' request views (internal host). */
@Injectable({ providedIn: 'root' })
export class ServicesApi {
  private readonly http = inject(HttpClient);

  /** The public catalog: categories and listed services (no sign-in needed). */
  portalCatalog(): Observable<PortalCatalog> {
    return this.http.get<PortalCatalog>(`${SERVICES_API}/portal/catalog`);
  }

  serviceCard(code: string): Observable<ServiceCard> {
    return this.http.get<ServiceCard>(`${SERVICES_API}/portal/services/${encodeURIComponent(code)}`);
  }

  portalStatistics(): Observable<PortalStatistics> {
    return this.http.get<PortalStatistics>(`${SERVICES_API}/portal/statistics`);
  }

  catalog(): Observable<CatalogEntry[]> {
    return this.http.get<CatalogEntry[]>(`${SERVICES_API}/catalog`);
  }

  service(code: string): Observable<ServiceView> {
    return this.http.get<ServiceView>(`${SERVICES_API}/services/${encodeURIComponent(code)}`);
  }

  myRequests(status?: StatusCategory): Observable<RequestSummary[]> {
    return this.http.get<RequestSummary[]>(`${SERVICES_API}/requests`, { params: status ? { status } : {} });
  }

  /** Starts a draft; a follow-up service needs the document it is for. */
  create(serviceCode: string, documentId?: string): Observable<RequestView> {
    return this.http.post<RequestView>(`${SERVICES_API}/requests`, { serviceCode, documentId: documentId ?? null });
  }

  /** The follow-up services of one of the acting party's documents, available or not. */
  followUps(documentId: string): Observable<FollowUpService[]> {
    return this.http.get<FollowUpService[]>(`${SERVICES_API}/documents/${documentId}/services`);
  }

  request(id: string): Observable<RequestView> {
    return this.http.get<RequestView>(`${SERVICES_API}/requests/${id}`);
  }

  /** Saves a draft's answers; `validateStep` reports that step's problems; `keepalive` lets the save finish when the page closes. */
  save(
    id: string,
    body: { data?: Record<string, unknown>; levelCode?: string | null; currentStep?: string | null; validateStep?: string | null },
    options: { keepalive?: boolean } = {},
  ): Observable<RequestView> {
    return this.http.put<RequestView>(`${SERVICES_API}/requests/${id}`, body, options.keepalive ? { keepalive: true } : {});
  }

  /** Submits a draft; `nafathCheckId` is the Nafath confirmation of versions that ask for one (ADR-0043). */
  /** @param bundle The optional bundled services the applicant ticked (3f); the required ones are always requested. */
  submit(id: string, declarationAccepted: boolean, nafathCheckId: string | null = null, bundle: string[] | null = null): Observable<RequestView> {
    return this.http.post<RequestView>(`${SERVICES_API}/requests/${id}/submit`, { declarationAccepted, nafathCheckId, bundle });
  }

  /** Sends the applicant's Nafath app a request to confirm the draft: the number to choose there and until when. */
  startNafath(id: string): Observable<NafathChallenge> {
    return this.http.post<NafathChallenge>(`${SERVICES_API}/requests/${id}/nafath`, {});
  }

  /** Where the applicant's Nafath confirmation stands. */
  nafathStatus(id: string, checkId: string): Observable<NafathCheck> {
    return this.http.get<NafathCheck>(`${SERVICES_API}/requests/${id}/nafath/${checkId}`);
  }

  /** Uploads a file for a file question of a draft (or a request returned for correction); it is added to the answer. */
  uploadFile(requestId: string, question: string, file: File): Observable<RequestFileInfo> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<RequestFileInfo>(`${SERVICES_API}/requests/${requestId}/files`, body, { params: { question } });
  }

  deleteFile(requestId: string, fileId: string): Observable<void> {
    return this.http.delete<void>(`${SERVICES_API}/requests/${requestId}/files/${fileId}`);
  }

  /** A file of a request: the applicant's own (portal) or, for employees, of a submitted request. */
  downloadFile(requestId: string, fileId: string, staff = false): Observable<Blob> {
    return this.http.get(`${SERVICES_API}${staff ? '/admin' : ''}/requests/${requestId}/files/${fileId}`, { responseType: 'blob' });
  }

  /** The live result of a draft (or a request returned for correction) on the answers on screen; null: the saved answers. */
  liveResult(id: string, data: Record<string, unknown> | null, levelCode: string | null): Observable<LiveResultView> {
    return this.http.post<LiveResultView>(`${SERVICES_API}/requests/${id}/live-result`, { data, levelCode });
  }

  /** The live result of a service's decision step on any answers (the builder's preview). */
  previewLiveResult(service: string, step: string, data: Record<string, unknown> | null, levelCode: string | null): Observable<LiveResultView> {
    return this.http.post<LiveResultView>(`${SERVICES_API}/admin/live-result`, { service, step, data, levelCode });
  }

  /** The decision steps of the service's workflow (its latest published version). */
  decisionSteps(serviceCode: string): Observable<DecisionStepInfo[]> {
    return this.http.get<DecisionStepInfo[]>(`${SERVICES_API}/admin/services/${encodeURIComponent(serviceCode)}/decision-steps`);
  }

  cancel(id: string): Observable<void> {
    return this.http.delete<void>(`${SERVICES_API}/requests/${id}`);
  }

  /** `app`: only requests for the services of that business app. */
  searchRequests(query: {
    service?: string | null;
    status?: StatusCategory | null;
    search?: string | null;
    app?: string | null;
    skip?: number;
    take?: number;
  }): Observable<{ items: RequestSummary[]; total: number }> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '') params = params.set(key, String(value));
    }
    return this.http.get<{ items: RequestSummary[]; total: number }>(`${SERVICES_API}/admin/requests`, { params });
  }

  requestDetail(id: string): Observable<RequestDetail> {
    return this.http.get<RequestDetail>(`${SERVICES_API}/admin/requests/${id}`);
  }

  version(serviceCode: string, number: number): Observable<ResolvedVersion> {
    return this.http.get<ResolvedVersion>(`${SERVICES_API}/admin/versions/${encodeURIComponent(serviceCode)}/${number}`);
  }

  preview(serviceCode: string, definition: VersionDefinition): Observable<ResolvedVersion> {
    return this.http.post<ResolvedVersion>(`${SERVICES_API}/admin/versions/preview`, { serviceCode, definition });
  }
}
