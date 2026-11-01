import { ReportDefinition } from '@dynamic-cube/shared';
import { QuerySourceInfo } from '../apps-control/lovs/lov-builder-model';
import {
  EndpointQuery,
  emptyEndpointQuery,
  endpointColumns,
} from '../integration/api-endpoint-model';
import {
  aggregatesFor,
  checkReport,
  cleanFilters,
  columnFor,
  defaultFits,
  emptyReport,
  formatValue,
  joinValues,
  niceScale,
  normalizeReport,
  numberColumns,
  pieSlices,
  removeColumn,
  renameColumn,
  reportPayload,
  splitValues,
  uniqueName,
} from './report-model';

const REQUESTS: QuerySourceInfo = {
  kind: 'DATA_OBJECT',
  code: 'REQUESTS',
  nameAr: 'الطلبات',
  nameEn: 'Requests',
  schema: 'svc',
  dbObject: 'requests',
  columns: [
    {
      name: 'service_code',
      dataType: 'text',
      category: 'TEXT',
      labelAr: 'الخدمة',
      labelEn: 'Service',
    },
    { name: 'status', dataType: 'text', category: 'TEXT', labelAr: null, labelEn: null },
    { name: 'fee', dataType: 'numeric', category: 'NUMBER', labelAr: 'الرسوم', labelEn: 'Fee' },
    {
      name: 'submitted_at',
      dataType: 'timestamptz',
      category: 'DATE',
      labelAr: null,
      labelEn: null,
    },
    { name: 'is_urgent', dataType: 'boolean', category: 'BOOL', labelAr: null, labelEn: null },
  ],
};

function query(): EndpointQuery {
  return { ...emptyEndpointQuery(), from: { kind: 'DATA_OBJECT', code: 'REQUESTS', alias: 'r' } };
}

function report(change: Partial<ReportDefinition> = {}): ReportDefinition {
  return {
    ...emptyReport(),
    nameAr: 'الطلبات حسب الخدمة',
    nameEn: 'Requests by service',
    columns: [
      {
        name: 'service',
        labelAr: 'الخدمة',
        labelEn: 'Service',
        column: 'r.service_code',
        aggregate: null,
      },
      {
        name: 'requests',
        labelAr: 'العدد',
        labelEn: 'Requests',
        column: 'r.status',
        aggregate: 'COUNT',
      },
      { name: 'fees', labelAr: 'الرسوم', labelEn: 'Fees', column: 'r.fee', aggregate: 'SUM' },
    ],
    ...change,
  };
}

