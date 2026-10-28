import { jsonLogic } from '../expressions/jsonlogic';
import { answeredSections, displayAnswer, fromAnswers, stepSchema, toAnswers } from './service-form';
import { QuestionSnapshot, ResolvedVersion } from './service.models';

const question = (code: string, type: QuestionSnapshot['type'] = 'NUMBER', options: QuestionSnapshot['options'] = []): QuestionSnapshot => ({
  code,
  labelAr: code,
  labelEn: code,
  hintAr: null,
  hintEn: null,
  type,
  lov: null,
  lovParentQuestion: null,
  options,
  multiple: type === 'SELECT',
  validation: { min: 1, max: null, maxLength: null, pattern: null },
  prefill: null,
});

const version: ResolvedVersion = {
  serviceCode: 'CLS',
  number: 1,
  definition: {
    levels: [],
    eligibility: [],
    validations: [],
    fees: [],
    steps: [
      { code: 'SECTORS', type: 'FORM', titleAr: 'ق', titleEn: 'S', sections: [{ code: 'CHOICE', titleAr: 'ق', titleEn: 'C', questions: [{ code: 'SECTORS', required: true, readonly: false }] }] },
      {
        code: 'DETAILS',
        type: 'FORM',
        titleAr: 'ب',
        titleEn: 'D',
        sections: [
          { code: 'ICT', titleAr: 'ت', titleEn: 'ICT', visibleWhen: { in: ['ICT', { var: 'SECTORS' }] }, questions: [{ code: 'EMPLOYEES', required: true, readonly: false }] },
          { code: 'BUILDING', titleAr: 'ب', titleEn: 'B', visibleWhen: { in: ['BUILDING', { var: 'SECTORS' }] }, questions: [{ code: 'EMPLOYEES', required: true, readonly: false }, { code: 'PROJECTS', required: false, readonly: false }] },
        ],
      },
    ],
  },
  questions: {
    SECTORS: question('SECTORS', 'SELECT', [{ value: 'ICT', labelAr: 'تقنية', labelEn: 'ICT' }, { value: 'BUILDING', labelAr: 'بناء', labelEn: 'Building' }]),
    EMPLOYEES: question('EMPLOYEES'),
    PROJECTS: question('PROJECTS'),
    WHEN: question('WHEN', 'DATE'),
  },
};

describe('stepSchema', () => {
  const schema = stepSchema(version, version.definition.steps[1]);
  const employeesIn = (section: number) => schema.sections[section].fields.find((f) => f.code === 'EMPLOYEES')!;
  const visible = (section: number, data: Record<string, unknown>) =>
    jsonLogic.evaluateTruthy(schema.sections[section].visibleWhen ?? true, data as never) &&
    jsonLogic.evaluateTruthy(employeesIn(section).visibleWhen ?? true, data as never);

  it('asks a question shared by two visible sections once, in the first', () => {
    expect(visible(0, { SECTORS: ['ICT', 'BUILDING'] })).toBe(true);
    expect(visible(1, { SECTORS: ['ICT', 'BUILDING'] })).toBe(false);
  });

  it('asks it in the later section when the first is hidden', () => {
    expect(visible(0, { SECTORS: ['BUILDING'] })).toBe(false);
    expect(visible(1, { SECTORS: ['BUILDING'] })).toBe(true);
  });

  it('carries limits and choices to the form fields', () => {
    expect(employeesIn(0)).toMatchObject({ type: 'number', min: 1, required: true });
    expect(stepSchema(version, version.definition.steps[0]).sections[0].fields[0]).toMatchObject({ type: 'select', multiple: true });
  });
});

describe('answers', () => {
  it('lists answered sections once per question and shows option labels', () => {
    const sections = answeredSections(version, { SECTORS: ['ICT', 'BUILDING'], EMPLOYEES: 5 });
    expect(sections.map((s) => s.titleEn)).toEqual(['C', 'ICT', 'B']);
    expect(sections[2].questions.map((q) => q.code)).toEqual(['PROJECTS']);
    expect(displayAnswer(version.questions['SECTORS'], ['ICT', 'BUILDING'], 'en')).toBe('ICT، Building');
  });

  it('round-trips dates between the API and the date picker', () => {
    const form = fromAnswers(version, { WHEN: '2026-03-05' });
    expect(form['WHEN']).toBeInstanceOf(Date);
    expect(toAnswers(form)).toEqual({ WHEN: '2026-03-05' });
  });
});
