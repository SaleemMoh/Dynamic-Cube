import { JsonPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import {
  ApiEndpointDefinition,
  ApiEndpointField,
  ApiFunctionBinding,
  ApiFunctionInfo,
  I18nService,
  LocalizePipe,
  TranslatePipe,
} from '@dynamic-cube/shared';
import { QuerySourceInfo } from '../apps-control/lovs/lov-builder-model';
import {
  CLIENT_FACTS,
  ConditionSource,
  EndpointCondition,
  EndpointQuery,
  EndpointSourceKind,
  bindInputs,
  classDefaults,
  endpointColumns,
  fieldFor,
  operatorsFor,
} from './api-endpoint-model';

/**
 * What a public endpoint returns (ADR-0042): rows of a class or registered table — joins, the fields returned, conditions
 * on values, inputs or the calling client, sort and paging — or a system function with each input given a parameter or
 * a fixed value.
 */
@Component({
  selector: 'dc-api-endpoint-data',
  imports: [
    FormsModule,
    JsonPipe,
    LocalizePipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatIconModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './api-endpoint-data.html',
  styleUrl: './api-endpoint-data.scss',
})
export class ApiEndpointData {
  private readonly i18n = inject(I18nService);
  readonly def = model.required<ApiEndpointDefinition>();
  readonly query = model.required<EndpointQuery>();
  readonly sources = input<QuerySourceInfo[]>([]);
  readonly functions = input<ApiFunctionInfo[]>([]);

  protected readonly kinds: EndpointSourceKind[] = ['CLASS', 'DATA_OBJECT'];
  protected readonly facts = CLIENT_FACTS;
  protected readonly conditionSources: ConditionSource[] = ['VALUE', 'PARAM', 'USER'];

  protected readonly columns = computed(() => endpointColumns(this.query(), this.sources()));
  protected readonly params = computed(() => this.def().parameters.map((p) => p.name));
  protected readonly fn = computed(() =>
    this.functions().find((f) => f.code === this.def().function),
  );

  protected sourcesOf(kind: string): QuerySourceInfo[] {
    return this.sources().filter((s) => s.kind === kind);
  }

  protected sourceName(s: QuerySourceInfo): string {
    return `${this.i18n.localize({ ar: s.nameAr, en: s.nameEn })} (${s.code})`;
  }

  protected columnsOf(alias: string) {
    return this.columns().filter((c) => c.ref.startsWith(alias + '.'));
  }

  protected categoryOf(ref: string): string | undefined {
    return this.columns().find((c) => c.ref === ref)?.category;
  }

  protected operators(c: EndpointCondition): string[] {
    return operatorsFor(this.categoryOf(c.column), c.source);
  }

  protected setDef(change: Partial<ApiEndpointDefinition>): void {
    this.def.update((d) => ({ ...d, ...change }));
  }

  protected setQuery(change: Partial<EndpointQuery>): void {
    this.query.update((q) => ({ ...q, ...change }));
  }

  // ------------------------------------------------------------------ kind and source

  protected setKind(kind: 'QUERY' | 'FUNCTION'): void {
    this.def.update((d) => ({ ...d, kind }));
  }

  protected setFrom(kind: EndpointSourceKind | 'TABLE', code: string): void {
    const alias = this.query().from.alias || 'a';
    this.query.update((q) => ({
      ...q,
      from: { kind, code, alias },
      joins: [],
      conditions: [],
      sort: [],
    }));
    // A class starts with its usual fields; a table with none chosen yet.
    if (kind === 'CLASS' && code) {
      const defaults = classDefaults(alias);
      this.def.update((d) => ({ ...d, fields: defaults.fields }));
      this.query.update((q) => ({ ...q, sort: defaults.sort }));
    } else {
      this.def.update((d) => ({ ...d, fields: [] }));
    }
  }

  protected useClassDefaults(): void {
    const defaults = classDefaults(this.query().from.alias);
    this.def.update((d) => ({ ...d, fields: defaults.fields }));
  }

  protected addJoin(): void {
    const used = new Set([this.query().from.alias, ...this.query().joins.map((j) => j.alias)]);
    let alias = 'b';
    for (let i = 98; used.has(alias); i++) alias = String.fromCharCode(i + 1);
    this.query.update((q) => ({
      ...q,
      joins: [
        ...q.joins,
        { kind: 'CLASS', code: '', alias, type: 'LEFT', on: [{ left: '', right: '' }] },
      ],
    }));
  }

  protected setJoin(index: number, change: Record<string, unknown>): void {
    this.query.update((q) => ({
      ...q,
      joins: q.joins.map((j, i) => (i === index ? { ...j, ...change } : j)),
    }));
  }

  protected setJoinOn(index: number, side: 'left' | 'right', value: string): void {
    const join = this.query().joins[index];
    const on = { ...(join.on[0] ?? { left: '', right: '' }), [side]: value };
    this.setJoin(index, { on: [on] });
  }

  protected removeJoin(index: number): void {
    this.query.update((q) => ({ ...q, joins: q.joins.filter((_, i) => i !== index) }));
  }

  // ------------------------------------------------------------------ fields

  protected addField(column = ''): void {
    const fields = this.def().fields;
    const field: ApiEndpointField = column
      ? fieldFor(column, fields)
      : { name: '', column: '', descriptionAr: null, descriptionEn: null };
    this.def.update((d) => ({ ...d, fields: [...d.fields, field] }));
  }

  protected setField(index: number, change: Partial<ApiEndpointField>): void {
    this.def.update((d) => {
      const fields = d.fields.map((f, i) => (i === index ? { ...f, ...change } : f));
      // A column chosen for a field without a name names it.
      if (change.column && !d.fields[index].name) {
        fields[index] = { ...fields[index], name: fieldFor(change.column, d.fields).name };
      }
      return { ...d, fields };
    });
  }

  protected removeField(index: number): void {
    this.def.update((d) => ({ ...d, fields: d.fields.filter((_, i) => i !== index) }));
  }

  // ------------------------------------------------------------------ conditions and sort

  protected addCondition(): void {
    const condition: EndpointCondition = {
      column: '',
      op: 'EQ',
      source: this.params().length ? 'PARAM' : 'VALUE',
      value: '',
      param: this.params()[0] ?? '',
      user: 'COMPANY',
    };
    this.query.update((q) => ({ ...q, conditions: [...q.conditions, condition] }));
  }

  protected setCondition(index: number, change: Partial<EndpointCondition>): void {
    this.query.update((q) => ({
      ...q,
      conditions: q.conditions.map((c, i) => {
        if (i !== index) return c;
        const next = { ...c, ...change };
        if (!operatorsFor(this.categoryOf(next.column), next.source).includes(next.op))
          next.op = 'EQ';
        return next;
      }),
    }));
  }

  protected removeCondition(index: number): void {
    this.query.update((q) => ({ ...q, conditions: q.conditions.filter((_, i) => i !== index) }));
  }

  protected addSort(): void {
    this.query.update((q) => ({ ...q, sort: [...q.sort, { column: '', direction: 'ASC' }] }));
  }

  protected setSort(index: number, change: Partial<EndpointQuery['sort'][number]>): void {
    this.query.update((q) => ({
      ...q,
      sort: q.sort.map((s, i) => (i === index ? { ...s, ...change } : s)),
    }));
  }

  protected removeSort(index: number): void {
    this.query.update((q) => ({ ...q, sort: q.sort.filter((_, i) => i !== index) }));
  }

  // ------------------------------------------------------------------ function

  protected setFunction(code: string): void {
    const fn = this.functions().find((f) => f.code === code);
    this.def.update((d) => ({
      ...d,
      function: code,
      functionInputs: bindInputs(fn, [], d.parameters),
    }));
  }

  protected binding(input: string): ApiFunctionBinding {
    return (
      this.def().functionInputs.find((b) => b.input === input) ?? {
        input,
        parameter: null,
        value: null,
      }
    );
  }

  /** The source of an input: one of the parameters, or "" for a fixed value. */
  protected setBinding(input: string, change: Partial<ApiFunctionBinding>): void {
    this.def.update((d) => {
      const rest = d.functionInputs.filter((b) => b.input !== input);
      const next = { ...this.binding(input), ...change };
      return { ...d, functionInputs: [...rest, next] };
    });
  }
}
