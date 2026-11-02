import { placeholderPath, stripLiquidTags, tableColumns, textPiece } from './print-import';

describe('importing a template written as HTML', () => {
  it('keeps placeholders and drops Liquid tags', () => {
    expect(
      stripLiquidTags('<tr>{% for line in lines %}<td>{{ line.sector }}</td>{%- endfor -%}</tr>'),
    ).toBe('<tr><td>{{ line.sector }}</td></tr>');
  });

  it('reads a placeholder path without its filters', () => {
    expect(placeholderPath(' document.issuedOn | date: "%Y-%m-%d" ')).toBe('document.issuedOn');
    expect(placeholderPath('"text" | upcase')).toBeNull();
  });

  it('turns text with a placeholder into a field with its label', () => {
    expect(textPiece('  رقم الشهادة   {{ document.number }} ')).toEqual({
      kind: 'field',
      field: 'document.number',
      text: 'رقم الشهادة',
    });
    expect(textPiece('Sample document')).toEqual({ kind: 'text', text: 'Sample document' });
  });

  it('reads the lines table columns from its heading and first row', () => {
    const doc = new DOMParser().parseFromString(
      '<table><thead><tr><th>المجال</th><th>الدرجة</th></tr></thead><tbody><tr><td>{{ line.sector }}</td><td>{{ line.grade }}</td></tr></tbody></table>',
      'text/html',
    );
    expect(tableColumns(doc.querySelector('table')!)).toEqual([
      { field: 'sector', title: 'المجال' },
      { field: 'grade', title: 'الدرجة' },
    ]);
  });
});

describe('a lines table without thead', () => {
  it('takes its titles from the first row of th cells', () => {
    const doc = new DOMParser().parseFromString(
      '<table><tr><th>المجال</th><th>Domain</th></tr><tr><td>{{ line.sectorNameAr }}</td><td>{{ line.sectorNameEn }}</td></tr></table>',
      'text/html',
    );
    expect(tableColumns(doc.querySelector('table')!)).toEqual([
      { field: 'sectorNameAr', title: 'المجال' },
      { field: 'sectorNameEn', title: 'Domain' },
    ]);
  });
});
