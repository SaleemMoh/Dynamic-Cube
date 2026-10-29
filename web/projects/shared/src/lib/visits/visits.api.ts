import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { VisitStatus } from '../services/service.models';

export const VISITS_API = '/api/visits/v1';

export const VISIT_STATUSES: VisitStatus[] = ['PENDING', 'SCHEDULED', 'IN_PROGRESS', 'SENT', 'COMPLETED', 'CANCELLED'];

export type VisitMode = 'FIELD' | 'EXTERNAL' | 'MANUAL';
export type OutcomeKind = 'PASS' | 'FAIL' | 'OTHER';
export type VisitFieldType = 'TEXT' | 'TEXTAREA' | 'NUMBER' | 'DATE' | 'SELECT' | 'CHECKBOX';

export interface VisitOutcome {
  code: string;
  nameAr: string;
  nameEn: string;
  kind: OutcomeKind;
}

export interface VisitFieldOption {
  code: string;
  labelAr: string;
  labelEn: string;
}

export interface VisitField {
  code: string;
  labelAr: string;
  labelEn: string;
  type: VisitFieldType;
  required: boolean;
  options: VisitFieldOption[];
}

/** What a visit of the type records: outcomes, form fields, photos, location and signature. */
export interface VisitTypeView {
  code: string;
  nameAr: string;
  nameEn: string;
  mode: VisitMode;
  descriptionAr: string | null;
  descriptionEn: string | null;
  outcomes: VisitOutcome[];
  fields: VisitField[];
  photosRequired: number;
  requireLocation: boolean;
  requireSignature: boolean;
  inspectorRole: string | null;
  polled: boolean;
}

export interface VisitSummary {
  id: string;
  number: string;
  typeCode: string;
  typeNameAr: string;
  typeNameEn: string;
  mode: VisitMode;
  status: VisitStatus;
  subjectType: string;
  subjectId: string;
  subjectNumber: string | null;
  titleAr: string;
  titleEn: string;
  partyName: string | null;
  siteAddress: string | null;
  inspector: string | null;
  inspectorName: string | null;
  requestedAt: string;
  scheduledAt: string | null;
  dueAt: string | null;
  completedAt: string | null;
  outcome: string | null;
  overdue: boolean;
}

export interface VisitPhoto {
  id: string;
  fileName: string;
  contentType: string;
  length: number;
  caption: string | null;
  at: string;
}

export interface VisitDetail {
  visit: VisitSummary;
  type: VisitTypeView;
  latitude: number | null;
  longitude: number | null;
  instructions: string | null;
  data: Record<string, unknown>;
  report: string | null;
  capturedLatitude: number | null;
  capturedLongitude: number | null;
  capturedAccuracy: number | null;
  hasSignature: boolean;
  signedBy: string | null;
  completedBy: string | null;
  externalReference: string | null;
  externalStatus: string | null;
  nextPollAt: string | null;
  cancelReason: string | null;
  photos: VisitPhoto[];
  events: { at: string; type: string; actor: string; comment: string | null }[];
}

export interface Inspector {
  userName: string;
  nameAr: string;
  nameEn: string;
  openVisits: number;
}

/** A report sent by the field app; `submissionId` makes resending it harmless. */
export interface FieldReport {
  submissionId: string;
  outcome: string;
  data: Record<string, unknown>;
  report: string | null;
  photos: { fileName: string; content: string; caption: string | null; takenAt: string | null }[];
  signature: string | null;
  signedBy: string | null;
  latitude: number | null;
  longitude: number | null;
  accuracy: number | null;
  completedAt: string | null;
}

/** Visits for staff (internal host) and for inspectors (field app). */
@Injectable({ providedIn: 'root' })
export class VisitsApi {
  private readonly http = inject(HttpClient);

  search(query: {
    search?: string | null;
    status?: VisitStatus | null;
    type?: string | null;
    inspector?: string | null;
    overdue?: boolean | null;
    skip?: number;
    take?: number;
  }): Observable<{ items: VisitSummary[]; total: number }> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '' && value !== false) params = params.set(key, String(value));
    }
    return this.http.get<{ items: VisitSummary[]; total: number }>(`${VISITS_API}/visits`, { params });
  }

  get(id: string): Observable<VisitDetail> {
    return this.http.get<VisitDetail>(`${VISITS_API}/visits/${id}`);
  }

  inspectors(type: string | null): Observable<Inspector[]> {
    return this.http.get<Inspector[]>(`${VISITS_API}/visits/inspectors`, { params: type ? { type } : {} });
  }

  schedule(id: string, inspector: string, scheduledAt: string, comment: string | null): Observable<VisitDetail> {
    return this.http.post<VisitDetail>(`${VISITS_API}/visits/${id}/schedule`, { inspector, scheduledAt, comment });
  }

  cancel(id: string, reason: string): Observable<VisitDetail> {
    return this.http.post<VisitDetail>(`${VISITS_API}/visits/${id}/cancel`, { reason });
  }

  record(id: string, outcome: string, data: Record<string, unknown>, report: string | null, comment: string | null): Observable<VisitDetail> {
    return this.http.post<VisitDetail>(`${VISITS_API}/visits/${id}/outcome`, { outcome, data, report, comment });
  }

  poll(id: string): Observable<VisitDetail> {
    return this.http.post<VisitDetail>(`${VISITS_API}/visits/${id}/poll`, {});
  }

  photoUrl(id: string, photoId: string): string {
    return `${VISITS_API}/visits/${id}/photos/${photoId}`;
  }

  signatureUrl(id: string): string {
    return `${VISITS_API}/visits/${id}/signature`;
  }

  /** Field app: the signed-in inspector's visits. */
  mine(): Observable<VisitDetail[]> {
    return this.http.get<VisitDetail[]>(`${VISITS_API}/field/visits`);
  }

  start(id: string): Observable<VisitDetail> {
    return this.http.post<VisitDetail>(`${VISITS_API}/field/visits/${id}/start`, {});
  }

  report(id: string, report: FieldReport): Observable<VisitDetail> {
    return this.http.post<VisitDetail>(`${VISITS_API}/field/visits/${id}/report`, report);
  }

  fieldPhotoUrl(id: string, photoId: string): string {
    return `${VISITS_API}/field/visits/${id}/photos/${photoId}`;
  }
}

/** Badge class of a visit status. */
export function visitStatusBadge(status: VisitStatus): string {
  switch (status) {
    case 'COMPLETED':
      return 'dc-badge dc-badge--success';
    case 'SCHEDULED':
    case 'IN_PROGRESS':
    case 'SENT':
      return 'dc-badge dc-badge--info';
    case 'PENDING':
      return 'dc-badge dc-badge--warning';
    case 'CANCELLED':
      return 'dc-badge dc-badge--danger';
  }
}