describe('report model', () => {
  const sources = [REQUESTS];
  const columns = endpointColumns(query(), sources);

  it('offers only the totals that fit a column', () => {
    expect(aggregatesFor('NUMBER')).toEqual([
      'COUNT',
      'COUNT_DISTINCT',
      'SUM',
      'AVG',
      'MIN',
      'MAX',
    ]);
    expect(aggregatesFor('TEXT')).toEqual(['COUNT', 'COUNT_DISTINCT', 'MIN', 'MAX']);
    expect(aggregatesFor('BOOL')).toEqual(['COUNT', 'COUNT_DISTINCT']);
    expect(aggregatesFor('JSON')).toEqual(['COUNT', 'COUNT_DISTINCT']);
  });

  it('names and labels a column after its source column', () => {
    const one = columnFor('r.service_code', columns, []);
    expect(one).toEqual({
      name: 'serviceCode',
      labelAr: 'الخدمة',
      labelEn: 'Service',
      column: 'r.service_code',
      aggregate: null,
    });
    expect(columnFor('r.service_code', columns, [one]).name).toBe('serviceCode2');
    expect(columnFor('r.fee', columns, [], 'SUM').name).toBe('feeSum');
    expect(columnFor('r.status', columns, []).labelEn).toBe('status');
    expect(uniqueName('9lives', [])).toBe('c9lives');
  });

  it('finds the number columns a chart can draw', () => {
    expect(numberColumns(report(), columns).map((c) => c.name)).toEqual(['requests', 'fees']);
    const minDate = report({
      columns: [
        { name: 'first', labelAr: 'أ', labelEn: 'F', column: 'r.submitted_at', aggregate: 'MIN' },
      ],
    });
    expect(numberColumns(minDate, columns)).toEqual([]);
  });

  it('accepts a complete report and reports what is missing', () => {
    expect(checkReport('REQ_BY_SERVICE', report(), query(), true, sources)).toEqual([]);

    const bad = report({
      nameEn: '',
      columns: [
        {
          name: 'service',
          labelAr: 'الخدمة',
          labelEn: '',
          column: 'r.service_code',
          aggregate: null,
        },
        { name: 'service', labelAr: 'x', labelEn: 'x', column: 'r.is_urgent', aggregate: 'MAX' },
      ],
      sort: [{ column: 'gone', descending: false }],
      filters: [
        {
          name: 'svc',
          labelAr: 'خ',
          labelEn: 'S',
          type: 'LIST',
          lov: null,
          multiple: true,
          required: false,
          default: null,
        },
      ],
      chart: { type: 'PIE', category: 'missing', values: ['service', 'other'] },
      pageSize: 5,
    });
    const q = {
      ...query(),
      conditions: [
        {
          column: 'r.status',
          op: 'EQ',
          source: 'PARAM' as const,
          value: '',
          param: 'nope',
          user: '',
        },
      ],
    };
    const keys = checkReport('bad code', bad, q, true, sources).map((p) => `${p.part}:${p.key}`);
    expect(keys).toEqual([
      'details:rp.problem.code',
      'details:rp.problem.names',
      'data:rp.problem.conditionFilter',
      'columns:rp.problem.columnLabels',
      'columns:rp.problem.columnTwice',
      'columns:rp.problem.aggregate',
      'columns:rp.problem.sort',
      'columns:rp.problem.pageSize',
      'filters:rp.problem.filterLov',
      'chart:rp.problem.chartCategory',
      'chart:rp.problem.chartValues',
      'chart:rp.problem.pieOne',
    ]);
    expect(
      checkReport('X', report({ columns: [] }), emptyEndpointQuery(), false, sources).map(
        (p) => p.key,
      ),
    ).toEqual(['rp.problem.source', 'rp.problem.columns']);
  });

  it('checks filter defaults by kind, relative dates included', () => {
    const f = (type: 'DATE' | 'NUMBER' | 'YES_NO', value: string) =>
      defaultFits({
        name: 'a',
        labelAr: 'a',
        labelEn: 'a',
        type,
        lov: null,
        multiple: false,
        required: false,
        default: value,
      });
    expect(f('DATE', 'TODAY-30')).toBe(true);
    expect(f('DATE', 'month_start')).toBe(true);
    expect(f('DATE', '2026-01-31')).toBe(true);
    expect(f('DATE', 'yesterday')).toBe(false);
    expect(f('NUMBER', '12.5')).toBe(true);
    expect(f('NUMBER', 'twelve')).toBe(false);
    expect(f('YES_NO', 'true')).toBe(true);
    expect(f('YES_NO', 'yes')).toBe(false);
  });

  it('renames and removes a column where the sort and the chart refer to it', () => {
    const def = report({
      sort: [{ column: 'fees', descending: true }],
      chart: { type: 'BAR', category: 'service', values: ['requests', 'fees'] },
    });
    const renamed = renameColumn(def, 'fees', 'amount');
    expect(renamed.sort[0].column).toBe('amount');
    expect(renamed.chart!.values).toEqual(['requests', 'amount']);
    const removed = removeColumn(def, 2);
    expect(removed.columns.map((c) => c.name)).toEqual(['service', 'requests']);
    expect(removed.sort).toEqual([]);
    expect(removed.chart!.values).toEqual(['requests']);
    expect(removeColumn(def, 0).chart!.category).toBe('');
  });

  it('builds the payload from the builder and reads a stored row back', () => {
    const payload = reportPayload(
      report({
        descriptionEn: '  ',
        filters: [
          {
            name: ' from ',
            labelAr: 'من',
            labelEn: 'From',
            type: 'DATE',
            lov: 'X',
            multiple: true,
            required: false,
            default: ' TODAY-30 ',
          },
        ],
        chart: { type: 'BAR', category: '', values: [] },
      }),
      { ...query(), sort: [{ column: 'r.fee', direction: 'ASC' }] },
    );
    expect(payload.descriptionEn).toBeNull();
    expect(payload.filters[0]).toEqual({
      name: 'from',
      labelAr: 'من',
      labelEn: 'From',
      type: 'DATE',
      lov: null,
      multiple: false,
      required: false,
      default: 'TODAY-30',
    });
    expect(payload.chart).toBeNull();
    expect(payload.query!['from']).toEqual({ kind: 'DATA_OBJECT', code: 'REQUESTS', alias: 'r' });
    expect(payload.query!['sort']).toEqual([]);

    const stored = normalizeReport({
      ...payload,
      query: JSON.stringify(payload.query),
      columns: [{ ...payload.columns[0], aggregate: '' }],
    });
    expect(stored.query!['from']).toEqual({ kind: 'DATA_OBJECT', code: 'REQUESTS', alias: 'r' });
    expect(stored.columns[0].aggregate).toBeNull();
  });

  it('formats cells by kind', () => {
    expect(formatValue(1234.567, 'NUMBER', 'en')).toBe('1,234.57');
    expect(formatValue('2026-03-05T00:00:00+03:00', 'DATE', 'en')).toBe('2026-03-05');
    expect(formatValue('2026-03-05T09:30:00Z', 'DATE', 'en')).toBe('2026-03-05 09:30');
    expect(formatValue(true, 'BOOLEAN', 'ar')).toBe('نعم');
    expect(formatValue(null, 'TEXT', 'en')).toBe('');
    expect(formatValue({ a: 1 }, 'JSON', 'en')).toBe('{"a":1}');
  });

  it('draws round axes and pies', () => {
    expect(niceScale(0)).toEqual({ max: 1, ticks: [0, 1] });
    expect(niceScale(87)).toEqual({ max: 100, ticks: [0, 25, 50, 75, 100] });
    expect(niceScale(7).max).toBe(8);
    const slices = pieSlices([1, 1, 2]);
    expect(slices.map((s) => s.share)).toEqual([0.25, 0.25, 0.5]);
    expect(slices[0].path).toMatch(/^M 80 80 L 80 0 A 80 80 0 0 1 160 80 Z$/);
    expect(pieSlices([5])[0].path).toContain('A 80 80 0 1 1');
    expect(pieSlices([0, 0]).every((s) => s.path === '')).toBe(true);
  });

  it('joins several list values and drops empty filters', () => {
    expect(joinValues([' A ', '', 'B'])).toBe('A,B');
    expect(joinValues([])).toBeNull();
    expect(splitValues('A, B,,')).toEqual(['A', 'B']);
    expect(cleanFilters({ a: ' x ', b: '', c: null })).toEqual({ a: 'x' });
  });
});
