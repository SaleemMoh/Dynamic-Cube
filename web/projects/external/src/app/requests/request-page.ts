import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import {
  BillCard,
  DraftAutosave,
  EMPTY_READINESS,
  VisitCard,
  FeeTable,
  FieldError,
  ItemResults,
  LinkedRequests,
  LiveResultFeed,
  LocalizePipe,
  PanelReadiness,
  RequestAnswers,
  RequestFileInfo,
  UploadedFile,
  RequestView,
  ServicePanel,
  ServiceWizard,
  ServicesApi,
  TranslatePipe,
  WizardDraft,
  WizardMove,
  computeReadiness,
  hasPanel,
  openRequestFile,
  requestErrors,
  requestFileUploader,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { RequestDocuments } from '../documents/request-documents';
import { ContactCard } from '../portal/contact-card';
import { confirmWithNafath } from './nafath-confirm';

/** Statuses in which the applicant edits the answers. */
const EDITABLE = ['DRAFT', 'PENDING_CUSTOMER'];

/**
 * A request: the wizard while it is a draft (saved at every step and a moment after each change), then its answers, fees,
 * status and history. A request returned for correction shows the reason and opens in the wizard again, to be resubmitted.
 */
@Component({
  selector: 'dc-request-page',
  imports: [
    BillCard,
    VisitCard,
    ContactCard,
    RequestDocuments,
    DatePipe,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    RouterLink,
    FeeTable,
    ItemResults,
    LinkedRequests,
    LocalizePipe,
    PanelReadiness,
    RequestAnswers,
    ServicePanel,
    ServiceWizard,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-request">
      <div class="dc-request__main">
        @if (view(); as v) {
          <ol class="dc-crumbs" [attr.aria-label]="'portal.breadcrumb' | dcTranslate">
            <li>
              <a routerLink="/requests">{{ 'requests.title' | dcTranslate }}</a>
            </li>
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
              >{{ 'displayStatus.' + v.request.displayStatus | dcTranslate }}</span
            >
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
          <dc-linked-requests [links]="v.links ?? []" />
          @if (v.itemResults?.length) {
            <div class="dc-card">
              <dc-item-results [items]="v.itemResults ?? []" />
            </div>
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
            @if (panel()?.readiness?.enabled) {
              <dc-panel-readiness [version]="v.version" [readiness]="readiness()" [canOpen]="!busy()" (openStep)="openStep(v, $event)" />
            }
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
              [uploader]="uploader"
              [integrations]="v.integrations ?? {}"
              [changes]="v.changes ?? []"
              (moved)="move(v, $event)"
              (edited)="edited($event)"
              (submitted)="submit(v, $event)"
            />
            @if (v.request.status === 'DRAFT' && v.bundle?.length) {
              <section class="dc-card dc-request__bundle" data-testid="bundle">
                <h2>{{ 'portal.bundle' | dcTranslate }}</h2>
                <p class="dc-muted">{{ 'portal.bundleHint' | dcTranslate }}</p>
                <ul>
                  @for (b of v.bundle; track b.service) {
                    <li [attr.data-bundle]="b.service">
                      <label>
                        <input
                          type="checkbox"
                          [checked]="b.required || bundled().includes(b.service)"
                          [disabled]="b.required || !b.isEligible || busy()"
                          (change)="toggleBundle(b.service, $any($event.target).checked)"
                        />
                        {{ { ar: b.nameAr, en: b.nameEn } | dcLocalize }}
                      </label>
                      @if (b.required) {
                        <span class="dc-badge">{{ 'portal.bundleRequired' | dcTranslate }}</span>
                      }
                      @if (b.noteAr || b.noteEn) {
                        <p class="dc-muted" dir="auto">{{ { ar: b.noteAr ?? '', en: b.noteEn ?? '' } | dcLocalize }}</p>
                      }
                      @if (!b.isEligible) {
                        <p class="dc-muted">{{ 'portal.bundleNotOpen' | dcTranslate }}</p>
                        <ul class="dc-request__bundle-reasons">
                          @for (r of b.reasons; track r.code) {
                            <li dir="auto">{{ { ar: r.messageAr, en: r.messageEn } | dcLocalize }}</li>
                          }
                        </ul>
                      }
                    </li>
                  }
                </ul>
              </section>
            }
            <div class="dc-request__secondary">
              <span class="dc-request__saved" [class.dc-request__saved--failed]="autosave.failed()" data-testid="autosave-status" aria-live="polite">
                @if (autosave.saving()) {
                  <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">sync</mat-icon>
                  {{ 'requests.autosaving' | dcTranslate }}
                } @else if (autosave.failed()) {
                  <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">cloud_off</mat-icon>
                  {{ 'requests.autosaveFailed' | dcTranslate }}
                } @else if (autosave.savedAt(); as at) {
                  <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">cloud_done</mat-icon>
                  {{ 'requests.autosaved' | dcTranslate }} <span dir="ltr">{{ at | date: 'shortTime' }}</span>
                } @else {
                  {{ 'requests.savedAsDraft' | dcTranslate }}
                }
              </span>
              @if (v.request.status === 'DRAFT') {
                <button matButton type="button" class="dc-secondary-button" (click)="cancel(v)" [disabled]="busy()" data-testid="cancel-draft">
                  {{ 'requests.cancelDraft' | dcTranslate }}
                </button>
              }
            </div>
          } @else {
            @if (justSubmitted()) {
              <div class="dc-card dc-request__done" role="status" data-testid="submitted-banner">
                <mat-icon fontSet="material-symbols-outlined">task_alt</mat-icon>
                <div>
                  <strong>{{ (justSubmitted() === 'RESUBMITTED' ? 'requests.resubmitted' : 'requests.submitted') | dcTranslate }}</strong>
                  <p>
                    {{ 'requests.number' | dcTranslate }} <span dir="ltr">{{ v.request.number }}</span>
                  </p>
                </div>
              </div>
            }
            <section class="dc-card">
              <dc-request-answers [version]="v.version" [data]="v.data" [files]="v.files" (openFile)="openFile(v, $event)" />
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
      <div class="dc-request__side">
        @if (panel(); as p) {
          @if (view(); as v) {
            <dc-service-panel
              [panel]="p"
              [version]="v.version"
              [service]="{ ar: v.request.serviceNameAr, en: v.request.serviceNameEn }"
              [data]="onScreen().data"
              [levelCode]="onScreen().levelCode"
              [step]="v.currentStep"
              [number]="v.request.number"
              [readiness]="readiness()"
              [fees]="v.fees"
              [files]="uploader.files()"
              [errors]="errors()"
              [live]="live.result()"
              [liveLoading]="live.loading()"
              [liveError]="live.error()"
            />
          }
        }
        <dc-contact-card />
      </div>
    </div>
  `,
  styles: `
    .dc-request {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 24rem;
      gap: 24px 36px;
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
      display: grid;
      gap: 16px;
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
      font-size: 30px;
      font-weight: 700;
      line-height: 40px;
    }
    .dc-request__saved {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      color: var(--dga-gray-500, #667085);
      font-size: 13px;
    }
    .dc-request__saved mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .dc-request__saved--failed {
      color: var(--dga-error-600, #d92d20);
    }
    .dc-request__bundle h2 {
      margin: 0 0 4px;
      font-size: 16px;
    }
    .dc-request__bundle > ul {
      display: grid;
      gap: 10px;
      margin: 8px 0 0;
      padding: 0;
      list-style: none;
    }
    .dc-request__bundle label {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      font-weight: 500;
    }
    .dc-request__bundle p {
      margin: 2px 0 0;
    }
    .dc-request__bundle-reasons {
      margin: 2px 0 0;
      padding-inline-start: 20px;
      font-size: 13px;
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
      font-size: 32px;
      width: 40px;
      height: 32px;
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
      font-size: 30px;
      width: 36px;
      height: 28px;
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
  private readonly dialog = inject(MatDialog);
  /** The Nafath confirmation given on this page, used again if the submit fails for another reason (it counts for 15 minutes). */
  private nafathCheckId: string | null = null;
  protected readonly view = signal<RequestView | null>(null);
  /** The request's files (answers of file questions); uploads add to them. */
  private readonly files = signal<RequestFileInfo[]>([]);
  protected readonly uploader = requestFileUploader(this.api, () => this.view()!.request.id, this.files);
  private readonly syncFiles = effect(() => this.files.set(this.view()?.files ?? []));
  protected readonly errors = signal<FieldError[]>([]);
  protected readonly loadErrors = signal<FieldError[]>([]);
  protected readonly busy = signal(false);
  /** Set right after sending: SUBMITTED or RESUBMITTED. */
  protected readonly justSubmitted = signal<string | null>(null);
  /** The optional bundled services the applicant ticked (3f); required ones are always sent by the server. */
  protected readonly bundled = signal<string[]>([]);
  /** The answers and level as the applicant has them on screen (typed, not yet saved); null: as saved. */
  private readonly typed = signal<WizardDraft | null>(null);
  protected readonly onScreen = computed<WizardDraft>(() => {
    const v = this.view();
    return this.typed() ?? { data: v?.data ?? {}, levelCode: v?.levelCode ?? null };
  });
  /** The panel beside the form (ADR-0040), while the applicant edits the request and the version has one. */
  protected readonly panel = computed(() => {
    const v = this.view();
    const panel = v?.version.definition.panel;
    return v && EDITABLE.includes(v.request.status) && hasPanel(panel) ? panel : null;
  });
  protected readonly readiness = computed(() => {
    const v = this.view();
    return v ? computeReadiness({ version: v.version, data: this.onScreen().data, levelCode: this.onScreen().levelCode, integrations: v.integrations }) : EMPTY_READINESS;
  });
  /** The live result: the service's decision step on the answers on screen, a moment after they change (not recorded). */
  protected readonly live = new LiveResultFeed((data, levelCode) => this.api.liveResult(this.view()!.request.id, data, levelCode));
  /**
   * Saves the answers a moment after each change (the step does not move and nothing is checked), so closing the page by
   * accident loses nothing; when the page is hidden or left, the waiting change is saved at once.
   */
  protected readonly autosave = new DraftAutosave<WizardDraft>(async (draft, leaving) => {
    const v = this.view();
    if (!v || !EDITABLE.includes(v.request.status)) return;
    await firstValueFrom(this.api.save(v.request.id, { data: draft.data, levelCode: draft.levelCode }, { keepalive: leaving }));
  });

  constructor() {
    effect(() => void this.load(this.id()));
    // A new view (opened, moved, submitted) holds the saved answers: what was typed is in it, and the live result is read again.
    effect(() => {
      const v = this.view();
      untracked(() => {
        this.typed.set(null);
        if (v && this.panel()?.result.enabled) void this.live.now(null, null);
        else this.live.stop();
      });
    });
    const hidden = () => {
      if (document.visibilityState === 'hidden') void this.autosave.flush(true);
    };
    document.addEventListener('visibilitychange', hidden);
    window.addEventListener('pagehide', hidden);
    inject(DestroyRef).onDestroy(() => {
      document.removeEventListener('visibilitychange', hidden);
      window.removeEventListener('pagehide', hidden);
      void this.autosave.flush(true);
      this.live.stop();
    });
  }

  protected async move(v: RequestView, move: WizardMove): Promise<void> {
    // The move saves the same answers: a change waiting to be saved is dropped, and a save already running finishes first.
    this.autosave.discard();
    await this.run(async () => {
      const saved = await this.autosave.exclusive(() =>
        firstValueFrom(
          this.api.save(v.request.id, { data: move.data, levelCode: move.levelCode, currentStep: move.to, ...(move.forward ? { validateStep: move.from } : {}) }),
        ),
      );
      this.view.set(saved);
      this.errors.set(saved.stepErrors);
      window.scrollTo({ top: 0 });
    });
  }

  /** An answer or the level changed on screen: saved a moment later, and the panel follows at once (the live result after a pause). */
  protected edited(draft: WizardDraft): void {
    this.autosave.change(draft);
    this.typed.set(draft);
    if (this.panel()?.result.enabled) this.live.change(draft.data, draft.levelCode);
  }

  /**
   * A readiness group opens its step: going back keeps what was typed; going on checks the step being left first, as the
   * wizard's own Next does (the steps in between are checked again when the request is submitted).
   */
  protected openStep(v: RequestView, step: string): void {
    const steps = v.version.definition.steps.map((s) => s.code);
    const from = v.currentStep ?? steps[0];
    if (step === from) {
      document.querySelector('dc-service-wizard')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      return;
    }
    const draft = this.onScreen();
    void this.move(v, { data: draft.data, levelCode: draft.levelCode, from, to: step, forward: steps.indexOf(step) > steps.indexOf(from) });
  }

  /**
   * Submits the request. A version that asks for a Nafath confirmation (ADR-0043) is checked by the server first; only when
   * the confirmation is all that is missing does the applicant confirm in their Nafath app, and the request is sent again.
   */
  protected async submit(v: RequestView, declarationAccepted: boolean): Promise<void> {
    this.autosave.discard();
    const nafath = v.version.definition.confirmWithNafath === true && v.request.status === 'DRAFT';
    await this.run(async () => {
      await this.autosave.flush();
      let submitted: RequestView;
      try {
        submitted = await firstValueFrom(this.api.submit(v.request.id, declarationAccepted, nafath ? this.nafathCheckId : null, this.bundled()));
      } catch (error) {
        const errors = requestErrors(error);
        if (!nafath || !errors.some((e) => e.field === 'nafath')) throw error;
        if (errors.length > 1) {
          this.errors.set(errors.filter((e) => e.field !== 'nafath'));
          return;
        }
        const checkId = await firstValueFrom(confirmWithNafath(this.dialog, v.request.id));
        if (!checkId) return;
        this.nafathCheckId = checkId;
        submitted = await firstValueFrom(this.api.submit(v.request.id, declarationAccepted, checkId, this.bundled()));
      }
      this.view.set(submitted);
      this.justSubmitted.set(v.request.status === 'PENDING_CUSTOMER' ? 'RESUBMITTED' : 'SUBMITTED');
      window.scrollTo({ top: 0 });
    });
  }

  protected toggleBundle(service: string, ticked: boolean): void {
    this.bundled.update((codes) => (ticked ? [...codes.filter((c) => c !== service), service] : codes.filter((c) => c !== service)));
  }

  protected openFile(v: RequestView, file: UploadedFile): void {
    void openRequestFile(this.api, v.request.id, file);
  }

  protected async cancel(v: RequestView): Promise<void> {
    this.autosave.discard();
    await this.run(async () => {
      await this.autosave.flush();
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
