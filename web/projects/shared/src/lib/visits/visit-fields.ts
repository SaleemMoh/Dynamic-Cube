import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { VisitField } from './visits.api';

/**
 * The form fields a visit type asks for (text, notes, number, date, list, tick box), editing a plain values object.
 * Used by staff recording a result and by the field app.
 */
@Component({
  selector: 'dc-visit-fields',
  imports: [FormsModule, MatCheckboxModule, MatFormFieldModule, MatInputModule, MatSelectModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (f of fields(); track f.code) {
      @let label = { ar: f.labelAr, en: f.labelEn } | dcLocalize;
      @switch (f.type) {
        @case ('CHECKBOX') {
          <mat-checkbox [ngModel]="value()[f.code] === true" (ngModelChange)="set(f.code, $event)" [attr.data-testid]="'visit-field-' + f.code">
            {{ label }}
            @if (f.required) {
              *
            }
          </mat-checkbox>
        }
        @case ('SELECT') {
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ label }}</mat-label>
            <mat-select
              [ngModel]="value()[f.code] ?? null"
              (ngModelChange)="set(f.code, $event)"
              [required]="f.required"
              [attr.data-testid]="'visit-field-' + f.code"
            >
              @if (!f.required) {
                <mat-option [value]="null">—</mat-option>
              }
              @for (o of f.options; track o.code) {
                <mat-option [value]="o.code">{{ { ar: o.labelAr, en: o.labelEn } | dcLocalize }}</mat-option>
              }
            </mat-select>
            @if (errors()['data.' + f.code]; as e) {
              <mat-hint class="dc-visit-fields__bad">{{ e }}</mat-hint>
            }
          </mat-form-field>
        }
        @case ('TEXTAREA') {
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ label }}</mat-label>
            <textarea
              matInput
              rows="3"
              dir="auto"
              [ngModel]="value()[f.code] ?? ''"
              (ngModelChange)="set(f.code, $event)"
              [required]="f.required"
              [attr.data-testid]="'visit-field-' + f.code"
            ></textarea>
            @if (errors()['data.' + f.code]; as e) {
              <mat-hint class="dc-visit-fields__bad">{{ e }}</mat-hint>
            }
          </mat-form-field>
        }
        @default {
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ label }}</mat-label>
            <input
              matInput
              [type]="f.type === 'NUMBER' ? 'number' : f.type === 'DATE' ? 'date' : 'text'"
              [attr.inputmode]="f.type === 'NUMBER' ? 'decimal' : null"
              [dir]="f.type === 'TEXT' ? 'auto' : 'ltr'"
              [ngModel]="value()[f.code] ?? ''"
              (ngModelChange)="set(f.code, f.type === 'NUMBER' && $event !== '' && $event !== null ? +$event : $event)"
              [required]="f.required"
              [attr.data-testid]="'visit-field-' + f.code"
            />
            @if (errors()['data.' + f.code]; as e) {
              <mat-hint class="dc-visit-fields__bad">{{ e }}</mat-hint>
            }
          </mat-form-field>
        }
      }
    } @empty {
      <p class="dc-visit-fields__none">{{ 'visitFields.none' | dcTranslate }}</p>
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: 12px;
    }
    .dc-visit-fields__bad {
      color: #b42318;
    }
    .dc-visit-fields__none {
      margin: 0;
      color: var(--dga-text-3, #384250);
    }
  `,
})
export class VisitFields {
  readonly fields = input.required<VisitField[]>();
  readonly value = input.required<Record<string, unknown>>();
  /** Problems by field (`data.<code>` → message), e.g. from the server. */
  readonly errors = input<Record<string, string>>({});
  readonly valueChange = output<Record<string, unknown>>();

  protected set(code: string, v: unknown): void {
    this.valueChange.emit({ ...this.value(), [code]: v === '' ? null : v });
  }
}

/** The problems the form itself can see: required fields left empty (the server checks the rest). */
export function missingVisitFields(fields: VisitField[], value: Record<string, unknown>): string[] {
  return fields
    .filter(
      (f) => f.required && (f.type === 'CHECKBOX' ? value[f.code] !== true : value[f.code] === null || value[f.code] === undefined || value[f.code] === ''),
    )
    .map((f) => f.code);
}
