import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ConfigRow, LocalizePipe, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { DraftsService } from '../drafts.service';
import { PrivilegesService } from '../privileges.service';
import { AttributeDefinition, ClassRow, DATA_TYPES } from './class-models';

type AttributeForm = FormGroup<{
  code: FormControl<string>;
  nameAr: FormControl<string>;
  nameEn: FormControl<string>;
  dataType: FormControl<string>;
  refClassCode: FormControl<string | null>;
  attributeGroup: FormControl<string>;
  isRequired: FormControl<boolean>;
  isReportable: FormControl<boolean>;
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
    isActive: new FormControl(true, { nonNullable: true }),
    attributes: new FormArray<AttributeForm>([]),
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
        isActive: cls.isActive,
      });
      cls.attributes.forEach((a) => this.addAttribute(a));
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
        attributeGroup: new FormControl<string>(a?.attributeGroup ?? 'ATTRIBUTE', { nonNullable: true }),
        isRequired: new FormControl(a?.isRequired ?? false, { nonNullable: true }),
        isReportable: new FormControl(a?.isReportable ?? true, { nonNullable: true }),
      }),
    );
  }

  protected removeAttribute(index: number): void {
    this.attributes.removeAt(index);
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
      attributes: value.attributes.map((a, index) => ({
        ...a,
        refClassCode: a.dataType === 'DOMAIN_REF' ? a.refClassCode : null,
        validation: existing.get(a.code)?.validation ?? null,
        displayOrder: (index + 1) * 10,
      })),
    };
    try {
      await this.drafts.addChange('apps.class', value.code, this.isNew() ? 'CREATE' : 'UPDATE', payload);
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}
