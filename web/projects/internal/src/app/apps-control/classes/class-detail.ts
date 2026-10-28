import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { AppsControlApi, I18nService, LocalizePipe, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { firstValueFrom, of } from 'rxjs';
import { ConfirmData, ConfirmDialog } from '../confirm-dialog';
import { DraftsService } from '../drafts.service';
import { PrivilegesService } from '../privileges.service';
import { AttributeDefinition, ClassRow, DATA_TYPES, DomainRow } from './class-models';
import { DomainEditor, DomainEditorData } from './domain-editor';

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

const RESOURCE = 'apps-control.classes';

/** A class: its definition and typed attributes (edited together) and its values (each edited on its own). */
@Component({
  selector: 'dc-class-detail',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    RouterLink,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './class-detail.html',
  styleUrl: './class-detail.scss',
})
export class ClassDetail {
  /** Route parameter: class code, or `new`. */
  readonly code = input.required<string>();

  private readonly api = inject(AppsControlApi);
  protected readonly drafts = inject(DraftsService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  protected readonly i18n = inject(I18nService);
  protected readonly privileges = inject(PrivilegesService);

  protected readonly dataTypes = DATA_TYPES;
  protected readonly errors = signal<string[]>([]);
  protected readonly isNew = computed(() => this.code() === 'new');

  private readonly classResource = rxResource({
    params: () => this.code(),
    stream: ({ params }) => (params === 'new' ? of(null) : this.api.get('apps.class', params)),
  });
  private readonly allClasses = rxResource({ stream: () => this.api.list('apps.class') });
  private readonly domainsResource = rxResource({
    params: () => (this.isNew() ? undefined : this.code()),
    stream: ({ params }) => this.api.list('apps.domain', params),
  });

  protected readonly cls = computed(() => (this.classResource.hasValue() ? (this.classResource.value() as ClassRow | null) : null));
  protected readonly isSystem = computed(() => this.cls()?.owner === 'SYSTEM');
  protected readonly canEdit = computed(() => !this.isSystem() && this.privileges.can(RESOURCE, this.isNew() ? 'CREATE' : 'EDIT'));
  protected readonly otherClasses = computed(() => (this.allClasses.hasValue() ? this.allClasses.value() : []).filter((c) => c.code !== this.code()));
  protected readonly domains = computed(() => (this.domainsResource.hasValue() ? (this.domainsResource.value() as DomainRow[]) : []));
  protected readonly domainColumns = computed(() => ['code', 'name', ...(this.cls()?.attributes ?? []).slice(0, 4).map((a) => 'attr:' + a.code), 'actions']);

  protected readonly form = new FormGroup({
    code: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[A-Z][A-Z0-9_-]{0,50}$/)] }),
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
    effect(() => {
      const cls = this.cls();
      this.form.reset();
      this.form.controls.attributes.clear();
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
      } else {
        this.form.controls.code.enable();
      }
      if (this.isSystem()) {
        this.form.disable();
      }
    });
  }

  protected get attributes(): FormArray<AttributeForm> {
    return this.form.controls.attributes;
  }

  protected addAttribute(a?: AttributeDefinition): void {
    this.attributes.push(
      new FormGroup({
        code: new FormControl(a?.code ?? '', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[A-Z][A-Z0-9_-]{0,50}$/)] }),
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

  protected attributeValue(row: DomainRow, column: string): string {
    const value = row.attributes?.[column.slice('attr:'.length)];
    return value === null || value === undefined ? '' : typeof value === 'object' ? JSON.stringify(value) : String(value);
  }

  protected attributeLabel(column: string) {
    const attribute = this.cls()?.attributes.find((a) => a.code === column.slice('attr:'.length));
    return attribute ? { ar: attribute.nameAr, en: attribute.nameEn } : { ar: column, en: column };
  }

  protected valueCode(row: DomainRow): string {
    return row.code.slice(this.code().length + 1);
  }

  protected async saveClass(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }
    this.errors.set([]);
    const value = this.form.getRawValue();
    const existing = new Map((this.cls()?.attributes ?? []).map((a) => [a.code, a]));
    const payload = {
      nameAr: value.nameAr,
      nameEn: value.nameEn,
      isActive: value.isActive,
      descriptionAr: value.descriptionAr || null,
      descriptionEn: value.descriptionEn || null,
      parentClassCode: value.parentClassCode,
      isHierarchical: value.isHierarchical,
      allowMultiAssign: this.cls()?.allowMultiAssign ?? false,
      attributes: value.attributes.map((a, index) => ({
        ...a,
        refClassCode: a.dataType === 'DOMAIN_REF' ? a.refClassCode : null,
        validation: existing.get(a.code)?.validation ?? null,
        displayOrder: (index + 1) * 10,
      })),
    };
    try {
      await this.drafts.addChange('apps.class', value.code, this.isNew() ? 'CREATE' : 'UPDATE', payload);
      if (this.isNew()) {
        await this.router.navigate(['/apps-control/classes']);
      }
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  protected async editValue(row: DomainRow | null): Promise<void> {
    const cls = this.cls();
    if (!cls) {
      return;
    }
    await firstValueFrom(this.dialog.open<DomainEditor, DomainEditorData, boolean>(DomainEditor, { data: { cls, row }, autoFocus: 'first-tabbable', width: '52rem', maxWidth: '95vw' }).afterClosed());
  }

  protected async deleteValue(row: DomainRow): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog
        .open<ConfirmDialog, ConfirmData, string>(ConfirmDialog, {
          data: { title: `${this.i18n.translate('config.delete')} ${row.code}`, message: this.i18n.translate('config.deleteHint'), confirmLabel: this.i18n.translate('config.delete'), danger: true },
        })
        .afterClosed(),
    );
    if (confirmed !== undefined) {
      try {
        await this.drafts.addChange('apps.domain', row.code, 'DELETE', null);
      } catch (error) {
        this.errors.set(problemMessages(error));
      }
    }
  }
}
