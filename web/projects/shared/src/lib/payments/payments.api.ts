import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BillInfo, BillStatus } from '../services/service.models';

export const PAYMENTS_API = '/api/payments/v1';

export const BILL_STATUSES: BillStatus[] = ['PENDING', 'PAID', 'EXPIRED', 'CANCELLED'];

export interface BillSummary {
  id: string;
  number: string;
  providerCode: string;
  providerBillNumber: string | null;
  subjectType: string;
  subjectId: string;
  subjectNumber: string | null;
  payerName: string;
  total: number;
  status: BillStatus;
  issuedAt: string;
  dueAt: string;
  paidAt: string | null;
}

export interface BillDetail {
  bill: BillSummary;
  info: BillInfo;
  descriptionAr: string;
  descriptionEn: string;
  payerPartyId: string;
  net: number;
  vat: number;
  paidAmount: number | null;
  events: { at: string; type: string; actor: string; comment: string | null }[];
}

/** Bills for staff (internal host): search, details, recording a payment made elsewhere, cancelling. */
@Injectable({ providedIn: 'root' })
export class PaymentsApi {
  private readonly http = inject(HttpClient);

  search(query: {
    search?: string | null;
    status?: BillStatus | null;
    skip?: number;
    take?: number;
  }): Observable<{ items: BillSummary[]; total: number }> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '')
        params = params.set(key, String(value));
    }
    return this.http.get<{ items: BillSummary[]; total: number }>(`${PAYMENTS_API}/bills`, {
      params,
    });
  }

  get(id: string): Observable<BillDetail> {
    return this.http.get<BillDetail>(`${PAYMENTS_API}/bills/${id}`);
  }

  markPaid(id: string, reference: string, comment: string | null): Observable<BillDetail> {
    return this.http.post<BillDetail>(`${PAYMENTS_API}/bills/${id}/mark-paid`, {
      reference,
      comment,
    });
  }

  cancel(id: string, reason: string): Observable<BillDetail> {
    return this.http.post<BillDetail>(`${PAYMENTS_API}/bills/${id}/cancel`, { reason });
  }
}

/** Badge class of a bill status. */
export function billStatusBadge(status: BillStatus): string {
  switch (status) {
    case 'PAID':
      return 'dc-badge dc-badge--success';
    case 'PENDING':
      return 'dc-badge dc-badge--warning';
    case 'EXPIRED':
    case 'CANCELLED':
      return 'dc-badge dc-badge--danger';
  }
}
