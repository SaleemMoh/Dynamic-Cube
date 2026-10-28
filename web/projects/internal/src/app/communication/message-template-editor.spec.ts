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
