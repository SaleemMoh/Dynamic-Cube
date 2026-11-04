import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { AppsControlApi, ConfigRow, LocalizedText, TaskField } from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';
import { MatrixInput, bankQuestionCode } from './matrix-model';
import type { FormLink } from './matrix-form';

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

/** A workflow deciding with a rule set, and the service it belongs to. */
export interface MatrixUse {
  workflow: string;
  workflowName: LocalizedText;
  /** Its decision steps naming the rule set. */
  steps: { code: string; name: LocalizedText }[];
  service: string | null;
  serviceName: LocalizedText | null;
  /** The service's latest version. */
  version: number | null;
  /** That version's panel shows the result to the applicant while filling. */
  liveResult: boolean;
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

/** One answer of a bank question (an option, a list value, or yes/no). */
export interface BankAnswer {
  value: string;
  labelAr: string;
  labelEn: string;
  /** The answer's weight the bank gives it, in points and/or as a grade. */
  points?: number | null;
  grade?: number | null;
}

/** What a bank question says about its answer: the matrix input type it gives, its answers and its limits. */
export interface BankQuestionInfo {
  code: string;
  questionType: string;
  inputType: 'number' | 'text' | 'option' | 'boolean' | 'date' | 'list';
  /** The answers, when the question has choices (options, a list of values, or yes/no); null otherwise. */
  answers: BankAnswer[] | null;
  /** The list of values the answers come from (its values may still be loading). */
  lov: string | null;
  min: number | null;
  max: number | null;
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
  private readonly serviceRows = signal<ConfigRow[]>([]);
  /** Bank questions added on the page and not saved yet (read like the bank's own). */
  readonly pendingQuestions = signal<ConfigRow[]>([]);
  readonly partyFacts = signal<PartyFact[]>([]);
  /** Values of the lists of values bank questions take their answers from, read once per list. */
  private readonly lovAnswers = signal<Record<string, BankAnswer[]>>({});
  private readonly lovRequested = new Set<string>();

