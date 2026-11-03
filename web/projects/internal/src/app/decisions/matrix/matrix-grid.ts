import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  I18nService,
  LocalizePipe,
  LogicChip,
  LogicField,
  TranslatePipe,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import {
  MatrixCellEditor,
  MatrixCellEditorData,
  MatrixCellEditorResult,
} from './matrix-cell-editor';
import { MatrixInputEditor, MatrixInputEditorData } from './matrix-input-editor';
import {
  CELL_ICONS,
  Matrix,
  MatrixColumn,
  MatrixInput,
  MatrixRow,
  SOURCE_ICONS,
  SourceKind,
  cellInputs,
  cellOf,
  cellSummary,
  freeCode,
  removeColumn,
  removeRow,
  renameColumn,
  renameRow,
  trackFields,
  withCell,
} from './matrix-model';
import { MatrixReferences } from './matrix-references';

interface HeaderDialogData {
  kind: 'row' | 'column';
  item: MatrixRow | MatrixColumn;
  isNew: boolean;
  used: string[];
  fields: LogicField[];
}

/** A sector (row) or a track (column): its code, names and how it counts in the final grade. */
@Component({
  selector: 'dc-matrix-header-dialog',
  imports: [
    FormsModule,
    LogicChip,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{
        (data.kind === 'row'
          ? data.isNew
            ? 'mx.row.new'
            : 'mx.row.edit'
          : data.isNew
            ? 'mx.column.new'
            : 'mx.column.edit'
        ) | dcTranslate
      }}
    </h2>
    <mat-dialog-content class="mx-hd">
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'mx.code' | dcTranslate }}</mat-label>
        <input matInput dir="ltr" [(ngModel)]="item.code" data-testid="mx-header-code" />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'mx.nameAr' | dcTranslate }}</mat-label>
        <input matInput dir="rtl" [(ngModel)]="item.nameAr" data-testid="mx-header-name-ar" />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'mx.nameEn' | dcTranslate }}</mat-label>
        <input matInput dir="ltr" [(ngModel)]="item.nameEn" data-testid="mx-header-name-en" />
      </mat-form-field>
      @if (data.kind === 'row') {
        <mat-checkbox [(ngModel)]="row.active">{{ 'mx.row.active' | dcTranslate }}</mat-checkbox>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'mx.row.final' | dcTranslate }}</mat-label>
          <mat-select [(ngModel)]="row.final">
            <mat-option value="worst">{{ 'mx.final.worst' | dcTranslate }}</mat-option>
            <mat-option value="weighted">{{ 'mx.final.weighted' | dcTranslate }}</mat-option>
            <mat-option value="formula">{{ 'mx.final.formula' | dcTranslate }}</mat-option>
          </mat-select>
        </mat-form-field>
        @if (row.final === 'formula') {
          <dc-logic-chip
            mode="value"
            [rule]="row.finalRule ?? null"
            [fields]="data.fields"
            (ruleChange)="row.finalRule = $event"
          />
        }
      } @else {
        <mat-checkbox [(ngModel)]="column.includeInFinal">{{
          'mx.column.includeInFinal' | dcTranslate
        }}</mat-checkbox>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'mx.column.weight' | dcTranslate }}</mat-label>
          <input matInput type="number" dir="ltr" [(ngModel)]="column.weight" />
        </mat-form-field>
      }
      @if (taken()) {
        <p class="dc-errors" role="alert">{{ 'mx.codeTaken' | dcTranslate }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close type="button">{{ 'form.cancel' | dcTranslate }}</button>
      <button matButton="filled" type="button" (click)="done()" data-testid="mx-header-done">
        {{ 'logic.done' | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .mx-hd {
      display: grid;
      gap: 0.75rem;
      min-width: min(28rem, 80vw);
    }
  `,
})
export class MatrixHeaderDialog {
  protected readonly data = inject<HeaderDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<MatrixHeaderDialog, MatrixRow | MatrixColumn>);
  protected readonly item = structuredClone(this.data.item);
  protected readonly row = this.item as MatrixRow;
  protected readonly column = this.item as MatrixColumn;
  protected readonly taken = signal(false);

  protected done(): void {
    const code = this.item.code.trim();
    if (
      !/^[A-Za-z0-9][A-Za-z0-9_-]*$/.test(code) ||
      this.data.used.some((u) => u.toLowerCase() === code.toLowerCase())
    ) {
      this.taken.set(true);
      return;
    }
    this.ref.close({ ...this.item, code });
  }
}

/**
 * The matrix as a table: sectors are rows, tracks are columns, and each cell holds that sector's calculation for the
 * track. A cell shows its kind, a short summary and an icon per kind of source its inputs come from; an icon opens a
 * list of those inputs and each one opens its source editor (question, review task field, integration…) in place.
 */
@Component({
  selector: 'dc-matrix-grid',
  imports: [
    LocalizePipe,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTooltipModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-grid__scroll">
      <table class="mx-grid" data-testid="mx-grid">
        <thead>
          <tr>
            <th class="mx-grid__corner">
              <span>{{ 'mx.grid.rows' | dcTranslate }}</span> ╲
              <span>{{ 'mx.grid.columns' | dcTranslate }}</span>
            </th>
            @for (c of matrix().columns; track c.code) {
              <th class="mx-grid__col" [attr.data-testid]="'mx-col-' + c.code">
                <div class="mx-grid__head">
                  <span class="mx-grid__name">{{
                    { ar: c.nameAr, en: c.nameEn } | dcLocalize
                  }}</span>
                  @if (!c.includeInFinal) {
                    <span class="dc-badge" [matTooltip]="'mx.column.excludedHint' | dcTranslate">{{
                      'mx.column.excluded' | dcTranslate
                    }}</span>
                  }
                  @if (editable()) {
                    <button
                      matIconButton
                      [matMenuTriggerFor]="colMenu"
                      [attr.aria-label]="'mx.more' | dcTranslate"
                      class="mx-grid__menu"
                    >
                      <mat-icon fontSet="material-symbols-outlined">more_vert</mat-icon>
                    </button>
                    <mat-menu #colMenu="matMenu">
                      <button mat-menu-item (click)="editColumn(c)">
                        <mat-icon fontSet="material-symbols-outlined">edit</mat-icon
                        >{{ 'mx.column.edit' | dcTranslate }}
                      </button>
                      <button mat-menu-item (click)="moveColumn(c, -1)">
                        <mat-icon fontSet="material-symbols-outlined">arrow_back</mat-icon
                        >{{ 'mx.moveBefore' | dcTranslate }}
                      </button>
                      <button mat-menu-item (click)="moveColumn(c, 1)">
                        <mat-icon fontSet="material-symbols-outlined">arrow_forward</mat-icon
                        >{{ 'mx.moveAfter' | dcTranslate }}
                      </button>
                      <button mat-menu-item (click)="deleteColumn(c)">
                        <mat-icon fontSet="material-symbols-outlined">delete</mat-icon
                        >{{ 'mx.remove' | dcTranslate }}
                      </button>
                    </mat-menu>
                  }
                </div>
                <span class="mx-grid__code" dir="ltr">{{ c.code }}</span>
              </th>
            }
            @if (editable()) {
              <th class="mx-grid__add">
                <button matButton type="button" (click)="addColumn()" data-testid="mx-add-column">
                  <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
                  {{ 'mx.column.add' | dcTranslate }}
                </button>
              </th>
            }
          </tr>
        </thead>
        <tbody>
          @for (r of matrix().rows; track r.code) {
            <tr [class.mx-grid__inactive]="!r.active" [attr.data-testid]="'mx-row-' + r.code">
              <th class="mx-grid__row">
                <div class="mx-grid__head">
                  <span class="mx-grid__name">{{
                    { ar: r.nameAr, en: r.nameEn } | dcLocalize
                  }}</span>
                  @if (editable()) {
                    <button
                      matIconButton
                      [matMenuTriggerFor]="rowMenu"
                      [attr.aria-label]="'mx.more' | dcTranslate"
                      class="mx-grid__menu"
                    >
                      <mat-icon fontSet="material-symbols-outlined">more_vert</mat-icon>
                    </button>
                    <mat-menu #rowMenu="matMenu">
                      <button mat-menu-item (click)="editRow(r)">
                        <mat-icon fontSet="material-symbols-outlined">edit</mat-icon
                        >{{ 'mx.row.edit' | dcTranslate }}
                      </button>
                      <button mat-menu-item (click)="copyRow(r)">
                        <mat-icon fontSet="material-symbols-outlined">content_copy</mat-icon
                        >{{ 'mx.row.copy' | dcTranslate }}
                      </button>
                      <button mat-menu-item (click)="moveRow(r, -1)">
                        <mat-icon fontSet="material-symbols-outlined">arrow_upward</mat-icon
                        >{{ 'mx.moveUp' | dcTranslate }}
                      </button>
                      <button mat-menu-item (click)="moveRow(r, 1)">
                        <mat-icon fontSet="material-symbols-outlined">arrow_downward</mat-icon
                        >{{ 'mx.moveDown' | dcTranslate }}
                      </button>
                      <button mat-menu-item (click)="deleteRow(r)">
                        <mat-icon fontSet="material-symbols-outlined">delete</mat-icon
                        >{{ 'mx.remove' | dcTranslate }}
                      </button>
                    </mat-menu>
                  }
                </div>
                <span class="mx-grid__code" dir="ltr">{{ r.code }}</span>
                <span class="mx-grid__final"
                  >{{ 'mx.final.' + r.final | dcTranslate
                  }}{{ r.active ? '' : ' · ' + ('mx.row.inactive' | dcTranslate) }}</span
                >
              </th>
              @for (c of matrix().columns; track c.code) {
                @let cell = cellAt(r.code, c.code);
                <td class="mx-grid__cell">
                  <div
                    class="mx-card"
                    [class.mx-card--empty]="!cell"
                    [class.mx-card--same]="cell?.kind === 'same'"
                  >
                    <button
                      type="button"
                      class="mx-card__main"
                      (click)="editCell(r.code, c.code)"
                      [disabled]="!editable()"
                      [attr.data-testid]="'mx-cell-' + r.code + '-' + c.code"
                    >
                      <mat-icon fontSet="material-symbols-outlined" class="mx-card__kind">{{
                        cellIcons[cell?.kind ?? 'empty']
                      }}</mat-icon>
                      <span class="mx-card__text">{{ summary(r.code, c.code) }}</span>
                    </button>
                    @if (sources(r.code, c.code); as list) {
                      @if (list.length) {
                        <div class="mx-card__sources">
                          @for (s of list; track s.kind) {
                            <button
                              type="button"
                              class="mx-chip"
                              [class]="'mx-chip mx-chip--' + s.kind"
                              [matMenuTriggerFor]="srcMenu"
                              [matTooltip]="
                                ('mx.source.' + s.kind | dcTranslate) + ' (' + s.inputs.length + ')'
                              "
                              [attr.data-testid]="'mx-src-' + r.code + '-' + c.code + '-' + s.kind"
                            >
                              <mat-icon fontSet="material-symbols-outlined">{{
                                sourceIcons[s.kind]
                              }}</mat-icon>
                              {{ s.inputs.length }}
                            </button>
                            <mat-menu #srcMenu="matMenu">
                              <p class="mx-menu__title">
                                {{ 'mx.source.' + s.kind | dcTranslate }}
                              </p>
                              @for (i of s.inputs; track i.code) {
                                <button
                                  mat-menu-item
                                  (click)="editInput(i)"
                                  [attr.data-testid]="'mx-src-input-' + i.code"
                                >
                                  <mat-icon fontSet="material-symbols-outlined">{{
                                    sourceIcons[s.kind]
                                  }}</mat-icon>
                                  <span>{{ { ar: i.nameAr, en: i.nameEn } | dcLocalize }}</span>
                                  <span class="mx-menu__detail" dir="ltr">{{
                                    sourceDetail(i)
                                  }}</span>
                                </button>
                              }
                            </mat-menu>
                          }
                        </div>
                      }
                    }
                  </div>
                </td>
              }
              @if (editable()) {
                <td></td>
              }
            </tr>
          }
        </tbody>
      </table>
    </div>
    @if (editable()) {
      <button matButton type="button" (click)="addRow()" data-testid="mx-add-row">
        <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
        {{ 'mx.row.add' | dcTranslate }}
      </button>
    }
    <div class="mx-legend">
      @for (k of legend; track k) {
        <span class="mx-chip" [class]="'mx-chip mx-chip--' + k"
          ><mat-icon fontSet="material-symbols-outlined">{{ sourceIcons[k] }}</mat-icon>
          {{ 'mx.source.' + k | dcTranslate }}</span
        >
      }
    </div>
  `,
  styles: `
    .mx-grid__scroll {
      overflow: auto;
      max-width: 100%;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 10px;
    }
    .mx-grid {
      border-collapse: separate;
      border-spacing: 0;
      min-width: 100%;
    }
    .mx-grid th,
    .mx-grid td {
      border-bottom: 1px solid var(--dga-gray-200, #e5e7eb);
      border-inline-end: 1px solid var(--dga-gray-100, #f3f4f6);
      padding: 6px;
      vertical-align: top;
      text-align: start;
    }
    .mx-grid thead th {
      position: sticky;
      top: 0;
      background: var(--dga-gray-50, #f9fafb);
      z-index: 1;
    }
    .mx-grid__corner {
      min-width: 12rem;
      font-size: 12px;
      color: var(--dga-text-3, #6c737f);
      font-weight: 500;
    }
    .mx-grid__row {
      position: sticky;
      inset-inline-start: 0;
      background: var(--dga-gray-50, #f9fafb);
      min-width: 12rem;
      max-width: 16rem;
      z-index: 1;
    }
    .mx-grid__col {
      min-width: 13rem;
    }
    .mx-grid__head {
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .mx-grid__name {
      flex: 1;
      font-weight: 600;
      font-size: 14px;
    }
    .mx-grid__menu {
      flex: none;
      margin: -6px 0;
    }
    .mx-grid__code,
    .mx-grid__final {
      display: block;
      font-size: 11px;
      color: var(--dga-text-3, #6c737f);
      font-weight: 400;
    }
    .mx-grid__code {
      font-family: ui-monospace, monospace;
    }
    .mx-grid__inactive .mx-grid__cell,
    .mx-grid__inactive .mx-grid__row {
      opacity: 0.55;
    }
    .mx-grid__add {
      min-width: 9rem;
      vertical-align: middle;
    }
    .mx-card {
      display: flex;
      flex-direction: column;
      gap: 6px;
      border: 1px solid var(--dga-gray-300, #d2d6db);
      border-radius: 8px;
      background: #fff;
      padding: 6px 8px;
      min-height: 4.5rem;
    }
    .mx-card--empty {
      border-style: dashed;
      background: transparent;
    }
    .mx-card--same {
      background: var(--dga-gray-50, #f9fafb);
    }
    .mx-card:hover {
      border-color: var(--mat-sys-primary, #1b8354);
    }
    .mx-card__main {
      display: flex;
      align-items: flex-start;
      gap: 6px;
      border: 0;
      padding: 0;
      background: none;
      font: inherit;
      font-size: 12px;
      text-align: start;
      color: inherit;
      cursor: pointer;
    }
    .mx-card__main:disabled {
      cursor: default;
    }
    .mx-card__kind {
      flex: none;
      font-size: 18px;
      width: 18px;
      height: 18px;
      color: var(--mat-sys-primary, #1b8354);
    }
    .mx-card__text {
      flex: 1;
    }
    .mx-card__sources {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
    }
    .mx-chip {
      display: inline-flex;
      align-items: center;
      gap: 2px;
      border: 1px solid transparent;
      border-radius: 12px;
      padding: 1px 7px 1px 5px;
      font: inherit;
      font-size: 11px;
      cursor: pointer;
      background: var(--dga-gray-100, #f3f4f6);
      color: var(--dga-text-2, #384250);
    }
    .mx-chip mat-icon {
      font-size: 14px;
      width: 14px;
      height: 14px;
    }
    .mx-chip:hover {
      border-color: currentColor;
    }
    .mx-chip--applicant {
      background: #e8f1fd;
      color: #1d4ed8;
    }
    .mx-chip--reviewer {
      background: #fdf3e2;
      color: #a15c07;
    }
    .mx-chip--integration {
      background: #e7f6ee;
      color: #11703c;
    }
    .mx-chip--party {
      background: #f1ecfd;
      color: #5b21b6;
    }
    .mx-chip--derived,
    .mx-chip--projects {
      background: #eef2f6;
      color: #364152;
    }
    .mx-legend {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
      margin-top: 0.75rem;
    }
    .mx-legend .mx-chip {
      cursor: default;
    }
    .mx-menu__title {
      margin: 4px 16px;
      font-size: 12px;
      font-weight: 600;
      color: var(--dga-text-3, #6c737f);
    }
    .mx-menu__detail {
      margin-inline-start: 8px;
      font-size: 11px;
      color: var(--dga-text-3, #6c737f);
      font-family: ui-monospace, monospace;
    }
  `,
})
export class MatrixGrid {
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(I18nService);
  readonly matrix = input.required<Matrix>();
  readonly references = input.required<MatrixReferences>();
  readonly editable = input(true);
  readonly matrixChange = output<Matrix>();
  protected readonly cellIcons = CELL_ICONS;
  protected readonly sourceIcons = SOURCE_ICONS;
  protected readonly legend: SourceKind[] = [
    'applicant',
    'reviewer',
    'integration',
    'party',
    'derived',
    'projects',
  ];

  /** Each cell's inputs by source kind, worked out once per matrix. */
  private readonly sourceMap = computed(() => {
    const m = this.matrix();
    const map = new Map<string, { kind: SourceKind; inputs: MatrixInput[] }[]>();
    for (const r of m.rows) {
      for (const c of m.columns) {
        const inputs = cellInputs(m, cellOf(m, r.code, c.code));
        const kinds = [...new Set(inputs.map((i) => i.source.kind))];
        map.set(
          `${r.code}|${c.code}`,
          kinds.map((kind) => ({ kind, inputs: inputs.filter((i) => i.source.kind === kind) })),
        );
      }
    }
    return map;
  });

  protected cellAt(row: string, column: string) {
    return cellOf(this.matrix(), row, column);
  }

  protected summary(row: string, column: string): string {
    return cellSummary(this.matrix(), this.cellAt(row, column), (k) => this.i18n.translate(k));
  }

  protected sources(row: string, column: string) {
    return this.sourceMap().get(`${row}|${column}`) ?? [];
  }

  /** A short line saying exactly where the value is read: question code, task › field, operation › output… */
  protected sourceDetail(i: MatrixInput): string {
    const s = i.source;
    switch (s.kind) {
      case 'applicant':
        return s.question ?? '?';
      case 'reviewer':
        return `${s.step ?? '?'} › ${s.field ?? '?'}`;
      case 'integration':
        return `${s.operation ?? '?'} › ${s.output ?? '?'}`;
      case 'party':
        return s.path ?? '?';
      case 'projects':
        return `${s.function ?? 'sum'}(${s.column ?? ''})`;
      default:
        return i.code;
    }
  }

  private emit(m: Matrix): void {
    this.matrixChange.emit(m);
  }

  protected async editCell(row: string, column: string): Promise<void> {
    const m = this.matrix();
    const result = await firstValueFrom(
      this.dialog
        .open<MatrixCellEditor, MatrixCellEditorData, MatrixCellEditorResult>(MatrixCellEditor, {
          data: {
            matrix: m,
            row,
            column,
            cell: cellOf(m, row, column),
            references: this.references(),
          },
          width: '72rem',
          maxWidth: '96vw',
        })
        .afterClosed(),
    );
    if (result) this.emit(withCell({ ...m, inputs: result.inputs }, result.cell));
  }

  protected async editInput(input: MatrixInput): Promise<void> {
    const m = this.matrix();
    const result = await firstValueFrom(
      this.dialog
        .open<
          MatrixInputEditor,
          MatrixInputEditorData,
          { input: MatrixInput | null; removed?: boolean }
        >(MatrixInputEditor, {
          data: { matrix: m, input, isNew: false, references: this.references() },
          width: '60rem',
          maxWidth: '96vw',
        })
        .afterClosed(),
    );
    if (!result) return;
    const inputs = result.removed
      ? m.inputs.filter((i) => i.code !== input.code)
      : m.inputs.map((i) => (i.code === input.code ? result.input! : i));
    this.emit({ ...m, inputs });
  }

  private async header(kind: 'row' | 'column', item: MatrixRow | MatrixColumn, isNew: boolean) {
    const m = this.matrix();
    const used = (kind === 'row' ? m.rows : m.columns)
      .map((x) => x.code)
      .filter((c) => isNew || c !== item.code);
    return firstValueFrom(
      this.dialog
        .open<MatrixHeaderDialog, HeaderDialogData, MatrixRow | MatrixColumn>(MatrixHeaderDialog, {
          data: { kind, item, isNew, used, fields: trackFields(m) },
          width: '32rem',
          maxWidth: '96vw',
        })
        .afterClosed(),
    );
  }

  protected async addRow(): Promise<void> {
    const m = this.matrix();
    const code = freeCode(
      m.rows.map((r) => r.code),
      'ROW',
    );
    const row = (await this.header(
      'row',
      { code, nameAr: '', nameEn: '', active: true, final: 'worst' },
      true,
    )) as MatrixRow | undefined;
    if (row) this.emit({ ...this.matrix(), rows: [...this.matrix().rows, row] });
  }

  protected async editRow(row: MatrixRow): Promise<void> {
    const saved = (await this.header('row', row, false)) as MatrixRow | undefined;
    if (!saved) return;
    const m =
      saved.code !== row.code ? renameRow(this.matrix(), row.code, saved.code) : this.matrix();
    this.emit({ ...m, rows: m.rows.map((r) => (r.code === saved.code ? saved : r)) });
  }

  /** A new sector starting from this one's cells, settings and gates. */
  protected async copyRow(row: MatrixRow): Promise<void> {
    const m = this.matrix();
    const code = freeCode(
      m.rows.map((r) => r.code),
      `${row.code}-`,
    );
    const saved = (await this.header('row', { ...structuredClone(row), code }, true)) as
      MatrixRow | undefined;
    if (!saved) return;
    const cells = m.cells
      .filter((c) => c.row === row.code)
      .map((c) => ({ ...structuredClone(c), row: saved.code }));
    this.emit({ ...m, rows: [...m.rows, saved], cells: [...m.cells, ...cells] });
  }

  protected deleteRow(row: MatrixRow): void {
    if (confirm(this.i18n.translate('mx.row.confirmRemove')))
      this.emit(removeRow(this.matrix(), row.code));
  }

  protected moveRow(row: MatrixRow, by: number): void {
    this.emit({ ...this.matrix(), rows: move(this.matrix().rows, row, by) });
  }

  protected async addColumn(): Promise<void> {
    const m = this.matrix();
    const code = freeCode(
      m.columns.map((c) => c.code),
      'track',
    );
    const column = (await this.header(
      'column',
      { code, nameAr: '', nameEn: '', includeInFinal: true },
      true,
    )) as MatrixColumn | undefined;
    if (column) this.emit({ ...this.matrix(), columns: [...this.matrix().columns, column] });
  }

  protected async editColumn(column: MatrixColumn): Promise<void> {
    const saved = (await this.header('column', column, false)) as MatrixColumn | undefined;
    if (!saved) return;
    const m =
      saved.code !== column.code
        ? renameColumn(this.matrix(), column.code, saved.code)
        : this.matrix();
    this.emit({ ...m, columns: m.columns.map((c) => (c.code === saved.code ? saved : c)) });
  }

  protected deleteColumn(column: MatrixColumn): void {
    if (confirm(this.i18n.translate('mx.column.confirmRemove')))
      this.emit(removeColumn(this.matrix(), column.code));
  }

  protected moveColumn(column: MatrixColumn, by: number): void {
    this.emit({ ...this.matrix(), columns: move(this.matrix().columns, column, by) });
  }
}

function move<T>(list: T[], item: T, by: number): T[] {
  const from = list.indexOf(item);
  const to = Math.max(0, Math.min(list.length - 1, from + by));
  if (from < 0 || from === to) return list;
  const copy = [...list];
  copy.splice(from, 1);
  copy.splice(to, 0, item);
  return copy;
}
