import { signal } from '@angular/core';
import { checkInput } from './matrix-connections';
import { MatrixInput, emptyMatrix } from './matrix-model';
import { MatrixReferences } from './matrix-references';

/** A stand-in for the references: one question asked by one service, one review task, one operation called once. */
function refs(): MatrixReferences {
  const fake = {
    question: (code: string | null | undefined) =>
      code === 'ENGINEERS' || code === 'UNUSED' ? { code } : undefined,
    questionServices: signal(new Map([['ENGINEERS', ['CLS_NEW']]])),
    tasks: signal([
      {
        workflow: 'WF',
        workflowName: { ar: '', en: '' },
        step: 'REVIEW',
        stepName: { ar: '', en: '' },
        fields: [{ code: 'credit' }],
      },
    ]),
    operation: (code: string | null | undefined) =>
      code === 'GOSI' || code === 'ZATCA' ? { code } : undefined,
    integrationSteps: signal([
      { workflow: 'WF', step: 'CALL_GOSI', operation: 'GOSI', result: 'gosi' },
    ]),
    formOperations: signal(new Map<string, string[]>()),
    partyFacts: signal([{ code: 'party.identifiers.CR_NUMBER', name: { ar: '', en: '' } }]),
    bankQuestion: (code: string | null) =>
      code === 'ENGINEERS'
        ? {
            code,
            answers: [
              { value: 'few', labelAr: '', labelEn: '' },
              { value: 'many', labelAr: '', labelEn: '' },
            ],
          }
        : null,
  };
  return fake as unknown as MatrixReferences;
}

const input = (source: MatrixInput['source']): MatrixInput => ({
  code: 'x',
  nameAr: '',
  nameEn: '',
  type: 'number',
  source,
});

describe('checkInput', () => {
  it('checks applicant questions against the bank and the service forms', () => {
    expect(checkInput(input({ kind: 'applicant', question: 'ENGINEERS' }), refs())).toEqual({
      state: 'ok',
      message: 'mx.link.askedIn',
      detail: 'CLS_NEW',
    });
    expect(checkInput(input({ kind: 'applicant', question: 'UNUSED' }), refs()).state).toBe('warn');
    expect(checkInput(input({ kind: 'applicant', question: 'NOPE' }), refs()).message).toBe(
      'mx.link.questionMissing',
    );
  });

  it('checks reviewer fields on the tasks', () => {
    expect(
      checkInput(input({ kind: 'reviewer', step: 'REVIEW', field: 'credit' }), refs()).state,
    ).toBe('ok');
    expect(checkInput(input({ kind: 'reviewer', field: 'credit' }), refs()).state).toBe('ok');
    expect(
      checkInput(input({ kind: 'reviewer', step: 'REVIEW', field: 'other' }), refs()).message,
    ).toBe('mx.link.fieldMissing');
    expect(
      checkInput(input({ kind: 'reviewer', step: 'GONE', field: 'credit' }), refs()).message,
    ).toBe('mx.link.taskMissing');
  });

  it('checks that an operation exists and some step calls it', () => {
    expect(
      checkInput(input({ kind: 'integration', operation: 'GOSI', output: 'count' }), refs()).detail,
    ).toBe('WF › CALL_GOSI');
    expect(checkInput(input({ kind: 'integration', operation: 'ZATCA' }), refs()).message).toBe(
      'mx.link.notCalled',
    );
    expect(checkInput(input({ kind: 'integration', operation: 'NIC' }), refs()).message).toBe(
      'mx.link.operationMissing',
    );
    expect(
      checkInput(input({ kind: 'integration', operation: 'GOSI', step: 'other' }), refs()).state,
    ).toBe('error');
  });

  it('flags points given to answers the bank question no longer has', () => {
    const scored = input({ kind: 'applicant', question: 'ENGINEERS' });
    const m = emptyMatrix();
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
                code: 'c',
                nameAr: '',
                nameEn: '',
                input: 'x',
                scoring: 'options',
                output: 'points',
                bands: [],
                options: [
                  { value: 'FEW', points: 1 },
                  { value: 'old', points: 2 },
                ],
              },
            ],
          },
        ],
      },
    ];
    expect(checkInput(scored, refs(), m)).toEqual({
      state: 'warn',
      message: 'mx.link.staleAnswers',
      detail: 'old',
    });
    expect(checkInput(scored, refs()).state).toBe('ok');
  });

  it('checks party facts, and computed and given inputs', () => {
    expect(
      checkInput(input({ kind: 'party', path: 'party.identifiers.CR_NUMBER' }), refs()).state,
    ).toBe('ok');
    expect(checkInput(input({ kind: 'party', path: 'party.x' }), refs()).state).toBe('warn');
    expect(checkInput(input({ kind: 'derived', rule: 1 }), refs()).state).toBe('ok');
    expect(checkInput(input({ kind: 'given' }), refs()).message).toBe('mx.link.given');
  });
});
