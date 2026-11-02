import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';
import { FormField } from '../forms/form-schema';
import { RecordPick, RecordProvider } from '../forms/record-source';
import { PARTY_API } from './party.api';

export type RecordColumnType = 'TEXT' | 'NUMBER' | 'DATE' | 'CHECKBOX' | 'SELECT';

export interface RecordColumn {
  code: string;
  labelAr: string;
  labelEn: string;
  type: RecordColumnType;
  required: boolean;
  inTable: boolean;
  options: { value: string; labelAr: string; labelEn: string }[];
}

export interface PartyRecord {
  id: string;
  data: Record<string, unknown>;
  source: 'MANUAL' | 'INTEGRATION' | 'SIGN_IN';
  key: string | null;
  importedAt: string | null;
  updatedAt: string;
}

export interface RecordList {
  code: string;
  nameAr: string;
  nameEn: string;
  descriptionAr: string | null;
  descriptionEn: string | null;
  icon: string | null;
  columns: RecordColumn[];
  allowManual: boolean;
  canImport: boolean;
  records: PartyRecord[];
}

export interface RecordImportResult {
  outcome: string;
  messageAr: string | null;
  messageEn: string | null;
  added: number;
  updated: number;
  removed: number;
}

/** The record lists of the applicant's acting party (portal) and of any party (staff). */
@Injectable({ providedIn: 'root' })
export class RecordsApi {
  private readonly http = inject(HttpClient);

  mine(): Observable<RecordList[]> {
    return this.http.get<RecordList[]>(`${PARTY_API}/me/record-lists`);
  }

  myList(code: string): Observable<RecordList> {
    return this.http.get<RecordList>(`${PARTY_API}/me/record-lists/${code}`);
  }

  add(list: string, data: Record<string, unknown>): Observable<PartyRecord> {
    return this.http.post<PartyRecord>(`${PARTY_API}/me/record-lists/${list}/records`, { data });
  }

  update(id: string, data: Record<string, unknown>): Observable<PartyRecord> {
    return this.http.put<PartyRecord>(`${PARTY_API}/me/records/${id}`, { data });
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${PARTY_API}/me/records/${id}`);
  }

  import(list: string): Observable<RecordImportResult> {
    return this.http.post<RecordImportResult>(`${PARTY_API}/me/record-lists/${list}/import`, {});
  }

  /** Staff: a party's record lists. */
  ofParty(partyId: string): Observable<RecordList[]> {
    return this.http.get<RecordList[]>(`${PARTY_API}/parties/${partyId}/record-lists`);
  }

  /** Staff: fetches a party's imported list again from its source. */
  importForParty(partyId: string, list: string): Observable<RecordImportResult> {
    return this.http.post<RecordImportResult>(
      `${PARTY_API}/parties/${partyId}/record-lists/${list}/import`,
      {},
    );
  }
}

const FIELD_TYPES: Record<RecordColumnType, FormField['type']> = {
  TEXT: 'text',
  NUMBER: 'number',
  DATE: 'date',
  CHECKBOX: 'checkbox',
  SELECT: 'select',
};

/** The columns of a record list as form fields (to add or change a record). */
export function recordFields(list: RecordList): FormField[] {
  return list.columns.map((c) => ({
    code: c.code,
    type: FIELD_TYPES[c.type],
    label: { ar: c.labelAr, en: c.labelEn },
    required: c.required,
    options: c.options.map((o) => ({ value: o.value, label: { ar: o.labelAr, en: o.labelEn } })),
    maxLength: c.type === 'TEXT' ? 2000 : undefined,
  }));
}

/** The columns shown in a table of records: those marked, or the first four. */
export function recordTableColumns(list: RecordList): RecordColumn[] {
  const marked = list.columns.filter((c) => c.inTable);
  return marked.length ? marked : list.columns.slice(0, 4);
}

/** The portal's record source for rows questions: the acting party's records of a list. */
export function partyRecordProvider(api: RecordsApi): RecordProvider {
  return {
    async load(list: string): Promise<RecordPick | null> {
      try {
        const found = await firstValueFrom(api.myList(list));
        return {
          title: { ar: found.nameAr, en: found.nameEn },
          columns: recordTableColumns(found).map((c) => ({
            code: c.code,
            label: { ar: c.labelAr, en: c.labelEn },
            options: c.options.map((o) => ({
              value: o.value,
              label: { ar: o.labelAr, en: o.labelEn },
            })),
          })),
          rows: found.records.map((r) => ({ id: r.id, data: r.data })),
        };
      } catch {
        return null;
      }
    },
  };
}
