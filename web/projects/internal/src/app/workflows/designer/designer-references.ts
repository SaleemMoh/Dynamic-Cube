import { Injectable, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AppsControlApi, ConfigRow, DomainItem, LocalizedText, VersionDefinition } from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';
import { NODE_ICONS, NODE_TYPES, NodeType } from './designer-model';

export interface RefOption {
  value: string;
  label: LocalizedText;
}

/** A question of the service, as rules over `data` can read it. */
export interface QuestionRef {
  code: string;
  label: LocalizedText;
  type: string;
  options: RefOption[];
}

/**
 * What the side panel offers to pick from: roles, employees, rule sets, document types, payment providers, visit types, and the
 * service's levels and questions (from its latest version). A list that cannot be read is simply empty.
 */
@Injectable()
export class DesignerReferences {
  private readonly api = inject(AppsControlApi);

  private list(entityType: string) {
    return toSignal(this.api.list(entityType).pipe(catchError(() => of([] as ConfigRow[]))), {
      initialValue: [] as ConfigRow[],
    });
  }

  private readonly roleRows = this.list('apps.role');
  private readonly ruleSetRows = this.list('dec.rule-set');
  private readonly documentTypeRows = this.list('doc.document-type');
  private readonly providerRows = this.list('pay.provider');
  private readonly visitTypeRows = this.list('vis.visit-type');
  private readonly questionRows = this.list('svc.question');
  private readonly versionRows = this.list('svc.service-version');
  private readonly userRows = this.list('apps.user');
  private readonly orgUnitRows = this.list('apps.org-unit');
  private readonly positionRows = this.list('apps.position');
  private readonly service = signal<string | null>(null);
  /** The step types as class WF_STEP_TYPE (Class/Domain) names them: active ones, in their order. */
  private readonly stepRows = toSignal(this.api.classDomains('WF_STEP_TYPE').pipe(catchError(() => of([] as DomainItem[]))), {
    initialValue: [] as DomainItem[],
  });
  private readonly steps = computed(() => new Map(this.stepRows().map((d) => [d.code, d])));

  /** What the palette offers: the active step types in the class's order (every type until the class is read). */
  readonly paletteTypes = computed<NodeType[]>(() => {
    const rows = this.stepRows().filter((d) => (NODE_TYPES as readonly string[]).includes(d.code));
    const types = rows.length ? rows.map((d) => d.code as NodeType) : [...NODE_TYPES];
    return types.filter((t) => t !== 'START');
  });

  /** The name of a step type from its class value, or null to use the built-in one. */
  stepName(type: NodeType): LocalizedText | null {
    const row = this.steps().get(type);
    return row ? { ar: row.nameAr, en: row.nameEn } : null;
  }

  stepHint(type: NodeType): LocalizedText | null {
    const attributes = this.steps().get(type)?.attributes;
    const ar = attributes?.['HINT_AR'];
    const en = attributes?.['HINT_EN'];
    return typeof ar === 'string' && typeof en === 'string' && ar && en ? { ar, en } : null;
  }

  stepIcon(type: NodeType): string {
    const icon = this.steps().get(type)?.attributes?.['ICON'];
    return typeof icon === 'string' && icon ? icon : NODE_ICONS[type];
  }

  readonly roles = computed(() => options(this.roleRows()));
  readonly ruleSets = computed(() => options(this.ruleSetRows()));
  readonly documentTypes = computed(() => options(this.documentTypeRows()));
  readonly providers = computed(() => options(this.providerRows()));
  readonly visitTypes = computed(() => options(this.visitTypeRows()));
  readonly orgUnits = computed(() => options(this.orgUnitRows().filter((u) => u.isActive !== false)));
  readonly positions = computed(() => options(this.positionRows().filter((p) => p.isActive !== false)));

  /** A visit type's outcomes and form fields, for routing on its result. */
  visitType(code: string | null | undefined): { outcomes: RefOption[]; fields: QuestionRef[] } {
    const row = this.visitTypeRows().find((r) => r.code === code);
    const outcomes = (row?.['outcomes'] as { code: string; nameAr: string; nameEn: string }[] | undefined) ?? [];
    const fields =
      (row?.['fields'] as
        { code: string; labelAr: string; labelEn: string; type: string; options?: { code: string; labelAr: string; labelEn: string }[] }[] | undefined) ?? [];
    return {
      outcomes: outcomes.map((o) => ({ value: o.code, label: { ar: o.nameAr, en: o.nameEn } })),
      fields: fields.map((f) => ({
        code: f.code,
        label: { ar: f.labelAr, en: f.labelEn },
        type: f.type,
        options: (f.options ?? []).map((o) => ({ value: o.code, label: { ar: o.labelAr, en: o.labelEn } })),
      })),
    };
  }
  readonly users = computed(() => options(this.userRows().filter((u) => u['status'] !== 'DISABLED')));

  /** The service's latest version's definition. */
  private readonly definition = computed<VersionDefinition | null>(() => {
    const service = this.service();
    if (!service) return null;
    const rows = this.versionRows()
      .filter((r) => r.code.startsWith(service + '/'))
      .sort((a, b) => Number(b.code.split('/')[1]) - Number(a.code.split('/')[1]));
    return (rows[0]?.['definition'] as VersionDefinition | undefined) ?? null;
  });

  readonly levels = computed<RefOption[]>(() =>
    (this.definition()?.levels ?? []).map((l) => ({
      value: l.code,
      label: { ar: l.nameAr, en: l.nameEn },
    })),
  );

  readonly questions = computed<QuestionRef[]>(() => {
    const bank = new Map(this.questionRows().map((q) => [q.code, q]));
    const codes = (this.definition()?.steps ?? []).flatMap((s) => s.sections.flatMap((x) => x.questions.map((q) => q.code)));
    return [...new Set(codes)].map((code) => {
      const q = bank.get(code);
      const opts = (q?.['options'] as { value: string; labelAr: string; labelEn: string }[] | undefined) ?? [];
      return {
        code,
        label: { ar: (q?.['labelAr'] as string) ?? code, en: (q?.['labelEn'] as string) ?? code },
        type: (q?.['type'] as string) ?? 'TEXT',
        options: opts.map((o) => ({ value: o.value, label: { ar: o.labelAr, en: o.labelEn } })),
      };
    });
  });

  load(service: string | null): void {
    this.service.set(service);
  }
}

function options(rows: ConfigRow[]): RefOption[] {
  return rows.map((r) => ({
    value: r.code,
    label: { ar: (r.nameAr as string) || r.code, en: (r.nameEn as string) || r.code },
  }));
}
