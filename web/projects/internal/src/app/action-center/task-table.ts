import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { LocalizePipe, SlaChip, TaskSummary, TranslatePipe } from '@dynamic-cube/shared';

/** Tasks as inboxes and supervision list them: the request, the step, who has it and its deadline. */
@Component({
  selector: 'dc-task-table',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatTableModule, MatTooltipModule, RouterLink, LocalizePipe, SlaChip, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <table mat-table [dataSource]="tasks()" class="dc-table" data-testid="task-table">
      <ng-container matColumnDef="number">
        <th mat-header-cell *matHeaderCellDef>{{ 'requests.number' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let t">
          <a class="dc-link" dir="ltr" [routerLink]="['/action-center/tasks', t.id]" [attr.data-testid]="'task-' + t.subjectNumber + '-' + t.nodeCode">{{
            t.subjectNumber
          }}</a>
        </td>
      </ng-container>
      <ng-container matColumnDef="task">
        <th mat-header-cell *matHeaderCellDef>{{ 'tasks.step' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let t">
          <strong>{{ { ar: t.nameAr, en: t.nameEn } | dcLocalize }}</strong>
          <div class="dc-muted">{{ { ar: t.titleAr, en: t.titleEn } | dcLocalize }}</div>
        </td>
      </ng-container>
      <ng-container matColumnDef="party">
        <th mat-header-cell *matHeaderCellDef>{{ 'requests.beneficiary' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let t">
          {{ { ar: t.partyNameAr, en: t.partyNameEn } | dcLocalize }}
        </td>
      </ng-container>
      <ng-container matColumnDef="assignee">
        <th mat-header-cell *matHeaderCellDef>{{ 'tasks.assignee' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let t">
          @if (t.assignee) {
            {{ { ar: t.assigneeNameAr ?? t.assignee, en: t.assigneeNameEn ?? t.assignee } | dcLocalize }}
          } @else {
            <span class="dc-badge">{{ 'tasks.unassigned' | dcTranslate }}</span>
          }
        </td>
      </ng-container>
      <ng-container matColumnDef="created">
        <th mat-header-cell *matHeaderCellDef>{{ 'tasks.received' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let t" dir="ltr">{{ t.createdAt | date: 'yyyy-MM-dd HH:mm' }}</td>
      </ng-container>
      <ng-container matColumnDef="sla">
        <th mat-header-cell *matHeaderCellDef>{{ 'tasks.due' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let t"><dc-sla-chip [sla]="t.sla" [dueAt]="t.dueAt" /></td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let t" class="dc-table__actions">
          @if (claimable()) {
            <button matButton="tonal" (click)="claim.emit(t)" [attr.data-testid]="'claim-' + t.subjectNumber + '-' + t.nodeCode">
              {{ 'tasks.claim' | dcTranslate }}
            </button>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
      <tr class="mat-mdc-row" *matNoDataRow>
        <td class="mat-mdc-cell dc-muted" [attr.colspan]="columns.length">
          {{ 'tasks.none' | dcTranslate }}
        </td>
      </tr>
    </table>
  `,
})
export class TaskTable {
  readonly tasks = input.required<TaskSummary[]>();
  /** Shows a claim button on each row (the queue). */
  readonly claimable = input(false);
  readonly claim = output<TaskSummary>();
  protected readonly columns = ['number', 'task', 'party', 'assignee', 'created', 'sla', 'actions'];
}
