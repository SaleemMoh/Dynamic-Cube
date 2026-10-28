import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, InjectionToken, inject } from '@angular/core';
import { Observable } from 'rxjs';

export const COMMUNICATION_API = '/api/communication/v1';

export type MessageStatus = 'PENDING' | 'SENT' | 'FAILED' | 'SKIPPED';

export const MESSAGE_STATUSES: MessageStatus[] = ['PENDING', 'SENT', 'FAILED', 'SKIPPED'];

export const CHANNELS = ['EMAIL', 'SMS', 'IN_APP'] as const;

export interface EventTypeView {
  code: string;
  nameAr: string;
  nameEn: string;
  module: string;
  sample: Record<string, unknown>;
}

export interface MessageSummary {
  id: string;
  createdAt: string;
  eventCode: string;
  ruleCode: string;
  channel: string;
  recipientKind: string;
  recipient: string;
  recipientName: string | null;
  address: string | null;
  subjectType: string;
  subjectId: string;
  subjectNumber: string | null;
  title: string | null;
  status: MessageStatus;
  attempts: number;
  error: string | null;
  sentAt: string | null;
}

export interface MessagePage {
  items: MessageSummary[];
  total: number;
}

export interface MessageView {
  message: MessageSummary;
  body: string;
}

export interface NotificationView {
  id: string;
  eventCode: string;
  titleAr: string;
  titleEn: string;
  bodyAr: string;
  bodyEn: string;
  link: string | null;
  subjectType: string;
  subjectId: string;
  subjectNumber: string | null;
  createdAt: string;
  readAt: string | null;
}

export interface NotificationPage {
  items: NotificationView[];
  unread: number;
}

export interface MessageTemplateWording {
  emailSubjectAr?: string | null;
  emailSubjectEn?: string | null;
  emailBodyAr?: string | null;
  emailBodyEn?: string | null;
  smsAr?: string | null;
  smsEn?: string | null;
  inAppTitleAr?: string | null;
  inAppTitleEn?: string | null;
  inAppBodyAr?: string | null;
  inAppBodyEn?: string | null;
  link?: string | null;
}

export interface ChannelPreview {
  channel: string;
  titleAr: string | null;
  titleEn: string | null;
  bodyAr: string;
  bodyEn: string;
}

export interface MessageQuery {
  search?: string | null;
  status?: MessageStatus | null;
  channel?: string | null;
  eventCode?: string | null;
  subjectId?: string | null;
  skip?: number;
  take?: number;
}

/** Event types, template previews and the message log (internal host). */
@Injectable({ providedIn: 'root' })
export class CommunicationApi {
  private readonly http = inject(HttpClient);

  events(): Observable<EventTypeView[]> {
    return this.http.get<EventTypeView[]>(`${COMMUNICATION_API}/events`);
  }

  preview(eventCode: string, template: MessageTemplateWording): Observable<ChannelPreview[]> {
    return this.http.post<ChannelPreview[]>(`${COMMUNICATION_API}/templates/preview`, { eventCode, template });
  }

  messages(query: MessageQuery): Observable<MessagePage> {
    let params = new HttpParams().set('skip', query.skip ?? 0).set('take', query.take ?? 50);
    for (const key of ['search', 'status', 'channel', 'eventCode', 'subjectId'] as const) {
      const value = query[key];
      if (value) params = params.set(key, value);
    }
    return this.http.get<MessagePage>(`${COMMUNICATION_API}/messages`, { params });
  }

  message(id: string): Observable<MessageView> {
    return this.http.get<MessageView>(`${COMMUNICATION_API}/messages/${id}`);
  }

  resend(id: string): Observable<MessageView> {
    return this.http.post<MessageView>(`${COMMUNICATION_API}/messages/${id}/resend`, {});
  }
}

/** Where notifications are read: an employee's on the internal host (default), the acting party's on the portal. */
export const NOTIFICATIONS_BASE = new InjectionToken<string>('NOTIFICATIONS_BASE', { factory: () => `${COMMUNICATION_API}/notifications` });

/** The portal's notifications: those of the acting party. */
export const PARTY_NOTIFICATIONS = `${COMMUNICATION_API}/my/notifications`;

/** Notifications of the signed-in person (see {@link NOTIFICATIONS_BASE}). */
@Injectable({ providedIn: 'root' })
export class NotificationsApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(NOTIFICATIONS_BASE);

  list(unread = false, take = 20): Observable<NotificationPage> {
    return this.http.get<NotificationPage>(this.base, { params: new HttpParams().set('unread', unread).set('take', take) });
  }

  unreadCount(): Observable<{ count: number }> {
    return this.http.get<{ count: number }>(`${this.base}/unread-count`);
  }

  read(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/read`, {});
  }

  readAll(): Observable<void> {
    return this.http.post<void>(`${this.base}/read-all`, {});
  }
}
