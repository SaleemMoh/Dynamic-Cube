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
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import type { FormValue } from './dynamic-form';
import { FileUploader } from './file-input';
import { FormField, FormOption } from './form-schema';
import { LOV_SOURCE } from './lov-source';
import { RECORD_PROVIDER, RecordPick } from './record-source';
import { RowDialog } from './row-dialog';
import { ROW_FORM, Row, RowDialogData, rowFiles } from './row-form';
import { MatCheckboxModule } from '@angular/material/checkbox';

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
          @if (canPick()) {
            <button
              matButton="outlined"
              type="button"
              (click)="pick()"
              [disabled]="full()"
              [attr.data-testid]="'rows-pick-' + field().code"
            >
              <mat-icon>playlist_add_check</mat-icon>
              {{ 'rows.pick' | dcTranslate }}
            </button>
          }
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
  private readonly recordProvider = inject(RECORD_PROVIDER, { optional: true });
  /** The form of a row: the dynamic form this field sits in (it provides itself). */
  private readonly rowForm = inject(ROW_FORM);
  private readonly destroyRef = inject(DestroyRef);
  /** Whether rows can be picked from a record list here. */
  protected readonly canPick = computed(() => !!this.recordProvider && !!this.field().rows?.source);
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

  /** Picks records of the field's record list and adds them as rows (their values go into the columns of the same code). */
  protected async pick(): Promise<void> {
    const field = this.field();
    const picked = await this.recordProvider!.load(field.rows!.source!);
    if (!picked) return;
    const room = (field.rows?.maxRows ?? Number.MAX_SAFE_INTEGER) - this.rows().length;
    const ref = this.dialog.open<RecordPicker, RecordPickerData, Row[]>(RecordPicker, {
      width: '760px',
      maxWidth: '95vw',
      data: { picked, room },
    });
    const subscription = ref.afterClosed().subscribe((records) => {
      this.onTouched();
      if (!records?.length) return;
      const columns = field.rows?.columns ?? [];
      const rows = records.map((record) => {
        const row: Row = {};
        for (const column of columns) {
          const value = record[column.code];
          if (value === null || value === undefined || value === '') continue;
          if (
            column.type === 'select' &&
            !column.lov &&
            !(column.options ?? []).some((o) => o.value === value)
          )
            continue;
          if (column.type === 'file') continue;
          row[column.code] = value;
        }
        return row;
      });
      this.commit([...this.rows(), ...rows.filter((r) => Object.keys(r).length > 0)]);
    });
    this.destroyRef.onDestroy(() => subscription.unsubscribe());
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
        form: this.rowForm,
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

interface RecordPickerData {
  picked: RecordPick;
  /** How many more rows the field takes. */
  room: number;
}

/** The records of a list to pick from, with a box each. */
@Component({
  selector: 'dc-record-picker',
  imports: [MatDialogModule, MatButtonModule, MatCheckboxModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ 'rows.pickFrom' | dcTranslate }} {{ data.picked.title | dcLocalize }}
    </h2>
    <mat-dialog-content>
      @if (data.picked.rows.length) {
        <div class="dc-rows__scroll">
          <table class="dc-picker__table">
            <thead>
              <tr>
                <th scope="col">
                  <span class="cdk-visually-hidden">{{ 'rows.choose' | dcTranslate }}</span>
                </th>
                @for (c of data.picked.columns; track c.code) {
                  <th scope="col">{{ c.label | dcLocalize }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (r of data.picked.rows; track r.id) {
                <tr>
                  <td>
                    <mat-checkbox
                      [checked]="chosen().has(r.id)"
                      [disabled]="!chosen().has(r.id) && chosen().size >= data.room"
                      (change)="toggle(r.id)"
                      [attr.data-testid]="'pick-' + r.id"
                    />
                  </td>
                  @for (c of data.picked.columns; track c.code) {
                    <td>{{ text(c, r.data[c.code]) }}</td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      } @else {
        <p class="dc-muted">{{ 'rows.noRecords' | dcTranslate }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" mat-dialog-close>{{ 'rows.cancel' | dcTranslate }}</button>
      <button
        matButton="filled"
        type="button"
        [disabled]="!chosen().size"
        (click)="done()"
        data-testid="pick-done"
      >
        {{ 'rows.addChosen' | dcTranslate }} ({{ chosen().size }})
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-rows__scroll {
      overflow-x: auto;
    }
    .dc-picker__table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.875rem;
    }
    .dc-picker__table th,
    .dc-picker__table td {
      padding: 4px 8px;
      border-bottom: 1px solid var(--dc-border, #d9dee3);
      text-align: start;
    }
  `,
})
export class RecordPicker {
  protected readonly data = inject<RecordPickerData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<RecordPicker, Row[]>>(MatDialogRef);
  private readonly i18n = inject(I18nService);
  protected readonly chosen = signal<ReadonlySet<string>>(new Set());

  protected toggle(id: string): void {
    const next = new Set(this.chosen());
    if (next.has(id)) next.delete(id);
    else next.add(id);
    this.chosen.set(next);
  }

  protected text(column: RecordPick['columns'][number], value: unknown): string {
    if (value === null || value === undefined || value === '') return '—';
    const ar = this.i18n.language() === 'ar';
    if (typeof value === 'boolean') return value ? (ar ? 'نعم' : 'Yes') : ar ? 'لا' : 'No';
    const option = column.options?.find((o) => o.value === value);
    return option ? (ar ? option.label.ar : option.label.en) : String(value);
  }

  protected done(): void {
    this.ref.close(this.data.picked.rows.filter((r) => this.chosen().has(r.id)).map((r) => r.data));
  }
}
