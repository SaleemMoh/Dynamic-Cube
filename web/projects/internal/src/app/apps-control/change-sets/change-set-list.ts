import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { AppsControlApi, ChangeSetStatus, TranslatePipe } from '@dynamic-cube/shared';
import { StatusChip } from './status-chip';

type Filter = 'TO_REVIEW' | 'MINE' | 'ALL';

/** Change-set inbox: what waits for my review, my own drafts and submissions, or everything. */
@Component({
  selector: 'dc-change-set-list',
  imports: [DatePipe, MatButtonToggleModule, MatTableModule, RouterLink, StatusChip, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'changeSets.title' | dcTranslate }}</h1>
      <mat-button-toggle-group [value]="filter()" (change)="filter.set($event.value)" hideSingleSelectionIndicator>
        <mat-button-toggle value="TO_REVIEW" data-testid="filter-review">{{ 'changeSets.toReview' | dcTranslate }}</mat-button-toggle>
        <mat-button-toggle value="MINE" data-testid="filter-mine">{{ 'changeSets.mine' | dcTranslate }}</mat-button-toggle>
        <mat-button-toggle value="ALL">{{ 'changeSets.all' | dcTranslate }}</mat-button-toggle>
      </mat-button-toggle-group>
    </header>

    @if (rows().length === 0) {
      <p class="dc-empty">{{ 'changeSets.empty' | dcTranslate }}</p>
    } @else {
      <table mat-table [dataSource]="rows()" class="dc-table">
        <ng-container matColumnDef="title">
          <th mat-header-cell *matHeaderCellDef>{{ 'changeSets.changeSet' | dcTranslate }}</th>
          <td mat-cell *matCellDef="let row">
            <a [routerLink]="[row.id]" class="dc-link" data-testid="change-set-link">{{ row.title }}</a>
            <div class="dc-muted">{{ 'area.' + row.area | dcTranslate }}</div>
          </td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>{{ 'changeSets.status' | dcTranslate }}</th>
          <td mat-cell *matCellDef="let row"><dc-status-chip [status]="row.status" /></td>
        </ng-container>
        <ng-container matColumnDef="items">
          <th mat-header-cell *matHeaderCellDef>{{ 'changeSets.items' | dcTranslate }}</th>
          <td mat-cell *matCellDef="let row">{{ row.itemCount }}</td>
        </ng-container>
        <ng-container matColumnDef="maker">
          <th mat-header-cell *matHeaderCellDef>{{ 'changeSets.maker' | dcTranslate }}</th>
          <td mat-cell *matCellDef="let row">{{ row.createdBy }}</td>
        </ng-container>
        <ng-container matColumnDef="created">
          <th mat-header-cell *matHeaderCellDef>{{ 'changeSets.created' | dcTranslate }}</th>
          <td mat-cell *matCellDef="let row" dir="ltr">{{ row.createdAt | date: 'yyyy-MM-dd HH:mm' }}</td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
      </table>
    }
  `,
})
export class ChangeSetList {
  private readonly api = inject(AppsControlApi);
  protected readonly filter = signal<Filter>('TO_REVIEW');
  protected readonly columns = ['title', 'status', 'items', 'maker', 'created'];

  private readonly resource = rxResource({
    params: () => this.filter(),
    stream: ({ params }) =>
      params === 'TO_REVIEW'
        ? this.api.changeSets({ status: 'SUBMITTED' as ChangeSetStatus })
        : this.api.changeSets(params === 'MINE' ? { mine: true } : {}),
  });

  protected readonly rows = computed(() => {
    const rows = this.resource.hasValue() ? this.resource.value() : [];
    return this.filter() === 'TO_REVIEW' ? rows.filter((r) => !r.isMine) : rows;
  });
}
