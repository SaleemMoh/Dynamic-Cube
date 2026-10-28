import { factText, visibleKeys } from '../action-center/instance-facts';
import { traceText } from '../decisions/rule-set-versions';
import { samplePaths } from './message-template-editor';

describe('samplePaths', () => {
  it('lists the dotted paths of a sample, arrays as one field', () => {
    expect(samplePaths({ request: { number: 'CLS-1', fees: [1, 2] }, party: { nameAr: 'م', identifiers: { CR_NUMBER: '1' } }, flag: null })).toEqual([
      'request.number',
      'request.fees',
      'party.nameAr',
      'party.identifiers.CR_NUMBER',
      'flag',
    ]);
    expect(samplePaths({})).toEqual([]);
  });
});

describe('traceText', () => {
  it('shows the value of a trace step, or its other properties', () => {
    expect(traceText({ step: 'vars.grade', value: 3 })).toBe('3');
    expect(traceText({ step: 'items', count: 2 })).toBe('{"count":2}');
  });
});

describe('visibleKeys', () => {
  it('keeps the reader’s language of bilingual pairs', () => {
    const keys = ['sector', 'sectorNameAr', 'sectorNameEn', 'grade'];
    expect(visibleKeys(keys, 'ar')).toEqual([
      { key: 'sector', label: 'sector' },
      { key: 'sectorNameAr', label: 'sectorName' },
      { key: 'grade', label: 'grade' },
    ]);
    expect(visibleKeys(keys, 'en').map((k) => k.key)).toEqual(['sector', 'sectorNameEn', 'grade']);
    expect(visibleKeys(['nameAr'], 'en')).toEqual([{ key: 'nameAr', label: 'nameAr' }]);
  });
});

describe('factText', () => {
  it('rounds fractions', () => {
    expect(factText(81.81818181818183)).toBe('81.82');
    expect(factText(2)).toBe('2');
    expect(factText(true)).toBe('✓');
  });
});
