import { InjectionToken, OutputRef, Type } from '@angular/core';
import { LocalizedText } from '../i18n/language';
import type { FormValue } from './dynamic-form';
import { FileUploader } from './file-input';
import { FormField } from './form-schema';

/**
 * The form component a rows field opens for a row: the dynamic form it sits in provides itself, so neither the rows field
 * nor the row dialog imports it (no import cycle, and no form code in an app's first download).
 */
export const ROW_FORM = new InjectionToken<Type<unknown>>('ROW_FORM');

/** What the row dialog needs of the form it creates. */
export interface RowFormInstance {
  submitted: OutputRef<FormValue>;
  cancelled: OutputRef<void>;
  edited: OutputRef<FormValue>;
}

export type Row = Record<string, unknown>;

/** What the row dialog is given: the columns, the row being edited (none: a new row) and where its files go. */
export interface RowDialogData {
  title: LocalizedText;
  columns: FormField[];
  row: Row | null;
  context: FormValue;
  uploader: FileUploader | null;
  /** The form component (the shared dynamic form), created inside the dialog. */
  form: Type<unknown>;
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
