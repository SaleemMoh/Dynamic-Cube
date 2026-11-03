import {
  Matrix,
  MatrixCell,
  bankWeightsChanged,
  cellInputs,
  cellOf,
  cellSummary,
  detachCell,
  dropUnusedShared,
  emptyMatrix,
  identicalCells,
  pasteCell,
  resolveCell,
  shareCells,
  shareIdentical,
  sharedUsers,
  syncFromBank,
  withShared,
} from './matrix-model';

function governance(row: string, points = 10): MatrixCell {
  return {
    row,
    column: 'governance',
    kind: 'points',
    scale: 'S',
    groups: [
      {
        code: 'g',
        nameAr: '',
        nameEn: '',
        mandatory: false,
        criteria: [
          {
            code: 'board',
            nameAr: '',
            nameEn: '',
            input: 'board',
            scoring: 'options',
            output: 'points',
            bands: [],
            options: [{ value: 'yes', points }],
          },
        ],
      },
    ],
  };
}

function sample(): Matrix {
  const m = emptyMatrix();
  m.rows = ['A', 'B', 'C'].map((code) => ({
    code,
    nameAr: code,
    nameEn: code,
    active: true,
    final: 'worst' as const,
  }));
  m.inputs = [
    {
      code: 'board',
      nameAr: '',
      nameEn: '',
      type: 'option',
      source: { kind: 'applicant', question: 'BOARD' },
    },
  ];
  m.cells = [governance('A'), governance('B'), governance('C', 5)];
  return m;
}

describe('shared calculations', () => {
  it('finds identical cells in a track and links them to one shared calculation', () => {
    const m = sample();
    expect(identicalCells(m, 'governance')).toEqual([['A', 'B']]);
    const shared = shareIdentical(m, 'governance');
    expect(shared.shared?.length).toBe(1);
    expect(sharedUsers(shared, shared.shared![0].code).map((c) => c.row)).toEqual(['A', 'B']);
    expect(cellOf(shared, 'C', 'governance')?.kind).toBe('points');
    expect(resolveCell(shared, cellOf(shared, 'A', 'governance'))?.groups[0].criteria[0].code).toBe(
      'board',
    );
    expect(cellInputs(shared, cellOf(shared, 'B', 'governance')).map((i) => i.code)).toEqual([
      'board',
    ]);
    expect(cellSummary(shared, cellOf(shared, 'A', 'governance'), (k) => k)).toContain(
      '1 mx.cell.criteria',
    );
  });

  it('changes every linked cell at once, and a detached cell keeps its own copy', () => {
    let m = shareCells(sample(), 'A', 'governance', 'gov', ['B', 'C']);
    expect(m.cells.every((c) => c.kind === 'shared' && c.shared === 'gov')).toBe(true);
    m = withShared(m, 'gov', governance('', 20));
    expect(
      resolveCell(m, cellOf(m, 'C', 'governance'))?.groups[0].criteria[0].options[0].points,
    ).toBe(20);
    m = detachCell(m, 'C', 'governance');
    expect(cellOf(m, 'C', 'governance')?.kind).toBe('points');
    m = withShared(m, 'gov', governance('', 30));
    expect(cellOf(m, 'C', 'governance')?.groups[0].criteria[0].options[0].points).toBe(20);
    expect(
      resolveCell(m, cellOf(m, 'A', 'governance'))?.groups[0].criteria[0].options[0].points,
    ).toBe(30);
  });

  it('pastes a copy or a link into another cell, also across tracks', () => {
    const m = sample();
    const source = cellOf(m, 'A', 'governance')!;
    const copied = pasteCell(m, source, 'C', 'financial', false);
    expect(cellOf(copied, 'C', 'financial')).toEqual(
      expect.objectContaining({ kind: 'points', row: 'C', column: 'financial' }),
    );
    expect(copied.shared?.length ?? 0).toBe(0);
    const linked = pasteCell(m, source, 'C', 'financial', true);
    const code = linked.shared![0].code;
    expect(cellOf(linked, 'A', 'governance')?.shared).toBe(code);
    expect(cellOf(linked, 'C', 'financial')?.shared).toBe(code);
  });

  it('drops shared calculations nothing links to', () => {
    let m = shareCells(sample(), 'A', 'governance', 'gov', []);
    m = detachCell(m, 'A', 'governance');
    expect(dropUnusedShared(m).shared).toEqual([]);
  });
});

describe('weights from the question bank', () => {
  it('takes the bank weights for criteria that follow it, keeping attachments', () => {
    const m = sample();
    m.cells[0].groups[0].criteria[0] = {
      ...m.cells[0].groups[0].criteria[0],
      fromBank: true,
      options: [{ value: 'YES', points: 1, attachment: 'doc' }],
    };
    const answers = [
      { value: 'YES', labelAr: '', labelEn: '', points: 8 },
      { value: 'NO', labelAr: '', labelEn: '', points: 0 },
    ];
    expect(bankWeightsChanged(m.cells[0].groups[0].criteria[0], answers)).toBe(true);
    const synced = syncFromBank(m, () => answers);
    expect(synced.cells[0].groups[0].criteria[0].options).toEqual([
      { value: 'YES', points: 8, grade: null, attachment: 'doc' },
      { value: 'NO', points: 0, grade: null, attachment: null },
    ]);
    expect(bankWeightsChanged(synced.cells[0].groups[0].criteria[0], answers)).toBe(false);
    // A criterion that does not follow the bank keeps its own weights.
    expect(synced.cells[1].groups[0].criteria[0].options).toEqual([{ value: 'yes', points: 10 }]);
  });
});
