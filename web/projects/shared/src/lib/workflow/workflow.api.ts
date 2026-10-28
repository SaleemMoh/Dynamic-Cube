import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { DelegationView, InboxBox, InboxCounts, InstanceView, SlaState, StaffOption, TaskDetail, TaskPage } from './workflow.models';

export const WORKFLOW_API = '/api/workflow/v1';

/** The Action Center (internal host): inboxes, task work, delegations and supervision. */
@Injectable({ providedIn: 'root' })
export class WorkflowApi {
  private readonly http = inject(HttpClient);

  inbox(box: InboxBox, search: string | null, skip = 0, take = 50): Observable<TaskPage> {
    let params = new HttpParams().set('box', box).set('skip', skip).set('take', take);
    if (search) {
      params = params.set('search', search);
    }
    return this.http.get<TaskPage>(`${WORKFLOW_API}/inbox`, { params });
  }

  counts(): Observable<InboxCounts> {
    return this.http.get<InboxCounts>(`${WORKFLOW_API}/inbox/counts`);
  }

  supervision(filter: { search?: string | null; sla?: SlaState | null; unassigned?: boolean | null }, skip = 0, take = 50): Observable<TaskPage> {
    let params = new HttpParams().set('skip', skip).set('take', take);
    if (filter.search) params = params.set('search', filter.search);
    if (filter.sla) params = params.set('sla', filter.sla);
    if (filter.unassigned !== null && filter.unassigned !== undefined) params = params.set('unassigned', filter.unassigned);
    return this.http.get<TaskPage>(`${WORKFLOW_API}/supervision`, { params });
  }

  task(id: string): Observable<TaskDetail> {
    return this.http.get<TaskDetail>(`${WORKFLOW_API}/tasks/${id}`);
  }

  candidates(id: string): Observable<StaffOption[]> {
    return this.http.get<StaffOption[]>(`${WORKFLOW_API}/tasks/${id}/candidates`);
  }

  claim(id: string): Observable<TaskDetail | null> {
    return this.http.post<TaskDetail | null>(`${WORKFLOW_API}/tasks/${id}/claim`, {});
  }

  release(id: string): Observable<TaskDetail | null> {
    return this.http.post<TaskDetail | null>(`${WORKFLOW_API}/tasks/${id}/release`, {});
  }

  assign(id: string, userName: string): Observable<TaskDetail | null> {
    return this.http.post<TaskDetail | null>(`${WORKFLOW_API}/tasks/${id}/assign`, { userName });
  }

  act(id: string, action: string, comment: string | null): Observable<TaskDetail | null> {
    return this.http.post<TaskDetail | null>(`${WORKFLOW_API}/tasks/${id}/act`, {
      action,
      comment,
    });
  }

  note(id: string, text: string): Observable<TaskDetail | null> {
    return this.http.post<TaskDetail | null>(`${WORKFLOW_API}/tasks/${id}/notes`, { text });
  }

  instance(subjectType: string, subjectId: string): Observable<InstanceView> {
    return this.http.get<InstanceView>(`${WORKFLOW_API}/instances/${subjectType}/${subjectId}`);
  }

  delegations(): Observable<DelegationView[]> {
    return this.http.get<DelegationView[]>(`${WORKFLOW_API}/delegations`);
  }

  delegate(request: { delegate: string; fromDate: string; toDate: string; reason: string | null }): Observable<DelegationView> {
    return this.http.post<DelegationView>(`${WORKFLOW_API}/delegations`, request);
  }

  cancelDelegation(id: string): Observable<void> {
    return this.http.delete<void>(`${WORKFLOW_API}/delegations/${id}`);
  }

  staff(): Observable<StaffOption[]> {
    return this.http.get<StaffOption[]>(`${WORKFLOW_API}/staff`);
  }
}
