import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AppsControlApi, ConfigRow, LocalizedText, RuleVariable } from '@dynamic-cube/shared';
import { Observable, catchError, map, of } from 'rxjs';

/** A question of the bank, as the builder lists it. */
export interface BankQuestion {
  code: string;
  label: LocalizedText;
  type: string;
  options: { value: string; label: LocalizedText }[];
  lov: string | null;
  prefill: string | null;
  /** The service the question belongs to; null for a shared bank question. */
  serviceCode: string | null;
}

/** A path an integration input can take its value from, with its name. */
export interface FillSource {
  code: string;
  nameAr: string;
  nameEn: string;
}

/** What the builder offers: the question bank, profile fields, document statuses and the service's workflow. */
@Injectable()
export class BuilderReferences {
  private readonly api = inject(AppsControlApi);

  private list(entityType: string) {
    return toSignal(this.api.list(entityType).pipe(catchError(() => of([] as ConfigRow[]))), { initialValue: [] as ConfigRow[] });
  }

  private readonly questionRows = signal<ConfigRow[]>([]);
  private readonly profileRows = this.list('party.profile-field');
  readonly workflows = this.list('wf.workflow');
  /** Integration Manager operations (for integration steps). */
  readonly operations = this.list('integ.operation');
  /** Service archetypes, with the template a service without versions starts from. */
  readonly archetypes = this.list('svc.archetype');
  /** Where integration inputs can take their values from: the party's data and the answers. */
  readonly fillSources = toSignal(
    inject(HttpClient)
      .get<FillSource[]>('/api/services/v1/admin/fill-sources')
      .pipe(catchError(() => of([] as FillSource[]))),
    { initialValue: [] as FillSource[] },
  );
  readonly workflowVersions = this.list('wf.workflow-version');

  constructor() {
    this.loadQuestions();
  }

  /** Reads the question bank again (after a question was added). */
  loadQuestions(): void {
    this.api
      .list('svc.question')
      .pipe(catchError(() => of([] as ConfigRow[])))
      .subscribe((rows) => this.questionRows.set(rows));
  }

  /** A bank question's row as the configuration editor edits it. */
  row(code: string): Observable<ConfigRow | undefined> {
    return of(this.questionRows().find((q) => q.code === code)).pipe(map((r) => r));
  }

  readonly questions = computed<BankQuestion[]>(() =>
    this.questionRows().map((q) => {
      const opts = (q['options'] as { value: string; labelAr: string; labelEn: string }[] | undefined) ?? [];
      return {
        code: q.code,
        label: { ar: (q.nameAr as string) || q.code, en: (q.nameEn as string) || q.code },
        type: (q['type'] as string) ?? 'TEXT',
        options: opts.map((o) => ({ value: o.value, label: { ar: o.labelAr, en: o.labelEn } })),
        lov: (q['lov'] as string | null) ?? null,
        prefill: (q['prefill'] as string | null) ?? null,
        serviceCode: q['serviceCode'] && q['serviceCode'] !== '*' ? (q['serviceCode'] as string) : null,
      };
    }),
  );
  readonly bank = computed(() => new Set(this.questions().map((q) => q.code)));
  readonly byCode = computed(() => new Map(this.questions().map((q) => [q.code, q])));

  /** Variables of a question by its code alone (what section and question rules see) or under `data.` (other rules). */
  questionVariables(codes: string[], prefix: '' | 'data.'): RuleVariable[] {
    const byCode = this.byCode();
    return codes.map((code) => {
      const q = byCode.get(code);
      return {
        path: prefix + code,
        label: q?.label ?? { ar: code, en: code },
        type: q?.type === 'NUMBER' ? 'number' : q?.type === 'DATE' ? 'date' : q?.type === 'CHECKBOX' ? 'boolean' : 'text',
        options: q?.type === 'CHECKBOX' ? [{ value: 'true', label: { ar: 'نعم', en: 'Yes' } }] : q?.options,
      };
    });
  }

  /** The beneficiary's facts eligibility and fee rules can read. */
  readonly partyVariables = computed<RuleVariable[]>(() => [
    {
      path: 'party.kind',
      label: { ar: 'نوع المستفيد', en: 'Beneficiary kind' },
      options: [
        { value: 'INDIVIDUAL', label: { ar: 'فرد', en: 'Individual' } },
        { value: 'ESTABLISHMENT', label: { ar: 'منشأة', en: 'Establishment' } },
      ],
    },
    {
      path: 'party.blocked',
      label: { ar: 'عليه جزاء يمنع التقديم', en: 'Banned by a sanction' },
      type: 'boolean',
      options: [{ value: 'true', label: { ar: 'نعم', en: 'Yes' } }],
    },
    ...this.profileRows().map((f) => ({ path: `party.profile.${f.code}`, label: { ar: (f.nameAr as string) || f.code, en: (f.nameEn as string) || f.code } })),
    { path: 'applicant.relationship', label: { ar: 'صفة مقدم الطلب', en: "Applicant's relationship" } },
    { path: 'today', label: { ar: 'تاريخ اليوم', en: 'Today' }, type: 'date' },
    {
      path: 'document.status',
      label: { ar: 'حالة المستند', en: 'Document status' },
      options: ['ACTIVE', 'EXPIRED', 'SUSPENDED'].map((v) => ({ value: v, label: { ar: v, en: v } })),
    },
  ]);

  /** The latest published version of the service's workflow. */
  workflowOf(service: string | null): { code: string; number: number; definition: { nodes: Record<string, unknown>[] } } | null {
    if (!service) return null;
    const wf = this.workflows().find((w) => w['serviceCode'] === service);
    if (!wf) return null;
    const versions = this.workflowVersions()
      .filter((v) => v.code.startsWith(wf.code + '/') && !v['isRetired'])
      .sort((a, b) => Number(b.code.split('/')[1]) - Number(a.code.split('/')[1]));
    const latest = versions[0];
    return latest
      ? { code: wf.code, number: Number(latest.code.split('/')[1]), definition: latest['definition'] as { nodes: Record<string, unknown>[] } }
      : { code: wf.code, number: 0, definition: { nodes: [] } };
  }
}
