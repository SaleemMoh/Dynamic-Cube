import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  forwardRef,
  inject,
  input,
  signal,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { I18nService } from '../i18n/i18n.service';
import { LocalizedText } from '../i18n/language';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { DynamicForm, FormValue } from './dynamic-form';
import { FileUploader } from './file-input';
import { FormField, FormOption, FormSchema } from './form-schema';
import { LOV_SOURCE } from './lov-source';

export type Row = Record<string, unknown>;

/** What the row dialog is given: the columns, the row being edited (none: a new row) and where its files go. */
interface RowDialogData {
  title: LocalizedText;
  columns: FormField[];
  row: Row | null;
  context: FormValue;
  uploader: FileUploader | null;
}

/** A date as the API keeps it (yyyy-MM-dd); other values as they are. */
function toStored(value: unknown): unknown {
  return value instanceof Date
    ? `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`
    : value;
}

/** A row's values for its form: date columns become dates for the date picker. */
export function rowForForm(columns: FormField[], row: Row): Row {
  const result: Row = { ...row };
  for (const column of columns) {
    const value = row[column.code];
    if (column.type === 'date' && typeof value === 'string' && /^\d{4}-\d{2}-\d{2}/.test(value)) {
      const [y, m, d] = value.slice(0, 10).split('-').map(Number);
      result[column.code] = new Date(y, m - 1, d);
    }
  }
  return result;
}

/** A row as it is kept: dates as yyyy-MM-dd, empty values left out. */
export function rowForStorage(row: Row): Row {
  const result: Row = {};
  for (const [key, value] of Object.entries(row)) {
    const stored = toStored(value);
    if (
      stored !== null &&
      stored !== undefined &&
      stored !== '' &&
      !(Array.isArray(stored) && stored.length === 0)
    )
      result[key] = stored;
  }
  return result;
}

/** The ids of the files of a row (in its file columns). */
export function rowFiles(columns: FormField[], row: Row | null): { column: string; id: string }[] {
  if (!row) return [];
  return columns
    .filter((c) => c.type === 'file')
    .flatMap((c) => {
      const value = row[c.code];
      const ids = Array.isArray(value) ? value : value ? [value] : [];
      return ids.map((id) => ({ column: c.code, id: String(id) }));
    });
}

/** The columns shown in the table: the field's choice, or the first four. */
export function tableColumns(field: FormField): FormField[] {
  const columns = field.rows?.columns ?? [];
  const chosen = field.rows?.tableColumns?.length
    ? columns.filter((c) => field.rows!.tableColumns!.includes(c.code))
    : [];
  return chosen.length ? chosen : columns.slice(0, 4);
}

/** A file uploader for a row's columns: files are kept under FIELD/COLUMN. */
function columnUploader(uploader: FileUploader | null, field: string): FileUploader | null {
  if (!uploader) return null;
  return {
    files: () => uploader.files(),
    upload: (column, file) => uploader.upload(`${field}/${column}`, file),
    remove: (column, id) => uploader.remove(`${field}/${column}`, id),
    open: (id) => uploader.open(id),
  };
}

/**
 * Repeating rows (projects, owners, branches…): a table of the rows with a form per row, opened to add or edit one. The
 * row's form is the shared {@link DynamicForm}, so a column can be any field type, with its rules, lists of values and
 * files (kept under FIELD/COLUMN).
 */
