import { ChangeDetectionStrategy, Component, computed, inject, input, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import {
  I18nService,
  REPORT_FILTER_TYPES,
  ReportAggregate,
  ReportColumnDef,
  ReportDefinition,
  ReportFilterDef,
  ReportTileDef,
  TranslatePipe,
} from '@dynamic-cube/shared';
import { QuerySourceInfo, USER_FACTS } from '../apps-control/lovs/lov-builder-model';
import {
  ConditionSource,
  EndpointCondition,
  EndpointQuery,
  endpointColumns,
  operatorsFor,
} from '../integration/api-endpoint-model';
import {
  DATE_DEFAULTS,
  MAX_FILTERS,
  MAX_TILES,
  ReportPart,
  aggregatesFor,
  columnFor,
  isGrouped,
  newFilter,
  newTile,
  removeColumn,
  renameColumn,
  uniqueName,
} from './report-model';

/** A list of values a list filter may use. */
export interface LovOption {
  code: string;
  nameAr: string;
  nameEn: string;
}

/**
 * The editing parts of the report designer (ADR-0045): the data (source, joins, conditions on values, filters or the
 * user's data), the columns with their totals and sort, the filters users fill and the tiles (charts have their own designer).
 */
@Component({
  selector: 'dc-report-designer-parts',
  imports: [
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatIconModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './report-designer-parts.html',
  styleUrls: ['../integration/api-endpoint-data.scss', './report-designer-parts.scss'],
})
export class ReportDesignerParts {
  private readonly i18n = inject(I18nService);
  readonly part = input.required<ReportPart>();
  readonly def = model.required<ReportDefinition>();
  readonly query = model.required<EndpointQuery>();
  readonly sources = input<QuerySourceInfo[]>([]);
  readonly lovs = input<LovOption[]>([]);

  protected readonly kinds = ['CLASS', 'DATA_OBJECT'] as const;
  protected readonly facts = USER_FACTS;
  protected readonly conditionSources: ConditionSource[] = ['VALUE', 'PARAM', 'USER'];
  protected readonly filterTypes = REPORT_FILTER_TYPES;
  protected readonly dateDefaults = DATE_DEFAULTS;
  protected readonly maxFilters = MAX_FILTERS;
  protected readonly maxTiles = MAX_TILES;

  protected readonly columns = computed(() => endpointColumns(this.query(), this.sources()));
  protected readonly filterNames = computed(() =>
    this.def()
      .filters.map((f) => f.name)
      .filter(Boolean),
  );
  protected readonly groupedBy = computed(() => {
    const def = this.def();
    if (!isGrouped(def.columns)) return '';
    return def.columns
      .filter((c) => !c.aggregate)
      .map((c) => this.label(c))
      .join('، ');
  });

  protected label(c: { labelAr: string; labelEn: string; name: string }): string {
    return (this.i18n.language() === 'ar' ? c.labelAr : c.labelEn) || c.name;
  }

  protected groupedText(): string {
    return this.i18n.translate('rp.columns.grouped').replace('{cols}', this.groupedBy());
  }

  protected sourcesOf(kind: string): QuerySourceInfo[] {
    return this.sources().filter((s) => s.kind === kind);
  }

  protected sourceName(s: QuerySourceInfo): string {
    return `${this.i18n.localize({ ar: s.nameAr, en: s.nameEn })} (${s.code})`;
  }

  protected lovName(l: LovOption): string {
    return `${this.i18n.localize({ ar: l.nameAr, en: l.nameEn })} (${l.code})`;
  }

  protected columnsOf(alias: string) {
    return this.columns().filter((c) => c.ref.startsWith(alias + '.'));
  }

  protected categoryOf(ref: string | null): string | undefined {
    return this.columns().find((c) => c.ref === ref)?.category;
  }

  protected aggregates(ref: string | null): ReportAggregate[] {
    return aggregatesFor(this.categoryOf(ref));
  }

  private setDef(change: Partial<ReportDefinition>): void {
    this.def.update((d) => ({ ...d, ...change }));
  }

  protected set(change: Partial<ReportDefinition>): void {
    this.setDef(change);
  }

  protected setQuery(change: Partial<EndpointQuery>): void {
    this.query.update((q) => ({ ...q, ...change }));
  }

  // ------------------------------------------------------------------ data

  protected setFrom(kind: string, code: string): void {
    const alias = this.query().from.alias || 'a';
    this.query.update((q) => ({
      ...q,
      from: { kind: kind as 'CLASS', code, alias },
      joins: [],
      conditions: [],
      sort: [],
    }));
    this.setDef({
      columns: [],
      sort: [],
      tiles: this.def().tiles.filter((t) => !t.column),
      charts: [],
    });
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
    this.setJoin(index, { on: [{ ...(join.on[0] ?? { left: '', right: '' }), [side]: value }] });
  }

  protected removeJoin(index: number): void {
    this.query.update((q) => ({ ...q, joins: q.joins.filter((_, i) => i !== index) }));
  }

  protected operators(c: EndpointCondition): string[] {
    return operatorsFor(
      this.categoryOf(c.column),
      c.source === 'USER' ? 'USER' : c.source === 'PARAM' ? 'PARAM' : 'VALUE',
    );
  }

  protected addCondition(): void {
    const condition: EndpointCondition = {
      column: '',
      op: 'EQ',
      source: this.filterNames().length ? 'PARAM' : 'VALUE',
      value: '',
      param: this.filterNames()[0] ?? '',
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
        if (!this.operators(next).includes(next.op)) next.op = 'EQ';
        return next;
      }),
    }));
  }

  protected removeCondition(index: number): void {
    this.query.update((q) => ({ ...q, conditions: q.conditions.filter((_, i) => i !== index) }));
  }

  // ------------------------------------------------------------------ columns and sort

  protected addColumn(ref = '', aggregate: ReportAggregate | null = null): void {
    const columns = this.def().columns;
    const column: ReportColumnDef = ref
      ? columnFor(ref, this.columns(), columns, aggregate)
      : { name: '', labelAr: '', labelEn: '', column: '', aggregate: null };
    this.setDef({ columns: [...columns, column] });
  }

  /** A column counting the rows (of the source's first column). */
  protected addCount(): void {
    const first = this.columnsOf(this.query().from.alias)[0];
    if (!first) return;
    const columns = this.def().columns;
    const column = columnFor(first.ref, this.columns(), columns, 'COUNT');
    this.setDef({
      columns: [
        ...columns,
        {
          ...column,
          name: uniqueName(
            'count',
            columns.map((c) => c.name),
          ),
          labelAr: 'العدد',
          labelEn: 'Count',
        },
      ],
    });
  }

  protected setColumn(index: number, change: Partial<ReportColumnDef>): void {
    const before = this.def().columns[index];
    this.def.update((d) => {
      let columns = d.columns.map((c, i) => (i === index ? { ...c, ...change } : c));
      // A source column chosen for an unnamed column names and labels it; an aggregate that no longer fits is cleared.
      if (change.column !== undefined) {
        const fresh = columnFor(
          change.column,
          this.columns(),
          d.columns.filter((_, i) => i !== index),
          columns[index].aggregate,
        );
        const current = columns[index];
        columns[index] = {
          ...current,
          name: current.name || fresh.name,
          labelAr: current.labelAr || fresh.labelAr,
          labelEn: current.labelEn || fresh.labelEn,
          aggregate:
            current.aggregate &&
            aggregatesFor(this.categoryOf(change.column)).includes(current.aggregate)
              ? current.aggregate
              : null,
        };
      }
      columns = columns.map((c) => ({ ...c }));
      const next = { ...d, columns };
      return change.name !== undefined ? renameColumn(next, before.name, change.name) : next;
    });
  }

  protected moveColumn(index: number, by: number): void {
    const columns = [...this.def().columns];
    const to = index + by;
    if (to < 0 || to >= columns.length) return;
    [columns[index], columns[to]] = [columns[to], columns[index]];
    this.setDef({ columns });
  }

  protected deleteColumn(index: number): void {
    this.def.update((d) => removeColumn(d, index));
  }

  protected addSort(): void {
    this.setDef({
      sort: [...this.def().sort, { column: this.def().columns[0]?.name ?? '', descending: false }],
    });
  }

  protected setSort(index: number, change: Partial<ReportDefinition['sort'][number]>): void {
    this.setDef({ sort: this.def().sort.map((s, i) => (i === index ? { ...s, ...change } : s)) });
  }

  protected removeSort(index: number): void {
    this.setDef({ sort: this.def().sort.filter((_, i) => i !== index) });
  }

  // ------------------------------------------------------------------ filters

  protected addFilter(): void {
    this.setDef({ filters: [...this.def().filters, newFilter(this.def().filters)] });
  }

  protected setFilter(index: number, change: Partial<ReportFilterDef>): void {
    const before = this.def().filters[index];
    this.setDef({
      filters: this.def().filters.map((f, i) => {
        if (i !== index) return f;
        const next = { ...f, ...change };
        if (change.type && change.type !== before.type) {
          next.default = null;
          if (change.type !== 'LIST') next.multiple = false;
        }
        return next;
      }),
    });
    // Renaming a filter renames it in the conditions that compare with it.
    if (change.name !== undefined && change.name !== before.name) {
      this.query.update((q) => ({
        ...q,
        conditions: q.conditions.map((c) =>
          c.source === 'PARAM' && c.param === before.name ? { ...c, param: change.name! } : c,
        ),
      }));
    }
  }

  protected moveFilter(index: number, by: number): void {
    const filters = [...this.def().filters];
    const to = index + by;
    if (to < 0 || to >= filters.length) return;
    [filters[index], filters[to]] = [filters[to], filters[index]];
    this.setDef({ filters });
  }

  protected removeFilter(index: number): void {
    this.setDef({ filters: this.def().filters.filter((_, i) => i !== index) });
  }

  // ------------------------------------------------------------------ tiles

  protected addTile(): void {
    this.setDef({ tiles: [...this.def().tiles, newTile(this.def().tiles)] });
  }

  protected setTile(index: number, change: Partial<ReportTileDef>): void {
    this.setDef({
      tiles: this.def().tiles.map((t, i) => {
        if (i !== index) return t;
        const next = { ...t, ...change };
        if (!next.column) next.aggregate = 'COUNT';
        else if (!aggregatesFor(this.categoryOf(next.column)).includes(next.aggregate))
          next.aggregate = 'COUNT';
        return next;
      }),
    });
  }

  protected removeTile(index: number): void {
    this.setDef({ tiles: this.def().tiles.filter((_, i) => i !== index) });
  }
}
