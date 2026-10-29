import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, output, signal } from '@angular/core';
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
import { FormField, FormOption, FormSchema } from './form-schema';
import { LOV_SOURCE } from './lov-source';

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
  /** Shows a secondary cancel button that emits {@link cancelled}. */
  readonly cancellable = input(false);
  readonly cancelLabel = input<string | undefined>(undefined);
  readonly submitIcon = input('send');
  /**
   * Values outside this form that rules may refer to (e.g. answers given in earlier wizard steps). The form's own values
   * win over them.
   */
  readonly context = input<FormValue>({});
  readonly submitted = output<FormValue>();
  readonly cancelled = output<void>();

  /** The reactive form built from the schema; rebuilt when the schema changes. */
  readonly formGroup = computed(() => this.buildGroup(this.schema(), this.initialValue()));

  private readonly visibleCodes = signal<ReadonlySet<string>>(new Set());
  private readonly requiredCodes = signal<ReadonlySet<string>>(new Set());
  private readonly lovOptions = signal<Readonly<Record<string, FormOption[]>>>({});
  private readonly lovSource = inject(LOV_SOURCE, { optional: true });
  private readonly destroyRef = inject(DestroyRef);
  private readonly lovParents = new Map<string, string | null>();

  constructor() {
    effect((onCleanup) => {
      const group = this.formGroup();
      this.context();
      this.lovParents.clear();
      this.applyRules(group);
      this.loadLovOptions(group);
      const subscription = group.valueChanges.subscribe(() => {
        this.applyRules(group);
        this.loadLovOptions(group);
      });
      onCleanup(() => subscription.unsubscribe());
    });
  }

  protected options(field: FormField): FormOption[] {
    return field.lov ? (this.lovOptions()[field.code] ?? []) : (field.options ?? []);
  }

  protected isVisible(code: string): boolean {
    return this.visibleCodes().has(code);
  }

  /** Whether this placement of a field is the one shown (a code placed in several sections shows once). */
  protected isShown(section: string, code: string): boolean {
    return this.visibleCodes().has(`${section}/${code}`);
  }

  protected isRequired(code: string): boolean {
    return this.requiredCodes().has(code);
  }

  /** Translation key and value of the first problem of a field. */
  protected errorText(code: string): { key: string; value: string } {
    const errors = this.control(code).errors ?? {};
    const field = this.allFields().find((f) => f.code === code);
    if (errors['min']) return { key: 'form.min', value: String(field?.min ?? '') };
    if (errors['max']) return { key: 'form.max', value: String(field?.max ?? '') };
    if (errors['maxlength']) return { key: 'form.maxLength', value: String(field?.maxLength ?? '') };
    if (errors['pattern']) return { key: 'form.pattern', value: '' };
    return { key: 'form.required', value: '' };
  }

  protected control(code: string): FormControl {
    return this.formGroup().controls[code] as FormControl;
  }

  /** Used for checkboxes, which have no mat-form-field to show errors. */
  protected showError(code: string): boolean {
    const control = this.control(code);
    return control.invalid && (control.touched || control.dirty);
  }

  protected cancel(): void {
    this.cancelled.emit();
  }

  protected submit(): void {
    const group = this.formGroup();
    group.markAllAsTouched();
    // Not "valid": a form of read-only fields only is DISABLED, and it can still be submitted.
    if (!group.invalid) {
      // Disabled (hidden) controls are excluded; read-only fields are disabled too but still submitted.
      const value = group.value as FormValue;
      for (const field of this.allFields()) {
        if (field.readonly && this.isVisible(field.code)) {
          value[field.code] = group.controls[field.code].getRawValue();
        }
      }
      this.submitted.emit(value);
    }
  }

  private allFields(): FormField[] {
    return this.schema().sections.flatMap((section) => section.fields);
  }

  /** Loads LOV options for visible LOV fields whose parent value changed (or that were never loaded). */
  private loadLovOptions(group: FormGroup): void {
    if (!this.lovSource) {
      return;
    }
    for (const field of this.allFields()) {
      if (!field.lov || !this.isVisible(field.code)) {
        continue;
      }
      const parentValue = field.lovParentField ? ((group.controls[field.lovParentField]?.value as string | null) ?? null) : null;
      if (this.lovParents.has(field.code) && this.lovParents.get(field.code) === parentValue) {
        continue;
      }
      const firstLoad = !this.lovParents.has(field.code);
      this.lovParents.set(field.code, parentValue);
      if (field.lovParentField && !firstLoad) {
        // The parent changed: the previous choice may not belong to the new parent.
        group.controls[field.code].setValue(field.multiple ? [] : null, { emitEvent: false });
      }
      if (field.lovParentField && parentValue === null) {
        this.lovOptions.update((all) => ({ ...all, [field.code]: [] }));
        continue;
      }
      const subscription = this.lovSource.options(field.lov, parentValue).subscribe({
        next: (options) => this.lovOptions.update((all) => ({ ...all, [field.code]: options })),
        error: () => this.lovOptions.update((all) => ({ ...all, [field.code]: [] })),
      });
      this.destroyRef.onDestroy(() => subscription.unsubscribe());
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
    const data = { ...this.context(), ...group.getRawValue() } as JsonValue;
    const visible = new Set<string>();
    const required = new Set<string>();
    // A code may be placed in several sections (a shared question): its one control is enabled when any placement is
    // visible, and only the first visible placement is shown.
    const shownAt = new Map<string, { key: string; field: FormField }>();

    for (const section of this.schema().sections) {
      const sectionVisible = section.visibleWhen === undefined || jsonLogic.evaluateTruthy(section.visibleWhen, data);
      if (sectionVisible) {
        visible.add(section.code);
      }

      for (const field of section.fields) {
        const fieldVisible = sectionVisible && (field.visibleWhen === undefined || jsonLogic.evaluateTruthy(field.visibleWhen, data));
        if (!fieldVisible) {
          continue;
        }
        if (!shownAt.has(field.code)) {
          shownAt.set(field.code, { key: `${section.code}/${field.code}`, field });
        }
        if (field.required === true || (field.requiredWhen !== undefined && jsonLogic.evaluateTruthy(field.requiredWhen, data))) {
          required.add(field.code);
        }
      }
    }

    for (const field of this.allFields()) {
      const shown = shownAt.get(field.code);
      if (shown) {
        visible.add(field.code);
        visible.add(shown.key);
      }
      const control = group.controls[field.code];
      const enabled = !!shown && !shown.field.readonly;
      if (enabled && control.disabled) {
        control.enable({ emitEvent: false });
      } else if (!enabled && control.enabled) {
        control.disable({ emitEvent: false });
      }
      const rules = shown?.field ?? field;
      const validators: ValidatorFn[] = required.has(field.code) ? [rules.type === 'checkbox' ? Validators.requiredTrue : Validators.required] : [];
      if (rules.min !== undefined) validators.push(Validators.min(rules.min));
      if (rules.max !== undefined) validators.push(Validators.max(rules.max));
      if (rules.maxLength !== undefined) validators.push(Validators.maxLength(rules.maxLength));
      if (rules.pattern) validators.push(Validators.pattern(rules.pattern));
      control.setValidators(validators);
      control.updateValueAndValidity({ emitEvent: false });
    }

    group.updateValueAndValidity({ emitEvent: false });
    this.visibleCodes.set(visible);
    this.requiredCodes.set(required);
  }
}

function defaultValue(field: FormField): unknown {
  if (field.type === 'checkbox') {
    return false;
  }
  return field.multiple ? [] : null;
}
