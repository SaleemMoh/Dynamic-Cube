import { STARTER, addQuestion, addSection, addStep, check, moveQuestion, normalize, remove, usedQuestions } from './builder-model';

const bank = new Set(['NAME', 'CITY', 'EMPLOYEES']);

describe('service builder model', () => {
  it('fills in what an older definition lacks', () => {
    const def = normalize({ steps: [{ code: 'A', type: 'FORM', titleAr: 'أ', titleEn: 'A', sections: [] }] } as never);
    expect(def.levels).toEqual([]);
    expect(def.fees).toEqual([]);
    expect(def.card.channels).toEqual(['PORTAL']);
  });

  it('builds a form: steps, sections and bank questions placed and moved', () => {
    let def = normalize(STARTER);
    def = addQuestion(def, 0, 0, 'NAME');
    def = addQuestion(def, 0, 0, 'NAME');
    expect(def.steps[0].sections[0].questions.map((q) => q.code)).toEqual(['NAME']);
    const added = addSection(def, 0);
    def = addQuestion(added.def, 0, added.index, 'CITY');
    def = addQuestion(def, 0, 0, 'EMPLOYEES', 0);
    expect(def.steps[0].sections[0].questions.map((q) => q.code)).toEqual(['EMPLOYEES', 'NAME']);
    def = moveQuestion(def, { step: 0, section: 0, question: 1 }, { step: 0, section: 1, index: 0 });
    expect(def.steps[0].sections[1].questions.map((q) => q.code)).toEqual(['NAME', 'CITY']);
    expect(usedQuestions(def)).toEqual(['EMPLOYEES', 'NAME', 'CITY']);

    const level = addStep(def, 'LEVEL', -1);
    expect(level.index).toBe(0);
    expect(level.def.steps[0].code).toBe('LEVEL');
    // A level step needs levels.
    expect(check(level.def, bank).map((p) => p.key)).toContain('builder.problem.levelStepWithoutLevels');
    expect(check(def, bank)).toEqual([]);
  });

  it('flags what the server would refuse', () => {
    let def = normalize(STARTER);
    expect(check(def, bank).map((p) => p.key)).toContain('builder.problem.emptySection');
    def = addQuestion(def, 0, 0, 'GONE');
    def = addStep(def, 'REVIEW').def;
    def = { ...def, fees: [{ code: 'FEE', nameAr: 'ر', nameEn: 'F', levels: ['NOPE'], vatRate: 0.15, vatIncluded: true }] };
    const keys = check(def, bank).map((p) => p.key);
    expect(keys).toEqual(
      expect.arrayContaining(['builder.problem.unknownQuestion', 'builder.problem.oneOf', 'builder.problem.fee', 'builder.problem.feeLevel']),
    );
    expect(check(remove(def, { step: 0, section: 0, question: 0 }), bank).map((p) => p.key)).not.toContain('builder.problem.unknownQuestion');
  });
});
