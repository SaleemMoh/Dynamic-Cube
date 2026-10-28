import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CatalogEntry, PortalCatalog, PortalStatistics, RequestDetail, ServiceCard, RequestSummary, RequestView, ResolvedVersion, ServiceView, StatusCategory, VersionDefinition } from './service.models';

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

  create(serviceCode: string): Observable<RequestView> {
    return this.http.post<RequestView>(`${SERVICES_API}/requests`, { serviceCode });
  }

  request(id: string): Observable<RequestView> {
    return this.http.get<RequestView>(`${SERVICES_API}/requests/${id}`);
  }

  save(id: string, body: { data?: Record<string, unknown>; levelCode?: string | null; currentStep?: string | null }): Observable<RequestView> {
    return this.http.put<RequestView>(`${SERVICES_API}/requests/${id}`, body);
  }

  submit(id: string, declarationAccepted: boolean): Observable<RequestView> {
    return this.http.post<RequestView>(`${SERVICES_API}/requests/${id}/submit`, { declarationAccepted });
  }

  cancel(id: string): Observable<void> {
    return this.http.delete<void>(`${SERVICES_API}/requests/${id}`);
  }

  searchRequests(query: { service?: string | null; status?: StatusCategory | null; search?: string | null; skip?: number; take?: number }): Observable<{ items: RequestSummary[]; total: number }> {
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
