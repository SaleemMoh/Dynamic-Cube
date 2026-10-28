import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { CallLogSummary, IntegrationApi, IntegrationOutcome, TranslatePipe } from '@dynamic-cube/shared';
import { OutcomeChip } from '../common/outcome-chip';
import { CallLogDetail } from './call-log-detail';

/** Every integration call (personal data masked), newest first. */
@Component({
  selector: 'dc-call-log',
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
    OutcomeChip,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'integration.callLog' | dcTranslate }}</h1>
    </header>
    <div class="dc-toolbar">
      <mat-form-field subscriptSizing="dynamic">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input matInput dir="ltr" [placeholder]="'integration.operation' | dcTranslate" [ngModel]="operation()" (ngModelChange)="filter('operation', $event)" />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-select [placeholder]="'integration.outcome' | dcTranslate" [ngModel]="outcome()" (ngModelChange)="filter('outcome', $event)" [attr.aria-label]="'integration.outcome' | dcTranslate">
          <mat-option [value]="null">{{ 'integration.allOutcomes' | dcTranslate }}</mat-option>
          @for (o of outcomes; track o) {
            <mat-option [value]="o">{{ 'outcome.' + o | dcTranslate }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>
    <table mat-table [dataSource]="page.value()?.items ?? []" class="dc-table">
      <ng-container matColumnDef="startedAt">
        <th mat-header-cell *matHeaderCellDef>{{ 'integration.time' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr">{{ row.startedAt | date: 'yyyy-MM-dd HH:mm:ss' }}</td>
      </ng-container>
      <ng-container matColumnDef="operation">
        <th mat-header-cell *matHeaderCellDef>{{ 'integration.operation' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr" class="dc-code">{{ row.operationCode }}</td>
      </ng-container>
      <ng-container matColumnDef="outcome">
        <th mat-header-cell *matHeaderCellDef>{{ 'integration.outcome' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row"><dc-outcome-chip [outcome]="row.outcome" /></td>
      </ng-container>
      <ng-container matColumnDef="code">
        <th mat-header-cell *matHeaderCellDef>{{ 'integration.code' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr">{{ row.code }}{{ row.fromCache ? ' (cache)' : '' }}</td>
      </ng-container>
      <ng-container matColumnDef="duration">
        <th mat-header-cell *matHeaderCellDef>{{ 'integration.duration' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr">{{ row.durationMs }} ms</td>
      </ng-container>
      <ng-container matColumnDef="user">
        <th mat-header-cell *matHeaderCellDef>{{ 'integration.user' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr">{{ row.userId }} · {{ row.appCode }}</td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns" class="dc-row--link" (click)="open(row)" tabindex="0" (keydown.enter)="open(row)"></tr>
    </table>
    <mat-paginator [length]="page.value()?.total ?? 0" [pageSize]="take()" [pageSizeOptions]="[25, 50, 100]" (page)="paginate($event)" />
  `,
  styles: `
    .dc-row--link {
      cursor: pointer;
    }
  `,
})
export class CallLog {
  private readonly api = inject(IntegrationApi);
  private readonly dialog = inject(MatDialog);
  protected readonly outcomes: IntegrationOutcome[] = ['SUCCESS', 'NOT_FOUND', 'BUSINESS_ERROR', 'TECHNICAL_ERROR'];
  protected readonly columns = ['startedAt', 'operation', 'outcome', 'code', 'duration', 'user'];
  protected readonly operation = signal<string | null>(null);
  protected readonly outcome = signal<IntegrationOutcome | null>(null);
  protected readonly skip = signal(0);
  protected readonly take = signal(50);

  protected readonly page = rxResource({
    params: () => ({ operation: this.operation()?.trim() || null, outcome: this.outcome(), skip: this.skip(), take: this.take() }),
    stream: ({ params }) => this.api.callLogs(params),
  });

  protected filter(name: 'operation' | 'outcome', value: string | null): void {
    this.skip.set(0);
    if (name === 'operation') this.operation.set(value);
    else this.outcome.set(value as IntegrationOutcome | null);
  }

  protected paginate(event: PageEvent): void {
    this.take.set(event.pageSize);
    this.skip.set(event.pageIndex * event.pageSize);
  }

  protected open(row: CallLogSummary): void {
    this.dialog.open(CallLogDetail, { data: row.id, width: '56rem', maxWidth: '95vw' });
  }
}
