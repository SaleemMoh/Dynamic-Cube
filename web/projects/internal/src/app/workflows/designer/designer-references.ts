import { Injectable, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AppsControlApi, ConfigRow, LocalizedText, VersionDefinition } from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';

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
 * What the side panel offers to pick from: roles, employees, rule sets, document types, payment providers, and the
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
  private readonly questionRows = this.list('svc.question');
  private readonly versionRows = this.list('svc.service-version');
  private readonly userRows = this.list('apps.user');
  private readonly service = signal<string | null>(null);

  readonly roles = computed(() => options(this.roleRows()));
  readonly ruleSets = computed(() => options(this.ruleSetRows()));
  readonly documentTypes = computed(() => options(this.documentTypeRows()));
  readonly providers = computed(() => options(this.providerRows()));
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
