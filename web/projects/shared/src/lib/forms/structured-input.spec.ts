import { jsonText, lookupColumns, mapRows, parseLoose, rowsMap, withColumnOptions } from './structured-input';

describe('structured input', () => {
  it('reads a value cell as JSON only when it looks like JSON', () => {
    expect(parseLoose('{"var": "task.assignee"}')).toEqual({ var: 'task.assignee' });
    expect(parseLoose('CLS_MANAGER')).toBe('CLS_MANAGER');
    expect(parseLoose('42')).toBe(42);
    expect(parseLoose(' ')).toBeNull();
    expect(jsonText(null)).toBe('');
  });

  it('edits a map as rows and back, dropping rows without a name', () => {
    const rows = mapRows({ REGION: 'RIYADH', LEVEL: 2 });
    expect(rows).toEqual([
      { key: 'REGION', value: 'RIYADH' },
      { key: 'LEVEL', value: '2' },
    ]);
    expect(rowsMap([...rows, { key: ' ', value: 'x' }])).toEqual({ REGION: 'RIYADH', LEVEL: 2 });
  });

  it('fills the options of lookup columns, nested ones included', () => {
    const shape = {
      kind: 'table' as const,
      columns: [
        { key: 'unit', label: { ar: 'و', en: 'Unit' }, type: 'select' as const, ref: 'apps.org-unit' },
        {
          key: 'items',
          label: { ar: 'ب', en: 'Items' },
          type: 'table' as const,
          columns: [{ key: 'kind', label: { ar: 'ن', en: 'Kind' }, type: 'select' as const, refDomains: 'K' }],
        },
      ],
    };
    expect(lookupColumns(shape).map((c) => c.key)).toEqual(['unit', 'kind']);
    const filled = withColumnOptions(shape, (c) => [{ value: c.ref ?? c.refDomains!, label: { ar: '', en: '' } }]);
    expect(filled.kind === 'table' && filled.columns[0].options?.[0].value).toBe('apps.org-unit');
    expect(filled.kind === 'table' && filled.columns[1].columns?.[0].options?.[0].value).toBe('K');
  });
});
