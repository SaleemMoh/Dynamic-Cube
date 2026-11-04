import { VersionDefinition } from '@dynamic-cube/shared';
import {
  formForSector,
  moveQuestion,
  parseOptions,
  placeQuestion,
  placements,
  sectorsRule,
  setRequired,
  showForSector,
  shownFor,
  stepForTrack,
  visibility,
  withSector,
} from './matrix-form';

const Q = 'CLS_DOMAINS';
const ALL = ['RE', 'ICT', 'EX'];

function form(): VersionDefinition {
  return {
    levels: [],
    eligibility: [],
    validations: [],
    fees: [],
    steps: [
      { code: 'SOURCES', type: 'INTEGRATION', titleAr: '', titleEn: 'Sources', sections: [] },
      {
        code: 'TECHNICAL',
        type: 'FORM',
        titleAr: 'فني',
        titleEn: 'Technical',
        sections: [
          {
            code: 'TECHNICAL',
            titleAr: '',
            titleEn: '',
            questions: [
              {
                code: 'RE_RISK',
                required: true,
                readonly: false,
                visibleWhen: sectorsRule(Q, ['RE']),
              },
              { code: 'STAFF', required: false, readonly: false },
              {
                code: 'CUSTOM',
                required: false,
                readonly: false,
                visibleWhen: { '>': [{ var: 'STAFF' }, 3] },
              },
            ],
          },
        ],
      },
      { code: 'GOVERNANCE', type: 'FORM', titleAr: '', titleEn: '', sections: [] },
    ],
  };
}

describe('application form of a sector', () => {
  it('reads which sectors a question shows for', () => {
    expect(visibility(null, Q)).toEqual({ kind: 'all' });
    expect(visibility({ in: ['RE', { var: Q }] }, Q)).toEqual({ kind: 'sectors', sectors: ['RE'] });
    expect(visibility(sectorsRule(Q, ['RE', 'ICT']), Q)).toEqual({
      kind: 'sectors',
      sectors: ['RE', 'ICT'],
    });
    expect(visibility({ '>': [{ var: 'X' }, 1] }, Q).kind).toBe('custom');
    expect(shownFor(sectorsRule(Q, ['RE']), Q, 'ict')).toBe(false);
    expect(shownFor(sectorsRule(Q, ['RE']), Q, 're')).toBe(true);
  });

  it('adds and takes off a sector; every sector again means no rule', () => {
    expect(withSector(null, Q, 'RE', false, ALL)).toEqual(sectorsRule(Q, ['ICT', 'EX']));
    expect(withSector(sectorsRule(Q, ['RE']), Q, 'ICT', true, ALL)).toEqual(
      sectorsRule(Q, ['RE', 'ICT']),
    );
    expect(withSector(sectorsRule(Q, ['RE', 'ICT']), Q, 'EX', true, ALL)).toBeNull();
  });

  it('shows the form step by step as one sector sees it', () => {
    const steps = formForSector(form(), Q, 'ICT');
    expect(steps[1].questions.map((q) => [q.use.code, q.sectorOnly, q.custom])).toEqual([
      ['STAFF', false, false],
      ['CUSTOM', false, true],
    ]);
    expect(formForSector(form(), Q, 'RE')[1].questions[0].sectorOnly).toBe(true);
  });

  it('places, shows, hides, requires and moves questions', () => {
    let def = placeQuestion(form(), 'GOVERNANCE', 'BOARD', Q, 'ICT');
    expect(def.steps[2].sections[0].questions[0]).toEqual(
      expect.objectContaining({ code: 'BOARD', visibleWhen: sectorsRule(Q, ['ICT']) }),
    );
    def = showForSector(def, 'RE_RISK', Q, 'EX', true, ALL);
    expect(visibility(placements(def, 'RE_RISK')[0].use.visibleWhen, Q)).toEqual({
      kind: 'sectors',
      sectors: ['RE', 'EX'],
    });
    def = showForSector(def, 'BOARD', Q, 'ICT', false, ALL);
    expect(placements(def, 'BOARD')).toEqual([]);
    def = setRequired(def, 'STAFF', true);
    expect(placements(def, 'STAFF')[0].use.required).toBe(true);
    def = moveQuestion(def, placements(def, 'STAFF')[0], -1);
    expect(def.steps[1].sections[0].questions.map((q) => q.code)).toEqual([
      'STAFF',
      'RE_RISK',
      'CUSTOM',
    ]);
  });

  it('finds the step of a track and reads typed answers', () => {
    expect(stepForTrack(form(), 'governance', [])).toBe('GOVERNANCE');
    expect(stepForTrack(form(), 'financial', ['STAFF'])).toBe('TECHNICAL');
    expect(parseOptions('yes | نعم | Yes\nno\n\n')).toEqual([
      { value: 'yes', labelAr: 'نعم', labelEn: 'Yes' },
      { value: 'no', labelAr: 'no', labelEn: 'no' },
    ]);
  });
});
