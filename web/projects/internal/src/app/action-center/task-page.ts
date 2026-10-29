import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import {
  ActionView,
  I18nService,
  LocalizePipe,
  RequestAnswers,
  UploadedFile,
  ServicesApi,
  openRequestFile,
  SlaChip,
  TaskDetail,
  TranslatePipe,
  WorkflowApi,
  localize,
  problemMessages,
} from '@dynamic-cube/shared';
import { Observable, catchError, firstValueFrom, of } from 'rxjs';
import { ConfirmData, ConfirmDialog } from '../apps-control/confirm-dialog';
import { AssignDialog } from './assign-dialog';
import { InstanceFactsCard } from './instance-facts';
import { WorkflowCard } from './workflow-card';

/** One task: the request's answers, what the employee may do now (claim, assign, act) and the workflow so far. */
@Component({
  selector: 'dc-task-page',
  imports: [
    FormsModule,
    InstanceFactsCard,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    RouterLink,
    LocalizePipe,
    RequestAnswers,
    SlaChip,
    TranslatePipe,
    WorkflowCard,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a routerLink="/action-center" class="dc-task__back">
      <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">arrow_forward</mat-icon>
      {{ 'ac.back' | dcTranslate }}
    </a>
    @if (detail(); as d) {
      <header class="dc-task__head">
        <div class="dc-task__title">
          <span class="dc-task__icon" aria-hidden="true"><mat-icon fontSet="material-symbols-outlined">assignment</mat-icon></span>
          <div>
            <h1>{{ { ar: d.task.nameAr, en: d.task.nameEn } | dcLocalize }}</h1>
            <div class="dc-task__meta">
              <span class="dc-task__chip mono" data-testid="task-subject">{{ d.task.subjectNumber }}</span>
              <span class="dc-task__chip">
                <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">design_services</mat-icon>
                {{ { ar: d.task.titleAr, en: d.task.titleEn } | dcLocalize }}
              </span>
              <span class="dc-task__chip">
                <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">apartment</mat-icon>
                {{ { ar: d.task.partyNameAr, en: d.task.partyNameEn } | dcLocalize }}
              </span>
              <span class="dc-task__chip">
                <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">person</mat-icon>
                <strong data-testid="task-assignee">{{
                  d.task.assignee
                    ? ({ ar: d.task.assigneeNameAr ?? d.task.assignee, en: d.task.assigneeNameEn ?? d.task.assignee } | dcLocalize)
                    : ('tasks.unassigned' | dcTranslate)
                }}</strong>
                @if (d.onBehalfOf) {
                  ({{ 'workflow.onBehalfOf' | dcTranslate }} {{ d.onBehalfOf }})
                }
              </span>
            </div>
          </div>
        </div>
        @if (d.task.status === 'OPEN') {
          <dc-sla-chip [sla]="d.task.sla" [dueAt]="d.task.dueAt" />
        } @else {
          <span class="dc-badge dc-badge--success" data-testid="task-status">{{ 'taskStatus.' + d.task.status | dcTranslate }}</span>
        }
      </header>

      @if (d.task.status === 'OPEN') {
        <div class="dc-task-actions" data-testid="task-actions">
          @if (d.permissions.canClaim) {
            <button matButton="filled" (click)="run(api.claim(d.task.id))" data-testid="claim">
              <mat-icon fontSet="material-symbols-outlined">front_hand</mat-icon>
              {{ 'tasks.claim' | dcTranslate }}
            </button>
          }
          @if (d.permissions.canAssign) {
            <button matButton="outlined" (click)="assign(d)" data-testid="assign">
              <mat-icon fontSet="material-symbols-outlined">person_add</mat-icon>
              {{ (d.task.assignee ? 'tasks.reassign' : 'tasks.assign') | dcTranslate }}
            </button>
          }
          @if (d.permissions.canRelease) {
            <button matButton="outlined" (click)="run(api.release(d.task.id))" data-testid="release">
              <mat-icon fontSet="material-symbols-outlined">undo</mat-icon>
              {{ 'tasks.release' | dcTranslate }}
            </button>
          }
          @if (d.permissions.canAct) {
            <span class="dc-task-actions__spacer"></span>
            @for (a of d.actions; track a.code) {
              <button
                [matButton]="a.kind === 'APPROVE' || a.kind === 'COMPLETE' ? 'filled' : 'outlined'"
                [class.dc-danger]="a.kind === 'REJECT'"
                (click)="act(d, a)"
                [attr.data-testid]="'act-' + a.code"
              >
                {{ { ar: a.nameAr, en: a.nameEn } | dcLocalize }}
              </button>
            }
          }
        </div>
        @if (d.task.assignee === null && !d.permissions.canClaim && !d.permissions.canAssign) {
          <p class="dc-muted">{{ 'tasks.waitingAssignment' | dcTranslate }}</p>
        }
      }
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li dir="auto">{{ e }}</li>
          }
        </ul>
      }

      <dc-instance-facts [facts]="d.instance.facts" />
      <div class="dc-request-grid">
        <section class="dc-card">
          <h2>{{ 'tasks.request' | dcTranslate }}</h2>
          @if (request.value(); as r) {
            <p>
              <a class="dc-link" [routerLink]="['/services/requests', d.task.subjectId]">{{ 'tasks.openRequest' | dcTranslate }}</a>
            </p>
            <dc-request-answers [version]="r.view.version" [data]="r.view.data" [files]="r.view.files" (openFile)="openFile(r.view.request.id, $event)" />
          } @else if (request.isLoading()) {
            <mat-progress-bar mode="indeterminate" />
          } @else {
            <p class="dc-muted">{{ 'tasks.requestHidden' | dcTranslate }}</p>
          }
        </section>
        <div class="dc-request-side">
          @if (d.permissions.canNote) {
            <section class="dc-card dc-notes">
              <h2>{{ 'tasks.note' | dcTranslate }}</h2>
              <mat-form-field subscriptSizing="dynamic">
                <textarea matInput rows="3" [(ngModel)]="note" [placeholder]="'tasks.noteHint' | dcTranslate" data-testid="note-text"></textarea>
              </mat-form-field>
              <button matButton="outlined" [disabled]="!note.trim()" (click)="addNote(d)" data-testid="add-note">
                {{ 'tasks.addNote' | dcTranslate }}
              </button>
            </section>
          }
          <dc-workflow-card [instance]="d.instance" [currentTask]="d.task.id" />
        </div>
      </div>
    } @else if (task.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (task.error()) {
      <p class="dc-errors" role="alert">{{ 'tasks.notFound' | dcTranslate }}</p>
    }
  `,
  styles: `
    .dc-task__back {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      margin-bottom: 8px;
      color: var(--dga-gray-600);
      font-size: 13px;
      font-weight: 600;
      text-decoration: none;
    }
    .dc-task__back:hover {
      color: var(--dynamic-cube-gold-700);
    }
    .dc-task__back mat-icon {
      width: 18px;
      height: 18px;
      font-size: 18px;
    }
    :host-context([dir='ltr']) .dc-task__back mat-icon {
      transform: scaleX(-1);
    }
    .dc-task__head {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 12px;
    }
    .dc-task__title {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      min-width: 0;
    }
    .dc-task__title h1 {
      margin: 0 0 6px;
      font-size: 22px;
      line-height: 1.3;
    }
    .dc-task__icon {
      display: grid;
      place-items: center;
      flex: none;
      width: 44px;
      height: 44px;
      border-radius: var(--shell-radius);
      background: var(--dynamic-cube-gold-50);
      color: var(--dynamic-cube-gold-700);
    }
    .dc-task__meta {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }
    .dc-task__chip {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      height: 26px;
      padding: 0 10px;
      border: 1px solid var(--dga-gray-200);
      border-radius: 9999px;
      background: #fff;
      color: var(--dga-gray-700);
      font-size: 13px;
    }
    .dc-task__chip mat-icon {
      width: 16px;
      height: 16px;
      font-size: 16px;
      color: var(--dga-gray-500);
    }
    /* What the employee may do now: a bar that stays in view while the request is read. */
    .dc-task-actions {
      position: sticky;
      top: 0;
      z-index: 2;
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
      margin-bottom: 12px;
      padding: 10px 12px;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius-lg);
      background: #fff;
      box-shadow: var(--shell-shadow-md);
    }
    .dc-task-actions__spacer {
      flex: 1;
    }
    .dc-request-grid {
      display: grid;
      grid-template-columns: minmax(0, 3fr) minmax(20rem, 2fr);
      gap: 1rem;
      align-items: start;
    }
    @media (max-width: 1100px) {
      .dc-request-grid {
        grid-template-columns: 1fr;
      }
    }
    .dc-request-grid h2 {
      margin-top: 0;
      font-size: 16px;
    }
    .dc-request-side {
      display: grid;
      gap: 1rem;
    }
    .dc-notes {
      display: grid;
      gap: 0.5rem;
      justify-items: start;
    }
    .dc-notes mat-form-field {
      width: 100%;
    }
    .dc-danger {
      --mat-button-outlined-label-text-color: var(--mat-sys-error);
      --mat-button-outlined-outline-color: var(--mat-sys-error);
    }
  `,
})
export class TaskPage {
  readonly id = input.required<string>();
  protected readonly api = inject(WorkflowApi);
  private readonly services = inject(ServicesApi);

  protected openFile(requestId: string, file: UploadedFile): void {
    void openRequestFile(this.services, requestId, file, true);
  }
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(I18nService);
  protected readonly task = rxResource({
    params: () => this.id(),
    stream: ({ params }) => this.api.task(params),
  });
  /** The latest state: from the last change, else as loaded. */
  private readonly updated = signal<TaskDetail | null>(null);
  protected readonly detail = computed(() => {
    const updated = this.updated();
    return updated?.task.id === this.id() ? updated : (this.task.value() ?? null);
  });
  protected readonly request = rxResource({
    params: () => {
      const task = this.task.value()?.task;
      return task?.subjectType === 'SERVICE_REQUEST' ? task.subjectId : undefined;
    },
    // The employee may work on the task without the right to browse requests: the answers are then not shown.
    stream: ({ params }) => this.services.requestDetail(params).pipe(catchError(() => of(undefined))),
  });
  protected readonly errors = signal<string[]>([]);
  protected note = '';

  protected async run(change: Observable<TaskDetail | null>, done?: string): Promise<boolean> {
    this.errors.set([]);
    try {
      const result = await firstValueFrom(change);
      if (done) {
        this.snackBar.open(this.i18n.translate(done), undefined, { duration: 4000 });
      }
      if (result) {
        this.updated.set(result);
      } else {
        // The task closed and is no longer ours to see.
        await this.router.navigate(['/action-center']);
      }
      return true;
    } catch (error) {
      this.errors.set(problemMessages(error));
      this.task.reload();
      this.updated.set(null);
      return false;
    }
  }

  protected async assign(d: TaskDetail): Promise<void> {
    const userName = await firstValueFrom(this.dialog.open(AssignDialog, { data: d.task.id, width: '30rem', maxWidth: '95vw' }).afterClosed());
    if (userName) {
      await this.run(this.api.assign(d.task.id, userName), 'tasks.assigned');
    }
  }

  protected async act(d: TaskDetail, action: ActionView): Promise<void> {
    const name = localize({ ar: action.nameAr, en: action.nameEn }, this.i18n.language());
    const data: ConfirmData = {
      title: name,
      message: this.i18n.translate(action.visibleToApplicant ? 'tasks.applicantWillSee' : 'tasks.confirmAct'),
      confirmLabel: name,
      comment: true,
      commentRequired: action.commentRequired,
      danger: action.kind === 'REJECT',
    };
    const comment = await firstValueFrom(this.dialog.open<ConfirmDialog, ConfirmData, string>(ConfirmDialog, { data }).afterClosed());
    if (comment !== undefined) {
      await this.run(this.api.act(d.task.id, action.code, comment || null), 'tasks.done');
    }
  }

  protected async addNote(d: TaskDetail): Promise<void> {
    if (await this.run(this.api.note(d.task.id, this.note.trim()))) {
      this.note = '';
    }
  }
}
