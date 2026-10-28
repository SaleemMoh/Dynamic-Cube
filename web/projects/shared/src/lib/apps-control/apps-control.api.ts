import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ChangeOperation,
  ChangeSetDetail,
  ChangeSetStatus,
  ChangeSetSummary,
  BrandingInfo,
  ConfigRow,
  DomainItem,
  EntityTypeInfo,
  LovPage,
  MeInfo,
  MenuGroupNode,
  UiTextItem,
  UploadedAsset,
} from './apps-control.models';

export const APPS_CONTROL_API = '/api/apps-control/v1';

export interface LovRequest {
  parent?: string | null;
  q?: string | null;
  page?: number;
  size?: number;
  resource?: string | null;
}

@Injectable({ providedIn: 'root' })
export class AppsControlApi {
  private readonly http = inject(HttpClient);

  me(): Observable<MeInfo> {
    return this.http.get<MeInfo>(`${APPS_CONTROL_API}/me`);
  }

  menu(app?: string): Observable<MenuGroupNode[]> {
    return this.http.get<MenuGroupNode[]>(`${APPS_CONTROL_API}/me/menu`, {
      params: params({ app }),
    });
  }

  privileges(): Observable<Record<string, string[]>> {
    return this.http.get<Record<string, string[]>>(`${APPS_CONTROL_API}/me/privileges`);
  }

  lovValues(code: string, request: LovRequest = {}): Observable<LovPage> {
    return this.http.get<LovPage>(`${APPS_CONTROL_API}/lovs/${encodeURIComponent(code)}/values`, {
      params: params({ ...request }),
    });
  }

  classDomains(classCode: string, parent?: string | null): Observable<DomainItem[]> {
    return this.http.get<DomainItem[]>(
      `${APPS_CONTROL_API}/classes/${encodeURIComponent(classCode)}/domains`,
      { params: params({ parent }) },
    );
  }

  branding(): Observable<BrandingInfo> {
    return this.http.get<BrandingInfo>(`${APPS_CONTROL_API}/branding`);
  }

  /** Uploads an image (logo); it becomes visible to everyone once a change set referencing it is published. */
  uploadBrandingAsset(file: File): Observable<UploadedAsset> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<UploadedAsset>(`${APPS_CONTROL_API}/admin/branding/assets`, form);
  }

  uiTexts(app: string): Observable<UiTextItem[]> {
    return this.http.get<UiTextItem[]>(`${APPS_CONTROL_API}/ui-texts`, { params: params({ app }) });
  }

  types(): Observable<EntityTypeInfo[]> {
    return this.http.get<EntityTypeInfo[]>(`${APPS_CONTROL_API}/admin/types`);
  }

  list(entityType: string, parent?: string | null): Observable<ConfigRow[]> {
    return this.http.get<ConfigRow[]>(`${APPS_CONTROL_API}/admin/${entityType}`, {
      params: params({ parent }),
    });
  }

  get(entityType: string, code: string): Observable<ConfigRow> {
    return this.http.get<ConfigRow>(
      `${APPS_CONTROL_API}/admin/${entityType}/${code.split('/').map(encodeURIComponent).join('/')}`,
    );
  }

  changeSets(
    filter: { status?: ChangeSetStatus; area?: string; mine?: boolean } = {},
  ): Observable<ChangeSetSummary[]> {
    return this.http.get<ChangeSetSummary[]>(`${APPS_CONTROL_API}/change-sets`, {
      params: params({ ...filter }),
    });
  }

  changeSet(id: string): Observable<ChangeSetDetail> {
    return this.http.get<ChangeSetDetail>(`${APPS_CONTROL_API}/change-sets/${id}`);
  }

  createChangeSet(area: string, title: string): Observable<ChangeSetDetail> {
    return this.http.post<ChangeSetDetail>(`${APPS_CONTROL_API}/change-sets`, { area, title });
  }

  addItem(
    id: string,
    entityType: string,
    entityCode: string,
    operation: ChangeOperation,
    payload: unknown,
  ): Observable<ChangeSetDetail> {
    return this.http.post<ChangeSetDetail>(`${APPS_CONTROL_API}/change-sets/${id}/items`, {
      entityType,
      entityCode,
      operation,
      payload,
    });
  }

  /** Saves and publishes one change in an area that needs no approval. */
  applyNow(
    entityType: string,
    entityCode: string,
    operation: ChangeOperation,
    payload: unknown,
  ): Observable<ChangeSetDetail> {
    return this.http.post<ChangeSetDetail>(`${APPS_CONTROL_API}/change-sets/apply`, {
      entityType,
      entityCode,
      operation,
      payload,
    });
  }

  removeItem(id: string, itemId: string): Observable<ChangeSetDetail> {
    return this.http.delete<ChangeSetDetail>(
      `${APPS_CONTROL_API}/change-sets/${id}/items/${itemId}`,
    );
  }

  submit(id: string): Observable<ChangeSetDetail> {
    return this.http.post<ChangeSetDetail>(`${APPS_CONTROL_API}/change-sets/${id}/submit`, {});
  }

  approve(id: string, comment?: string): Observable<ChangeSetDetail> {
    return this.http.post<ChangeSetDetail>(`${APPS_CONTROL_API}/change-sets/${id}/approve`, {
      comment,
    });
  }

  reject(id: string, comment: string): Observable<ChangeSetDetail> {
    return this.http.post<ChangeSetDetail>(`${APPS_CONTROL_API}/change-sets/${id}/reject`, {
      comment,
    });
  }

  reopen(id: string): Observable<ChangeSetDetail> {
    return this.http.post<ChangeSetDetail>(`${APPS_CONTROL_API}/change-sets/${id}/reopen`, {});
  }

  publish(id: string): Observable<ChangeSetDetail> {
    return this.http.post<ChangeSetDetail>(`${APPS_CONTROL_API}/change-sets/${id}/publish`, {});
  }

  discard(id: string): Observable<void> {
    return this.http.delete<void>(`${APPS_CONTROL_API}/change-sets/${id}`);
  }
}

function params(values: Record<string, string | number | boolean | null | undefined>): HttpParams {
  let result = new HttpParams();
  for (const [key, value] of Object.entries(values)) {
    if (value !== null && value !== undefined && value !== '') {
      result = result.set(key, String(value));
    }
  }
  return result;
}
