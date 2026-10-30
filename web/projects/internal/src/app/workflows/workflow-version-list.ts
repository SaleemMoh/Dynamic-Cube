import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { AppsControlApi, BusinessAppContext, ConfigRow, LocalizePipe, problemMessages, TranslatePipe } from '@dynamic-cube/shared';
import { DraftsService } from '../apps-control/drafts.service';
import { PrivilegesService } from '../apps-control/privileges.service';
import { GraphDefinition, WorkflowGraph } from './workflow-graph';
import { SubmitButton } from '../apps-control/submit-button';

const STARTER = {
  slaWorkingDays: 10,
  nodes: [
    { code: 'START', type: 'START', nameAr: 'البداية', nameEn: 'Start', next: 'REVIEW' },
    {
      code: 'REVIEW',
      type: 'HUMAN_TASK',
      nameAr: 'المراجعة',
      nameEn: 'Review',
      assignment: { target: 'ROLE', role: 'REVIEWER', mode: 'QUEUE' },
      sla: { workingDays: 5 },
      actions: [
        { code: 'APPROVE', kind: 'APPROVE', nameAr: 'اعتماد', nameEn: 'Approve', to: 'APPROVED' },
        { code: 'REJECT', kind: 'REJECT', nameAr: 'رفض', nameEn: 'Reject', to: 'REJECTED' },
        {
          code: 'RETURN',
          kind: 'RETURN',
          nameAr: 'إعادة للتعديل',
          nameEn: 'Return for correction',
        },
      ],
    },
    { code: 'APPROVED', type: 'END', nameAr: 'معتمد', nameEn: 'Approved', outcome: 'APPROVED' },
    { code: 'REJECTED', type: 'END', nameAr: 'مرفوض', nameEn: 'Rejected', outcome: 'REJECTED' },
  ],
};

