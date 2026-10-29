import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { DocumentDetail, DocumentPage, DocumentQuery, DocumentSummary, VerifyView } from './documents.models';

export const DOCUMENTS_API = '/api/documents/v1';

/** Issued documents: the registry (internal host), the applicant's documents and public verification (external host). */
@Injectable({ providedIn: 'root' })
export class DocumentsApi {
  private readonly http = inject(HttpClient);

  search(query: DocumentQuery): Observable<DocumentPage> {
    let params = new HttpParams().set('skip', query.skip ?? 0).set('take', query.take ?? 50);
    for (const key of ['search', 'type', 'status', 'holder', 'app'] as const) {
      const value = query[key];
      if (value) params = params.set(key, value);
    }
    return this.http.get<DocumentPage>(`${DOCUMENTS_API}/documents`, { params });
  }

  get(id: string): Observable<DocumentDetail> {
    return this.http.get<DocumentDetail>(`${DOCUMENTS_API}/documents/${id}`);
  }

  bySource(sourceType: string, sourceId: string): Observable<DocumentSummary[]> {
    return this.http.get<DocumentSummary[]>(`${DOCUMENTS_API}/documents/by-source/${sourceType}/${sourceId}`);
  }

  pdf(id: string, version: number): Observable<Blob> {
    return this.http.get(`${DOCUMENTS_API}/documents/${id}/versions/${version}/pdf`, { responseType: 'blob' });
  }

  html(id: string, version: number): Observable<string> {
    return this.http.get(`${DOCUMENTS_API}/documents/${id}/versions/${version}/html`, { responseType: 'text' });
  }

  changeStatus(id: string, action: 'suspend' | 'reinstate' | 'cancel', comment: string): Observable<DocumentDetail> {
    return this.http.post<DocumentDetail>(`${DOCUMENTS_API}/documents/${id}/${action}`, { comment });
  }

  /** A print template on sample data: HTML (default) or PDF. */
  preview(request: { html: string; pageSize: string | null; landscape: boolean; data?: unknown; lines?: unknown; format?: 'html' | 'pdf' }): Observable<Blob> {
    return this.http.post(`${DOCUMENTS_API}/templates/preview`, request, { responseType: 'blob' });
  }

  // Portal (acting party)
  mine(): Observable<DocumentPage> {
    return this.http.get<DocumentPage>(`${DOCUMENTS_API}/my`);
  }

  myDocument(id: string): Observable<DocumentDetail> {
    return this.http.get<DocumentDetail>(`${DOCUMENTS_API}/my/${id}`);
  }

  myBySource(sourceType: string, sourceId: string): Observable<DocumentSummary[]> {
    return this.http.get<DocumentSummary[]>(`${DOCUMENTS_API}/my/by-source/${sourceType}/${sourceId}`);
  }

  myPdf(id: string): Observable<Blob> {
    return this.http.get(`${DOCUMENTS_API}/my/${id}/pdf`, { responseType: 'blob' });
  }

  verify(code: string): Observable<VerifyView> {
    return this.http.get<VerifyView>(`${DOCUMENTS_API}/verify/${encodeURIComponent(code)}`);
  }
}

/** Saves a downloaded file under a name (PDFs of documents). */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
