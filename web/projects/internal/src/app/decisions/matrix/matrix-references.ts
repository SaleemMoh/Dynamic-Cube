import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { AppsControlApi, ConfigRow, LocalizedText, TaskField } from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';

/** A human task of a workflow, with the fields the employee fills in on it. */
export interface ReviewTask {
  workflow: string;
  workflowName: LocalizedText;
  step: string;
  stepName: LocalizedText;
  fields: TaskField[];
}

/** An Integration Manager operation: what it is called with and what it gives back. */
export interface OperationInfo {
  code: string;
  name: LocalizedText;
  parameters: { name: string; required: boolean; sample: string | null }[];
  outputs: { name: string; path: string }[];
}

/** A workflow step that calls an operation (its result is kept as `integrations.<result>`). */
export interface IntegrationStep {
  workflow: string;
  workflowName: LocalizedText;
  step: string;
  stepName: LocalizedText;
  operation: string;
  result: string;
}

/** A workflow decision step and the rule set it evaluates. */
export interface DecisionStep {
  workflow: string;
  workflowName: LocalizedText;
  step: string;
  stepName: LocalizedText;
  ruleSet: string;
  /** The step maps its own input instead of passing the request's facts. */
  mapsInput: boolean;
}

/** A party fact a value can be read from (`party.crNumber`, …). */
export interface PartyFact {
  code: string;
  name: LocalizedText;
}

/** Where a matrix input can come from: bank questions, workflow tasks, operations and party facts (read once per page). */
@Injectable()
export class MatrixReferences {
  private readonly api = inject(AppsControlApi);
  private readonly http = inject(HttpClient);

  readonly questions = signal<ConfigRow[]>([]);
  private readonly workflowRows = signal<ConfigRow[]>([]);
  private readonly versionRows = signal<ConfigRow[]>([]);
  private readonly operationRows = signal<ConfigRow[]>([]);
  private readonly serviceVersionRows = signal<ConfigRow[]>([]);
  readonly partyFacts = signal<PartyFact[]>([]);

  constructor() {
    this.loadQuestions();
    this.load('wf.workflow', this.workflowRows);
    this.load('wf.workflow-version', this.versionRows);
    this.load('integ.operation', this.operationRows);
    this.load('svc.service-version', this.serviceVersionRows);
    this.http
      .get<{ code: string; nameAr: string; nameEn: string }[]>(
        '/api/services/v1/admin/fill-sources',
      )
      .pipe(catchError(() => of([])))
      .subscribe((rows) =>
        this.partyFacts.set(
          rows.map((r) => ({
            code: r.code,
            name: { ar: r.nameAr || r.code, en: r.nameEn || r.code },
          })),
        ),
      );
  }

  loadQuestions(): void {
    this.load('svc.question', this.questions);
  }

  private load(entityType: string, into: ReturnType<typeof signal<ConfigRow[]>>): void {
    this.api
      .list(entityType)
      .pipe(catchError(() => of([] as ConfigRow[])))
      .subscribe((rows) => into.set(rows));
  }

  /** Every node of each workflow's latest version, with the workflow's name. */
  private readonly nodes = computed(() => {
    const latest = new Map<string, ConfigRow>();
    for (const v of this.versionRows()) {
      const workflow = v.code.slice(0, v.code.lastIndexOf('/'));
      const current = latest.get(workflow);
      if (!current || versionNumber(v.code) > versionNumber(current.code)) latest.set(workflow, v);
    }
    const names = new Map(
      this.workflowRows().map((w) => [w.code, { ar: w.nameAr || w.code, en: w.nameEn || w.code }]),
    );
    return [...latest].flatMap(([workflow, version]) =>
      (
        ((version['definition'] as { nodes?: unknown[] } | undefined)?.nodes ??
          []) as WorkflowNode[]
      ).map((node) => ({
        workflow,
        workflowName: names.get(workflow) ?? { ar: workflow, en: workflow },
        node,
        stepName: { ar: node.nameAr || node.code, en: node.nameEn || node.code },
      })),
    );
  });

  /** The human tasks of each workflow's latest version. */
  readonly tasks = computed<ReviewTask[]>(() =>
    this.nodes()
      .filter((n) => n.node.type === 'HUMAN_TASK')
      .map((n) => ({
        workflow: n.workflow,
        workflowName: n.workflowName,
        step: n.node.code,
        stepName: n.stepName,
        fields: n.node.fields ?? [],
      })),
  );

