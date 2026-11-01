import { JsonPipe } from '@angular/common';
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
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  AppsControlApi,
  BusinessAppContext,
  ConfigRow,
  I18nService,
  IntegrationOutcome,
  LocalizePipe,
  OperationDefinition,
  OperationOutputDef,
  OperationParameterDef,
  OperationProtocol,
  ParameterLocation,
  ResponseCodeDef,
  SoapSettings,
  TestOperationResult,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';
import { ConfigEditorData } from '../apps-control/config/config-editor';
import { payloadOf } from '../apps-control/config/form-values';
import { DraftsService } from '../apps-control/drafts.service';
import { PrivilegesService } from '../apps-control/privileges.service';
import { SubmitButton } from '../apps-control/submit-button';
import {
  DesignProblem,
  METHODS,
  addOutput,
  checkOperation,
  emptySoap,
  isSoap,
  normalizeOperation,
  placeholders,
  previewBody,
  previewEnvelope,
  previewPath,
  soapResponseToJson,
  syncPathParameters,
  withProtocol,
} from './operation-designer-model';
import { OPERATION_DESIGNER_TRANSLATIONS } from './operation-designer-translations';
import { OperationTry } from './operation-try';
import { ResponseTree } from './response-tree';

type Part = DesignProblem['part'];
const PARTS: Part[] = ['request', 'response', 'codes', 'details'];
const LOCATIONS: ParameterLocation[] = ['PATH', 'QUERY', 'HEADER', 'BODY'];
const OUTCOMES: IntegrationOutcome[] = [
  'SUCCESS',
  'NOT_FOUND',
  'BUSINESS_ERROR',
  'TECHNICAL_ERROR',
];

/**
 * Designs an Integration Manager operation without filling a form of JSON (ADR-0041): the request (method, a path whose
 * placeholders become inputs, each input's place, a preview of the URL and body), the response (try the unsaved
 * operation on its connection, or paste a sample, then click the fields that become outputs), the provider's codes and
 * the details. Saving adds the operation to a change set, like every configuration.
 */
