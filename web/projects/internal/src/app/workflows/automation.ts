import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { I18nService, TranslatePipe, WORKFLOW_API, problemMessages } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { ConfigEditorData } from '../apps-control/config/config-editor';
import { AUTOMATION_TRANSLATIONS } from './automation-translations';

type AutomationAction = 'ASSIGN_LEAST_LOADED' | 'REMIND' | 'ESCALATE';
type AutomationOutcome = 'DONE' | 'SKIPPED' | 'FAILED';

interface AutomationMatch {
  taskId: string;
  subjectNumber: string;
  step: string;
  nameAr: string;
  nameEn: string;
  assignee: string | null;
  detail: string | null;
}

interface AutomationLogEntry {
  id: string;
  ruleCode: string;
  taskId: string;
  subjectNumber: string;
  step: string;
  action: AutomationAction;
  outcome: AutomationOutcome;
  detail: string | null;
  at: string;
}

interface AutomationLogPage {
  items: AutomationLogEntry[];
  total: number;
}

function outcomeBadge(outcome: AutomationOutcome): string {
  return outcome === 'DONE' ? 'dc-badge--success' : outcome === 'FAILED' ? 'dc-badge--danger' : '';
}

/** Above the automation rules: how they run and the facts a condition reads. */
@Component({
  selector: 'dc-automation-intro',
  imports: [MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="au-intro" data-testid="automation-intro">
      <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">smart_toy</mat-icon>
      <div>
        <h2>{{ 'au.intro.title' | dcTranslate }}</h2>
        <p>{{ 'au.intro.text' | dcTranslate }}</p>
        <details>
          <summary>{{ 'au.intro.facts' | dcTranslate }}</summary>
          <dl>
            @for (f of facts; track f) {
              <div>
                <dt dir="ltr">task.{{ f }}</dt>
                <dd>{{ 'au.fact.' + f | dcTranslate }}</dd>
              </div>
            }
          </dl>
        </details>
      </div>
    </section>
  `,
  styles: `
    .au-intro {
      display: flex;
      gap: 12px;
      margin-bottom: 12px;
      padding: 12px 16px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 10px;
      background: var(--dga-gray-50, #f9fafb);
      font-size: 13px;
    }
    .au-intro h2 {
      margin: 0 0 4px;
      font-size: 15px;
    }
    .au-intro p {
      margin: 0 0 6px;
    }
    .au-intro summary {
      cursor: pointer;
      font-weight: 600;
    }
    .au-intro dl {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(18rem, 1fr));
      gap: 4px 16px;
      margin: 8px 0 0;
    }
    .au-intro dl div {
      display: flex;
      gap: 8px;
    }
    .au-intro dt {
      font-family: monospace;
    }
    .au-intro dd {
      margin: 0;
      color: var(--dga-text-3, #6c737f);
    }
  `,
})
export class AutomationIntro {
  protected readonly facts = [
    'step',
    'workflow',
    'service',
    'role',
    'assigned',
    'ageHours',
    'unassignedHours',
    'assignedHours',
    'dueInHours',
    'overdue',
    'sla',
    'escalated',
  ];

  constructor() {
    inject(I18nService).register(AUTOMATION_TRANSLATIONS);
  }
}

/** Tests a saved rule (a row action): the open tasks it applies to now and what it would do. */
@Component({
  selector: 'dc-automation-test',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    RouterLink,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ 'au.test' | dcTranslate }} — <span dir="ltr">{{ data.row?.code }}</span>
    </h2>
    <mat-dialog-content class="au-test">
      <p class="dc-muted">{{ 'au.test.hint' | dcTranslate }}</p>
      @if (running()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li>{{ e }}</li>
          }
        </ul>
      }
      @if (matches(); as list) {
        @if (list.length) {
          <p>
            <strong>{{ 'au.test.count' | dcTranslate }} {{ list.length }}</strong>
          </p>
          <ul class="au-matches" data-testid="automation-matches">
            @for (m of list; track m.taskId) {
              <li>
                <a
                  class="dc-link"
                  dir="ltr"
                  [routerLink]="['/action-center/tasks', m.taskId]"
                  mat-dialog-close
                  >{{ m.subjectNumber }}</a
                >
                <span>{{ lang() === 'ar' ? m.nameAr : m.nameEn }}</span>
                @if (m.assignee) {
                  <span class="dc-muted" dir="ltr">{{ m.assignee }}</span>
                }
                <span class="dc-badge dc-badge--info">{{ m.detail }}</span>
              </li>
            }
          </ul>
        } @else {
          <p class="dc-empty" data-testid="automation-none">{{ 'au.test.none' | dcTranslate }}</p>
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      <button matButton mat-dialog-close>{{ 'au.close' | dcTranslate }}</button>
      <button
        matButton="filled"
        type="button"
        (click)="run()"
        [disabled]="running()"
        data-testid="automation-test-run"
      >
        <mat-icon fontSet="material-symbols-outlined">play_arrow</mat-icon
        >{{ 'au.test.run' | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .au-test {
      display: grid;
      gap: 10px;
    }
    .au-matches {
      display: grid;
      gap: 6px;
      margin: 0;
      padding: 0;
      list-style: none;
      font-size: 13px;
    }
    .au-matches li {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      align-items: center;
      padding: 6px 0;
      border-bottom: 1px solid var(--dga-gray-200, #e5e7eb);
    }
  `,
})
export class AutomationTest {
  protected readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  private readonly http = inject(HttpClient);
  private readonly i18n = inject(I18nService);
  protected readonly lang = this.i18n.language;
  protected readonly running = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly matches = signal<AutomationMatch[] | null>(null);

  constructor() {
    this.i18n.register(AUTOMATION_TRANSLATIONS);
    void this.run();
  }

  protected async run(): Promise<void> {
    const row = this.data.row ?? ({} as Record<string, unknown>);
    this.running.set(true);
    this.errors.set([]);
    try {
      this.matches.set(
        await firstValueFrom(
          this.http.post<AutomationMatch[]>(`${WORKFLOW_API}/automation/test`, {
            code: row['code'],
            workflows: row['workflows'] ?? [],
            steps: row['steps'] ?? [],
            when: row['when'],
            action: row['action'],
            escalateToRole: row['escalateToRole'] ?? null,
            repeatHours: row['repeatHours'] ?? null,
          }),
        ),
      );
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.running.set(false);
    }
  }
}

/** Everything the automation rules did, newest first. */
@Component({
  selector: 'dc-automation-log',
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    RouterLink,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <div>
        <h1>{{ 'au.log.title' | dcTranslate }}</h1>
        <p class="dc-muted">{{ 'au.log.subtitle' | dcTranslate }}</p>
      </div>
    </header>
    <div class="dc-toolbar">
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'au.log.rule' | dcTranslate }}</mat-label>
        <input
          matInput
          dir="ltr"
          [ngModel]="rule()"
          (ngModelChange)="rule.set($event.toUpperCase()); skip.set(0)"
          data-testid="automation-log-rule"
        />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'au.log.subject' | dcTranslate }}</mat-label>
        <input
          matInput
          dir="ltr"
          [ngModel]="subject()"
          (ngModelChange)="subject.set($event); skip.set(0)"
          data-testid="automation-log-subject"
        />
      </mat-form-field>
      <button matButton type="button" (click)="page.reload()">
        <mat-icon fontSet="material-symbols-outlined">refresh</mat-icon
        >{{ 'au.log.refresh' | dcTranslate }}
      </button>
    </div>
    <table class="dc-table au-log" data-testid="automation-log">
      <tbody>
        @for (e of page.value()?.items ?? []; track e.id) {
          <tr>
            <td dir="ltr" class="dc-muted">{{ e.at | date: 'yyyy-MM-dd HH:mm' }}</td>
            <td dir="ltr">
              <b>{{ e.ruleCode }}</b>
            </td>
            <td>
              <a class="dc-link" dir="ltr" [routerLink]="['/action-center/tasks', e.taskId]">{{
                e.subjectNumber
              }}</a>
              · <span dir="ltr">{{ e.step }}</span>
            </td>
            <td>{{ 'au.action.' + e.action | dcTranslate }}</td>
            <td>
              <span class="dc-badge" [class]="badge(e.outcome)">{{
                'au.outcome.' + e.outcome | dcTranslate
              }}</span>
            </td>
            <td class="dc-muted" dir="ltr">{{ e.detail }}</td>
          </tr>
        }
      </tbody>
    </table>
    @if ((page.value()?.items ?? []).length === 0 && !page.isLoading()) {
      <p class="dc-empty">{{ 'au.log.empty' | dcTranslate }}</p>
    }
    <mat-paginator
      [length]="page.value()?.total ?? 0"
      [pageSize]="take()"
      [pageSizeOptions]="[25, 50, 100]"
      (page)="paginate($event)"
    />
  `,
  styles: `
    .au-log {
      width: 100%;
      font-size: 13px;
    }
    .au-log td {
      padding: 8px 10px;
      border-bottom: 1px solid var(--dga-gray-200, #e5e7eb);
    }
  `,
})
export class AutomationLog {
  private readonly http = inject(HttpClient);
  protected readonly rule = signal('');
  protected readonly subject = signal('');
  protected readonly skip = signal(0);
  protected readonly take = signal(50);
  protected readonly page = rxResource({
    params: () => ({
      rule: this.rule().trim(),
      subject: this.subject().trim(),
      skip: this.skip(),
      take: this.take(),
    }),
    stream: ({ params }) => {
      const query: Record<string, string> = { skip: `${params.skip}`, take: `${params.take}` };
      if (params.rule) query['rule'] = params.rule;
      if (params.subject) query['subject'] = params.subject;
      return this.http.get<AutomationLogPage>(`${WORKFLOW_API}/automation/log`, { params: query });
    },
  });

  constructor() {
    inject(I18nService).register(AUTOMATION_TRANSLATIONS);
  }

  protected badge(outcome: AutomationOutcome): string {
    return outcomeBadge(outcome);
  }

  protected paginate(event: PageEvent): void {
    this.take.set(event.pageSize);
    this.skip.set(event.pageIndex * event.pageSize);
  }
}