/** A published workflow version's graph, read-only. */
@Component({
  selector: 'dc-workflow-graph-dialog',
  imports: [MatButtonModule, MatDialogModule, TranslatePipe, WorkflowGraph],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title dir="ltr">{{ data.code }}</h2>
    <mat-dialog-content><dc-workflow-graph [definition]="data.definition" /></mat-dialog-content>
    <mat-dialog-actions align="end"
      ><button matButton mat-dialog-close>
        {{ 'common.close' | dcTranslate }}
      </button></mat-dialog-actions
    >
  `,
})
export class WorkflowGraphDialog {
  protected readonly data = inject<{ code: string; definition: GraphDefinition }>(MAT_DIALOG_DATA);
}

/**
 * Workflow versions. A new version starts from the latest one's graph (JSON, drawn as you go), is checked by the server
 * when added to a change set and never changes once published: requests stay on the version they started on.
 */
@Component({
  selector: 'dc-workflow-version-list',
  imports: [
    SubmitButton,
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
    RouterLink,
    LocalizePipe,
    TranslatePipe,
    WorkflowGraph,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header dc-versions__header">
      <h1>{{ 'wfVersions.title' | dcTranslate }}</h1>
      <a matButton="filled" routerLink="/workflows/designer" [queryParams]="{ workflowCode: workflow() ?? undefined }" data-testid="open-designer">
        <mat-icon fontSet="material-symbols-outlined">design_services</mat-icon>
        {{ 'designer.open' | dcTranslate }}
      </a>
    </header>
    <p class="dc-muted">{{ 'wfVersions.hint' | dcTranslate }}</p>

    @if (canCreate()) {
      <section class="dc-card dc-authoring">
        <h2>{{ 'versions.new' | dcTranslate }}</h2>
        <label class="dc-field-label" for="dc-wf-version-workflow">{{ 'wfVersions.workflow' | dcTranslate }}</label>
        <mat-form-field subscriptSizing="dynamic">
          <mat-select id="dc-wf-version-workflow" [ngModel]="workflow()" (ngModelChange)="chooseWorkflow($event)" data-testid="wf-version-workflow">
            @for (w of workflows.value() ?? []; track w.code) {
              <mat-option [value]="w.code"
                >{{ { ar: w.nameAr, en: w.nameEn } | dcLocalize }} — {{ 'wfVersions.service' | dcTranslate }} {{ w['serviceCode'] ?? '—' }}</mat-option
              >
            }
          </mat-select>
        </mat-form-field>
        @if (workflow()) {
          <p dir="ltr" class="dc-code">{{ workflow() }}/{{ nextNumber() }}</p>
          <div class="dc-authoring__split">
            <div>
              <label class="dc-field-label" for="dc-wf-version-json">{{ 'wfVersions.definition' | dcTranslate }}</label>
              <mat-form-field subscriptSizing="dynamic">
                <textarea
                  id="dc-wf-version-json"
                  matInput
                  rows="24"
                  dir="ltr"
                  class="dc-json"
                  [ngModel]="text()"
                  (ngModelChange)="text.set($event)"
                  data-testid="wf-version-definition"
                ></textarea>
              </mat-form-field>
            </div>
            <div>
              <span class="dc-field-label">{{ 'wfVersions.graph' | dcTranslate }}</span>
              @if (parsed(); as p) {
                <dc-workflow-graph [definition]="p" />
              } @else {
                <p class="dc-errors" role="alert">{{ 'wfVersions.invalidJson' | dcTranslate }}</p>
              }
            </div>
          </div>
          @if (errors().length) {
            <ul class="dc-errors" role="alert">
              @for (e of errors(); track $index) {
                <li dir="ltr">{{ e }}</li>
              }
            </ul>
          }
          <div class="dc-authoring__actions">
            <dc-submit-button [entityType]="'wf.workflow-version'" [disabled]="!parsed()" (submitted)="addVersion(true)" />
            <button matButton="filled" [disabled]="!parsed()" (click)="addVersion()" data-testid="add-wf-version">
              <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
              {{ drafts.saveLabel('wf.workflow-version') | dcTranslate }}
            </button>
          </div>
        }
      </section>
    }

    <table mat-table [dataSource]="rows()" class="dc-table">
      <ng-container matColumnDef="code">
        <th mat-header-cell *matHeaderCellDef>{{ 'versions.version' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr" class="dc-code">{{ row.code }}</td>
      </ng-container>
      <ng-container matColumnDef="name">
        <th mat-header-cell *matHeaderCellDef>{{ 'config.name' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row">
          {{ { ar: row.nameAr, en: row.nameEn } | dcLocalize }}
        </td>
      </ng-container>
      <ng-container matColumnDef="published">
        <th mat-header-cell *matHeaderCellDef>{{ 'versions.published' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr">
          {{ row['publishedAt'] | date: 'yyyy-MM-dd HH:mm' }}
        </td>
      </ng-container>
      <ng-container matColumnDef="state">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let row">
          @if (row['isRetired']) {
            <span class="dc-badge">{{ 'versions.retired' | dcTranslate }}</span>
          }
        </td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let row" class="dc-table__actions">
          <button matIconButton (click)="showGraph(row)" [matTooltip]="'wfVersions.graph' | dcTranslate" [attr.data-testid]="'graph-' + row.code">
            <mat-icon fontSet="material-symbols-outlined">account_tree</mat-icon>
          </button>
          <a
            matIconButton
            routerLink="/workflows/designer"
            [queryParams]="{
              workflowCode: row.code.split('/')[0],
              version: row.code.split('/')[1],
            }"
            [matTooltip]="'designer.openFrom' | dcTranslate"
            [attr.data-testid]="'design-' + row.code"
          >
            <mat-icon fontSet="material-symbols-outlined">design_services</mat-icon>
          </a>
          @if (canEdit() && !row['isRetired']) {
            <button matIconButton (click)="retire(row)" [matTooltip]="'versions.retire' | dcTranslate">
              <mat-icon fontSet="material-symbols-outlined">archive</mat-icon>
            </button>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
  `,
  styles: `
    .dc-versions__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
    }
    .dc-authoring {
      display: grid;
      gap: 0.25rem;
      margin-bottom: 1.5rem;
    }
    .dc-authoring h2 {
      margin-top: 0;
    }
    .dc-authoring__split {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
      gap: 1rem;
    }
    .dc-authoring__split mat-form-field {
      width: 100%;
    }
    @media (max-width: 1100px) {
      .dc-authoring__split {
        grid-template-columns: 1fr;
      }
    }
    .dc-json {
      font-family: ui-monospace, monospace;
      font-size: 13px;
    }
    .dc-authoring__actions {
      display: flex;
      justify-content: flex-end;
      margin-top: 0.5rem;
    }
  `,
})
export class WorkflowVersionList {
  private readonly api = inject(AppsControlApi);
  private readonly dialog = inject(MatDialog);
  protected readonly drafts = inject(DraftsService);
  private readonly privileges = inject(PrivilegesService);
  protected readonly columns = ['code', 'name', 'published', 'state', 'actions'];
  private readonly appContext = inject(BusinessAppContext);
  protected readonly workflows = rxResource({ params: () => ({ app: this.appContext.current() }), stream: ({ params }) => this.api.list('wf.workflow', null, params.app) });
  private readonly versions = rxResource({ stream: () => this.api.list('wf.workflow-version') });
  private readonly allRows = computed(() => this.versions.value() ?? []);
  /** The chosen workflow's versions, or every version until one is chosen. */
  protected readonly rows = computed(() => {
    const chosen = this.workflow();
    return chosen ? this.allRows().filter((r) => r.code.startsWith(chosen + '/')) : this.allRows();
  });
  protected readonly workflow = signal<string | null>(null);
  protected readonly text = signal('');
  protected readonly errors = signal<string[]>([]);
  protected readonly canCreate = computed(() => this.privileges.can('workflows.config', 'CREATE'));
  protected readonly canEdit = computed(() => this.privileges.can('workflows.config', 'EDIT'));