  constructor() {
    this.loadQuestions();
    this.load('wf.workflow', this.workflowRows);
    this.load('wf.workflow-version', this.versionRows);
    this.load('integ.operation', this.operationRows);
    this.load('svc.service-version', this.serviceVersionRows);
    this.load('svc.service', this.serviceRows);
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

  /** A bank question's answer type, answers and limits (its list of values is read the first time it is asked for). */
  bankQuestion(code: string | null | undefined): BankQuestionInfo | null {
    const q = this.question(code);
    if (!q) return null;
    const type = String(q['type'] ?? 'TEXT').toUpperCase();
    const multiple = q['multiple'] === true;
    const lov = (q['lov'] as string | null | undefined) || null;
    const validation =
      (q['validation'] as { min?: number | null; max?: number | null } | null | undefined) ?? {};
    let answers: BankAnswer[] | null;
    if (type === 'CHECKBOX') {
      answers = [
        { value: 'yes', labelAr: 'نعم', labelEn: 'Yes' },
        { value: 'no', labelAr: 'لا', labelEn: 'No' },
      ];
    } else if (lov) {
      this.loadLov(lov);
      answers = this.lovAnswers()[lov] ?? [];
    } else {
      const options = (q['options'] as BankAnswer[] | undefined) ?? [];
      answers = options.length
        ? options.map((o) => ({
            value: o.value,
            labelAr: o.labelAr || o.value,
            labelEn: o.labelEn || o.value,
            points: o.points ?? null,
            grade: o.grade ?? null,
          }))
        : null;
    }
    const inputType =
      type === 'NUMBER'
        ? 'number'
        : type === 'CHECKBOX'
          ? 'boolean'
          : type === 'DATE'
            ? 'date'
            : type === 'ROWS' || (type === 'SELECT' && multiple)
              ? 'list'
              : type === 'SELECT'
                ? 'option'
                : 'text';
    return {
      code: q.code,
      questionType: type,
      inputType,
      answers,
      lov,
      min: validation.min ?? null,
      max: validation.max ?? null,
    };
  }

  /** An input's answers from its bank question (null when it has no bank question with choices). */
  readonly answersOf = (i: MatrixInput): BankAnswer[] | null =>
    this.bankQuestion(bankQuestionCode(i))?.answers ?? null;

  private loadLov(lov: string): void {
    if (this.lovRequested.has(lov)) return;
    this.lovRequested.add(lov);
    this.api
      .lovOptions(lov)
      .pipe(catchError(() => of(null)))
      .subscribe((page) =>
        this.lovAnswers.update((all) => ({
          ...all,
          [lov]: (page?.items ?? []).map((i) => ({
            value: i.value,
            labelAr: i.labelAr || i.value,
            labelEn: i.labelEn || i.value,
          })),
        })),
      );
  }

  question(code: string | null | undefined): ConfigRow | undefined {
    if (!code) return undefined;
    return (
      this.questions().find((q) => q.code === code) ??
      this.pendingQuestions().find((q) => q.code === code)
    );
  }

  /**
   * Who uses the rule set: each workflow with a decision step naming it, the service owning that workflow (with its
   * latest version) and whether that version's panel shows the result live on the portal.
   */
  usedBy(ruleSet: string | null): MatrixUse[] {
    if (!ruleSet) return [];
    const steps = this.decisionSteps().filter(
      (d) => d.ruleSet.toLowerCase() === ruleSet.toLowerCase(),
    );
    const workflows = [...new Set(steps.map((d) => d.workflow))];
    return workflows.map((workflow) => {
      const mine = steps.filter((d) => d.workflow === workflow);
      const row = this.workflowRows().find((w) => w.code === workflow);
      const service = (row?.['serviceCode'] as string | null | undefined) || null;
      const serviceRow = service ? this.serviceRows().find((r) => r.code === service) : undefined;
      const latest = service
        ? this.serviceVersionRows()
            .filter((v) => v.code.startsWith(service + '/'))
            .sort((a, b) => versionNumber(b.code) - versionNumber(a.code))[0]
        : undefined;
      const result = (
        latest?.['definition'] as
          | { panel?: { result?: { enabled?: boolean; step?: string | null } | null } | null }
          | undefined
      )?.panel?.result;
      return {
        workflow,
        workflowName: mine[0].workflowName,
        steps: mine.map((d) => ({ code: d.step, name: d.stepName })),
        service,
        serviceName: serviceRow
          ? { ar: String(serviceRow.nameAr || service), en: String(serviceRow.nameEn || service) }
          : null,
        version: latest ? versionNumber(latest.code) : null,
        liveResult: !!result?.enabled && (!result.step || mine.some((d) => d.step === result.step)),
      };
    });
  }

  /** Reads the service versions again (after saving a new one). */
  reloadServiceVersions(): void {
    this.load('svc.service-version', this.serviceVersionRows);
  }

  /**
   * The application forms of the services whose workflow decides with the rule set (a decision step naming it): each
   * service's latest version, newest services first.
   */
  formsOf(ruleSet: string | null): Omit<FormLink, 'newQuestions'>[] {
    if (!ruleSet) return [];
    const workflows = new Set(
      this.decisionSteps()
        .filter((d) => d.ruleSet.toLowerCase() === ruleSet.toLowerCase())
        .map((d) => d.workflow),
    );
    const services = [
      ...new Set(
        this.workflowRows()
          .filter((w) => workflows.has(w.code) && w['serviceCode'])
          .map((w) => String(w['serviceCode'])),
      ),
    ];
    return services.flatMap((service) => {
      const versions = this.serviceVersionRows()
        .filter((v) => v.code.startsWith(service + '/'))
        .sort((a, b) => versionNumber(b.code) - versionNumber(a.code));
      const latest = versions[0];
      const definition = latest?.['definition'] as FormLink['definition'] | undefined;
      if (!latest || !definition?.steps) return [];
      const row = this.serviceRows().find((r) => r.code === service);
      return [
        {
          service,
          serviceName: {
            ar: String(row?.nameAr || service),
            en: String(row?.nameEn || service),
          },
          version: versionNumber(latest.code),
          definition,
        },
      ];
    });
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
