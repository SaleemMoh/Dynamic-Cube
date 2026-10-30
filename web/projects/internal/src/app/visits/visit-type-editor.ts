import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  AppsControlApi,
  ConfigRow,
  LocalizePipe,
  OutcomeKind,
  TranslatePipe,
  VisitField,
  VisitFieldType,
  VisitMode,
  VisitOutcome,
  StructuredInput,
  StructuredShape,
  problemMessages,
} from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';
import { ConfigEditorData } from '../apps-control/config/config-editor';
import { DraftsService } from '../apps-control/drafts.service';
import { SubmitButton } from '../apps-control/submit-button';

const CODE = /^[A-Z][A-Z0-9_-]{0,50}$/;
const FIELD_CODE = /^[A-Za-z][A-Za-z0-9_]{0,63}$/;
const OUTCOME_CODE = /^[A-Z][A-Z0-9_]{0,99}$/;

/** A form field as edited: its choices as text, one `CODE | Arabic | English` per line. */
interface FieldRow {
  code: string;
  labelAr: string;
  labelEn: string;
  type: VisitFieldType;
  required: boolean;
  options: VisitOption[];
}

interface VisitOption {
  code: string;
  labelAr: string;
  labelEn: string;
}

const DEFAULT_RESULT = { reference: 'reference', status: 'status', completedValue: 'COMPLETED', outcome: 'outcome', data: 'findings', report: 'report' };

const t = (ar: string, en: string) => ({ ar, en });
/** A choice field's options. */
const OPTIONS: StructuredShape = {
  kind: 'table',
  columns: [
    { key: 'code', label: t('الرمز', 'Code'), type: 'text', ltr: true, required: true },
    { key: 'labelAr', label: t('بالعربية', 'Arabic'), type: 'text', required: true },
    { key: 'labelEn', label: t('بالإنجليزية', 'English'), type: 'text', ltr: true, required: true },
  ],
};
/** Operation input → value or JSONLogic over the visit and the request. */
const INPUTS: StructuredShape = { kind: 'map', keyLabel: t('مدخل العملية', 'Operation input'), valueLabel: t('القيمة أو التعبير', 'Value or expression') };
/** Where results carry their values. */
const RESULT: StructuredShape = {
  kind: 'object',
  properties: [
    { key: 'reference', label: t('المرجع', 'Reference'), type: 'text', ltr: true },
    { key: 'status', label: t('الحالة', 'Status'), type: 'text', ltr: true },
    { key: 'completedValue', label: t('قيمة الإنجاز', 'Completed value'), type: 'text', ltr: true },
    { key: 'outcome', label: t('النتيجة', 'Outcome'), type: 'text', ltr: true },
    { key: 'data', label: t('البيانات', 'Data'), type: 'text', ltr: true },
    { key: 'report', label: t('التقرير', 'Report'), type: 'text', ltr: true },
  ],
};
/** External outcome value → outcome code. */
const OUTCOME_MAP: StructuredShape = { kind: 'map', keyLabel: t('قيمة الجهة', 'External value'), valueLabel: t('رمز النتيجة', 'Outcome code') };

/**
 * Edits a visit type: who carries it out, its outcomes, what the inspector records, the evidence required, and for
 * external inspection bodies how they are called and how their results are read.
 */