@Component({
  selector: 'dc-rows-input',
  imports: [MatButtonModule, MatIconModule, MatTooltipModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => RowsInput), multi: true },
  ],
  template: `
    <div class="dc-rows" [attr.data-testid]="'rows-' + field().code">
      @if (rows().length) {
        <div class="dc-rows__scroll">
          <table class="dc-rows__table">
            <thead>
              <tr>
                <th scope="col" class="dc-rows__number">#</th>
                @for (column of shown(); track column.code) {
                  <th scope="col">{{ column.label | dcLocalize }}</th>
                }
                <th scope="col" class="dc-rows__actions">
                  <span class="cdk-visually-hidden">{{ 'rows.actions' | dcTranslate }}</span>
                </th>
              </tr>
            </thead>
            <tbody>
              @for (row of rows(); track $index; let i = $index) {
                <tr [attr.data-testid]="'row-' + i">
                  <td class="dc-rows__number">{{ i + 1 }}</td>
                  @for (column of shown(); track column.code) {
                    <td>{{ cell(column, row) }}</td>
                  }
                  <td class="dc-rows__actions">
                    @if (!disabled()) {
                      <button
                        matIconButton
                        type="button"
                        (click)="edit(i)"
                        [matTooltip]="'rows.edit' | dcTranslate"
                        [attr.aria-label]="('rows.edit' | dcTranslate) + ' ' + (i + 1)"
                        [attr.data-testid]="'row-edit-' + i"
                      >
                        <mat-icon>edit</mat-icon>
                      </button>
                      <button
                        matIconButton
                        type="button"
                        (click)="remove(i)"
                        [matTooltip]="'rows.remove' | dcTranslate"
                        [attr.aria-label]="('rows.remove' | dcTranslate) + ' ' + (i + 1)"
                        [attr.data-testid]="'row-remove-' + i"
                      >
                        <mat-icon>delete</mat-icon>
                      </button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      } @else {
        <p class="dc-muted dc-rows__empty">{{ 'rows.empty' | dcTranslate }}</p>
      }
      <div class="dc-rows__footer">
        @if (!disabled()) {
          <button
            matButton="outlined"
            type="button"
            (click)="add()"
            [disabled]="full()"
            [attr.data-testid]="'rows-add-' + field().code"
          >
            <mat-icon>add</mat-icon>
            {{ 'rows.add' | dcTranslate }}
          </button>
        }
        @if (field().rows?.maxRows; as most) {
          <small class="dc-muted"
            >{{ 'rows.count' | dcTranslate }} {{ rows().length }} {{ 'rows.of' | dcTranslate }}
            {{ most }}</small
          >
        }
      </div>
    </div>
  `,
  styles: `
    .dc-rows {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .dc-rows__scroll {
      overflow-x: auto;
      border: 1px solid var(--dc-border, #d9dee3);
      border-radius: 8px;
    }
    .dc-rows__table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.875rem;
    }
    .dc-rows__table th,
    .dc-rows__table td {
      padding: 6px 10px;
      text-align: start;
      border-bottom: 1px solid var(--dc-border, #d9dee3);
      vertical-align: middle;
    }
    .dc-rows__table tbody tr:last-child td {
      border-bottom: 0;
    }
    .dc-rows__table th {
      font-weight: 600;
      background: var(--dc-surface-2, #f6f7f9);
    }
    .dc-rows__number {
      width: 36px;
      color: var(--dc-muted, #667085);
    }
    .dc-rows__actions {
      width: 96px;
      white-space: nowrap;
      text-align: end;
    }
    .dc-rows__empty {
      margin: 0;
    }
    .dc-rows__footer {
      display: flex;
      align-items: center;
      gap: 12px;
      flex-wrap: wrap;
    }
  `,
})
export class RowsInput implements ControlValueAccessor {
  readonly field = input.required<FormField>();
  /** The form's values (and its outside context), seen by the columns' rules under the row's own values. */
  readonly context = input<FormValue>({});
  readonly uploader = input<FileUploader | null>(null);

  protected readonly rows = signal<Row[]>([]);
  protected readonly disabled = signal(false);
  protected readonly shown = computed(() => tableColumns(this.field()));
  protected readonly full = computed(() => {
    const most = this.field().rows?.maxRows;
    return most !== undefined && this.rows().length >= most;
  });
  private readonly lovLabels = signal<Readonly<Record<string, FormOption[]>>>({});
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(I18nService);
  private readonly lovSource = inject(LOV_SOURCE, { optional: true });
  private readonly destroyRef = inject(DestroyRef);
  private onChange: (value: Row[]) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  constructor() {
    // Labels of list-of-values columns shown in the table (columns of a row have no parent list).
    effect(() => {
      if (!this.lovSource) return;
      for (const column of this.shown().filter((c) => c.lov && !c.lovParentField)) {
        if (this.lovLabels()[column.code]) continue;
        const subscription = this.lovSource.options(column.lov!, null).subscribe({
          next: (options) => this.lovLabels.update((all) => ({ ...all, [column.code]: options })),
          error: () => this.lovLabels.update((all) => ({ ...all, [column.code]: [] })),
        });
        this.destroyRef.onDestroy(() => subscription.unsubscribe());
      }
    });
  }

  writeValue(value: unknown): void {
    this.rows.set(
      Array.isArray(value)
        ? value.filter((r): r is Row => !!r && typeof r === 'object' && !Array.isArray(r))
        : [],
    );
  }

  registerOnChange(fn: (value: Row[]) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.disabled.set(disabled);
  }

