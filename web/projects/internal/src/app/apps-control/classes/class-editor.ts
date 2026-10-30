import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { AppsControlApi, ConfigRow, LocalizePipe, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';
import { DraftsService } from '../drafts.service';
import { PrivilegesService } from '../privileges.service';
import { AttributeDefinition, ClassRow, ClassSetting, DATA_TYPES, MULTI_TYPES } from './class-models';

type AttributeForm = FormGroup<{
  code: FormControl<string>;
  nameAr: FormControl<string>;
  nameEn: FormControl<string>;
  dataType: FormControl<string>;
  refClassCode: FormControl<string | null>;
  refLovCode: FormControl<string | null>;
  isMultiple: FormControl<boolean>;
  attributeGroup: FormControl<string>;
  isRequired: FormControl<boolean>;
  isReportable: FormControl<boolean>;
}>;

type SettingForm = FormGroup<{
  code: FormControl<string>;
  nameAr: FormControl<string>;
  nameEn: FormControl<string>;
  value: FormControl<string | null>;
}>;

export interface ClassEditorData {
  /** The class to edit or view, or null for a new one. */
  cls: ClassRow | null;
  /** Every class (parent and referenced-class choices). */
  classes: ConfigRow[];
  readonly?: boolean;
}

const RESOURCE = 'apps-control.classes';
const CODE = /^[A-Z][A-Z0-9_-]{0,50}$/;

/** A class's definition and typed attributes, edited together; saving adds the change to the draft change set. */
@Component({
  selector: 'dc-class-editor',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './class-editor.html',
  styleUrl: './class-editor.scss',
})
export class ClassEditor {
  protected readonly data = inject<ClassEditorData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ClassEditor, boolean>);
  protected readonly drafts = inject(DraftsService);
  private readonly privileges = inject(PrivilegesService);

  protected readonly dataTypes = DATA_TYPES;
  protected readonly multiTypes: string[] = MULTI_TYPES;
  /** Lists of values a field can take its options from. */
  protected readonly lovs = toSignal(inject(AppsControlApi).list('apps.lov').pipe(catchError(() => of([] as ConfigRow[]))), { initialValue: [] as ConfigRow[] });
  protected readonly errors = signal<string[]>([]);
  protected readonly isNew = computed(() => this.data.cls === null);
  protected readonly isSystem = computed(() => this.data.cls?.owner === 'SYSTEM');
  protected readonly canEdit = computed(() => !this.data.readonly && !this.isSystem() && this.privileges.can(RESOURCE, this.isNew() ? 'CREATE' : 'EDIT'));
  protected readonly otherClasses = this.data.classes.filter((c) => c.code !== this.data.cls?.code);

  protected readonly form = new FormGroup({
    code: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(CODE)] }),
    nameAr: new FormControl('', { nonNullable: true, validators: Validators.required }),
    nameEn: new FormControl('', { nonNullable: true, validators: Validators.required }),
    descriptionAr: new FormControl<string | null>(null),
    descriptionEn: new FormControl<string | null>(null),
    parentClassCode: new FormControl<string | null>(null),
    isHierarchical: new FormControl(false, { nonNullable: true }),
    valuesPerCompany: new FormControl(false, { nonNullable: true }),
    isActive: new FormControl(true, { nonNullable: true }),
    attributes: new FormArray<AttributeForm>([]),
    settings: new FormArray<SettingForm>([]),
  });

  constructor() {
    const cls = this.data.cls;
    if (cls) {
      this.form.patchValue({
        code: cls.code,
        nameAr: cls.nameAr,
        nameEn: cls.nameEn,
        descriptionAr: cls.descriptionAr,
        descriptionEn: cls.descriptionEn,
        parentClassCode: cls.parentClassCode,
        isHierarchical: cls.isHierarchical,
        valuesPerCompany: cls.valuesPerCompany ?? false,
        isActive: cls.isActive,
      });
      cls.attributes.forEach((a) => this.addAttribute(a));
      (cls.settings ?? []).forEach((c) => this.addSetting(c));
      this.form.controls.code.disable();
    }
    if (!this.canEdit()) {
      this.form.disable();
    }
  }

  protected get attributes(): FormArray<AttributeForm> {
    return this.form.controls.attributes;
  }

  protected addAttribute(a?: AttributeDefinition): void {
    this.attributes.push(
      new FormGroup({
        code: new FormControl(a?.code ?? '', { nonNullable: true, validators: [Validators.required, Validators.pattern(CODE)] }),
        nameAr: new FormControl(a?.nameAr ?? '', { nonNullable: true, validators: Validators.required }),
        nameEn: new FormControl(a?.nameEn ?? '', { nonNullable: true, validators: Validators.required }),
        dataType: new FormControl<string>(a?.dataType ?? 'TEXT', { nonNullable: true }),
        refClassCode: new FormControl<string | null>(a?.refClassCode ?? null),
        refLovCode: new FormControl<string | null>(a?.refLovCode ?? null),
        isMultiple: new FormControl(a?.isMultiple ?? false, { nonNullable: true }),
        attributeGroup: new FormControl<string>(a?.attributeGroup ?? 'ATTRIBUTE', { nonNullable: true }),
        isRequired: new FormControl(a?.isRequired ?? false, { nonNullable: true }),
        isReportable: new FormControl(a?.isReportable ?? true, { nonNullable: true }),
      }),
    );
  }

  protected removeAttribute(index: number): void {
    this.attributes.removeAt(index);
  }

  protected get settings(): FormArray<SettingForm> {
    return this.form.controls.settings;
  }

  protected addSetting(c?: ClassSetting): void {
    this.settings.push(
      new FormGroup({
        code: new FormControl(c?.code ?? '', { nonNullable: true, validators: [Validators.required, Validators.pattern(CODE)] }),
        nameAr: new FormControl(c?.nameAr ?? '', { nonNullable: true, validators: Validators.required }),
        nameEn: new FormControl(c?.nameEn ?? '', { nonNullable: true, validators: Validators.required }),
        value: new FormControl<string | null>(c?.value ?? null),
      }),
    );
  }

  protected removeSetting(index: number): void {
    this.settings.removeAt(index);
  }

  protected async saveClass(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid || !this.canEdit()) {
      return;
    }
    this.errors.set([]);
    const value = this.form.getRawValue();
    const existing = new Map((this.data.cls?.attributes ?? []).map((a) => [a.code, a]));
    const payload = {
      nameAr: value.nameAr,
      nameEn: value.nameEn,
      isActive: value.isActive,
      descriptionAr: value.descriptionAr || null,
      descriptionEn: value.descriptionEn || null,
      parentClassCode: value.parentClassCode,
      isHierarchical: value.isHierarchical,
      allowMultiAssign: this.data.cls?.allowMultiAssign ?? false,
      valuesPerCompany: value.valuesPerCompany,
      attributes: value.attributes.map((a, index) => ({
        ...a,
        refClassCode: a.dataType === 'DOMAIN_REF' ? a.refClassCode : null,
        refLovCode: a.dataType === 'LOV_REF' ? a.refLovCode : null,
        isMultiple: MULTI_TYPES.includes(a.dataType as never) && a.isMultiple,
        validation: existing.get(a.code)?.validation ?? null,
        displayOrder: (index + 1) * 10,
      })),
      settings: value.settings.map((c) => ({ ...c, value: c.value || null })),
    };
    try {
      await this.drafts.addChange('apps.class', value.code, this.isNew() ? 'CREATE' : 'UPDATE', payload);
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}