@Component({
  selector: 'dc-operation-designer',
  imports: [
    FormsModule,
    JsonPipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTabsModule,
    MatTooltipModule,
    LocalizePipe,
    OperationTry,
    ResponseTree,
    SubmitButton,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './operation-designer.html',
  styleUrl: './operation-designer.scss',
})
export class OperationDesigner {
  protected readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<OperationDesigner, boolean>);
  private readonly api = inject(AppsControlApi);
  protected readonly drafts = inject(DraftsService);
  private readonly privileges = inject(PrivilegesService);
  protected readonly i18n = inject(I18nService);

  protected readonly entityType = 'integ.operation';
  protected readonly methods = METHODS;
  protected readonly locations = LOCATIONS;
  protected readonly outcomes = OUTCOMES;
  protected readonly parts = PARTS;
  protected readonly isNew = !this.data.row;

  protected readonly code = signal(this.data.row?.code ?? '');
  protected readonly def = signal<OperationDefinition>(
    normalizeOperation(this.withDefaults(payloadOf(this.data.row))),
  );
  protected readonly entityCode = signal<string | null>(
    (this.data.row?.['entityCode'] as string | null | undefined) ?? null,
  );
  protected readonly businessApps = signal<string[]>(this.initialApps());
  protected readonly part = signal(0);

  protected readonly connections = toSignal(
    this.api.list('integ.connection').pipe(catchError(() => of([] as ConfigRow[]))),
    { initialValue: [] as ConfigRow[] },
  );
  protected readonly envelopes = toSignal(
    this.api.list('integ.envelope-profile').pipe(catchError(() => of([] as ConfigRow[]))),
    { initialValue: [] as ConfigRow[] },
  );
  protected readonly entities = toSignal(
    this.api.list('apps.entity').pipe(catchError(() => of([] as ConfigRow[]))),
    { initialValue: [] as ConfigRow[] },
  );
  protected readonly apps = toSignal(
    this.api.list('apps.business-app').pipe(catchError(() => of([] as ConfigRow[]))),
    { initialValue: [] as ConfigRow[] },
  );

  protected readonly connection = computed(
    () => this.connections().find((c) => c.code === this.def().connectionCode) ?? null,
  );
  protected readonly envelope = computed(
    () => this.envelopes().find((e) => e.code === this.def().envelopeProfileCode) ?? null,
  );
  protected readonly placeholders = computed(() => placeholders(this.def().pathTemplate));
  protected readonly problems = computed(() =>
    checkOperation(this.code().trim(), this.def(), this.isNew),
  );
  protected readonly partProblems = computed(() =>
    PARTS.map((p) => this.problems().filter((x) => x.part === p).length),
  );

  // Try it, and the response it brings back (the part the envelope reads as the result), or a pasted sample.
  protected readonly canTry = computed(() =>
    this.privileges.can('integration.test-console', 'EXECUTE'),
  );
  protected readonly trial = computed(() => this.payload());
  protected readonly result = signal<TestOperationResult | null>(null);
  protected readonly urlPreview = computed(
    () =>
      `${(this.connection()?.['baseUrl'] as string | undefined) ?? ''}${previewPath(this.def(), {})}`,
  );
  protected readonly soap = computed(() => isSoap(this.def()));
  protected readonly bodyPreview = computed(() =>
    this.soap() ? null : previewBody(this.def(), {}),
  );
  protected readonly envelopePreview = computed(() =>
    this.soap() ? previewEnvelope(this.def(), {}) : null,
  );
  protected readonly sampleText = signal('');
  protected readonly sampleError = signal(false);
  private readonly sample = signal<unknown>(undefined);
  protected readonly response = computed(() => {
    const data = this.result()?.data;
    return data !== null && data !== undefined ? data : this.sample();
  });
  protected readonly outputPaths = computed(() => new Set(this.def().outputs.map((o) => o.path)));

  protected readonly saving = signal(false);
  protected readonly errors = signal<string[]>([]);

  constructor() {
    this.i18n.register(OPERATION_DESIGNER_TRANSLATIONS);
  }

  // ---------------------------------------------------------------- request

  protected set(change: Partial<OperationDefinition>): void {
    this.def.update((d) => ({ ...d, ...change }));
  }

  protected setPath(template: string): void {
    this.def.update((d) => ({
      ...d,
      pathTemplate: template,
      parameters: syncPathParameters(d.parameters, template),
    }));
  }

  protected setMethod(method: string): void {
    this.set({ method });
  }

  /** REST or SOAP (ADR-0044): SOAP is always a POST and describes its envelope. */
  protected setProtocol(protocol: OperationProtocol): void {
    this.def.update((d) => withProtocol(d, protocol));
  }

  protected setSoap(change: Partial<SoapSettings>): void {
    this.def.update((d) => ({ ...d, soap: { ...(d.soap ?? emptySoap()), ...change } }));
  }

  protected addParameter(location: ParameterLocation = 'QUERY'): void {
    const taken = new Set(this.def().parameters.map((p) => p.name));
    let name = 'input';
    for (let i = 2; taken.has(name); i++) name = `input${i}`;
    this.set({
      parameters: [
        ...this.def().parameters,
        { name, location, target: null, isRequired: true, isMasked: false, sampleValue: null },
      ],
    });
  }

  protected setParameter(index: number, change: Partial<OperationParameterDef>): void {
    const before = this.def().parameters[index];
    this.def.update((d) => {
      const parameters = d.parameters.map((p, i) => (i === index ? { ...p, ...change } : p));
      // Renaming a path parameter renames its placeholder.
      let template = d.pathTemplate;
      if (change.name !== undefined && before.location === 'PATH' && change.name !== before.name) {
        template = template.replaceAll(`{${before.name}}`, `{${change.name}}`);
      }
      return { ...d, parameters, pathTemplate: template };
    });
  }

  protected removeParameter(index: number): void {
    const removed = this.def().parameters[index];
    this.def.update((d) => ({
      ...d,
      parameters: d.parameters.filter((_, i) => i !== index),
      pathTemplate:
        removed.location === 'PATH'
          ? d.pathTemplate.replaceAll(`{${removed.name}}`, '')
          : d.pathTemplate,
    }));
  }

  protected tried(result: TestOperationResult | null): void {
    this.result.set(result);
  }

  protected outputValue(name: string): string {
    const value = this.result()?.outputs?.[name];
    return value === undefined ? '' : JSON.stringify(value);
  }

  // ---------------------------------------------------------------- response

  protected useSample(): void {
    try {
      // A SOAP response is read as the platform reads it: the body's element as JSON.
      const text = this.sampleText().trim();
      const value = text.startsWith('<') ? soapResponseToJson(text) : JSON.parse(text);
      if (value === null && text.startsWith('<')) throw new Error('Not a SOAP envelope');
      this.sample.set(value);
      this.sampleError.set(false);
      this.result.set(null);
    } catch {
      this.sampleError.set(true);
    }
  }

  protected pick(path: string): void {
    this.set({ outputs: addOutput(this.def().outputs, path) });
  }

  protected setOutput(index: number, change: Partial<OperationOutputDef>): void {
    this.set({
      outputs: this.def().outputs.map((o, i) => (i === index ? { ...o, ...change } : o)),
    });
  }

  protected removeOutput(index: number): void {
    this.set({ outputs: this.def().outputs.filter((_, i) => i !== index) });
  }

  protected addBlankOutput(): void {
    this.set({ outputs: [...this.def().outputs, { name: '', path: '' }] });
  }

  // ---------------------------------------------------------------- codes and details

  protected addCode(): void {
    this.set({
      responseCodes: [
        ...this.def().responseCodes,
        { code: '', outcome: 'BUSINESS_ERROR', messageAr: null, messageEn: null },
      ],
    });
  }

  protected setCode(index: number, change: Partial<ResponseCodeDef>): void {
    this.set({
      responseCodes: this.def().responseCodes.map((c, i) =>
        i === index ? { ...c, ...change } : c,
      ),
    });
  }

  protected removeCode(index: number): void {
    this.set({ responseCodes: this.def().responseCodes.filter((_, i) => i !== index) });
  }

  protected setMaskFields(text: string): void {
    this.set({
      maskFields: text
        .split(/[,،\n]/)
        .map((f) => f.trim())
        .filter(Boolean),
    });
  }

  protected setRetry(value: 'DEFAULT' | 'YES' | 'NO'): void {
    this.set({ isIdempotent: value === 'DEFAULT' ? null : value === 'YES' });
  }

  protected retryValue(): 'DEFAULT' | 'YES' | 'NO' {
    const v = this.def().isIdempotent;
    return v === null || v === undefined ? 'DEFAULT' : v ? 'YES' : 'NO';
  }

  protected problemText(problem: DesignProblem): string {
    let text = this.i18n.translate(problem.key);
    for (const [k, v] of Object.entries(problem.params ?? {}))
      text = text.replace(`{${k}}`, String(v));
    return text;
  }

  protected rowName(row: ConfigRow | null): string {
    return row ? `${this.i18n.localize({ ar: row.nameAr, en: row.nameEn })} (${row.code})` : '';
  }

  // ---------------------------------------------------------------- save

  private payload(): OperationDefinition {
    const d = this.def();
    return {
      ...d,
      code: this.code().trim() || null,
      nameAr: d.nameAr.trim(),
      nameEn: d.nameEn.trim(),
      pathTemplate: d.pathTemplate.trim(),
      soap:
        d.protocol === 'SOAP' && d.soap
          ? {
              ...d.soap,
              operationName: d.soap.operationName.trim(),
              namespace: d.soap.namespace?.trim() || null,
              action: d.soap.action?.trim() || null,
            }
          : null,
      parameters: d.parameters.map((p) => ({
        ...p,
        target: p.target?.trim() || null,
        sampleValue: p.sampleValue?.trim() || null,
      })),
    };
  }

  protected async save(submit = false): Promise<void> {
    this.errors.set([]);
    if (this.problems().length) {
      this.part.set(PARTS.indexOf(this.problems()[0].part));
      this.errors.set(this.problems().map((p) => this.problemText(p)));
      return;
    }
    const payload: Record<string, unknown> = {
      ...payloadOf(this.data.row),
      ...this.payload(),
      entityCode: this.entityCode() ?? '',
      businessApps: this.businessApps(),
    };
    delete payload['code'];
    this.saving.set(true);
    try {
      await this.drafts.addChange(
        this.entityType,
        this.code().trim(),
        this.isNew ? 'CREATE' : 'UPDATE',
        payload,
        { submit },
      );
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected close(): void {
    this.ref.close(false);
  }

  private withDefaults(row: Record<string, unknown>): Record<string, unknown> {
    return { ...row, ...(this.data.defaults ?? {}) };
  }

  private initialApps(): string[] {
    const row = this.data.row;
    if (row) return (row['businessApps'] as string[] | undefined) ?? [];
    const current = inject(BusinessAppContext).current();
    return current ? [current] : [];
  }
}
