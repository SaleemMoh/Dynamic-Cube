import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { FeeTable, LocalizePipe, RequestAnswers, ServicesApi, TranslatePipe } from '@dynamic-cube/shared';

/** One request as submitted: who, what, the answers, fees and the timeline. */
@Component({
  selector: 'dc-request-detail',
  imports: [DatePipe, MatProgressBarModule, RouterLink, FeeTable, LocalizePipe, RequestAnswers, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a routerLink="/services/requests" class="dc-link">{{ 'requests.title' | dcTranslate }}</a>
    @if (detail.value(); as d) {
      <header class="dc-page__header">
        <div>
          <h1><span dir="ltr" data-testid="request-number">{{ d.view.request.number }}</span> — {{ { ar: d.view.request.serviceNameAr, en: d.view.request.serviceNameEn } | dcLocalize }}</h1>
          <p class="dc-muted">
            {{ 'requests.beneficiary' | dcTranslate }}:
            <a class="dc-link" [routerLink]="['/party/parties', d.beneficiaryPartyId]">{{ { ar: d.view.request.beneficiaryNameAr, en: d.view.request.beneficiaryNameEn } | dcLocalize }}</a>
            · {{ 'requests.submitter' | dcTranslate }}:
            <a class="dc-link" [routerLink]="['/party/parties', d.submitterPartyId]">{{ d.submitterName }}</a>
            · {{ 'requests.version' | dcTranslate }} {{ d.view.versionNumber }}
            @if (d.view.levelCode) {
              · {{ levelName(d.view) | dcLocalize }}
            }
          </p>
        </div>
        <span class="dc-badge dc-badge--info">{{ 'displayStatus.' + d.view.request.displayStatus | dcTranslate }}</span>
      </header>
      <div class="dc-request-grid">
        <section class="dc-card">
          <dc-request-answers [version]="d.view.version" [data]="d.view.data" />
        </section>
        <div class="dc-request-side">
          <section class="dc-card">
            <h2>{{ 'fees.grandTotal' | dcTranslate }}</h2>
            <dc-fee-table [lines]="d.view.fees" />
          </section>
          <section class="dc-card">
            <h2>{{ 'requests.timeline' | dcTranslate }}</h2>
            <ol class="dc-timeline" data-testid="timeline">
              @for (e of d.timeline; track $index) {
                <li>
                  <strong>{{ 'event.' + e.type | dcTranslate }}</strong>
                  <span class="dc-muted" dir="ltr">{{ e.at | date: 'yyyy-MM-dd HH:mm' }} · {{ e.userId }}</span>
                  @if (e.comment) {
                    <span>{{ e.comment }}</span>
                  }
                </li>
              }
            </ol>
          </section>
        </div>
      </div>
    } @else if (detail.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (detail.error()) {
      <p class="dc-errors" role="alert">{{ 'requests.notFound' | dcTranslate }}</p>
    }
  `,
  styles: `
    .dc-request-grid {
      display: grid;
      grid-template-columns: minmax(0, 2fr) minmax(18rem, 1fr);
      gap: 1rem;
      align-items: start;
    }
    @media (max-width: 960px) {
      .dc-request-grid {
        grid-template-columns: 1fr;
      }
    }
    .dc-request-side {
      display: grid;
      gap: 1rem;
    }
    .dc-request-side h2 {
      margin-top: 0;
      font-size: 18px;
    }
    .dc-timeline {
      display: grid;
      gap: 0.75rem;
      margin: 0;
      padding-inline-start: 1.25rem;
    }
    .dc-timeline li {
      display: grid;
      gap: 0.125rem;
    }
  `,
})
export class RequestDetail {
  readonly id = input.required<string>();
  private readonly api = inject(ServicesApi);
  protected readonly detail = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.requestDetail(params) });

  protected levelName(view: { levelCode: string | null; version: { definition: { levels: { code: string; nameAr: string; nameEn: string }[] } } }): { ar: string; en: string } {
    const level = view.version.definition.levels.find((l) => l.code === view.levelCode);
    return { ar: level?.nameAr ?? view.levelCode ?? '', en: level?.nameEn ?? view.levelCode ?? '' };
  }
}
