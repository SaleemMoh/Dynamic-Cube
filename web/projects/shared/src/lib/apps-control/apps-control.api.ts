import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AccountCapabilities,
  MyBusinessApp,
  AccountPage,
  AccountView,
  NewAccountRequest,
  NewPortalAccountRequest,
  ChangeOperation,
  ChangeSetDetail,
  ExportRequest,
  ExportedPackage,
  PackageCheck,
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
  WorkspaceInfo,
} from './apps-control.models';

export const APPS_CONTROL_API = '/api/apps-control/v1';

/** Request header naming the company the employee works in; Apps Control checks it. */
export const COMPANY_HEADER = 'X-Company';

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

  /** Every value of a list, whatever the signed-in administrator's own grants (to choose values a role may see). */
  /** Every value of a list, for fields of class values that take their options from it. */
  lovOptions(code: string): Observable<LovPage> {
    return this.http.get<LovPage>(`${APPS_CONTROL_API}/admin/lovs/${encodeURIComponent(code)}/options`);
  }

  lovValuesForGrants(code: string, search?: string | null): Observable<LovPage> {
    return this.http.get<LovPage>(`${APPS_CONTROL_API}/admin/lovs/${encodeURIComponent(code)}/values`, { params: params({ q: search }) });
  }

  classDomains(classCode: string, parent?: string | null): Observable<DomainItem[]> {
    return this.http.get<DomainItem[]>(
      `${APPS_CONTROL_API}/classes/${encodeURIComponent(classCode)}/domains`,
      { params: params({ parent }) },
    );
  }

  /** The look of a channel (and an app on it): what they leave empty comes from the organization. */
  branding(channel?: string | null, app?: string | null): Observable<BrandingInfo> {
    let params = new HttpParams();
    if (channel) params = params.set('channel', channel);
    if (app) params = params.set('app', app);
    return this.http.get<BrandingInfo>(`${APPS_CONTROL_API}/branding`, { params });
  }

  /** Uploads an image (logo); it becomes visible to everyone once a change set referencing it is published. */
  uploadBrandingAsset(file: File): Observable<UploadedAsset> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<UploadedAsset>(`${APPS_CONTROL_API}/admin/branding/assets`, form);
  }

  accountCapabilities(): Observable<AccountCapabilities> {
    return this.http.get<AccountCapabilities>(`${APPS_CONTROL_API}/admin/accounts/capabilities`);
  }

  account(userName: string): Observable<AccountView> {
    return this.http.get<AccountView>(`${APPS_CONTROL_API}/admin/accounts/${encodeURIComponent(userName)}`);
  }

  createAccount(request: NewAccountRequest): Observable<AccountView> {
    return this.http.post<AccountView>(`${APPS_CONTROL_API}/admin/accounts`, request);
  }

  setAccountEnabled(userName: string, enabled: boolean): Observable<void> {
    return this.http.post<void>(`${APPS_CONTROL_API}/admin/accounts/${encodeURIComponent(userName)}/${enabled ? 'enable' : 'disable'}`, {});
  }

  setAccountPassword(userName: string, request: { temporaryPassword: string | null; sendEmail: boolean }): Observable<void> {
    return this.http.post<void>(`${APPS_CONTROL_API}/admin/accounts/${encodeURIComponent(userName)}/password`, request);
  }

  signOutAccount(userName: string): Observable<void> {
    return this.http.post<void>(`${APPS_CONTROL_API}/admin/accounts/${encodeURIComponent(userName)}/sign-out`, {});
  }

  portalAccountCapabilities(): Observable<AccountCapabilities> {
    return this.http.get<AccountCapabilities>(`${APPS_CONTROL_API}/admin/portal-accounts/capabilities`);
  }

  /** Applicants' portal accounts whose national ID, name or email contains `search`. */
  portalAccounts(search: string, first: number, max: number): Observable<AccountPage> {
    return this.http.get<AccountPage>(`${APPS_CONTROL_API}/admin/portal-accounts`, { params: { search, first, max } });
  }

  portalAccount(nationalId: string): Observable<AccountView> {
    return this.http.get<AccountView>(`${APPS_CONTROL_API}/admin/portal-accounts/${encodeURIComponent(nationalId)}`);
  }

  createPortalAccount(request: NewPortalAccountRequest): Observable<AccountView> {
    return this.http.post<AccountView>(`${APPS_CONTROL_API}/admin/portal-accounts`, request);
  }

  setPortalAccountEnabled(nationalId: string, enabled: boolean): Observable<void> {
    return this.http.post<void>(`${APPS_CONTROL_API}/admin/portal-accounts/${encodeURIComponent(nationalId)}/${enabled ? 'enable' : 'disable'}`, {});
  }

  setPortalAccountPassword(nationalId: string, request: { temporaryPassword: string | null; sendEmail: boolean }): Observable<void> {
    return this.http.post<void>(`${APPS_CONTROL_API}/admin/portal-accounts/${encodeURIComponent(nationalId)}/password`, request);
  }

  signOutPortalAccount(nationalId: string): Observable<void> {
    return this.http.post<void>(`${APPS_CONTROL_API}/admin/portal-accounts/${encodeURIComponent(nationalId)}/sign-out`, {});
  }

  uiTexts(app: string): Observable<UiTextItem[]> {
    return this.http.get<UiTextItem[]>(`${APPS_CONTROL_API}/ui-texts`, { params: params({ app }) });
  }

  types(): Observable<EntityTypeInfo[]> {
    return this.http.get<EntityTypeInfo[]>(`${APPS_CONTROL_API}/admin/types`);
  }

  /** Rows of a configuration type; with `app`, those of that business app and of no app. */
  list(entityType: string, parent?: string | null, app?: string | null): Observable<ConfigRow[]> {
    return this.http.get<ConfigRow[]>(`${APPS_CONTROL_API}/admin/${entityType}`, {
      params: params({ parent, app }),
    });
  }

  /** The business apps the signed-in employee may work in (in the company they work in, or in `company`). */
  myBusinessApps(company?: string): Observable<MyBusinessApp[]> {
    return this.http.get<MyBusinessApp[]>(`${APPS_CONTROL_API}/me/business-apps`, company ? { headers: { [COMPANY_HEADER]: company } } : {});
  }

  /** The companies the signed-in employee may work in and the current one (ADR-0036 step 5). */
  myWorkspaces(): Observable<WorkspaceInfo> {
    return this.http.get<WorkspaceInfo>(`${APPS_CONTROL_API}/me/workspaces`);
  }

  /** Remembers the company the employee works in. */
  chooseWorkspace(company: string): Observable<WorkspaceInfo> {
    return this.http.put<WorkspaceInfo>(`${APPS_CONTROL_API}/me/workspace`, { company });
  }

  /** Rows under each row of a type (values per class, users per role, menus per group), keyed by code. */
  counts(entityType: 'apps.class' | 'apps.role' | 'apps.menu-group'): Observable<Record<string, number>> {
    return this.http.get<Record<string, number>>(`${APPS_CONTROL_API}/admin/counts/${entityType}`);
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

  /** Imports a configuration package as draft change sets (one per area). */
  importPackage(pkg: { title: string; items: unknown[] }): Observable<ChangeSetDetail[]> {
    return this.http.post<ChangeSetDetail[]>(`${APPS_CONTROL_API}/change-sets/import`, pkg);
  }

  /** Exports rows as a configuration package (UPSERT items keyed by code). */
  exportPackage(request: ExportRequest): Observable<ExportedPackage> {
    return this.http.post<ExportedPackage>(`${APPS_CONTROL_API}/admin/export`, request);
  }

  /** Checks a package as the import would, keeping nothing: problems of the package and of each item. */
  checkPackage(pkg: unknown): Observable<PackageCheck> {
    return this.http.post<PackageCheck>(`${APPS_CONTROL_API}/change-sets/import/check`, pkg);
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

  /** Sends one change for approval at once, in a change set of its own (nothing is stored when it is invalid). */
  submitChange(
    entityType: string,
    entityCode: string,
    operation: ChangeOperation,
    payload: unknown,
  ): Observable<ChangeSetDetail> {
    return this.http.post<ChangeSetDetail>(`${APPS_CONTROL_API}/change-sets/submit-change`, {
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
