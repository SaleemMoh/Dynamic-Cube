import {
  addBand,
  addCriterion,
  addGroup,
  copyBandsToSizes,
  editTrack,
  moveGroup,
  ownTrack,
  setAnswer,
  setBand,
  startTrack,
  trackOf,
} from './matrix-drill';
import { Matrix, MatrixCell, cellOf, emptyMatrix, resolveCell, shareCells } from './matrix-model';

function governance(row: string): MatrixCell {
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
            options: [{ value: 'yes', points: 10 }],
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
  m.cells = [governance('A')];
  return shareCells(m, 'A', 'governance', 'gov', ['B']);
}

describe('sector page drill-down', () => {
  it('tells how each sector track is calculated', () => {
    const m = sample();
    expect(trackOf(m, 'A', 'governance')).toEqual(
      expect.objectContaining({ mode: 'shared', sharedCode: 'gov', sharedWith: ['B'] }),
    );
    expect(trackOf(m, 'A', 'governance').calculation?.groups[0].code).toBe('g');
    expect(trackOf(m, 'C', 'governance').mode).toBe('empty');
  });

  it('edits a shared track for every linked sector, and an own copy only for its sector', () => {
    let m = editTrack(sample(), 'A', 'governance', addGroup);
    expect(resolveCell(m, cellOf(m, 'B', 'governance'))?.groups.length).toBe(2);

    m = ownTrack(m, 'B', 'governance');
    expect(trackOf(m, 'B', 'governance').mode).toBe('own');
    expect(trackOf(m, 'A', 'governance').sharedWith).toEqual([]);
    m = editTrack(m, 'B', 'governance', (c) => moveGroup(addGroup(c), 2, -1));
    expect(trackOf(m, 'B', 'governance').calculation?.groups.length).toBe(3);
    expect(trackOf(m, 'A', 'governance').calculation?.groups.length).toBe(2);
  });

  it('starts a calculation on an empty track and builds groups, criteria and bands', () => {
    let m = startTrack(sample(), 'C', 'governance', 'points');
    expect(trackOf(m, 'C', 'governance').mode).toBe('own');
    m = editTrack(m, 'C', 'governance', (c) => {
      let x = c.groups.length ? c : addGroup(c);
      x = addCriterion(x, 0, 'board');
      const ci = x.groups[0].criteria.length - 1;
      x = addBand(x, 0, ci, 'small');
      return setBand(x, 0, ci, 0, { min: 0, max: 5, points: 3 });
    });
    const criteria = trackOf(m, 'C', 'governance').calculation!.groups[0].criteria;
    expect(criteria.at(-1)?.bands).toEqual([
      expect.objectContaining({ size: 'small', min: 0, max: 5, points: 3 }),
    ]);
  });

  it('copies the bands of one size to the other sizes and scores answers', () => {
    let c = governance('A');
    c = addBand(c, 0, 0, 'small');
    c = setBand(c, 0, 0, 0, { min: 1, points: 5 });
    c = addBand(c, 0, 0, 'large');
    c = copyBandsToSizes(c, 0, 0, 'small', ['small', 'medium', 'large']);
    expect(c.groups[0].criteria[0].bands.map((b) => [b.size, b.min])).toEqual([
      ['small', 1],
      ['medium', 1],
      ['large', 1],
    ]);

    c = setAnswer(c, 0, 0, 'yes', { points: 7 });
    c = setAnswer(c, 0, 0, 'no', { points: 0 });
    expect(c.groups[0].criteria[0].options).toEqual([
      { value: 'yes', points: 7 },
      { value: 'no', points: 0 },
    ]);
  });
});
