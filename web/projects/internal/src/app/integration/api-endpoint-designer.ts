import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import {
  ApiEndpointDefinition,
  ApiEndpointParameter,
  ApiFunctionInfo,
  ApiParameterType,
  AppsControlApi,
  BusinessAppContext,
  I18nService,
  PublicApiAdmin,
  PublicApiInfo,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';
import { ConfigEditorData } from '../apps-control/config/config-editor';
import { payloadOf } from '../apps-control/config/form-values';
import { DraftsService } from '../apps-control/drafts.service';
import { QuerySourceInfo } from '../apps-control/lovs/lov-builder-model';
import { PrivilegesService } from '../apps-control/privileges.service';
import { SubmitButton } from '../apps-control/submit-button';
import { ApiEndpointData } from './api-endpoint-data';
import {
  EndpointProblem,
  EndpointQuery,
  checkEndpoint,
  endpointPayload,
  fromQuery,
  normalizeEndpoint,
  previewUrl,
  syncParameters,
} from './api-endpoint-model';
import { ApiEndpointTry } from './api-endpoint-try';
import { placeholders } from './operation-designer-model';
import { PUBLIC_API_TRANSLATIONS } from './public-api-translations';

type Part = EndpointProblem['part'];
const PARTS: Part[] = ['path', 'data', 'details'];
const TYPES: ApiParameterType[] = ['TEXT', 'NUMBER', 'DATE', 'BOOLEAN'];

/**
 * Designs a public API endpoint (ADR-0042): its path, whose placeholders become inputs, and the query inputs; what it
 * returns (rows built over classes and registered tables, or a system function); and its details. Try it calls the
 * unsaved endpoint as a partner would. Saving adds the endpoint to a change set like every configuration.
 */
@Component({
  selector: 'dc-api-endpoint-designer',
  imports: [
    ApiEndpointData,
    ApiEndpointTry,
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTabsModule,
    SubmitButton,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './api-endpoint-designer.html',
  styleUrl: './api-endpoint-designer.scss',
})
export class ApiEndpointDesigner {
  protected readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ApiEndpointDesigner, boolean>);
  private readonly config = inject(AppsControlApi);
  private readonly api = inject(PublicApiAdmin);
  protected readonly drafts = inject(DraftsService);
  private readonly privileges = inject(PrivilegesService);
  protected readonly i18n = inject(I18nService);

  protected readonly entityType = 'integ.api-endpoint';
  protected readonly parts = PARTS;
  protected readonly types = TYPES;
  protected readonly isNew = !this.data.row;

  protected readonly code = signal(this.data.row?.code ?? '');
  protected readonly def = signal<ApiEndpointDefinition>(
    normalizeEndpoint({ ...payloadOf(this.data.row), ...(this.data.defaults ?? {}) }),
  );
  protected readonly query = signal<EndpointQuery>(fromQuery(this.def().query));
  private readonly entityCode =
    (this.data.row?.['entityCode'] as string | null | undefined) ?? null;
  private readonly businessApps = this.initialApps();
  protected readonly part = signal(0);

  protected readonly sources = toSignal(
    this.config
      .lovBuilderSources<QuerySourceInfo>()
      .pipe(catchError(() => of([] as QuerySourceInfo[]))),
    { initialValue: [] as QuerySourceInfo[] },
  );
  protected readonly functions = toSignal(
    this.api.functions().pipe(catchError(() => of([] as ApiFunctionInfo[]))),
    { initialValue: [] as ApiFunctionInfo[] },
  );
  protected readonly info = toSignal(
    this.api.info().pipe(catchError(() => of(null as PublicApiInfo | null))),
    { initialValue: null as PublicApiInfo | null },
  );

  protected readonly placeholders = computed(() => placeholders(this.def().path));
  protected readonly url = computed(() => previewUrl(this.def(), {}, this.info()?.apiUrl ?? ''));
  protected readonly problems = computed(() =>
    checkEndpoint(this.code().trim(), this.def(), this.isNew, this.functions()),
  );
  protected readonly partProblems = computed(() =>
    PARTS.map((p) => this.problems().filter((x) => x.part === p).length),
  );
  protected readonly canTry = computed(() =>
    this.privileges.can('integration.test-console', 'EXECUTE'),
  );
  protected readonly trial = computed(() => endpointPayload(this.code(), this.def(), this.query()));

  protected readonly saving = signal(false);
  protected readonly errors = signal<string[]>([]);

  constructor() {
    this.i18n.register(PUBLIC_API_TRANSLATIONS);
  }

  protected set(change: Partial<ApiEndpointDefinition>): void {
    this.def.update((d) => ({ ...d, ...change }));
  }

  protected setPath(path: string): void {
    this.def.update((d) => ({ ...d, path, parameters: syncParameters(d.parameters, path) }));
  }

  protected addParameter(): void {
    const taken = new Set(this.def().parameters.map((p) => p.name));
    let name = 'q';
    for (let i = 2; taken.has(name); i++) name = `q${i}`;
    this.set({
      parameters: [
        ...this.def().parameters,
        {
          name,
          location: 'QUERY',
          type: 'TEXT',
          isRequired: false,
          isMasked: false,
          descriptionAr: null,
          descriptionEn: null,
          sample: null,
        },
      ],
    });
  }

  protected setParameter(index: number, change: Partial<ApiEndpointParameter>): void {
    const before = this.def().parameters[index];
    this.def.update((d) => {
      const parameters = d.parameters.map((p, i) => (i === index ? { ...p, ...change } : p));
      // Renaming a path parameter renames its placeholder (and what refers to it).
      let path = d.path;
      if (change.name !== undefined && change.name !== before.name) {
        if (before.location === 'PATH')
          path = path.replaceAll(`{${before.name}}`, `{${change.name}}`);
        const functionInputs = d.functionInputs.map((b) =>
          b.parameter === before.name ? { ...b, parameter: change.name } : b,
        );
        return { ...d, parameters, path, functionInputs };
      }
      return { ...d, parameters, path };
    });
    if (change.name !== undefined && change.name !== before.name) {
      this.query.update((q) => ({
        ...q,
        conditions: q.conditions.map((c) =>
          c.param === before.name ? { ...c, param: change.name! } : c,
        ),
      }));
    }
  }

  protected removeParameter(index: number): void {
    const removed = this.def().parameters[index];
    this.def.update((d) => ({
      ...d,
      parameters: d.parameters.filter((_, i) => i !== index),
      path: removed.location === 'PATH' ? d.path.replaceAll(`/{${removed.name}}`, '') : d.path,
    }));
  }

  protected problemText(problem: EndpointProblem): string {
    let text = this.i18n.translate(problem.key);
    for (const [k, v] of Object.entries(problem.params ?? {}))
      text = text.replace(`{${k}}`, String(v));
    return text;
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
      ...this.trial(),
      entityCode: this.entityCode ?? '',
      businessApps: this.businessApps,
    };
    delete payload['code'];
    this.saving.set(true);
    try {
      await this.drafts.addChange(
        this.entityType,
        this.code().trim(),
        this.isNew ? 'CREATE' : 'UPDATE',
        payload,
        {
          submit,
        },
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

  private initialApps(): string[] {
    const row = this.data.row;
    if (row) return (row['businessApps'] as string[] | undefined) ?? [];
    const current = inject(BusinessAppContext).current();
    return current ? [current] : [];
  }
}