  /** The integration steps of each workflow's latest version. */
  readonly integrationSteps = computed<IntegrationStep[]>(() =>
    this.nodes()
      .filter((n) => n.node.type === 'INTEGRATION' && n.node.operation)
      .map((n) => ({
        workflow: n.workflow,
        workflowName: n.workflowName,
        step: n.node.code,
        stepName: n.stepName,
        operation: n.node.operation!,
        result: n.node.result || n.node.code,
      })),
  );

  /** The decision steps of each workflow's latest version. */
  readonly decisionSteps = computed<DecisionStep[]>(() =>
    this.nodes()
      .filter((n) => n.node.type === 'DECISION' && n.node.ruleSet)
      .map((n) => ({
        workflow: n.workflow,
        workflowName: n.workflowName,
        step: n.node.code,
        stepName: n.stepName,
        ruleSet: n.node.ruleSet!,
        mapsInput: Object.keys(n.node.input ?? {}).length > 0,
      })),
  );

  /** The services whose latest version asks a question, by question code. */
  readonly questionServices = computed(() => {
    const latest = new Map<string, ConfigRow>();
    for (const v of this.serviceVersionRows()) {
      const service = v.code.slice(0, v.code.lastIndexOf('/'));
      const current = latest.get(service);
      if (!current || versionNumber(v.code) > versionNumber(current.code)) latest.set(service, v);
    }
    const asked = new Map<string, string[]>();
    for (const [service, version] of latest) {
      const steps =
        (
          version['definition'] as
            | { steps?: { sections?: { questions?: { question?: string; code?: string }[] }[] }[] }
            | undefined
        )?.steps ?? [];
      for (const q of steps.flatMap((st) =>
        (st.sections ?? []).flatMap((sec) => sec.questions ?? []),
      )) {
        const code = q.question ?? q.code;
        if (code && !(asked.get(code) ?? []).includes(service))
          asked.set(code, [...(asked.get(code) ?? []), service]);
      }
    }
    return asked;
  });

  /** Operations called by a service form's integration steps, by operation code. */
  readonly formOperations = computed(() => {
    const called = new Map<string, string[]>();
    for (const v of this.serviceVersionRows()) {
      const service = v.code.slice(0, v.code.lastIndexOf('/'));
      const steps =
        (v['definition'] as { steps?: { integration?: { operation?: string } }[] } | undefined)
          ?.steps ?? [];
      for (const op of steps
        .map((st) => st.integration?.operation)
        .filter((o): o is string => !!o)) {
        if (!(called.get(op) ?? []).includes(service))
          called.set(op, [...(called.get(op) ?? []), service]);
      }
    }
    return called;
  });

  readonly operations = computed<OperationInfo[]>(() =>
    this.operationRows().map((o) => ({
      code: o.code,
      name: { ar: o.nameAr || o.code, en: o.nameEn || o.code },
      parameters: (
        (o['parameters'] as
          { name: string; isRequired?: boolean; sampleValue?: string | null }[] | undefined) ?? []
      ).map((p) => ({
        name: p.name,
        required: p.isRequired !== false,
        sample: p.sampleValue ?? null,
      })),
      outputs: ((o['outputs'] as { name: string; path: string }[] | undefined) ?? []).map((x) => ({
        name: x.name,
        path: x.path,
      })),
    })),
  );

  question(code: string | null | undefined): ConfigRow | undefined {
    return code ? this.questions().find((q) => q.code === code) : undefined;
  }

  operation(code: string | null | undefined): OperationInfo | undefined {
    return code ? this.operations().find((o) => o.code === code) : undefined;
  }

  /** The task a reviewer input is read from (step codes may repeat across workflows: the first match). */
  task(step: string | null | undefined): ReviewTask | undefined {
    return step ? this.tasks().find((t) => t.step === step) : undefined;
  }
}

interface WorkflowNode {
  code: string;
  type: string;
  nameAr?: string;
  nameEn?: string;
  fields?: TaskField[];
  operation?: string | null;
  result?: string | null;
  ruleSet?: string | null;
  input?: Record<string, unknown> | null;
}

function versionNumber(code: string): number {
  return Number(code.slice(code.lastIndexOf('/') + 1)) || 0;
}