@Component({
  selector: 'dc-visit-type-editor',
  imports: [
    SubmitButton,
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    StructuredInput,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'visitTypes.title' | dcTranslate }} — {{ (data.row ? 'config.edit' : 'config.new') | dcTranslate }}</h2>
    <mat-dialog-content class="dc-vte">
      <section>
        <h3>{{ 'visitTypes.general' | dcTranslate }}</h3>
        <div class="dc-vte__grid">
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'visitTypes.code' | dcTranslate }}</mat-label>
            <input matInput dir="ltr" [readonly]="!!data.row" [ngModel]="code()" (ngModelChange)="code.set($event.toUpperCase())" data-testid="vte-code" />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'visitTypes.dueDays' | dcTranslate }}</mat-label>
            <input matInput type="number" min="1" max="90" [ngModel]="dueDays()" (ngModelChange)="dueDays.set(+$event || 1)" />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'visitTypes.nameAr' | dcTranslate }}</mat-label>
            <input matInput dir="rtl" [ngModel]="nameAr()" (ngModelChange)="nameAr.set($event)" data-testid="vte-name-ar" />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'visitTypes.nameEn' | dcTranslate }}</mat-label>
            <input matInput dir="ltr" [ngModel]="nameEn()" (ngModelChange)="nameEn.set($event)" data-testid="vte-name-en" />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'visitTypes.description' | dcTranslate }} (ع)</mat-label>
            <textarea matInput rows="2" dir="rtl" [ngModel]="descriptionAr()" (ngModelChange)="descriptionAr.set($event)"></textarea>
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'visitTypes.description' | dcTranslate }} (En)</mat-label>
            <textarea matInput rows="2" dir="ltr" [ngModel]="descriptionEn()" (ngModelChange)="descriptionEn.set($event)"></textarea>
          </mat-form-field>
        </div>
        <span class="dc-vte__label">{{ 'visitTypes.mode' | dcTranslate }}</span>
        <mat-button-toggle-group [ngModel]="mode()" (ngModelChange)="mode.set($event)" data-testid="vte-mode">
          @for (m of modes; track m) {
            <mat-button-toggle [value]="m">{{ 'visits.mode.' + m | dcTranslate }}</mat-button-toggle>
          }
        </mat-button-toggle-group>
        @if (mode() !== 'EXTERNAL') {
          <mat-form-field subscriptSizing="dynamic" class="dc-vte__wide">
            <mat-label>{{ 'visitTypes.inspectorRole' | dcTranslate }}</mat-label>
            <mat-select [ngModel]="inspectorRole()" (ngModelChange)="inspectorRole.set($event)" data-testid="vte-role">
              <mat-option [value]="null">{{ 'visitTypes.anyone' | dcTranslate }}</mat-option>
              @for (r of roles(); track r.code) {
                <mat-option [value]="r.code">{{ { ar: r.nameAr, en: r.nameEn } | dcLocalize }} ({{ r.code }})</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
        <mat-checkbox [ngModel]="active()" (ngModelChange)="active.set($event)">{{ 'visitTypes.active' | dcTranslate }}</mat-checkbox>
      </section>

      <section>
        <h3>{{ 'visitTypes.outcomes' | dcTranslate }}</h3>
        <p class="dc-muted">{{ 'visitTypes.outcomesHint' | dcTranslate }}</p>
        @for (o of outcomes(); track $index; let i = $index) {
          <div class="dc-vte__row" [attr.data-testid]="'vte-outcome-' + i">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'visitTypes.code' | dcTranslate }}</mat-label>
              <input matInput dir="ltr" [ngModel]="o.code" (ngModelChange)="setOutcome(i, { code: $event.toUpperCase() })" />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'visitTypes.nameAr' | dcTranslate }}</mat-label>
              <input matInput dir="rtl" [ngModel]="o.nameAr" (ngModelChange)="setOutcome(i, { nameAr: $event })" />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'visitTypes.nameEn' | dcTranslate }}</mat-label>
              <input matInput dir="ltr" [ngModel]="o.nameEn" (ngModelChange)="setOutcome(i, { nameEn: $event })" />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'visitTypes.kind' | dcTranslate }}</mat-label>
              <mat-select [ngModel]="o.kind" (ngModelChange)="setOutcome(i, { kind: $event })">
                @for (k of kinds; track k) {
                  <mat-option [value]="k">{{ 'visitTypes.kind.' + k | dcTranslate }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <button matIconButton type="button" (click)="removeOutcome(i)" [matTooltip]="'rule.remove' | dcTranslate">
              <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
            </button>
          </div>
        }
        <button matButton="outlined" type="button" (click)="addOutcome()" data-testid="vte-add-outcome">
          <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
          {{ 'visitTypes.addOutcome' | dcTranslate }}
        </button>
      </section>

      @if (mode() !== 'EXTERNAL') {
        <section>
          <h3>{{ 'visitTypes.fields' | dcTranslate }}</h3>
          <p class="dc-muted">{{ 'visitTypes.fieldsHint' | dcTranslate }}</p>
          @for (f of fields(); track $index; let i = $index) {
            <div class="dc-vte__field" [attr.data-testid]="'vte-field-' + i">
              <div class="dc-vte__row">
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ 'visitTypes.code' | dcTranslate }}</mat-label>
                  <input matInput dir="ltr" [ngModel]="f.code" (ngModelChange)="setField(i, { code: $event })" />
                </mat-form-field>
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ 'visitTypes.nameAr' | dcTranslate }}</mat-label>
                  <input matInput dir="rtl" [ngModel]="f.labelAr" (ngModelChange)="setField(i, { labelAr: $event })" />
                </mat-form-field>
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ 'visitTypes.nameEn' | dcTranslate }}</mat-label>
                  <input matInput dir="ltr" [ngModel]="f.labelEn" (ngModelChange)="setField(i, { labelEn: $event })" />
                </mat-form-field>
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ 'visitTypes.fieldType' | dcTranslate }}</mat-label>
                  <mat-select [ngModel]="f.type" (ngModelChange)="setField(i, { type: $event })">
                    @for (t of fieldTypes; track t) {
                      <mat-option [value]="t">{{ 'visitTypes.fieldType.' + t | dcTranslate }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                <button matIconButton type="button" (click)="removeField(i)" [matTooltip]="'rule.remove' | dcTranslate">
                  <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
                </button>
              </div>
              <mat-checkbox [ngModel]="f.required" (ngModelChange)="setField(i, { required: $event })">{{ 'visitTypes.required' | dcTranslate }}</mat-checkbox>
              @if (f.type === 'SELECT') {
                <div class="dc-vte__wide">
                  <span class="dc-field-label">{{ 'visitTypes.options' | dcTranslate }}</span>
                  <dc-structured-input [shape]="optionsShape" [ngModel]="f.options" (ngModelChange)="setField(i, { options: $event })" />
                </div>
              }
            </div>
          }
          <button matButton="outlined" type="button" (click)="addField()" data-testid="vte-add-field">
            <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
            {{ 'visitTypes.addField' | dcTranslate }}
          </button>
        </section>

        <section>
          <h3>{{ 'visitTypes.evidence' | dcTranslate }}</h3>
          <div class="dc-vte__grid">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'visitTypes.photosRequired' | dcTranslate }}</mat-label>
              <input
                matInput
                type="number"
                min="0"
                max="8"
                [ngModel]="photosRequired()"
                (ngModelChange)="photosRequired.set(+$event || 0)"
                data-testid="vte-photos"
              />
            </mat-form-field>
          </div>
          <mat-checkbox [ngModel]="requireLocation()" (ngModelChange)="requireLocation.set($event)">{{
            'visitTypes.requireLocation' | dcTranslate
          }}</mat-checkbox>
          <mat-checkbox [ngModel]="requireSignature()" (ngModelChange)="requireSignature.set($event)">{{
            'visitTypes.requireSignature' | dcTranslate
          }}</mat-checkbox>
        </section>
      } @else {
        <section>
          <h3>{{ 'visitTypes.external' | dcTranslate }}</h3>
          <div class="dc-vte__grid">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'visitTypes.createOperation' | dcTranslate }}</mat-label>
              <mat-select [ngModel]="createOperation()" (ngModelChange)="createOperation.set($event)" data-testid="vte-create-op">
                @for (op of operations(); track op.code) {
                  <mat-option [value]="op.code">{{ { ar: op.nameAr, en: op.nameEn } | dcLocalize }} ({{ op.code }})</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'visitTypes.referenceOutput' | dcTranslate }}</mat-label>
              <input matInput dir="ltr" [ngModel]="referenceOutput()" (ngModelChange)="referenceOutput.set($event)" />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'visitTypes.resultOperation' | dcTranslate }}</mat-label>
              <mat-select [ngModel]="resultOperation()" (ngModelChange)="resultOperation.set($event)">
                <mat-option [value]="null">{{ 'visitTypes.noPolling' | dcTranslate }}</mat-option>
                @for (op of operations(); track op.code) {
                  <mat-option [value]="op.code">{{ { ar: op.nameAr, en: op.nameEn } | dcLocalize }} ({{ op.code }})</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'visitTypes.pollMinutes' | dcTranslate }}</mat-label>
              <input matInput type="number" min="1" max="1440" [ngModel]="pollMinutes()" (ngModelChange)="pollMinutes.set(+$event || 15)" />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'visitTypes.callbackSecret' | dcTranslate }}</mat-label>
              <input matInput dir="ltr" [ngModel]="callbackSecret()" (ngModelChange)="callbackSecret.set($event.toUpperCase())" />
              <mat-hint>{{ 'visitTypes.callbackSecretHint' | dcTranslate }}</mat-hint>
            </mat-form-field>
          </div>
          <div class="dc-vte__wide">
            <span class="dc-field-label">{{ 'visitTypes.inputs' | dcTranslate }}</span>
            <dc-structured-input [shape]="inputsShape" [ngModel]="inputs()" (ngModelChange)="inputs.set($event)" testId="vte-inputs" />
            <small class="dc-muted">{{ 'visitTypes.inputsHint' | dcTranslate }}</small>
          </div>
          <div class="dc-vte__wide">
            <span class="dc-field-label">{{ 'visitTypes.resultFields' | dcTranslate }}</span>
            <dc-structured-input [shape]="resultShape" [ngModel]="result()" (ngModelChange)="result.set($event)" testId="vte-result" />
          </div>
          <div class="dc-vte__wide">
            <span class="dc-field-label">{{ 'visitTypes.outcomeMap' | dcTranslate }}</span>
            <dc-structured-input [shape]="outcomeMapShape" [ngModel]="outcomeMap()" (ngModelChange)="outcomeMap.set($event)" testId="vte-outcome-map" />
          </div>
        </section>
      }

      @if (problems().length || errors().length) {
        <ul class="dc-errors" role="alert">
          @for (p of problems(); track p) {
            <li>{{ p | dcTranslate }}</li>
          }
          @for (e of errors(); track $index) {
            <li>{{ e }}</li>
          }
        </ul>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close>{{ 'form.cancel' | dcTranslate }}</button>
      <dc-submit-button [entityType]="data.type.entityType" [disabled]="problems().length > 0" (submitted)="save(true)" />
      <button matButton="filled" [disabled]="problems().length > 0" (click)="save()" data-testid="vte-save">
        {{ drafts.saveLabel(data.type.entityType) | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-vte {
      display: grid;
      gap: 1.25rem;
      min-width: min(56rem, 90vw);
    }
    .dc-vte section {
      display: grid;
      gap: 0.75rem;
    }
    .dc-vte h3 {
      margin: 0;
      font-size: 16px;
    }
    .dc-vte p {
      margin: 0;
    }
    .dc-vte__grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr));
      gap: 0.75rem;
    }
    .dc-vte__row {
      display: grid;
      grid-template-columns: 9rem 1fr 1fr 10rem auto;
      gap: 0.5rem;
      align-items: center;
    }
    .dc-vte__field {
      display: grid;
      gap: 0.25rem;
      padding: 0.75rem;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 8px;
    }
    .dc-vte__label {
      font-size: 13px;
      color: var(--dga-text-3, #384250);
    }
    .dc-vte__wide {
      width: 100%;
    }
    @media (max-width: 720px) {
      .dc-vte__row {
        grid-template-columns: 1fr 1fr;
      }
    }
  `,
})
export class VisitTypeEditor {
  protected readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<VisitTypeEditor, boolean>);
  private readonly api = inject(AppsControlApi);
  protected readonly drafts = inject(DraftsService);

  protected readonly modes: VisitMode[] = ['FIELD', 'EXTERNAL', 'MANUAL'];
  protected readonly kinds: OutcomeKind[] = ['PASS', 'FAIL', 'OTHER'];
  protected readonly fieldTypes: VisitFieldType[] = ['TEXT', 'TEXTAREA', 'NUMBER', 'DATE', 'SELECT', 'CHECKBOX'];

  private readonly row = this.data.row;
  private readonly external = (this.row?.['external'] as Record<string, unknown> | null | undefined) ?? null;
  protected readonly code = signal(this.row?.code ?? '');
  protected readonly nameAr = signal((this.row?.nameAr as string) ?? '');
  protected readonly nameEn = signal((this.row?.nameEn as string) ?? '');
  protected readonly active = signal((this.row?.['isActive'] as boolean | undefined) ?? true);
  protected readonly mode = signal<VisitMode>((this.row?.['mode'] as VisitMode) ?? 'FIELD');
  protected readonly descriptionAr = signal((this.row?.['descriptionAr'] as string) ?? '');
  protected readonly descriptionEn = signal((this.row?.['descriptionEn'] as string) ?? '');
  protected readonly dueDays = signal(Number(this.row?.['dueDays'] ?? 5));
  protected readonly inspectorRole = signal<string | null>((this.row?.['inspectorRole'] as string) ?? null);
  protected readonly outcomes = signal<VisitOutcome[]>(
    (this.row?.['outcomes'] as VisitOutcome[] | undefined) ?? [
      { code: 'PASS', nameAr: 'مطابق', nameEn: 'Compliant', kind: 'PASS' },
      { code: 'FAIL', nameAr: 'غير مطابق', nameEn: 'Not compliant', kind: 'FAIL' },
    ],
  );
  protected readonly fields = signal<FieldRow[]>(
    ((this.row?.['fields'] as VisitField[] | undefined) ?? []).map((f) => ({
      code: f.code,
      labelAr: f.labelAr,
      labelEn: f.labelEn,
      type: f.type,
      required: f.required,
      options: (f.options ?? []).map((o) => ({ code: o.code, labelAr: o.labelAr, labelEn: o.labelEn })),
    })),
  );
  protected readonly photosRequired = signal(Number(this.row?.['photosRequired'] ?? 0));
  protected readonly requireLocation = signal(!!this.row?.['requireLocation']);
  protected readonly requireSignature = signal(!!this.row?.['requireSignature']);
  protected readonly createOperation = signal<string | null>((this.external?.['createOperation'] as string) ?? null);
  protected readonly referenceOutput = signal((this.external?.['referenceOutput'] as string) ?? 'reference');
  protected readonly resultOperation = signal<string | null>((this.external?.['resultOperation'] as string) ?? null);
  protected readonly pollMinutes = signal(Number(this.external?.['pollMinutes'] ?? 15));
  protected readonly callbackSecret = signal((this.external?.['callbackSecret'] as string) ?? '');
  protected readonly inputs = signal<unknown>(this.external?.['inputs'] ?? { visitNumber: { var: 'visit.number' } });
  protected readonly result = signal<unknown>(this.external?.['result'] ?? DEFAULT_RESULT);
  protected readonly outcomeMap = signal<unknown>(this.external?.['outcomeMap'] ?? {});
  protected readonly optionsShape = OPTIONS;
  protected readonly inputsShape = INPUTS;
  protected readonly resultShape = RESULT;
  protected readonly outcomeMapShape = OUTCOME_MAP;
  protected readonly errors = signal<string[]>([]);

  private readonly roleRows = toSignal(this.api.list('apps.role').pipe(catchError(() => of([] as ConfigRow[]))), { initialValue: [] as ConfigRow[] });
  private readonly operationRows = toSignal(this.api.list('integ.operation').pipe(catchError(() => of([] as ConfigRow[]))), {
    initialValue: [] as ConfigRow[],
  });
  protected readonly roles = computed(() => this.roleRows().map(named));
  protected readonly operations = computed(() => this.operationRows().map(named));

  protected readonly problems = computed(() => {
    const list: string[] = [];
    if (!CODE.test(this.code().trim())) list.push('visitTypes.problem.code');
    if (!this.nameAr().trim() || !this.nameEn().trim()) list.push('visitTypes.problem.names');
    const outcomes = this.outcomes();
    if (
      !outcomes.length ||
      outcomes.some((o) => !OUTCOME_CODE.test(o.code) || !o.nameAr.trim() || !o.nameEn.trim()) ||
      new Set(outcomes.map((o) => o.code)).size !== outcomes.length
    ) {
      list.push('visitTypes.problem.outcomes');
    }
    if (this.mode() !== 'EXTERNAL') {
      const fields = this.fields();
      if (
        fields.some((f) => !FIELD_CODE.test(f.code) || !f.labelAr.trim() || !f.labelEn.trim() || (f.type === 'SELECT' && !cleanOptions(f.options).length)) ||
        new Set(fields.map((f) => f.code)).size !== fields.length
      ) {
        list.push('visitTypes.problem.fields');
      }
    } else {
      if (!this.createOperation() || (!this.resultOperation() && !this.callbackSecret().trim())) list.push('visitTypes.problem.external');
      if ([this.inputs(), this.result(), this.outcomeMap()].some((v) => !isObject(v))) list.push('visitTypes.problem.json');
    }
    return list;
  });

  protected setOutcome(index: number, change: Partial<VisitOutcome>): void {
    this.outcomes.update((list) => list.map((o, i) => (i === index ? { ...o, ...change } : o)));
  }

  protected addOutcome(): void {
    this.outcomes.update((list) => [...list, { code: '', nameAr: '', nameEn: '', kind: 'OTHER' }]);
  }

  protected removeOutcome(index: number): void {
    this.outcomes.update((list) => list.filter((_, i) => i !== index));
  }

  protected setField(index: number, change: Partial<FieldRow>): void {
    this.fields.update((list) => list.map((f, i) => (i === index ? { ...f, ...change } : f)));
  }

  protected addField(): void {
    this.fields.update((list) => [...list, { code: '', labelAr: '', labelEn: '', type: 'TEXT', required: false, options: [] }]);
  }

  protected removeField(index: number): void {
    this.fields.update((list) => list.filter((_, i) => i !== index));
  }

  protected async save(submit = false): Promise<void> {
    this.errors.set([]);
    const external = this.mode() === 'EXTERNAL';
    const payload = {
      nameAr: this.nameAr().trim(),
      nameEn: this.nameEn().trim(),
      isActive: this.active(),
      mode: this.mode(),
      descriptionAr: this.descriptionAr().trim() || null,
      descriptionEn: this.descriptionEn().trim() || null,
      dueDays: this.dueDays(),
      inspectorRole: external ? null : this.inspectorRole(),
      outcomes: this.outcomes().map((o) => ({ ...o, code: o.code.trim(), nameAr: o.nameAr.trim(), nameEn: o.nameEn.trim() })),
      fields: external
        ? []
        : this.fields().map((f) => ({
            code: f.code.trim(),
            labelAr: f.labelAr.trim(),
            labelEn: f.labelEn.trim(),
            type: f.type,
            required: f.required,
            options: f.type === 'SELECT' ? cleanOptions(f.options) : [],
          })),
      photosRequired: external ? 0 : this.photosRequired(),
      requireLocation: !external && this.requireLocation(),
      requireSignature: !external && this.requireSignature(),
      external: external
        ? {
            createOperation: this.createOperation(),
            inputs: this.inputs(),
            referenceOutput: this.referenceOutput().trim() || 'reference',
            resultOperation: this.resultOperation(),
            pollMinutes: this.pollMinutes(),
            result: this.result(),
            outcomeMap: this.outcomeMap(),
            callbackSecret: this.callbackSecret().trim() || null,
          }
        : null,
    };
    try {
      await this.drafts.addChange(this.data.type.entityType, this.code().trim(), this.row ? 'UPDATE' : 'CREATE', payload, { submit });
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}

function named(r: ConfigRow) {
  return { code: r.code, nameAr: (r.nameAr as string) || r.code, nameEn: (r.nameEn as string) || r.code };
}

/** Options with a code (the English label defaults to the Arabic one). */
function cleanOptions(options: VisitOption[]): VisitOption[] {
  return (options ?? [])
    .filter((o) => o?.code?.trim())
    .map((o) => ({
      code: o.code.trim().toUpperCase(),
      labelAr: o.labelAr?.trim() || o.code.trim(),
      labelEn: o.labelEn?.trim() || o.labelAr?.trim() || o.code.trim(),
    }));
}

function isObject(value: unknown): boolean {
  return !!value && typeof value === 'object' && !Array.isArray(value);
}
