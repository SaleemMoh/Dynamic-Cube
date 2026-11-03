import { emptyMatrix } from './matrix-model';
import { simulatorInputs } from './matrix-simulator';

describe('simulatorInputs', () => {
  const m = emptyMatrix();
  m.rows = [
    { code: 'A', nameAr: '', nameEn: '', active: true, final: 'worst' },
    { code: 'B', nameAr: '', nameEn: '', active: true, final: 'worst' },
  ];
  m.inputs = [
    { code: 'x', nameAr: '', nameEn: '', type: 'number', source: { kind: 'applicant' } },
    { code: 'y', nameAr: '', nameEn: '', type: 'number', source: { kind: 'reviewer' } },
    {
      code: 'ratio',
      nameAr: '',
      nameEn: '',
      type: 'number',
      source: { kind: 'derived', rule: { '/': [{ var: 'y' }, 2] } },
    },
    { code: 'size', nameAr: '', nameEn: '', type: 'number', source: { kind: 'given' } },
    { code: 'other', nameAr: '', nameEn: '', type: 'number', source: { kind: 'applicant' } },
  ];
  m.sizes = { input: 'size', categories: [] };
  m.cells = [
    {
      row: 'A',
      column: 'technical',
      kind: 'points',
      groups: [
        {
          code: 'g',
          nameAr: '',
          nameEn: '',
          mandatory: false,
          criteria: [
            {
              code: 'c1',
              nameAr: '',
              nameEn: '',
              input: 'x',
              scoring: 'direct',
              output: 'points',
              bands: [],
              options: [],
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
    {
      row: 'B',
      column: 'technical',
      kind: 'points',
      groups: [
        {
          code: 'g',
          nameAr: '',
          nameEn: '',
          mandatory: false,
          criteria: [
            {
              code: 'c',
              nameAr: '',
              nameEn: '',
              input: 'other',
              scoring: 'direct',
              output: 'points',
              bands: [],
              options: [],
            },
          ],
        },
      ],
    },
  ];

  it('offers every typed input when no sector is given', () => {
    expect(simulatorInputs(m).map((i) => i.code)).toEqual(['x', 'y', 'size', 'other']);
  });

  it('offers only what the chosen sectors read, including what calculated inputs are worked out from and the size', () => {
    expect(simulatorInputs(m, ['A']).map((i) => i.code)).toEqual(['x', 'y', 'size']);
    expect(simulatorInputs(m, ['b']).map((i) => i.code)).toEqual(['size', 'other']);
  });
});