  /** The definition being written, when it is JSON with a node list. */
  protected readonly parsed = computed<GraphDefinition | null>(() => {
    try {
      const value = JSON.parse(this.text()) as GraphDefinition;
      return Array.isArray(value?.nodes) ? value : null;
    } catch {
      return null;
    }
  });

  protected readonly nextNumber = computed(() => {
    const prefix = `${this.workflow()}/`;
    return (
      1 +
      Math.max(
        0,
        ...this.allRows()
          .filter((r) => r.code.startsWith(prefix))
          .map((r) => Number(r.code.slice(prefix.length))),
      )
    );
  });

  /** From the services list: the service whose workflow to open (query parameter). */
  readonly service = input<string | undefined>();
  /** From the workflows list: the workflow to open (query parameter). */
  readonly workflowCode = input<string | undefined>();

  constructor() {
    effect(() => {
      const list = this.workflows.value();
      if (!list || !this.versions.hasValue() || this.workflow()) return;
      const wanted = this.workflowCode() ?? list.find((w) => w['serviceCode'] === this.service())?.code;
      if (wanted && list.some((w) => w.code === wanted)) {
        untracked(() => this.chooseWorkflow(wanted));
      }
    });
  }

  protected chooseWorkflow(code: string): void {
    this.workflow.set(code);
    const latest = this.allRows().find((r) => r.code === `${code}/${this.nextNumber() - 1}`);
    this.text.set(JSON.stringify(latest?.['definition'] ?? STARTER, null, 2));
    this.errors.set([]);
  }

  protected async addVersion(submit = false): Promise<void> {
    const definition = this.parsed();
    if (!definition) return;
    this.errors.set([]);
    const number = this.nextNumber();
    try {
      await this.drafts.addChange('wf.workflow-version', `${this.workflow()}/${number}`, 'CREATE', {
        nameAr: `الإصدار ${number}`,
        nameEn: `Version ${number}`,
        definition,
      }, { submit });
      this.versions.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  protected showGraph(row: ConfigRow): void {
    this.dialog.open(WorkflowGraphDialog, {
      data: { code: row.code, definition: row['definition'] },
      width: '72rem',
      maxWidth: '95vw',
    });
  }

  protected async retire(row: ConfigRow): Promise<void> {
    await this.drafts.addChange('wf.workflow-version', row.code, 'UPDATE', {
      ...row,
      isRetired: true,
    });
    this.versions.reload();
  }
}
