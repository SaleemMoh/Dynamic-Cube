import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { AppsControlApi, ConfigRow } from '@dynamic-cube/shared';
import { of } from 'rxjs';
import { MatrixReferences } from './matrix-references';

const QUESTIONS: ConfigRow[] = [
  {
    code: 'BOARD',
    nameAr: 'مجلس الإدارة',
    nameEn: 'Board',
    type: 'SELECT',
    options: [
      { value: 'yes', labelAr: 'نعم', labelEn: 'Yes' },
      { value: 'no', labelAr: 'لا', labelEn: 'No' },
    ],
  } as unknown as ConfigRow,
  {
    code: 'REGION',
    nameAr: '',
    nameEn: '',
    type: 'SELECT',
    lov: 'REGIONS',
    multiple: true,
  } as unknown as ConfigRow,
  {
    code: 'ENGINEERS',
    nameAr: '',
    nameEn: '',
    type: 'NUMBER',
    validation: { min: 0, max: 500 },
  } as unknown as ConfigRow,
  { code: 'LICENSED', nameAr: '', nameEn: '', type: 'CHECKBOX' } as unknown as ConfigRow,
];

function setup(): MatrixReferences {
  const api = {
    list: (type: string) => of(type === 'svc.question' ? QUESTIONS : []),
    lovOptions: () =>
      of({
        lov: 'REGIONS',
        items: [{ value: 'RIYADH', labelAr: 'الرياض', labelEn: 'Riyadh', parent: null }],
        defaultValue: null,
        hasMore: false,
      }),
  };
  TestBed.configureTestingModule({
    providers: [
      MatrixReferences,
      { provide: AppsControlApi, useValue: api },
      { provide: HttpClient, useValue: { get: () => of([]) } },
    ],
  });
  return TestBed.inject(MatrixReferences);
}

describe('MatrixReferences.bankQuestion', () => {
  it("gives a choice question's answers and input type", () => {
    const board = setup().bankQuestion('BOARD');
    expect(board?.inputType).toBe('option');
    expect(board?.answers?.map((a) => a.value)).toEqual(['yes', 'no']);
  });

  it('reads the answers of a list of values, and a multiple choice is a list', () => {
    const region = setup().bankQuestion('REGION');
    expect(region?.inputType).toBe('list');
    expect(region?.answers).toEqual([{ value: 'RIYADH', labelAr: 'الرياض', labelEn: 'Riyadh' }]);
  });

  it('gives a number question its limits and no answers, and a checkbox yes/no', () => {
    const refs = setup();
    expect(refs.bankQuestion('ENGINEERS')).toEqual(
      expect.objectContaining({ inputType: 'number', answers: null, min: 0, max: 500 }),
    );
    expect(refs.bankQuestion('LICENSED')?.answers?.map((a) => a.value)).toEqual(['yes', 'no']);
    expect(refs.bankQuestion('NOPE')).toBeNull();
  });

  it("gives an input its bank question's answers", () => {
    const refs = setup();
    expect(
      refs.answersOf({
        code: 'b',
        nameAr: '',
        nameEn: '',
        type: 'option',
        source: { kind: 'applicant', question: 'BOARD' },
      })?.length,
    ).toBe(2);
    expect(
      refs.answersOf({
        code: 'b',
        nameAr: '',
        nameEn: '',
        type: 'option',
        source: { kind: 'reviewer' },
      }),
    ).toBeNull();
  });
});
