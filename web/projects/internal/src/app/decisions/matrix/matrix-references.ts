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
  readonly partyFacts = signal<PartyFact[]>([]);

  constructor() {
    this.loadQuestions();
    this.load('wf.workflow', this.workflowRows);
    this.load('wf.workflow-version', this.versionRows);
    this.load('integ.operation', this.operationRows);
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

  /** The human tasks of each workflow's latest version. */
  readonly tasks = computed<ReviewTask[]>(() => {
    const latest = new Map<string, ConfigRow>();
    for (const v of this.versionRows()) {
      const workflow = v.code.slice(0, v.code.lastIndexOf('/'));
      const current = latest.get(workflow);
      if (!current || versionNumber(v.code) > versionNumber(current.code)) latest.set(workflow, v);
    }
    const names = new Map(
      this.workflowRows().map((w) => [w.code, { ar: w.nameAr || w.code, en: w.nameEn || w.code }]),
    );
    const tasks: ReviewTask[] = [];
    for (const [workflow, version] of latest) {
      const nodes = ((version['definition'] as { nodes?: unknown[] } | undefined)?.nodes ?? []) as {
        code: string;
        type: string;
        nameAr?: string;
        nameEn?: string;
        fields?: TaskField[];
      }[];
      for (const n of nodes.filter((x) => x.type === 'HUMAN_TASK')) {
        tasks.push({
          workflow,
          workflowName: names.get(workflow) ?? { ar: workflow, en: workflow },
          step: n.code,
          stepName: { ar: n.nameAr || n.code, en: n.nameEn || n.code },
          fields: n.fields ?? [],
        });
      }
    }
    return tasks;
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

function versionNumber(code: string): number {
  return Number(code.slice(code.lastIndexOf('/') + 1)) || 0;
}
