import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PageDesign } from './page-design';

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
  /** The published version of the page's designed layout; none: the Markdown body is shown. */
  design?: PageDesign | null;
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

/** How a page was found: answers so far and the visitor's own, if any. */
export interface FeedbackSummary {
  page: string;
  helpful: number;
  total: number;
  mine: boolean | null;
}

/** A service's average rating, how many rated it and the user's own stars, if any. */
export interface RatingSummary {
  service: string;
  average: number;
  count: number;
  mine: number | null;
}

/** Portal pages and frequently asked questions (anyone may read them), page feedback and service ratings. */
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

  /** "Was this page helpful?" so far; `visitor` identifies a visitor who is not signed in. */
  feedback(page: string, visitor: string | null): Observable<FeedbackSummary> {
    return this.http.get<FeedbackSummary>(`${CONTENT_API}/feedback`, { params: visitor ? { page, visitor } : { page } });
  }

  answerFeedback(page: string, helpful: boolean, visitor: string | null): Observable<FeedbackSummary> {
    return this.http.post<FeedbackSummary>(`${CONTENT_API}/feedback`, { page, helpful, visitor });
  }

  rating(service: string): Observable<RatingSummary> {
    return this.http.get<RatingSummary>(`${CONTENT_API}/ratings/${encodeURIComponent(service)}`);
  }

  /** Signed-in users only. */
  rate(service: string, stars: number): Observable<RatingSummary> {
    return this.http.put<RatingSummary>(`${CONTENT_API}/ratings/${encodeURIComponent(service)}`, { stars });
  }
}

/** A page's address: the code in lower case with `-` for `_`. */
export function pageSlug(code: string): string {
  return code.toLowerCase().replace(/_/g, '-');
}
