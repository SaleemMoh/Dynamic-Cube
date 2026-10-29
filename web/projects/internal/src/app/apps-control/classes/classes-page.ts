import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import { AppsControlApi, I18nService, LocalizePipe, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { ConfirmData, ConfirmDialog } from '../confirm-dialog';
import { DraftsService } from '../drafts.service';
import { DetailsData, DetailsDialog, displayValue } from '../master/details-dialog';
import { MasterItem, MasterList } from '../master/master-list';
import { PrivilegesService } from '../privileges.service';
import { ClassEditor, ClassEditorData } from './class-editor';
import { ClassRow, DomainRow } from './class-models';
import { DomainEditor, DomainEditorData } from './domain-editor';
import { JsonImportButton } from '../change-sets/json-import-button';

const RESOURCE = 'apps-control.classes';

/**
 * Classes and their values side by side: the classes on the start side (with how many values each has), the chosen
 * class's values on the end side. Classes are edited in a dialog; every change goes through a change set.
 */
@Component({
  selector: 'dc-classes-page',
  imports: [
    JsonImportButton,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    MasterList,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header dc-page__header--actions">
      <div>
        <h1>{{ 'classes.title' | dcTranslate }}</h1>
        <p>{{ 'classes.hint' | dcTranslate }}</p>
      </div>
      <dc-json-import-button [title]="{ ar: 'التصنيفات والقيم', en: 'Classes & domains' }" [target]="{ entityType: 'apps.class', key: 'code' }" />
    </header>
    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (error of errors(); track $index) {
          <li>{{ error }}</li>
        }
      </ul>
    }
    <div class="dc-split">
      <dc-master-list
        testId="classes"
        [title]="{ ar: 'التصنيفات', en: 'Classes' }"
        [addLabel]="{ ar: 'إضافة تصنيف', en: 'Add class' }"
        [items]="items()"
        [selected]="selected()"
        [canCreate]="privileges.can(resource, 'CREATE')"
        (selectItem)="select($event)"
        (view)="openClass($event, true)"
        (edit)="openClass($event, false)"
        (remove)="removeClass($event)"
        (add)="openClass(null, false)"
      />

      <section class="dc-panel" data-testid="values-panel">
        @if (cls(); as c) {
          <header class="dc-panel__header">
            <div>
              <h2>
                {{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}
                <span class="dc-count" data-testid="values-count">{{ values().length }}</span>
              </h2>
              <span class="mono">{{ c.code }}</span>
            </div>
            @if (canAddValue()) {
              <button matButton="filled" (click)="editValue(null)" data-testid="new-value">
                <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
                {{ 'classes.newValue' | dcTranslate }}
              </button>
            }
          </header>
          <div class="dc-panel__search">
            <mat-form-field subscriptSizing="dynamic">
              <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
              <input
                matInput
                [placeholder]="'config.search' | dcTranslate"
                [value]="search()"
                (input)="search.set($any($event.target).value)"
                data-testid="values-search"
              />
            </mat-form-field>
          </div>
          @if (valuesResource.isLoading()) {
            <mat-progress-bar mode="indeterminate" />
          }
          <div class="dc-scroll">
            <table mat-table [dataSource]="dataSource" matSort class="dc-table" data-testid="values-table">
              <ng-container matColumnDef="code">
                <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'classes.value' | dcTranslate }}</th>
                <td mat-cell *matCellDef="let row" class="mono">{{ valueCode(row) }}</td>
              </ng-container>
              <ng-container matColumnDef="nameAr">
                <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'config.nameAr' | dcTranslate }}</th>
                <td mat-cell *matCellDef="let row" dir="rtl">{{ row.nameAr }}</td>
              </ng-container>
              <ng-container matColumnDef="nameEn">
                <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'config.nameEn' | dcTranslate }}</th>
                <td mat-cell *matCellDef="let row" dir="ltr">{{ row.nameEn }}</td>
              </ng-container>
              @for (attribute of shownAttributes(); track attribute.code) {
                <ng-container [matColumnDef]="'attr:' + attribute.code">
                  <th mat-header-cell *matHeaderCellDef>{{ { ar: attribute.nameAr, en: attribute.nameEn } | dcLocalize }}</th>
                  <td mat-cell *matCellDef="let row">{{ attributeValue(row, attribute.code) }}</td>
                </ng-container>
              }
              <ng-container matColumnDef="isActive">
                <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'config.status' | dcTranslate }}</th>
                <td mat-cell *matCellDef="let row">
                  <span class="dc-pill" [class.dc-pill--green]="row.isActive">{{ (row.isActive ? 'config.active' : 'config.inactive') | dcTranslate }}</span>
                  @if (row.isDefault) {
                    <span class="dc-pill dc-pill--gold">{{ 'classes.default' | dcTranslate }}</span>
                  }
                </td>
              </ng-container>
              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let row" class="dc-table__actions">
                  <button
                    matIconButton
                    (click)="viewValue(row)"
                    [attr.aria-label]="'config.view' | dcTranslate"
                    [attr.data-testid]="'view-value-' + valueCode(row)"
                  >
                    <mat-icon fontSet="material-symbols-outlined">visibility</mat-icon>
                  </button>
                  @if (row.owner !== 'SYSTEM' && privileges.can(resource, 'EDIT')) {
                    <button
                      matIconButton
                      (click)="editValue(row)"
                      [attr.aria-label]="'config.edit' | dcTranslate"
                      [attr.data-testid]="'edit-value-' + valueCode(row)"
                    >
                      <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>
                    </button>
                  }
                  @if (row.owner !== 'SYSTEM' && privileges.can(resource, 'DELETE')) {
                    <button
                      matIconButton
                      (click)="deleteValue(row)"
                      [attr.aria-label]="'config.delete' | dcTranslate"
                      [attr.data-testid]="'delete-value-' + valueCode(row)"
                    >
                      <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                    </button>
                  }
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="columns(); sticky: true"></tr>
              <tr mat-row *matRowDef="let row; columns: columns()"></tr>
              <tr class="mat-mdc-row" *matNoDataRow>
                <td class="mat-mdc-cell dc-empty" [attr.colspan]="columns().length">{{ 'classes.noValues' | dcTranslate }}</td>
              </tr>
            </table>
          </div>
          <mat-paginator [pageSize]="50" [pageSizeOptions]="[25, 50, 100, 250]" [hidePageSize]="false" />
        } @else if (!classes.isLoading()) {
          <p class="dc-empty dc-panel__body">{{ 'classes.pick' | dcTranslate }}</p>
        }
      </section>
    </div>
  `,
  styles: `
    .dc-scroll {
      max-height: calc(100vh - 360px);
      min-height: 12rem;
      overflow: auto;
    }
    .dc-pill + .dc-pill {
      margin-inline-start: 6px;
    }
  `,
})
export class ClassesPage {
  /** Route parameter: the chosen class (the first one when absent). */
  readonly code = input<string | undefined>(undefined);

  private readonly api = inject(AppsControlApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly drafts = inject(DraftsService);
  private readonly i18n = inject(I18nService);
  protected readonly privileges = inject(PrivilegesService);
  protected readonly resource = RESOURCE;

  protected readonly errors = signal<string[]>([]);
  protected readonly search = signal('');
  protected readonly classes = rxResource({ stream: () => this.api.list('apps.class') });
  private readonly counts = rxResource({ stream: () => this.api.counts('apps.class') });
  private readonly classRows = computed(() => (this.classes.hasValue() ? (this.classes.value() as ClassRow[]) : []));

  protected readonly selected = computed(() => this.code() ?? this.classRows()[0]?.code ?? null);
  protected readonly cls = computed(() => this.classRows().find((c) => c.code === this.selected()) ?? null);
  protected readonly valuesResource = rxResource({
    params: () => this.selected() ?? undefined,
    stream: ({ params }) => this.api.list('apps.domain', params),
  });
  protected readonly values = computed(() => (this.valuesResource.hasValue() ? (this.valuesResource.value() as DomainRow[]) : []));
  protected readonly shownAttributes = computed(() => (this.cls()?.attributes ?? []).slice(0, 3));
  protected readonly columns = computed(() => ['code', 'nameAr', 'nameEn', ...this.shownAttributes().map((a) => 'attr:' + a.code), 'isActive', 'actions']);
  protected readonly canAddValue = computed(() => this.cls()?.owner !== 'SYSTEM' && this.privileges.can(RESOURCE, 'CREATE'));

  protected readonly items = computed<MasterItem[]>(() => {
    const counts = this.counts.hasValue() ? this.counts.value() : {};
    const canEdit = this.privileges.can(RESOURCE, 'EDIT');
    const canDelete = this.privileges.can(RESOURCE, 'DELETE');
    return this.classRows().map((c) => {
      const count = counts[c.code] ?? 0;
      const system = c.owner === 'SYSTEM';
      return {
        code: c.code,
        title: { ar: c.nameAr, en: c.nameEn },
        badge: count
          ? { label: `${count} ${this.i18n.translate(count === 1 ? 'classes.valueWord' : 'classes.valuesWord')}`, tone: 'green' }
          : { label: this.i18n.translate('classes.noValuesShort'), tone: 'gray' },
        locked: system,
        canEdit: !system && canEdit,
        canDelete: !system && canDelete && count === 0,
        deleteBlockedReason: !system && canDelete && count > 0 ? this.i18n.translate('classes.deleteHasValues') : undefined,
      } satisfies MasterItem;
    });
  });

  protected readonly dataSource = new MatTableDataSource<DomainRow>([]);
  private readonly sort = viewChild(MatSort);
  private readonly paginator = viewChild(MatPaginator);

  constructor() {
    this.dataSource.filterPredicate = (row, term) => `${row.code} ${row.nameAr} ${row.nameEn}`.toLowerCase().includes(term);
    effect(() => (this.dataSource.data = this.values()));
    effect(() => (this.dataSource.filter = this.search().trim().toLowerCase()));
    effect(() => {
      const sort = this.sort();
      const paginator = this.paginator();
      if (sort) this.dataSource.sort = sort;
      if (paginator) this.dataSource.paginator = paginator;
    });
  }

  protected select(code: string): void {
    this.search.set('');
    void this.router.navigate(['/apps-control/classes', code]);
  }

  protected valueCode(row: DomainRow): string {
    return row.code.slice(row.code.indexOf('/') + 1);
  }

  protected attributeValue(row: DomainRow, code: string): string {
    return displayValue(row.attributes?.[code]);
  }

  protected async openClass(code: string | null, readonly: boolean): Promise<void> {
    const cls = code ? (this.classRows().find((c) => c.code === code) ?? null) : null;
    const saved = await firstValueFrom(
      this.dialog
        .open<ClassEditor, ClassEditorData, boolean>(ClassEditor, {
          data: { cls, classes: this.classRows(), readonly },
          autoFocus: 'first-tabbable',
          width: '64rem',
          maxWidth: '95vw',
        })
        .afterClosed(),
    );
    if (saved) this.reload();
  }

  protected async removeClass(code: string): Promise<void> {
    if (await this.confirmDelete(code)) {
      await this.change('apps.class', code);
    }
  }

  protected viewValue(row: DomainRow): void {
    const cls = this.cls();
    const data: DetailsData = {
      title: { ar: row.nameAr, en: row.nameEn },
      code: row.code,
      rows: [
        { label: { ar: 'الاسم بالعربية', en: 'Arabic name' }, value: row.nameAr },
        { label: { ar: 'الاسم بالإنجليزية', en: 'English name' }, value: row.nameEn },
        { label: { ar: 'القيمة الأعلى', en: 'Parent value' }, value: row.parentCode ?? '', ltr: true },
        { label: { ar: 'ترتيب العرض', en: 'Display order' }, value: String(row.displayOrder ?? '') },
        { label: { ar: 'القيمة الافتراضية', en: 'Default value' }, value: displayValue(row.isDefault) },
        { label: { ar: 'فعال', en: 'Active' }, value: displayValue(row.isActive) },
        { label: { ar: 'يسري من', en: 'Effective from' }, value: row.effectiveFrom ?? '', ltr: true },
        { label: { ar: 'يسري حتى', en: 'Effective to' }, value: row.effectiveTo ?? '', ltr: true },
        ...(cls?.attributes ?? []).map((a) => ({ label: { ar: a.nameAr, en: a.nameEn }, value: displayValue(row.attributes?.[a.code]) })),
      ],
    };
    this.dialog.open(DetailsDialog, { data, width: '40rem', maxWidth: '95vw' });
  }

  protected async editValue(row: DomainRow | null): Promise<void> {
    const cls = this.cls();
    if (!cls) return;
    const saved = await firstValueFrom(
      this.dialog
        .open<DomainEditor, DomainEditorData, boolean>(DomainEditor, { data: { cls, row }, autoFocus: 'first-tabbable', width: '52rem', maxWidth: '95vw' })
        .afterClosed(),
    );
    if (saved) this.reload();
  }

  protected async deleteValue(row: DomainRow): Promise<void> {
    if (await this.confirmDelete(row.code)) {
      await this.change('apps.domain', row.code);
    }
  }

  private async confirmDelete(code: string): Promise<boolean> {
    const result = await firstValueFrom(
      this.dialog
        .open<ConfirmDialog, ConfirmData, string>(ConfirmDialog, {
          data: {
            title: `${this.i18n.translate('config.delete')} ${code}`,
            message: this.i18n.translate('config.deleteHint'),
            confirmLabel: this.i18n.translate('config.delete'),
            danger: true,
          },
        })
        .afterClosed(),
    );
    return result !== undefined;
  }

  private async change(entityType: string, code: string): Promise<void> {
    this.errors.set([]);
    try {
      await this.drafts.addChange(entityType, code, 'DELETE', null);
      this.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  private reload(): void {
    this.classes.reload();
    this.counts.reload();
    this.valuesResource.reload();
  }
}
