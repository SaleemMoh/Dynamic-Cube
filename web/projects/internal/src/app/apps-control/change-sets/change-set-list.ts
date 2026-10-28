import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { AppsControlApi, ChangeSetStatus, I18nService, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { PrivilegesService } from '../privileges.service';
import { StatusChip } from './status-chip';

type Filter = 'TO_REVIEW' | 'MINE' | 'ALL';

/** Change-set inbox: what waits for my review, my own drafts and submissions, or everything. */
@Component({
  selector: 'dc-change-set-list',
  imports: [DatePipe, MatButtonModule, MatButtonToggleModule, MatIconModule, MatTableModule, RouterLink, StatusChip, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'changeSets.title' | dcTranslate }}</h1>
      <mat-button-toggle-group [value]="filter()" (change)="filter.set($event.value)" hideSingleSelectionIndicator>
        <mat-button-toggle value="TO_REVIEW" data-testid="filter-review">{{ 'changeSets.toReview' | dcTranslate }}</mat-button-toggle>
        <mat-button-toggle value="MINE" data-testid="filter-mine">{{ 'changeSets.mine' | dcTranslate }}</mat-button-toggle>
        <mat-button-toggle value="ALL">{{ 'changeSets.all' | dcTranslate }}</mat-button-toggle>
      </mat-button-toggle-group>
      @if (privileges.can('apps-control.change-sets', 'CREATE')) {
        <button matButton="outlined" (click)="file.click()" data-testid="import-package">
          <mat-icon fontSet="material-symbols-outlined">upload_file</mat-icon>
          {{ 'changeSets.import' | dcTranslate }}
        </button>
        <input #file type="file" accept="application/json,.json" hidden (change)="importPackage(file)" data-testid="import-file" />
      }
    </header>
    @if (importErrors().length) {
      <ul class="dc-errors" role="alert">
        @for (error of importErrors(); track $index) {
          <li>{{ error }}</li>
        }
      </ul>
    }

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
  private readonly i18n = inject(I18nService);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly privileges = inject(PrivilegesService);
  protected readonly importErrors = signal<string[]>([]);
  protected readonly filter = signal<Filter>('TO_REVIEW');
  protected readonly columns = ['title', 'status', 'items', 'maker', 'created'];

  private readonly resource = rxResource({
    params: () => this.filter(),
    stream: ({ params }) =>
      params === 'TO_REVIEW'
        ? this.api.changeSets({ status: 'SUBMITTED' as ChangeSetStatus })
        : this.api.changeSets(params === 'MINE' ? { mine: true } : {}),
  });

  /** Drafts a configuration package (JSON with title and items) as change sets the user then reviews and submits. */
  protected async importPackage(input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.importErrors.set([]);
    try {
      const pkg = JSON.parse(await file.text()) as { title: string; items: unknown[] };
      const created = await firstValueFrom(this.api.importPackage(pkg));
      this.snackBar.open(this.i18n.translate('changeSets.imported').replace('{count}', String(created.length)), undefined, { duration: 6000 });
      this.filter.set('MINE');
      this.resource.reload();
    } catch (error) {
      this.importErrors.set(error instanceof SyntaxError ? [this.i18n.translate('form.invalidJson')] : problemMessages(error));
    }
  }

  protected readonly rows = computed(() => {
    const rows = this.resource.hasValue() ? this.resource.value() : [];
    return this.filter() === 'TO_REVIEW' ? rows.filter((r) => !r.isMine) : rows;
  });
}
