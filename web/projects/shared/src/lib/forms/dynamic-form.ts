import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { JsonValue, jsonLogic } from '../expressions/jsonlogic';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { FormField, FormSchema } from './form-schema';

export type FormValue = Record<string, unknown>;

/**
 * Renders a {@link FormSchema}: visibility and conditional requirements are JSONLogic rules evaluated on every change,
 * exactly as the server will re-evaluate them on submit.
 */
@Component({
  selector: 'dc-dynamic-form',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dynamic-form.html',
  styleUrl: './dynamic-form.scss',
})
export class DynamicForm {
  readonly schema = input.required<FormSchema>();
  readonly initialValue = input<FormValue>({});
  readonly submitLabel = input<string | undefined>(undefined);
  readonly submitted = output<FormValue>();

  /** The reactive form built from the schema; rebuilt when the schema changes. */
  readonly formGroup = computed(() => this.buildGroup(this.schema(), this.initialValue()));

  private readonly visibleCodes = signal<ReadonlySet<string>>(new Set());
  private readonly requiredCodes = signal<ReadonlySet<string>>(new Set());

  constructor() {
    effect((onCleanup) => {
      const group = this.formGroup();
      this.applyRules(group);
      const subscription = group.valueChanges.subscribe(() => this.applyRules(group));
      onCleanup(() => subscription.unsubscribe());
    });
  }

  protected isVisible(code: string): boolean {
    return this.visibleCodes().has(code);
  }

  protected isRequired(code: string): boolean {
    return this.requiredCodes().has(code);
  }

  protected control(code: string): FormControl {
    return this.formGroup().controls[code] as FormControl;
  }

  /** Used for checkboxes, which have no mat-form-field to show errors. */
  protected showError(code: string): boolean {
    const control = this.control(code);
    return control.invalid && (control.touched || control.dirty);
  }

  protected submit(): void {
    const group = this.formGroup();
    group.markAllAsTouched();
    if (group.valid) {
      // Disabled (hidden) controls are excluded from `value`.
      this.submitted.emit(group.value as FormValue);
    }
  }

  private buildGroup(schema: FormSchema, initial: FormValue): FormGroup {
    const controls: Record<string, FormControl> = {};
    for (const section of schema.sections) {
      for (const field of section.fields) {
        controls[field.code] = new FormControl(initial[field.code] ?? defaultValue(field));
      }
    }
    return new FormGroup(controls);
  }

  private applyRules(group: FormGroup): void {
    const data = group.getRawValue() as JsonValue;
    const visible = new Set<string>();
    const required = new Set<string>();

    for (const section of this.schema().sections) {
      const sectionVisible = section.visibleWhen === undefined || jsonLogic.evaluateTruthy(section.visibleWhen, data);
      if (sectionVisible) {
        visible.add(section.code);
      }

      for (const field of section.fields) {
        const fieldVisible = sectionVisible && (field.visibleWhen === undefined || jsonLogic.evaluateTruthy(field.visibleWhen, data));
        const fieldRequired =
          fieldVisible && (field.required === true || (field.requiredWhen !== undefined && jsonLogic.evaluateTruthy(field.requiredWhen, data)));
        if (fieldVisible) {
          visible.add(field.code);
        }
        if (fieldRequired) {
          required.add(field.code);
        }

        const control = group.controls[field.code];
        if (fieldVisible && control.disabled) {
          control.enable({ emitEvent: false });
        } else if (!fieldVisible && control.enabled) {
          control.disable({ emitEvent: false });
        }
        const validators: ValidatorFn[] = fieldRequired ? [field.type === 'checkbox' ? Validators.requiredTrue : Validators.required] : [];
        control.setValidators(validators);
        control.updateValueAndValidity({ emitEvent: false });
      }
    }

    group.updateValueAndValidity({ emitEvent: false });
    this.visibleCodes.set(visible);
    this.requiredCodes.set(required);
  }
}

function defaultValue(field: FormField): unknown {
  return field.type === 'checkbox' ? false : null;
}
