import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { I18nService, LocalizePipe, TranslatePipe, VisitDetail } from '@dynamic-cube/shared';
import { FieldStore } from '../field-store';

/** The inspector's visits: today's and upcoming first, then those done (or waiting to be sent); works offline. */
@Component({
  selector: 'dc-home',
  imports: [DatePipe, NgTemplateOutlet, MatButtonModule, MatIconModule, MatProgressBarModule, RouterLink, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-field__status" [class.dc-field__status--offline]="!store.online()" role="status" data-testid="field-connection">
      <mat-icon fontSet="material-symbols-outlined">{{ store.online() ? 'cloud_done' : 'cloud_off' }}</mat-icon>
      <span>{{ (store.online() ? 'field.online' : 'field.offline') | dcTranslate }}</span>
      @if (store.outbox().length) {
        <span class="dc-field__waiting" data-testid="field-outbox">{{ waitingText() }}</span>
      }
      @if (store.lastSync(); as at) {
        <span class="dc-field__sync" dir="ltr">{{ at | date: 'HH:mm' }}</span>
      }
      <button matIconButton type="button" [disabled]="!store.online() || store.syncing()" (click)="store.sync()" data-testid="field-sync">
        <mat-icon fontSet="material-symbols-outlined">sync</mat-icon>
      </button>
    </div>
    @if (store.syncing() || !store.loaded()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @if (store.error(); as e) {
      <p class="dc-errors" role="alert">{{ e }}</p>
    }

    <h1 class="dc-field__title">{{ 'field.myVisits' | dcTranslate }}</h1>
    @for (v of store.open(); track v.visit.id) {
      <ng-container [ngTemplateOutlet]="card" [ngTemplateOutletContext]="{ $implicit: v }" />
    } @empty {
      @if (store.loaded()) {
        <p class="dc-field__empty">{{ 'field.none' | dcTranslate }}</p>
      }
    }

    @if (store.done().length) {
      <h2 class="dc-field__title">{{ 'field.done' | dcTranslate }}</h2>
      @for (v of store.done(); track v.visit.id) {
        <ng-container [ngTemplateOutlet]="card" [ngTemplateOutletContext]="{ $implicit: v }" />
      }
    }

    <ng-template #card let-v>
      <a class="dc-field__card" [routerLink]="['/visit', v.visit.id]" [attr.data-testid]="'field-visit-' + v.visit.number">
        <div class="dc-field__card-head">
          <strong>{{ { ar: v.type.nameAr, en: v.type.nameEn } | dcLocalize }}</strong>
          <span class="dc-field__chip" [attr.data-state]="state(v)">{{ 'field.state.' + state(v) | dcTranslate }}</span>
        </div>
        <span class="dc-field__party">{{ v.visit.partyName }}</span>
        @if (v.visit.siteAddress) {
          <span class="dc-field__muted"><mat-icon fontSet="material-symbols-outlined">location_on</mat-icon>{{ v.visit.siteAddress }}</span>
        }
        <span class="dc-field__muted" dir="ltr">
          <mat-icon fontSet="material-symbols-outlined">schedule</mat-icon>
          {{ v.visit.completedAt ?? v.visit.scheduledAt | date: 'EEE yyyy-MM-dd HH:mm' }} · {{ v.visit.number }}
        </span>
      </a>
    </ng-template>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 12px;
      max-width: 40rem;
      margin: 0 auto;
      padding: 12px 16px 32px;
    }
    .dc-field__status {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 6px 12px;
      border-radius: 8px;
      background: #ecfdf3;
      color: #085d3a;
      font-size: 14px;
    }
    .dc-field__status--offline {
      background: #fffaeb;
      color: #b54708;
    }
    .dc-field__status {
      flex-wrap: wrap;
    }
    .dc-field__status span:first-of-type {
      flex: 1 1 12rem;
    }
    .dc-field__waiting {
      font-weight: 600;
    }
    .dc-field__title {
      margin: 8px 0 0;
      font-size: 20px;
    }
    h2.dc-field__title {
      font-size: 17px;
      color: var(--dga-text-3, #384250);
    }
    .dc-field__empty {
      color: var(--dga-text-3, #384250);
    }
    .dc-field__card {
      display: grid;
      gap: 4px;
      padding: 14px 16px;
      border: 1px solid var(--dga-border, #d2d6db);
      border-radius: 12px;
      background: #fff;
      color: inherit;
      text-decoration: none;
    }
    .dc-field__card:active {
      background: var(--dga-gray-50, #f9fafb);
    }
    .dc-field__card-head {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .dc-field__card-head strong {
      flex: 1;
      font-size: 16px;
    }
    .dc-field__party {
      font-weight: 600;
    }
    .dc-field__muted {
      display: flex;
      align-items: center;
      gap: 4px;
      color: var(--dga-text-3, #384250);
      font-size: 14px;
    }
    .dc-field__muted mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .dc-field__chip {
      padding: 2px 10px;
      border-radius: 999px;
      font-size: 13px;
      font-weight: 600;
      background: #eff8ff;
      color: #1849a9;
    }
    .dc-field__chip[data-state='IN_PROGRESS'] {
      background: #fffaeb;
      color: #b54708;
    }
    .dc-field__chip[data-state='WAITING'] {
      background: #fef0c7;
      color: #93370d;
    }
    .dc-field__chip[data-state='REFUSED'] {
      background: #fef3f2;
      color: #b42318;
    }
    .dc-field__chip[data-state='COMPLETED'] {
      background: #ecfdf3;
      color: #085d3a;
    }
  `,
})
export class Home {
  protected readonly store = inject(FieldStore);
  private readonly i18n = inject(I18nService);

  constructor() {
    void this.store.start();
  }

  protected waitingText(): string {
    return this.i18n.translate('field.waiting').replace('{count}', String(this.store.outbox().length));
  }

  /** The visit as the inspector sees it: its status, or that its report waits to be sent (or was refused). */
  protected state(v: VisitDetail): string {
    const pending = this.store.pending(v.visit.id);
    if (pending) return pending.error ? 'REFUSED' : 'WAITING';
    return v.visit.status;
  }
}
