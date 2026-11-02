import { InjectionToken } from '@angular/core';
import { LocalizedText } from '../i18n/language';
import { FormOption } from './form-schema';

/** Records a rows field can pick from: a list's name, the columns to show and its records. */
export interface RecordPick {
  title: LocalizedText;
  columns: { code: string; label: LocalizedText; options?: FormOption[] }[];
  rows: { id: string; data: Record<string, unknown> }[];
}

/** Where a rows field finds the records of a list (the portal: the acting party's record lists). */
export interface RecordProvider {
  load(list: string): Promise<RecordPick | null>;
}

/** Provided by an app that can show the user's records; without it, rows fields offer no picking. */
export const RECORD_PROVIDER = new InjectionToken<RecordProvider>('RECORD_PROVIDER');