  /** A cell as text: option labels, yes/no, file names, numbers and dates as they are. */
  protected cell(column: FormField, row: Row): string {
    const value = row[column.code];
    const ar = this.i18n.language() === 'ar';
    const join = (items: string[]) => items.join(ar ? '، ' : ', ');
    if (
      value === null ||
      value === undefined ||
      value === '' ||
      (Array.isArray(value) && value.length === 0)
    )
      return '—';
    if (column.type === 'checkbox') return value ? (ar ? 'نعم' : 'Yes') : ar ? 'لا' : 'No';
    if (column.type === 'file') {
      const files = this.uploader()?.files() ?? [];
      const ids = Array.isArray(value) ? value : [value];
      return join(ids.map((id) => files.find((f) => f.id === id)?.fileName ?? String(id)));
    }
    if (column.type === 'select') {
      const options = column.lov ? (this.lovLabels()[column.code] ?? []) : (column.options ?? []);
      const label = (v: unknown) => {
        const option = options.find((o) => o.value === v);
        return option ? (ar ? option.label.ar : option.label.en) : String(v);
      };
      return Array.isArray(value) ? join(value.map(label)) : label(value);
    }
    return String(value);
  }

  protected add(): void {
    this.open(null, (row) => this.commit([...this.rows(), row]));
  }

  protected edit(index: number): void {
    this.open(this.rows()[index], (row) =>
      this.commit(this.rows().map((r, i) => (i === index ? row : r))),
    );
  }

  /** Removes a row and the files uploaded for it. */
  protected async remove(index: number): Promise<void> {
    const row = this.rows()[index];
    const uploader = columnUploader(this.uploader(), this.field().code);
    this.commit(this.rows().filter((_, i) => i !== index));
    for (const file of rowFiles(this.field().rows?.columns ?? [], row)) {
      await uploader?.remove(file.column, file.id).catch(() => undefined);
    }
  }

  private open(row: Row | null, done: (row: Row) => void): void {
    const field = this.field();
    const ref = this.dialog.open<RowDialog, RowDialogData, Row>(RowDialog, {
      width: '720px',
      maxWidth: '95vw',
      autoFocus: 'first-tabbable',
      // Escape and the backdrop cancel through the dialog, which removes files uploaded for nothing.
      disableClose: true,
      data: {
        title: field.label,
        columns: field.rows?.columns ?? [],
        row,
        context: this.context(),
        uploader: columnUploader(this.uploader(), field.code),
      },
    });
    const subscription = ref.afterClosed().subscribe((result) => {
      this.onTouched();
      if (result) done(result);
    });
    this.destroyRef.onDestroy(() => subscription.unsubscribe());
  }

  private commit(rows: Row[]): void {
    this.rows.set(rows);
    this.onChange(rows);
  }
}

/** The form of one row, in a dialog. Files uploaded and then cancelled are removed again. */
@Component({
  selector: 'dc-row-dialog',
  imports: [
    MatDialogModule,
    MatButtonModule,
    forwardRef(() => DynamicForm),
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ data.title | dcLocalize }} — {{ (data.row ? 'rows.edit' : 'rows.add') | dcTranslate }}
    </h2>
    <mat-dialog-content>
      <dc-dynamic-form
        [schema]="schema"
        [initialValue]="initial"
        [context]="data.context"
        [uploader]="data.uploader"
        [submitLabel]="'rows.save' | dcTranslate"
        submitIcon="check"
        [cancellable]="true"
        [cancelLabel]="'rows.cancel' | dcTranslate"
        (submitted)="save($event)"
        (cancelled)="cancel()"
        (edited)="typed = $event"
      />
    </mat-dialog-content>
  `,
})
export class RowDialog {
  protected readonly data = inject<RowDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<RowDialog, Row>>(MatDialogRef);
  protected readonly schema: FormSchema = {
    code: 'row',
    sections: [{ code: 'row', title: { ar: '', en: '' }, fields: this.data.columns }],
  };
  protected readonly initial = rowForForm(this.data.columns, this.data.row ?? {});
  /** The row as typed so far, to find files uploaded before a cancel. */
  protected typed: FormValue | null = null;

  constructor() {
    this.ref.backdropClick().subscribe(() => this.cancel());
    this.ref.keydownEvents().subscribe((event) => {
      if (event.key === 'Escape') this.cancel();
    });
  }

  protected save(value: FormValue): void {
    this.ref.close(rowForStorage(value));
  }

  protected async cancel(): Promise<void> {
    const kept = new Set(rowFiles(this.data.columns, this.data.row).map((f) => f.id));
    for (const file of rowFiles(this.data.columns, this.typed).filter((f) => !kept.has(f.id))) {
      await this.data.uploader?.remove(file.column, file.id).catch(() => undefined);
    }
    this.ref.close();
  }
}
