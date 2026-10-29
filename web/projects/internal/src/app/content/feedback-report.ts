import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { AppsControlApi, CONTENT_API, FeedbackSummary, I18nService, RatingSummary, TranslatePipe } from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';

interface FeedbackReport {
  pages: FeedbackSummary[];
  services: RatingSummary[];
}

/** What portal visitors said: "was this page helpful?" by page and the services' ratings. */
@Component({
  selector: 'dc-feedback-report',
  imports: [DecimalPipe, MatIconModule, MatProgressBarModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <div>
        <h1>{{ 'feedback.title' | dcTranslate }}</h1>
        <p class="dc-muted">{{ 'feedback.lead' | dcTranslate }}</p>
      </div>
    </header>
    @if (report.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    <div class="dc-feedback">
      <section class="dc-card">
        <h2>{{ 'feedback.ratings' | dcTranslate }}</h2>
        <table class="dc-feedback__table" data-testid="feedback-ratings">
          <thead>
            <tr>
              <th>{{ 'feedback.service' | dcTranslate }}</th>
              <th>{{ 'feedback.average' | dcTranslate }}</th>
              <th>{{ 'feedback.count' | dcTranslate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (s of report.value()?.services ?? []; track s.service) {
              <tr>
                <td>
                  {{ serviceName(s.service) }} <span class="dc-muted" dir="ltr">{{ s.service }}</span>
                </td>
                <td class="dc-feedback__stars">
                  <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">star</mat-icon> {{ s.average | number: '1.1-1' }}
                </td>
                <td>{{ s.count }}</td>
              </tr>
            } @empty {
              <tr>
                <td colspan="3" class="dc-muted">{{ 'feedback.none' | dcTranslate }}</td>
              </tr>
            }
          </tbody>
        </table>
      </section>
      <section class="dc-card">
        <h2>{{ 'feedback.pages' | dcTranslate }}</h2>
        <table class="dc-feedback__table" data-testid="feedback-pages">
          <thead>
            <tr>
              <th>{{ 'feedback.page' | dcTranslate }}</th>
              <th>{{ 'feedback.helpful' | dcTranslate }}</th>
              <th>{{ 'feedback.answers' | dcTranslate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (p of report.value()?.pages ?? []; track p.page) {
              <tr>
                <td dir="ltr" class="dc-feedback__key">{{ p.page }}</td>
                <td>
                  <span class="dc-feedback__bar"><span [style.width.%]="(p.helpful * 100) / p.total"></span></span>
                  {{ (p.helpful * 100) / p.total | number: '1.0-0' }}%
                </td>
                <td>{{ p.total }}</td>
              </tr>
            } @empty {
              <tr>
                <td colspan="3" class="dc-muted">{{ 'feedback.none' | dcTranslate }}</td>
              </tr>
            }
          </tbody>
        </table>
      </section>
    </div>
  `,
  styles: `
    .dc-feedback {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(26rem, 1fr));
      gap: 16px;
    }
    .dc-feedback h2 {
      margin: 0 0 12px;
      font-size: 16px;
    }
    .dc-feedback__table {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
    }
    .dc-feedback__table th {
      padding: 8px;
      background: var(--dga-gray-50);
      color: var(--dga-gray-600);
      font-weight: 600;
      text-align: start;
    }
    .dc-feedback__table td {
      padding: 8px;
      border-bottom: 1px solid var(--dga-gray-100);
    }
    .dc-feedback__stars mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
      vertical-align: -3px;
      color: var(--dynamic-cube-gold);
      font-variation-settings: 'FILL' 1;
    }
    .dc-feedback__key {
      text-align: start;
    }
    .dc-feedback__bar {
      display: inline-block;
      width: 5rem;
      height: 6px;
      margin-inline-end: 6px;
      border-radius: 3px;
      background: var(--dga-gray-100);
      vertical-align: middle;
    }
    .dc-feedback__bar span {
      display: block;
      height: 100%;
      border-radius: 3px;
      background: var(--dynamic-cube-gold);
    }
  `,
})
export class FeedbackReportPage {
  private readonly http = inject(HttpClient);
  private readonly i18n = inject(I18nService);
  private readonly appsControl = inject(AppsControlApi);
  protected readonly report = rxResource({ stream: () => this.http.get<FeedbackReport>(`${CONTENT_API}/feedback/report`) });
  /** Service names for the codes (only services the user may view). */
  private readonly services = rxResource({ stream: () => this.appsControl.list('svc.service').pipe(catchError(() => of([]))) });
  private readonly names = computed(() => new Map((this.services.value() ?? []).map((s) => [s.code, { ar: s.nameAr, en: s.nameEn }])));

  protected serviceName(code: string): string {
    const name = this.names().get(code);
    return name ? this.i18n.localize(name) : '';
  }
}
