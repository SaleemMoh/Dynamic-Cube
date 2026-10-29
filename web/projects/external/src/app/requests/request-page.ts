import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { BillCard, VisitCard, FeeTable, FieldError, LocalizePipe, RequestAnswers, RequestView, ServiceWizard, ServicesApi, TranslatePipe, WizardMove, requestErrors } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { RequestDocuments } from '../documents/request-documents';
import { ContactCard } from '../portal/contact-card';

/**
 * A request: the wizard while it is a draft (saved at every step), then its answers, fees, status and history. A request
 * returned for correction shows the reason and opens in the wizard again, to be resubmitted.
 */
@Component({
  selector: 'dc-request-page',
  imports: [BillCard, VisitCard, ContactCard, RequestDocuments, DatePipe, MatButtonModule, MatIconModule, MatProgressBarModule, RouterLink, FeeTable, LocalizePipe, RequestAnswers, ServiceWizard, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-request">
    <div class="dc-request__main">
    @if (view(); as v) {
      <ol class="dc-crumbs" [attr.aria-label]="'portal.breadcrumb' | dcTranslate">
        <li><a routerLink="/requests">{{ 'requests.title' | dcTranslate }}</a></li>
        <li aria-current="page">{{ { ar: v.request.serviceNameAr, en: v.request.serviceNameEn } | dcLocalize }}</li>
      </ol>
      <header class="dc-request__header">
        <h1>{{ { ar: v.request.serviceNameAr, en: v.request.serviceNameEn } | dcLocalize }}</h1>
        <span
          class="dc-badge"
          [class.dc-badge--success]="v.request.status === 'APPROVED'"
          [class.dc-badge--danger]="v.request.status === 'REJECTED'"
          [class.dc-badge--warning]="v.request.status === 'PENDING_CUSTOMER' || v.request.status === 'PENDING_PAYMENT'"
          [class.dc-badge--info]="v.request.status === 'SUBMITTED' || v.request.status === 'IN_PROGRESS'"
          data-testid="request-status"
          >{{ 'displayStatus.' + v.request.displayStatus | dcTranslate }}</span>
      </header>
      <p class="dc-muted">
        {{ { ar: v.request.beneficiaryNameAr, en: v.request.beneficiaryNameEn } | dcLocalize }}
        @if (v.request.number) {
          · <span dir="ltr" data-testid="request-number">{{ v.request.number }}</span>
        }
        @if (v.request.sourceDocumentNumber) {
          · {{ 'followUps.document' | dcTranslate }} <span dir="ltr" data-testid="request-document">{{ v.request.sourceDocumentNumber }}</span>
        }
        @if (v.request.status === 'DRAFT' && v.request.draftExpiresAt) {
          · {{ 'requests.expires' | dcTranslate }} <span dir="ltr">{{ v.request.draftExpiresAt | date: 'yyyy-MM-dd' }}</span>
        }
      </p>

      @for (bill of v.bills ?? []; track bill.id) {
        <dc-bill-card [bill]="bill" />
      }
      @for (visit of v.visits ?? []; track visit.id) {
        <dc-visit-card [visit]="visit" />
      }
      @if (v.request.status === 'PENDING_CUSTOMER') {
        <div class="dc-card dc-request__returned" role="status" data-testid="returned-banner">
          <mat-icon fontSet="material-symbols-outlined">edit_note</mat-icon>
          <div>
            <strong>{{ 'requests.returned' | dcTranslate }}</strong>
            @if (returnReason(v); as reason) {
              <p dir="auto" class="dc-request__reason" data-testid="return-reason">{{ reason }}</p>
            }
            <p class="dc-muted">{{ 'requests.returnedHint' | dcTranslate }}</p>
          </div>
        </div>
      }
      @if (v.request.status === 'DRAFT' || v.request.status === 'PENDING_CUSTOMER') {
        <dc-service-wizard
          [version]="v.version"
          [data]="v.data"
          [levelCode]="v.levelCode"
          [currentStep]="v.currentStep"
          [fees]="v.fees"
          [feesByLevel]="v.feesByLevel"
          [errors]="errors()"
          [busy]="busy()"
          [resubmission]="v.request.status === 'PENDING_CUSTOMER'"
          (moved)="move(v, $event)"
          (submitted)="submit(v, $event)"
        />
        @if (v.request.status === 'DRAFT') {
          <div class="dc-request__secondary">
            <span class="dc-muted">{{ 'requests.savedAsDraft' | dcTranslate }}</span>
            <button matButton type="button" class="dc-secondary-button" (click)="cancel(v)" [disabled]="busy()" data-testid="cancel-draft">
              {{ 'requests.cancelDraft' | dcTranslate }}
            </button>
          </div>
        }
      } @else {
        @if (justSubmitted()) {
          <div class="dc-card dc-request__done" role="status" data-testid="submitted-banner">
            <mat-icon fontSet="material-symbols-outlined">task_alt</mat-icon>
            <div>
              <strong>{{ (justSubmitted() === 'RESUBMITTED' ? 'requests.resubmitted' : 'requests.submitted') | dcTranslate }}</strong>
              <p>{{ 'requests.number' | dcTranslate }} <span dir="ltr">{{ v.request.number }}</span></p>
            </div>
          </div>
        }
        <section class="dc-card">
          <dc-request-answers [version]="v.version" [data]="v.data" />
        </section>
        <section class="dc-card">
          <h2>{{ 'fees.grandTotal' | dcTranslate }}</h2>
          <dc-fee-table [lines]="v.fees" />
        </section>
      }
      @if (v.request.status !== 'DRAFT') {
        <dc-request-documents [requestId]="v.request.id" />
        <section class="dc-card">
          <h2>{{ 'requests.history' | dcTranslate }}</h2>
          <ol class="dc-history" data-testid="request-history">
            @for (e of v.timeline; track $index) {
              <li>
                <strong>{{ 'event.' + e.type | dcTranslate }}</strong>
                <span class="dc-muted" dir="ltr">{{ e.at | date: 'yyyy-MM-dd HH:mm' }}</span>
                @if (e.comment) {
                  <span dir="auto" class="dc-history__comment">{{ e.comment }}</span>
                }
              </li>
            }
          </ol>
        </section>
      }
    } @else if (loadErrors().length) {
      <p class="dc-errors" role="alert">{{ { ar: loadErrors()[0].messageAr, en: loadErrors()[0].messageEn } | dcLocalize }}</p>
    } @else {
      <mat-progress-bar mode="indeterminate" />
    }
    </div>
    <dc-contact-card class="dc-request__side" />
    </div>
  `,
  styles: `
    .dc-request {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 24rem;
      gap: 32px 48px;
      align-items: start;
      padding-top: 16px;
    }
    .dc-request__main {
      display: flex;
      flex-direction: column;
      gap: 16px;
      min-width: 0;
    }
    .dc-request__main > .dc-crumbs {
      margin-bottom: -8px;
    }
    .dc-request__side {
      position: sticky;
      top: 16px;
    }
    @media (max-width: 64rem) {
      .dc-request {
        grid-template-columns: minmax(0, 1fr);
      }
      .dc-request__side {
        position: static;
      }
    }
    .dc-request__header {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 1rem;
    }
    .dc-request__header h1 {
      margin: 0;
      font-size: 36px;
      font-weight: 700;
      line-height: 48px;
    }
    .dc-request__secondary {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      margin-top: 1rem;
    }
    .dc-request__done {
      display: flex;
      gap: 1rem;
      align-items: center;
      margin-bottom: 1rem;
      border-color: var(--dga-primary, #1b8354);
    }
    .dc-request__done mat-icon {
      color: var(--dga-primary, #1b8354);
      font-size: 40px;
      width: 40px;
      height: 40px;
    }
    .dc-card + .dc-card {
      margin-top: 1rem;
    }
    .dc-request__returned {
      display: flex;
      gap: 1rem;
      align-items: flex-start;
      margin-bottom: 1rem;
      border-color: #dc6803;
      background: #fffaeb;
    }
    .dc-request__returned mat-icon {
      color: #b54708;
      font-size: 36px;
      width: 36px;
      height: 36px;
    }
    .dc-request__returned p {
      margin: 0.25rem 0 0;
    }
    .dc-request__reason {
      white-space: pre-line;
      font-weight: 500;
    }
    .dc-history {
      display: grid;
      gap: 0.75rem;
      margin: 0;
      padding-inline-start: 1.25rem;
    }
    .dc-history li {
      display: grid;
      gap: 0.125rem;
    }
    .dc-history li > * {
      justify-self: start;
    }
    .dc-history__comment {
      white-space: pre-line;
    }
  `,
})
export class RequestPage {
  readonly id = input.required<string>();
  private readonly api = inject(ServicesApi);
  private readonly router = inject(Router);
  protected readonly view = signal<RequestView | null>(null);
  protected readonly errors = signal<FieldError[]>([]);
  protected readonly loadErrors = signal<FieldError[]>([]);
  protected readonly busy = signal(false);
  /** Set right after sending: SUBMITTED or RESUBMITTED. */
  protected readonly justSubmitted = signal<string | null>(null);

  constructor() {
    effect(() => void this.load(this.id()));
  }

  protected async move(v: RequestView, move: WizardMove): Promise<void> {
    await this.run(async () => {
      const saved = await firstValueFrom(
        this.api.save(v.request.id, { data: move.data, levelCode: move.levelCode, currentStep: move.to, ...(move.forward ? { validateStep: move.from } : {}) }),
      );
      this.view.set(saved);
      this.errors.set(saved.stepErrors);
      window.scrollTo({ top: 0 });
    });
  }

  protected async submit(v: RequestView, declarationAccepted: boolean): Promise<void> {
    await this.run(async () => {
      this.view.set(await firstValueFrom(this.api.submit(v.request.id, declarationAccepted)));
      this.justSubmitted.set(v.request.status === 'PENDING_CUSTOMER' ? 'RESUBMITTED' : 'SUBMITTED');
      window.scrollTo({ top: 0 });
    });
  }

  protected async cancel(v: RequestView): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.cancel(v.request.id));
      await this.router.navigate(['/requests']);
    });
  }

  /** Why the request came back: the comment of the latest return. */
  protected returnReason(v: RequestView): string | null {
    return [...v.timeline].reverse().find((e) => e.type === 'RETURNED')?.comment ?? null;
  }

  private async load(id: string): Promise<void> {
    try {
      this.view.set(await firstValueFrom(this.api.request(id)));
    } catch (error) {
      this.loadErrors.set(requestErrors(error));
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.errors.set([]);
    try {
      await action();
    } catch (error) {
      this.errors.set(requestErrors(error));
    } finally {
      this.busy.set(false);
    }
  }
}
