import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { NgTemplateOutlet } from '@angular/common';
import { JsonValue, jsonLogic } from '../expressions/jsonlogic';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { FormField, FormOption, FormSchema, FormSection } from './form-schema';
import { DualList } from './dual-list';
import { FileInput, FileUploader } from './file-input';
import { StructuredInput } from './structured-input';
import { pickIcon } from './icon-picker';
import { LOV_SOURCE } from './lov-source';

export type FormValue = Record<string, unknown>;

/**
 * Renders a {@link FormSchema}: visibility and conditional requirements are JSONLogic rules evaluated on every change,
 * exactly as the server will re-evaluate them on submit.
 */
@Component({
  selector: 'dc-dynamic-form',
  imports: [
    NgTemplateOutlet,
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTabsModule,
    DualList,
    StructuredInput,
    FileInput,
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
  /** Where `file` fields upload (none: file fields cannot take files). */
  readonly uploader = input<FileUploader | null>(null);
  readonly submitted = output<FormValue>();
  readonly cancelled = output<void>();

  /** The reactive form built from the schema; rebuilt when the schema changes. */
  readonly formGroup = computed(() => this.buildGroup(this.schema(), this.initialValue()));

  private readonly visibleCodes = signal<ReadonlySet<string>>(new Set());
  private readonly requiredCodes = signal<ReadonlySet<string>>(new Set());
  private readonly lovOptions = signal<Readonly<Record<string, FormOption[]>>>({});
  private readonly lovSource = inject(LOV_SOURCE, { optional: true });
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialog = inject(MatDialog);
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

  /** Sections shown for the current answers (tabs and panels are made of these). */
  protected readonly visibleSections = computed(() => this.schema().sections.filter((s) => this.visibleCodes().has(s.code)));
  /** The open tab, or the panel opened to show a problem. */
  protected readonly activeTab = signal(0);
  protected readonly expanded = signal<string | null>(null);

  /** Where a field sits in its section's grid: a share of six tracks when the section has columns. */
  protected span(section: FormSection, field: FormField): string | null {
    if (!section.columns) return null;
    const width = field.width ?? (field.type === 'checkbox' || field.type === 'textarea' ? 'full' : null);
    const tracks = width === 'full' ? 6 : width === 'half' ? 3 : width === 'third' ? 2 : 6 / Math.min(3, Math.max(1, section.columns));
    return `span ${tracks}`;
  }

  /** Spans the whole row in a section laid out automatically. */
  protected wide(section: FormSection, field: FormField): boolean {
    return (
      !section.columns &&
      (field.width === 'full' ||
        field.type === 'checkbox' ||
        field.type === 'textarea' ||
        field.type === 'dualList' ||
        field.type === 'structured' ||
        field.type === 'file')
    );
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

  /** Opens the icon picker for an icon field and puts the chosen name in it. */
  protected chooseIcon(code: string): void {
    const control = this.control(code);
    const subscription = pickIcon(this.dialog, control.value as string | null).subscribe((icon) => {
      if (icon !== undefined) {
        control.setValue(icon || null);
        control.markAsDirty();
      }
    });
    this.destroyRef.onDestroy(() => subscription.unsubscribe());
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
    } else {
      this.revealProblem();
    }
  }

  /** Opens the tab or panel of the first field with a problem, so it can be seen. */
  private revealProblem(): void {
    const group = this.formGroup();
    const sections = this.visibleSections();
    const index = sections.findIndex((section) => section.fields.some((f) => this.isShown(section.code, f.code) && group.controls[f.code]?.invalid));
    if (index < 0) return;
    this.activeTab.set(index);
    this.expanded.set(sections[index].code);
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
