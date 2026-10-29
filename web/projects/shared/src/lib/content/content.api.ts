import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export const CONTENT_API = '/api/content/v1';

export interface PageLink {
  slug: string;
  titleAr: string;
  titleEn: string;
  showInFooter: boolean;
  order: number;
}

export interface PageView {
  slug: string;
  titleAr: string;
  titleEn: string;
  bodyAr: string;
  bodyEn: string;
  updatedAt: string;
}

export interface FaqView {
  code: string;
  questionAr: string;
  questionEn: string;
  answerAr: string;
  answerEn: string;
  topicAr: string | null;
  topicEn: string | null;
  serviceCode: string | null;
}

/** Portal pages and frequently asked questions (anyone may read them). */
@Injectable({ providedIn: 'root' })
export class ContentApi {
  private readonly http = inject(HttpClient);

  pages(): Observable<PageLink[]> {
    return this.http.get<PageLink[]>(`${CONTENT_API}/pages`);
  }

  page(slug: string): Observable<PageView> {
    return this.http.get<PageView>(`${CONTENT_API}/pages/${encodeURIComponent(slug)}`);
  }

  faqs(service?: string | null): Observable<FaqView[]> {
    return this.http.get<FaqView[]>(`${CONTENT_API}/faqs`, { params: service ? { service } : {} });
  }
}

/** A page's address: the code in lower case with `-` for `_`. */
export function pageSlug(code: string): string {
  return code.toLowerCase().replace(/_/g, '-');
}
