import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import {
  AuthImage,
  I18nService,
  LocalizePipe,
  TranslatePipe,
  VisitDetail,
  VisitField,
  VisitStatus,
  VisitsApi,
  localize,
  problemMessages,
  visitStatusBadge,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { ConfirmData, ConfirmDialog } from '../apps-control/confirm-dialog';
import { PrivilegesService } from '../apps-control/privileges.service';
import { VisitOutcomeDialog, VisitScheduleDialog } from './visit-dialogs';

/**
 * A visit: for what, where and when, its result with the recorded data, photos, signature and the inspector's
 * position, and its history. With EDIT: schedule and assign, record a result, cancel, or check an external result now.
 */
@Component({
  selector: 'dc-visit-page',
  imports: [AuthImage, DatePipe, DecimalPipe, MatButtonModule, MatIconModule, MatProgressBarModule, RouterLink, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <a matButton routerLink="..">
        <mat-icon fontSet="material-symbols-outlined">{{ i18n.direction() === 'rtl' ? 'arrow_forward' : 'arrow_back' }}</mat-icon>
        {{ 'visits.title' | dcTranslate }}
      </a>
    </header>
    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (e of errors(); track $index) {
          <li>{{ e }}</li>
        }
      </ul>
    }
    @if (detail.value(); as d) {
      <div class="dc-visitpage__title">
        <h1 dir="ltr">{{ d.visit.number }}</h1>
        <span [class]="badge(d.visit.status)" data-testid="visit-page-status">{{ 'visit.status.' + d.visit.status | dcTranslate }}</span>
        @if (d.visit.overdue) {
          <span class="dc-badge dc-badge--danger">{{ 'visits.overdueBadge' | dcTranslate }}</span>
        }
      </div>
      <p class="dc-muted">
        {{ { ar: d.type.nameAr, en: d.type.nameEn } | dcLocalize }} · {{ 'visits.mode.' + d.type.mode | dcTranslate }}
        @if (d.visit.subjectType === 'SERVICE_REQUEST') {
          ·
          <a class="dc-link" [routerLink]="['/services/requests', d.visit.subjectId]" dir="ltr">{{ d.visit.subjectNumber }}</a>
        }
      </p>
      @if (canEdit() && open(d)) {
        <div class="dc-visitpage__actions">
          @if (d.type.mode !== 'EXTERNAL' && d.visit.status !== 'IN_PROGRESS') {
            <button matButton="filled" type="button" (click)="schedule(d)" data-testid="visit-schedule">
              <mat-icon fontSet="material-symbols-outlined">event</mat-icon>
              {{ (d.visit.inspector ? 'visits.reschedule' : 'visits.schedule') | dcTranslate }}
            </button>
          }
          @if (d.type.mode === 'EXTERNAL' && d.type.polled) {
            <button matButton="outlined" type="button" (click)="poll(d)" data-testid="visit-poll">
              <mat-icon fontSet="material-symbols-outlined">sync</mat-icon>
              {{ 'visits.poll' | dcTranslate }}
            </button>
          }
          <button matButton="outlined" type="button" (click)="record(d)" data-testid="visit-record">
            <mat-icon fontSet="material-symbols-outlined">fact_check</mat-icon>
            {{ 'visits.record' | dcTranslate }}
          </button>
          <button matButton="outlined" type="button" class="dc-danger" (click)="cancel(d)" data-testid="visit-cancel">
            <mat-icon fontSet="material-symbols-outlined">block</mat-icon>
            {{ 'visits.cancel' | dcTranslate }}
          </button>
        </div>
      }
      <div class="dc-visitpage__grid">
        <section class="dc-card">
          <h2>{{ 'visits.details' | dcTranslate }}</h2>
          <dl class="dc-visitpage__facts">
            <div>
              <dt>{{ 'visits.party' | dcTranslate }}</dt>
              <dd>{{ d.visit.partyName ?? '—' }}</dd>
            </div>
            <div>
              <dt>{{ 'visit.site' | dcTranslate }}</dt>
              <dd>
                {{ d.visit.siteAddress ?? '—' }}
                @if (d.latitude !== null && d.longitude !== null) {
                  <a class="dc-link" [href]="map(d.latitude, d.longitude)" target="_blank" rel="noopener">{{ 'visits.openMap' | dcTranslate }}</a>
                }
              </dd>
            </div>
            @if (d.type.mode === 'EXTERNAL') {
              <div>
                <dt>{{ 'visits.externalReference' | dcTranslate }}</dt>
                <dd dir="ltr" data-testid="visit-external-reference">{{ d.externalReference ?? '—' }}</dd>
              </div>
              @if (d.externalStatus) {
                <div>
                  <dt>{{ 'visits.externalStatus' | dcTranslate }}</dt>
                  <dd dir="ltr">{{ d.externalStatus }}</dd>
                </div>
              }
              @if (d.nextPollAt && d.visit.status === 'SENT') {
                <div>
                  <dt>{{ 'visits.nextPoll' | dcTranslate }}</dt>
                  <dd dir="ltr">{{ d.nextPollAt | date: 'yyyy-MM-dd HH:mm' }}</dd>
                </div>
              }
            } @else {
              <div>
                <dt>{{ 'visits.inspector' | dcTranslate }}</dt>
                <dd>{{ d.visit.inspectorName ?? '—' }}</dd>
              </div>
              <div>
                <dt>{{ 'visit.scheduledOn' | dcTranslate }}</dt>
                <dd dir="ltr">{{ d.visit.scheduledAt ? (d.visit.scheduledAt | date: 'yyyy-MM-dd HH:mm') : '—' }}</dd>
              </div>
            }
            <div>
              <dt>{{ 'visits.requestedAt' | dcTranslate }}</dt>
              <dd dir="ltr">{{ d.visit.requestedAt | date: 'yyyy-MM-dd' }}</dd>
            </div>
            <div>
              <dt>{{ 'visits.dueAt' | dcTranslate }}</dt>
              <dd dir="ltr">{{ d.visit.dueAt | date: 'yyyy-MM-dd' }}</dd>
            </div>
          </dl>
          @if (d.instructions) {
            <h3>{{ 'visits.instructions' | dcTranslate }}</h3>
            <p dir="auto">{{ d.instructions }}</p>
          }
          @if (d.cancelReason) {
            <h3>{{ 'visits.cancelReason' | dcTranslate }}</h3>
            <p dir="auto">{{ d.cancelReason }}</p>
          }
        </section>

        <section class="dc-card">
          <h2>{{ 'visits.history' | dcTranslate }}</h2>
          <ol class="dc-timeline">
            @for (e of d.events; track $index) {
              <li>
                <strong>{{ 'visitEvent.' + e.type | dcTranslate }}</strong>
                <span class="dc-muted dc-timeline__meta"
                  ><bdi dir="ltr">{{ e.at | date: 'yyyy-MM-dd HH:mm' }}</bdi> · <bdi>{{ e.actor }}</bdi></span
                >
                @if (e.comment) {
                  <p dir="auto">{{ e.comment }}</p>
                }
              </li>
            }
          </ol>
        </section>
      </div>

      @if (d.visit.status === 'COMPLETED') {
        <section class="dc-card" data-testid="visit-result">
          <h2>{{ 'visits.result' | dcTranslate }}</h2>
          <dl class="dc-visitpage__facts">
            <div>
              <dt>{{ 'visits.outcome' | dcTranslate }}</dt>
              <dd data-testid="visit-page-outcome">{{ outcomeName(d) }}</dd>
            </div>
            <div>
              <dt>{{ 'visit.completedOn' | dcTranslate }}</dt>
              <dd dir="ltr">{{ d.visit.completedAt | date: 'yyyy-MM-dd HH:mm' }}</dd>
            </div>
            <div>
              <dt>{{ 'visits.completedBy' | dcTranslate }}</dt>
              <dd dir="ltr">{{ d.completedBy }}</dd>
            </div>
            @for (f of d.type.fields; track f.code) {
              <div>
                <dt>{{ { ar: f.labelAr, en: f.labelEn } | dcLocalize }}</dt>
                <dd>{{ value(f, d.data[f.code]) }}</dd>
              </div>
            }
            @for (extra of extras(d); track extra[0]) {
              <div>
                <dt dir="ltr">{{ extra[0] }}</dt>
                <dd dir="ltr">{{ extra[1] }}</dd>
              </div>
            }
            @if (d.capturedLatitude !== null && d.capturedLongitude !== null) {
              <div>
                <dt>{{ 'visits.captured' | dcTranslate }}</dt>
                <dd dir="ltr">
                  <a class="dc-link" [href]="map(d.capturedLatitude, d.capturedLongitude)" target="_blank" rel="noopener"
                    >{{ d.capturedLatitude | number: '1.5-5' }}, {{ d.capturedLongitude | number: '1.5-5' }}</a
                  >
                  @if (d.capturedAccuracy !== null) {
                    · {{ accuracy(d.capturedAccuracy) }}
                  }
                </dd>
              </div>
            }
          </dl>
          @if (d.report) {
            <h3>{{ 'visits.report' | dcTranslate }}</h3>
            <p dir="auto" class="dc-visitpage__report">{{ d.report }}</p>
          }
          @if (d.photos.length) {
            <h3>{{ 'visits.photos' | dcTranslate }}</h3>
            <div class="dc-visitpage__photos">
              @for (p of d.photos; track p.id) {
                <figure>
                  <dc-auth-image [src]="api.photoUrl(d.visit.id, p.id)" [alt]="p.caption ?? p.fileName" />
                  @if (p.caption) {
                    <figcaption dir="auto">{{ p.caption }}</figcaption>
                  }
                </figure>
              }
            </div>
          }
          @if (d.hasSignature) {
            <h3>{{ 'visits.signature' | dcTranslate }}</h3>
            <div class="dc-visitpage__signature">
              <dc-auth-image [src]="api.signatureUrl(d.visit.id)" [alt]="'visits.signature' | dcTranslate" />
              <span>{{ 'visits.signedBy' | dcTranslate }}: {{ d.signedBy }}</span>
            </div>
          }
        </section>
      }
    } @else if (detail.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: 1rem;
    }
    .dc-visitpage__title {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    .dc-visitpage__title h1 {
      margin: 0;
    }
    .dc-visitpage__actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .dc-visitpage__grid {
      display: grid;
      grid-template-columns: minmax(0, 3fr) minmax(16rem, 2fr);
      gap: 1rem;
      align-items: start;
    }
    @media (max-width: 960px) {
      .dc-visitpage__grid {
        grid-template-columns: 1fr;
      }
    }
    .dc-visitpage__facts {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(13rem, 1fr));
      gap: 12px 24px;
      margin: 0;
    }
    .dc-visitpage__facts dt {
      color: var(--dga-text-3, #384250);
      font-size: 13px;
    }
    .dc-visitpage__facts dd {
      margin: 2px 0 0;
      font-weight: 600;
    }
    .dc-visitpage__facts dd a {
      font-weight: 400;
      margin-inline-start: 0.5rem;
    }
    .dc-visitpage__report {
      white-space: pre-wrap;
    }
    .dc-visitpage__photos {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(10rem, 1fr));
      gap: 0.75rem;
    }
    .dc-visitpage__photos figure {
      margin: 0;
    }
    .dc-visitpage__photos dc-auth-image {
      aspect-ratio: 4 / 3;
      border-radius: 8px;
      overflow: hidden;
    }
    .dc-visitpage__photos figcaption {
      font-size: 13px;
      margin-top: 4px;
    }
    .dc-visitpage__signature {
      display: flex;
      align-items: center;
      gap: 1rem;
    }
    .dc-visitpage__signature dc-auth-image {
      width: 16rem;
      height: 6rem;
      border: 1px solid var(--dga-border, #d2d6db);
      border-radius: 8px;
      background: #fff;
    }
    .dc-danger {
      color: #b42318;
    }
    .dc-timeline {
      display: grid;
      gap: 0.75rem;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .dc-timeline li {
      padding-inline-start: 0.75rem;
      border-inline-start: 3px solid var(--dga-border, #d2d6db);
    }
    .dc-timeline__meta {
      display: block;
      font-size: 13px;
    }
    .dc-timeline p {
      margin: 0.25rem 0 0;
    }
  `,
})
export class VisitPage {
  readonly id = input.required<string>();
  protected readonly api = inject(VisitsApi);
  private readonly dialog = inject(MatDialog);
  protected readonly i18n = inject(I18nService);
  private readonly privileges = inject(PrivilegesService);
  protected readonly detail = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.get(params) });
  protected readonly canEdit = computed(() => this.privileges.can('visits.registry', 'EDIT'));
  protected readonly errors = signal<string[]>([]);

  protected badge(status: VisitStatus): string {
    return visitStatusBadge(status);
  }

  protected open(d: VisitDetail): boolean {
    return d.visit.status !== 'COMPLETED' && d.visit.status !== 'CANCELLED';
  }

  protected map(lat: number, lng: number): string {
    return `https://www.google.com/maps?q=${lat},${lng}`;
  }

  protected accuracy(m: number): string {
    return this.i18n.translate('visits.accuracy').replace('{m}', String(Math.round(m)));
  }

  protected outcomeName(d: VisitDetail): string {
    const o = d.type.outcomes.find((x) => x.code === d.visit.outcome);
    return o ? localize({ ar: o.nameAr, en: o.nameEn }, this.i18n.language()) : (d.visit.outcome ?? '—');
  }

  /** A recorded value as read: the choice's label, yes/no, or the text. */
  protected value(f: VisitField, v: unknown): string {
    if (v === null || v === undefined || v === '') return '—';
    if (f.type === 'CHECKBOX') return this.i18n.translate(v === true ? 'visits.yes' : 'visits.no');
    if (f.type === 'SELECT') {
      const o = f.options.find((x) => x.code === v);
      return o ? localize({ ar: o.labelAr, en: o.labelEn }, this.i18n.language()) : String(v);
    }
    return String(v);
  }

  /** Values recorded beyond the type's fields (an external body's findings). */
  protected extras(d: VisitDetail): [string, string][] {
    const known = new Set(d.type.fields.map((f) => f.code));
    return Object.entries(d.data)
      .filter(([k]) => !known.has(k))
      .map(([k, v]) => [k, typeof v === 'object' ? JSON.stringify(v) : String(v)]);
  }

  protected async schedule(d: VisitDetail): Promise<void> {
    const done = await firstValueFrom(this.dialog.open<VisitScheduleDialog, VisitDetail, VisitDetail>(VisitScheduleDialog, { data: d }).afterClosed());
    if (done) this.detail.reload();
  }

  protected async record(d: VisitDetail): Promise<void> {
    const done = await firstValueFrom(this.dialog.open<VisitOutcomeDialog, VisitDetail, VisitDetail>(VisitOutcomeDialog, { data: d }).afterClosed());
    if (done) this.detail.reload();
  }

  protected async cancel(d: VisitDetail): Promise<void> {
    const reason = await firstValueFrom(
      this.dialog
        .open<ConfirmDialog, ConfirmData, string>(ConfirmDialog, {
          data: {
            title: this.i18n.translate('visits.cancel'),
            message: this.i18n.translate('visits.cancelHint'),
            confirmLabel: this.i18n.translate('visits.cancel'),
            comment: true,
            commentRequired: true,
            danger: true,
          },
        })
        .afterClosed(),
    );
    if (!reason) return;
    await this.run(() => firstValueFrom(this.api.cancel(d.visit.id, reason)));
  }

  protected async poll(d: VisitDetail): Promise<void> {
    await this.run(() => firstValueFrom(this.api.poll(d.visit.id)));
  }

  private async run(action: () => Promise<unknown>): Promise<void> {
    this.errors.set([]);
    try {
      await action();
      this.detail.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}
