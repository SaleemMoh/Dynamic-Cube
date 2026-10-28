import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { ConfigRow, LocalizePipe, LocalizedText, TranslatePipe } from '@dynamic-cube/shared';
import { ConfigType } from '../config/config-types';

export interface DetailsData {
  title: LocalizedText;
  code: string;
  rows: { label: LocalizedText; value: string; ltr?: boolean }[];
}

/** A row's fields, read-only (the eye button of the master lists and tables). */
@Component({
  selector: 'dc-details-dialog',
  imports: [MatButtonModule, MatDialogModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ data.title | dcLocalize }}
      <span class="mono dc-muted">{{ data.code }}</span>
    </h2>
    <mat-dialog-content>
      <dl class="dc-details" data-testid="details">
        @for (row of data.rows; track $index) {
          <dt>{{ row.label | dcLocalize }}</dt>
          <dd [attr.dir]="row.ltr ? 'ltr' : null">{{ row.value || '—' }}</dd>
        }
      </dl>
    </mat-dialog-content>
    <mat-dialog-actions>
      <button matButton="outlined" mat-dialog-close>{{ 'common.close' | dcTranslate }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-details {
      display: grid;
      grid-template-columns: minmax(10rem, max-content) 1fr;
      gap: 10px 24px;
      margin: 0;
    }
    dt {
      color: var(--dga-gray-500);
    }
    dd {
      margin: 0;
      color: var(--dga-gray-900);
      white-space: pre-wrap;
      overflow-wrap: anywhere;
    }
    [dir='ltr'] {
      text-align: start;
    }
  `,
})
export class DetailsDialog {
  protected readonly data = inject<DetailsData>(MAT_DIALOG_DATA);
}

/** Text of a stored value: lists joined, flags as ✓ / —, objects as JSON. */
export function displayValue(value: unknown): string {
  if (value === null || value === undefined || value === '') {
    return '';
  }
  if (typeof value === 'boolean') {
    return value ? '✓' : '—';
  }
  if (Array.isArray(value)) {
    return value.map((v) => (typeof v === 'object' ? JSON.stringify(v) : String(v))).join('، ');
  }
  return typeof value === 'object' ? JSON.stringify(value, null, 2) : String(value);
}

/** Details of a configuration row from its type's field definitions. */
export function detailsOf(type: ConfigType, row: ConfigRow): DetailsData {
  return {
    title: { ar: row.nameAr, en: row.nameEn },
    code: row.code,
    rows: [
      { label: { ar: 'الاسم بالعربية', en: 'Arabic name' }, value: row.nameAr },
      { label: { ar: 'الاسم بالإنجليزية', en: 'English name' }, value: row.nameEn },
      { label: { ar: 'فعال', en: 'Active' }, value: displayValue(row.isActive) },
      ...type.fields.map((f) => ({ label: f.label, value: displayValue(row[f.code]), ltr: f.type === 'json' || !!f.ref })),
    ],
  };
}
