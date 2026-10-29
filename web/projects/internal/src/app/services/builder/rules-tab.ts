import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { JsonLogicRule, RuleEditor, RuleVariable, ServiceLevelDefinition, TranslatePipe } from '@dynamic-cube/shared';
import { BuilderReferences } from './builder-references';
import { BuilderStore } from './builder-store';
import { AvailabilityDef, DateBound, FeeDef, RuleDef, ServiceDraft, freeCode, move, usedQuestions } from './builder-model';

type FeeKind = 'fixed' | 'formula' | 'bracket';

/** Levels, who may apply, cross-field checks, fees and (for document services) when the service is open. */
@Component({
  selector: 'dc-builder-rules-tab',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    TranslatePipe,
    RuleEditor,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './rules-tab.html',
  styleUrl: './rules-tab.scss',
})
export class RulesTab {
  protected readonly store = inject(BuilderStore);
  private readonly refs = inject(BuilderReferences);
  /** The service acts on documents of this type (its requests are made for one): the availability window applies. */
  readonly documentType = input<string | null>(null);

  protected readonly def = this.store.definition;
  protected readonly levelCodes = computed(() => this.def().levels.map((l) => l.code));
  protected readonly formSteps = computed(() => this.def().steps.filter((s) => s.type === 'FORM'));
  /** What eligibility, validation and fee rules read: answers under `data.`, the level, the beneficiary, today. */
  protected readonly variables = computed<RuleVariable[]>(() => [
    ...this.refs.questionVariables(usedQuestions(this.def()), 'data.'),
    {
      path: 'level',
      label: { ar: 'مستوى الخدمة', en: 'Service level' },
      options: this.def().levels.map((l) => ({ value: l.code, label: { ar: l.nameAr, en: l.nameEn } })),
    },
    ...this.refs.partyVariables(),
  ]);

  private set(change: (def: ServiceDraft) => Partial<ServiceDraft>): void {
    this.store.apply((def) => ({ ...def, ...change(def) }));
  }

  protected num(value: unknown): number | null {
    return value === '' || value === null || value === undefined || Number.isNaN(Number(value)) ? null : Number(value);
  }

  // Levels
  protected addLevel(): void {
    this.set((d) => ({
      levels: [
        ...d.levels,
        {
          code: freeCode(
            d.levels.map((l) => l.code),
            'LEVEL',
          ),
          nameAr: 'مستوى جديد',
          nameEn: 'New level',
          slaWorkingDays: null,
        },
      ],
    }));
  }

  protected level(index: number, change: Partial<ServiceLevelDefinition>): void {
    const before = this.def().levels[index].code;
    if (change.code !== undefined) change.code = change.code.trim().toUpperCase();
    this.set((d) => ({
      levels: d.levels.map((l, i) => (i === index ? { ...l, ...change } : l)),
      // Fees name the levels they apply to.
      fees: change.code && change.code !== before ? d.fees.map((f) => ({ ...f, levels: f.levels.map((l) => (l === before ? change.code! : l)) })) : d.fees,
    }));
  }

  protected removeLevel(index: number): void {
    const code = this.def().levels[index].code;
    this.set((d) => ({ levels: d.levels.filter((_, i) => i !== index), fees: d.fees.map((f) => ({ ...f, levels: f.levels.filter((l) => l !== code) })) }));
  }

  protected moveLevel(index: number, by: number): void {
    this.set((d) => ({ levels: move(d.levels, index, index + by) }));
  }

  // Eligibility and validations
  protected addRule(list: 'eligibility' | 'validations'): void {
    this.set((d) => ({
      [list]: [
        ...d[list],
        {
          code: freeCode(
            d[list].map((r) => r.code),
            list === 'eligibility' ? 'ELIGIBLE' : 'CHECK',
          ),
          rule: null,
          messageAr: '',
          messageEn: '',
          step: list === 'validations' ? (this.formSteps()[0]?.code ?? null) : undefined,
        },
      ],
    }));
  }

  protected rule(list: 'eligibility' | 'validations', index: number, change: Partial<RuleDef>): void {
    if (change.code !== undefined) change.code = change.code.trim().toUpperCase();
    this.set((d) => ({ [list]: d[list].map((r, i) => (i === index ? { ...r, ...change } : r)) }));
  }

  protected removeRule(list: 'eligibility' | 'validations', index: number): void {
    this.set((d) => ({ [list]: d[list].filter((_, i) => i !== index) }));
  }

  // Fees
  protected feeKind(fee: FeeDef): FeeKind {
    return fee.bracket ? 'bracket' : fee.formula !== null && fee.formula !== undefined ? 'formula' : 'fixed';
  }

  protected addFee(): void {
    this.set((d) => ({
      fees: [
        ...d.fees,
        {
          code: freeCode(
            d.fees.map((f) => f.code),
            'FEE',
          ),
          nameAr: 'رسوم الخدمة',
          nameEn: 'Service fee',
          levels: [],
          fixed: 0,
          vatRate: 0.15,
          vatIncluded: true,
        },
      ],
    }));
  }

  protected fee(index: number, change: Partial<FeeDef>): void {
    if (change.code !== undefined) change.code = change.code.trim().toUpperCase();
    this.set((d) => ({ fees: d.fees.map((f, i) => (i === index ? { ...f, ...change } : f)) }));
  }

  protected setFeeKind(index: number, kind: FeeKind): void {
    const change: Partial<FeeDef> =
      kind === 'fixed'
        ? { fixed: 0, formula: null, bracket: null }
        : kind === 'formula'
          ? { fixed: null, formula: { var: 'data.' }, bracket: null }
          : { fixed: null, formula: null, bracket: { of: null, rows: [{ from: 0, to: null, amount: 0 }] } };
    this.fee(index, change);
  }

  protected bracketRow(index: number, row: number, change: Partial<{ from: number; to: number | null; amount: number }>): void {
    const fee = this.def().fees[index];
    if (!fee.bracket) return;
    const rows = fee.bracket.rows.map((r, i) => (i === row ? { ...r, ...change } : r));
    this.fee(index, { bracket: { ...fee.bracket, rows } });
  }

  protected addBracketRow(index: number): void {
    const fee = this.def().fees[index];
    if (!fee.bracket) return;
    const last = fee.bracket.rows.at(-1);
    this.fee(index, { bracket: { ...fee.bracket, rows: [...fee.bracket.rows, { from: last?.to ?? 0, to: null, amount: 0 }] } });
  }

  protected removeBracketRow(index: number, row: number): void {
    const fee = this.def().fees[index];
    if (fee.bracket) this.fee(index, { bracket: { ...fee.bracket, rows: fee.bracket.rows.filter((_, i) => i !== row) } });
  }

  protected removeFee(index: number): void {
    this.set((d) => ({ fees: d.fees.filter((_, i) => i !== index) }));
  }

  protected bracketOf(index: number, of: JsonLogicRule | null): void {
    const fee = this.def().fees[index];
    if (fee.bracket) this.fee(index, { bracket: { ...fee.bracket, of } });
  }

  // Availability
  protected availability(change: Partial<AvailabilityDef> | null): void {
    this.set((d) => ({ availability: change === null ? null : { statuses: ['ACTIVE'], ...(d.availability ?? {}), ...change } }));
  }

  protected boundOf(which: 'from' | 'to'): DateBound | null {
    return this.def().availability?.[which] ?? null;
  }

  protected bound(which: 'from' | 'to', change: Partial<DateBound> | null): void {
    const current = this.def().availability?.[which] ?? { of: 'EXPIRES', days: 0 };
    this.availability({ [which]: change === null ? null : { ...current, ...change } });
  }
}
