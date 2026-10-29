import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { AppsControlApi, ChangeSetStatus, I18nService, TranslatePipe } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { PrivilegesService } from '../privileges.service';
import { PackageReviewData, PackageReviewDialog } from './package-review-dialog';
import { StatusChip } from './status-chip';
import { ExportButton } from './export-button';
import { TemplateDialog } from './template-dialog';

type Filter = 'TO_REVIEW' | 'MINE' | 'ALL';

/** Change-set inbox: what waits for my review, my own drafts and submissions, or everything. */
@Component({
  selector: 'dc-change-set-list',
  imports: [ExportButton, DatePipe, MatButtonModule, MatButtonToggleModule, MatIconModule, MatTableModule, RouterLink, StatusChip, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'changeSets.title' | dcTranslate }}</h1>
      <mat-button-toggle-group [value]="filter()" (change)="filter.set($event.value)" hideSingleSelectionIndicator>
        <mat-button-toggle value="TO_REVIEW" data-testid="filter-review">{{ 'changeSets.toReview' | dcTranslate }}</mat-button-toggle>
        <mat-button-toggle value="MINE" data-testid="filter-mine">{{ 'changeSets.mine' | dcTranslate }}</mat-button-toggle>
        <mat-button-toggle value="ALL">{{ 'changeSets.all' | dcTranslate }}</mat-button-toggle>
      </mat-button-toggle-group>
      <span class="dc-change-sets__actions">
        <dc-export-button />
        @if (privileges.can('apps-control.change-sets', 'CREATE')) {
          <button matButton="outlined" (click)="template()" data-testid="package-template">
            <mat-icon fontSet="material-symbols-outlined">download</mat-icon>
            {{ 'packages.template' | dcTranslate }}
          </button>
          <button matButton="outlined" (click)="file.click()" data-testid="import-package">
            <mat-icon fontSet="material-symbols-outlined">upload_file</mat-icon>
            {{ 'changeSets.import' | dcTranslate }}
          </button>
          <input #file type="file" accept="application/json,.json" hidden (change)="importPackage(file)" data-testid="import-file" />
        }
      </span>
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
  styles: `
    .dc-change-sets__actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
  `,
})
export class ChangeSetList {
  private readonly api = inject(AppsControlApi);
  private readonly i18n = inject(I18nService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  protected readonly privileges = inject(PrivilegesService);
  protected readonly filter = signal<Filter>('TO_REVIEW');
  protected readonly columns = ['title', 'status', 'items', 'maker', 'created'];

  private readonly resource = rxResource({
    params: () => this.filter(),
    stream: ({ params }) =>
      params === 'TO_REVIEW'
        ? this.api.changeSets({ status: 'SUBMITTED' as ChangeSetStatus })
        : this.api.changeSets(params === 'MINE' ? { mine: true } : {}),
  });

  constructor() {
    // A link can open a list (from the home page: ?filter=MINE).
    const wanted = inject(ActivatedRoute).snapshot.queryParamMap.get('filter');
    if (wanted === 'TO_REVIEW' || wanted === 'MINE' || wanted === 'ALL') this.filter.set(wanted);
  }

  /** Downloads a documented package template for the chosen entity types. */
  protected template(): void {
    this.dialog.open(TemplateDialog, { width: '56rem', maxWidth: '95vw' });
  }

  /** Reviews a configuration package (checked on the server, nothing saved) and drafts it as change sets on confirmation. */
  protected async importPackage(input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    const created = await firstValueFrom(
      this.dialog
        .open<PackageReviewDialog, PackageReviewData, number>(PackageReviewDialog, { data: { fileName: file.name, text: await file.text() }, width: '64rem', maxWidth: '95vw' })
        .afterClosed(),
    );
    if (created) {
      this.snackBar.open(this.i18n.translate('changeSets.imported').replace('{count}', String(created)), undefined, { duration: 6000 });
      this.filter.set('MINE');
      this.resource.reload();
    }
  }

  protected readonly rows = computed(() => {
    const rows = this.resource.hasValue() ? this.resource.value() : [];
    return this.filter() === 'TO_REVIEW' ? rows.filter((r) => !r.isMine) : rows;
  });
}
