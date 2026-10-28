export type DocumentStatus = 'ACTIVE' | 'SUSPENDED' | 'EXPIRED' | 'CANCELLED';

export const DOCUMENT_STATUSES: DocumentStatus[] = ['ACTIVE', 'SUSPENDED', 'EXPIRED', 'CANCELLED'];

export interface DocumentSummary {
  id: string;
  number: string;
  typeCode: string;
  typeNameAr: string;
  typeNameEn: string;
  holderPartyId: string;
  holderNameAr: string;
  holderNameEn: string;
  status: DocumentStatus;
  issuedAt: string;
  expiresAt: string | null;
  currentVersion: number;
}

export interface DocumentPage {
  items: DocumentSummary[];
  total: number;
}

export interface DocumentFieldView {
  code: string;
  nameAr: string;
  nameEn: string;
  isPublic: boolean;
}

export interface DocumentVersionView {
  version: number;
  issuedAt: string;
  expiresAt: string | null;
  /** Empty for applicants. */
  issuedBy: string;
  sourceType: string;
  sourceId: string;
  sourceNumber: string | null;
  reason: string | null;
  data: Record<string, unknown>;
  lines: Record<string, unknown>[];
  rendered: boolean;
  renderError: string | null;
}

export interface DocumentEventView {
  at: string;
  type: string;
  actor: string;
  version: number | null;
  comment: string | null;
}

export interface DocumentDetail {
  document: DocumentSummary;
  verifyUrl: string;
  fields: DocumentFieldView[];
  lineFields: DocumentFieldView[];
  versions: DocumentVersionView[];
  events: DocumentEventView[];
}

/** What the public verification page shows. */
export interface VerifyView {
  number: string;
  typeNameAr: string;
  typeNameEn: string;
  holderNameAr: string;
  holderNameEn: string;
  status: DocumentStatus;
  issuedAt: string;
  expiresAt: string | null;
  version: number;
  versionIssuedAt: string;
  fields: { nameAr: string; nameEn: string; value: unknown }[];
  lineFields: DocumentFieldView[];
  lines: Record<string, unknown>[];
}

export interface DocumentQuery {
  search?: string | null;
  type?: string | null;
  status?: DocumentStatus | null;
  holder?: string | null;
  skip?: number;
  take?: number;
}

/** A value of a document field as text (numbers, dates and choices as they are; objects as JSON). */
export function fieldText(value: unknown): string {
  if (value === null || value === undefined) return '';
  if (typeof value === 'boolean') return value ? '✓' : '—';
  return typeof value === 'object' ? JSON.stringify(value) : String(value);
}
