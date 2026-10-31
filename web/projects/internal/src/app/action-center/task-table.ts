import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { I18nService, LocalizePipe, SlaChip, TaskSummary, TranslatePipe, localize } from '@dynamic-cube/shared';
import { BranchNames } from '../common/branch-names';

/**
 * Tasks as inboxes and supervision list them: the request and its service, the step, the beneficiary, who has it (with
 * initials), how long ago it arrived and its deadline. A row opens the task.
 */
@Component({
  selector: 'dc-task-table',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatTableModule, MatTooltipModule, RouterLink, LocalizePipe, SlaChip, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <table mat-table [dataSource]="tasks()" class="dc-table dc-tasks" data-testid="task-table">
      <ng-container matColumnDef="number">
        <th mat-header-cell *matHeaderCellDef>{{ 'requests.number' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let t">
          <a
            class="dc-tasks__number mono"
            [routerLink]="['/action-center/tasks', t.id]"
            (click)="$event.stopPropagation()"
            [attr.data-testid]="'task-' + t.subjectNumber + '-' + t.nodeCode"
            >{{ t.subjectNumber }}</a
          >
          <div class="dc-tasks__sub">{{ { ar: t.titleAr, en: t.titleEn } | dcLocalize }}</div>
        </td>
      </ng-container>
      <ng-container matColumnDef="task">
        <th mat-header-cell *matHeaderCellDef>{{ 'tasks.step' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let t">
          <span class="dc-tasks__step">{{ { ar: t.nameAr, en: t.nameEn } | dcLocalize }}</span>
        </td>
      </ng-container>
      <ng-container matColumnDef="party">
        <th mat-header-cell *matHeaderCellDef>{{ 'requests.beneficiary' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let t">
          {{ { ar: t.partyNameAr, en: t.partyNameEn } | dcLocalize }}
          @if (branches.several() && t.branchCode) {
            <div class="dc-tasks__sub" data-testid="task-branch">{{ branches.name(t.branchCode) | dcLocalize }}</div>
          }
        </td>
      </ng-container>
      <ng-container matColumnDef="assignee">
        <th mat-header-cell *matHeaderCellDef>{{ 'tasks.assignee' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let t">
          @if (t.assignee) {
            <span class="dc-tasks__person">
              <span class="dc-tasks__avatar" aria-hidden="true">{{ initials(t) }}</span>
              {{ { ar: t.assigneeNameAr ?? t.assignee, en: t.assigneeNameEn ?? t.assignee } | dcLocalize }}
            </span>
          } @else {
            <span class="dc-pill">{{ 'tasks.unassigned' | dcTranslate }}</span>
          }
        </td>
      </ng-container>
      <ng-container matColumnDef="created">
        <th mat-header-cell *matHeaderCellDef>{{ 'tasks.received' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let t">
          <span [matTooltip]="(t.createdAt | date: 'yyyy-MM-dd HH:mm') ?? ''">{{ ago(t.createdAt) }}</span>
        </td>
      </ng-container>
      <ng-container matColumnDef="sla">
        <th mat-header-cell *matHeaderCellDef>{{ 'tasks.due' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let t"><dc-sla-chip [sla]="t.sla" [dueAt]="t.dueAt" /></td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let t" class="dc-table__actions">
          @if (claimable()) {
            <button matButton="filled" (click)="$event.stopPropagation(); claim.emit(t)" [attr.data-testid]="'claim-' + t.subjectNumber + '-' + t.nodeCode">
              <mat-icon fontSet="material-symbols-outlined">front_hand</mat-icon>
              {{ 'tasks.claim' | dcTranslate }}
            </button>
          } @else {
            <mat-icon class="dc-tasks__chevron" fontSet="material-symbols-outlined" aria-hidden="true">chevron_left</mat-icon>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns" class="dc-tasks__row" tabindex="0" (click)="open(row)" (keydown.enter)="open(row)"></tr>
      <tr class="mat-mdc-row" *matNoDataRow>
        <td class="mat-mdc-cell" [attr.colspan]="columns.length">
          <div class="dc-tasks__empty">
            <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">task_alt</mat-icon>
            <span>{{ emptyKey() | dcTranslate }}</span>
          </div>
        </td>
      </tr>
    </table>
  `,
  styles: `
    :host {
      display: block;
      overflow-x: auto;
    }
    .dc-tasks {
      width: 100%;
      background: #fff;
    }
    .dc-tasks__row {
      cursor: pointer;
    }
    .dc-tasks__number {
      color: var(--dynamic-cube-gold-700);
      font-weight: 600;
      text-decoration: none;
    }
    .dc-tasks__number:hover {
      text-decoration: underline;
    }
    .dc-tasks__sub {
      margin-top: 2px;
      color: var(--dga-gray-500);
      font-size: 12px;
    }
    .dc-tasks__step {
      font-weight: 600;
      color: var(--dga-gray-900);
    }
    .dc-tasks__person {
      display: inline-flex;
      align-items: center;
      gap: 8px;
    }
    .dc-tasks__avatar {
      display: inline-grid;
      place-items: center;
      width: 26px;
      height: 26px;
      border-radius: 50%;
      background: var(--dynamic-cube-gold-100);
      color: var(--dynamic-cube-gold-700);
      font-size: 11px;
      font-weight: 700;
    }
    .dc-tasks__chevron {
      color: var(--dga-gray-400);
    }
    :host-context([dir='ltr']) .dc-tasks__chevron {
      transform: scaleX(-1);
    }
    .dc-tasks__empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 6px;
      padding: 28px 0;
      color: var(--dga-gray-500);
    }
    .dc-tasks__empty mat-icon {
      width: 32px;
      height: 32px;
      font-size: 32px;
      color: var(--dga-gray-300);
    }
  `,
})
export class TaskTable {
  readonly tasks = input.required<TaskSummary[]>();
  /** Shows a claim button on each row (the queue). */
  readonly claimable = input(false);
  /** What the empty list says. */
  readonly emptyKey = input('tasks.none');
  readonly claim = output<TaskSummary>();
  protected readonly columns = ['number', 'task', 'party', 'assignee', 'created', 'sla', 'actions'];
  protected readonly branches = inject(BranchNames);
  private readonly router = inject(Router);
  private readonly i18n = inject(I18nService);

  protected open(task: TaskSummary): void {
    void this.router.navigate(['/action-center/tasks', task.id]);
  }

  protected initials(task: TaskSummary): string {
    const name = localize({ ar: task.assigneeNameAr ?? task.assignee ?? '', en: task.assigneeNameEn ?? task.assignee ?? '' }, this.i18n.language());
    const parts = name
      .trim()
      .split(/\s+/)
      .filter((p) => p);
    return (parts.length > 1 ? parts[0][0] + parts[1][0] : name.slice(0, 2)).toUpperCase();
  }

  /** "3 hours ago" in the current language. */
  protected ago(at: string): string {
    const seconds = (Date.parse(at) - Date.now()) / 1000;
    const format = new Intl.RelativeTimeFormat(this.i18n.language() === 'ar' ? 'ar' : 'en', { numeric: 'auto' });
    const units: [Intl.RelativeTimeFormatUnit, number][] = [
      ['day', 86400],
      ['hour', 3600],
      ['minute', 60],
    ];
    for (const [unit, size] of units) {
      if (Math.abs(seconds) >= size || unit === 'minute') return format.format(Math.round(seconds / size), unit);
    }
    return '';
  }
}
