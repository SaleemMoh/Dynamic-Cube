import {
  Matrix,
  cellInputs,
  cellOf,
  cellSummary,
  emptyMatrix,
  freeCode,
  inputFields,
  newCell,
  readMatrix,
  removeRow,
  renameColumn,
  renameRow,
  resolveCell,
  sourceCounts,
  trackFields,
  varsOf,
  withCell,
} from './matrix-model';

function sample(): Matrix {
  const m = emptyMatrix();
  m.rows = [
    { code: 'BUILD', nameAr: 'المباني', nameEn: 'Buildings', active: true, final: 'worst' },
    { code: 'ROADS', nameAr: 'الطرق', nameEn: 'Roads', active: true, final: 'worst' },
  ];
  m.optionSets = [
    {
      code: 'yesno',
      nameAr: '',
      nameEn: '',
      items: [{ code: 'yes', labelAr: 'نعم', labelEn: 'Yes', ordinal: 1 }],
    },
  ];
  m.inputs = [
    {
      code: 'engineers',
      nameAr: 'المهندسون',
      nameEn: 'Engineers',
      type: 'number',
      source: { kind: 'applicant', question: 'ENGINEERS' },
    },
    {
      code: 'rating',
      nameAr: 'التقييم',
      nameEn: 'Rating',
      type: 'option',
      optionSet: 'yesno',
      source: { kind: 'reviewer', step: 'REVIEW', field: 'rating' },
    },
    {
      code: 'revenue',
      nameAr: 'الإيرادات',
      nameEn: 'Revenue',
      type: 'number',
      source: { kind: 'integration', operation: 'ZATCA', output: 'revenue' },
    },
    {
      code: 'ratio',
      nameAr: 'النسبة',
      nameEn: 'Ratio',
      type: 'number',
      source: { kind: 'derived', rule: { '/': [{ var: 'revenue' }, 2] } },
    },
  ];
  m.cells = [
    {
      row: 'BUILD',
      column: 'technical',
      kind: 'points',
      scale: 'S1',
      groups: [
        {
          code: 'main',
          nameAr: '',
          nameEn: '',
          mandatory: false,
          criteria: [
            {
              code: 'c1',
              nameAr: '',
              nameEn: '',
              input: 'engineers',
              scoring: 'bands',
              output: 'points',
              bands: [],
              options: [],
              appliesWhen: { '==': [{ var: 'rating' }, 'yes'] },
            },
            {
              code: 'c2',
              nameAr: '',
              nameEn: '',
              input: 'ratio',
              scoring: 'direct',
              output: 'points',
              bands: [],
              options: [],
            },
          ],
        },
      ],
    },
    { row: 'ROADS', column: 'technical', kind: 'same', groups: [], sameRow: 'BUILD' },
  ];
  return m;
}

describe('matrix model', () => {
  it('reads a saved configuration with every list present', () => {
    const m = readMatrix({
      rows: [{ code: 'A' }],
      inputs: [{ code: 'x' }],
      cells: [{ row: 'A', column: 'technical' }],
    });
    expect(m.grades.length).toBe(5);
    expect(m.inputs[0].source.kind).toBe('applicant');
    expect(m.cells[0].groups).toEqual([]);
    expect(m.gates).toEqual([]);
  });

  it('finds cells ignoring case and follows "same as" cells', () => {
    const m = sample();
    expect(cellOf(m, 'build', 'TECHNICAL')?.kind).toBe('points');
    expect(resolveCell(m, cellOf(m, 'ROADS', 'technical'))?.row).toBe('BUILD');
    expect(
      resolveCell(m, {
        row: 'X',
        column: 'technical',
        kind: 'same',
        groups: [],
        sameRow: 'MISSING',
      }),
    ).toBeNull();
  });

  it("lists a cell's inputs from its criteria and rules, and counts them by source", () => {
    const m = sample();
    const codes = cellInputs(m, cellOf(m, 'ROADS', 'technical')).map((i) => i.code);
    expect(codes.sort()).toEqual(['engineers', 'rating', 'ratio']);
    expect(sourceCounts(cellInputs(m, cellOf(m, 'BUILD', 'technical')))).toEqual([
      { kind: 'applicant', count: 1 },
      { kind: 'reviewer', count: 1 },
      { kind: 'derived', count: 1 },
    ]);
  });

  it('reads the variables of a rule', () => {
    expect([...varsOf({ and: [{ '>': [{ var: 'a.b' }, 1] }, { var: ['c', 0] }] })]).toEqual([
      'a',
      'c',
    ]);
  });

  it('summarises cells', () => {
    const m = sample();
    const t = (k: string) => k;
    expect(cellSummary(m, cellOf(m, 'BUILD', 'technical'), t)).toBe('2 mx.cell.criteria · S1');
    expect(cellSummary(m, cellOf(m, 'ROADS', 'technical'), t)).toBe('mx.cell.sameAs المباني');
    expect(cellSummary(m, null, t)).toBe('mx.cell.none');
  });

  it('offers the inputs as Logic Builder fields with their answers', () => {
    const fields = inputFields(sample(), 'ratio');
    expect(fields.map((f) => f.path)).toEqual(['engineers', 'rating', 'revenue']);
    expect(fields[1].options).toEqual([{ value: 'yes', label: { ar: 'نعم', en: 'Yes' } }]);
    expect(trackFields(sample()).some((f) => f.path === 'columns.technical.grade')).toBe(true);
  });

  it('renames and removes rows and columns everywhere', () => {
    let m = renameRow(sample(), 'BUILD', 'BLD');
    expect(m.cells.map((c) => c.row)).toEqual(['BLD', 'ROADS']);
    expect(m.cells[1].sameRow).toBe('BLD');
    m = renameColumn(m, 'technical', 'tech');
    expect(m.cells.every((c) => c.column === 'tech')).toBe(true);
    m = removeRow(m, 'BLD');
    expect(m.rows.map((r) => r.code)).toEqual(['ROADS']);
    expect(m.cells).toEqual([
      expect.objectContaining({ row: 'ROADS', kind: 'empty', sameRow: null }),
    ]);
  });

  it('replaces a cell, and an empty cell removes it', () => {
    const m = sample();
    const replaced = withCell(
      m,
      newCell('BUILD', 'technical', 'formula', cellOf(m, 'BUILD', 'technical')),
    );
    expect(replaced.cells.length).toBe(2);
    expect(cellOf(replaced, 'BUILD', 'technical')?.kind).toBe('formula');
    expect(withCell(m, newCell('BUILD', 'technical', 'empty')).cells.length).toBe(1);
  });

  it('finds a free code', () => {
    expect(freeCode(['ROW1', 'ROW2'], 'ROW')).toBe('ROW3');
    expect(freeCode(['row3'], 'ROW')).toBe('ROW2');
  });
});
