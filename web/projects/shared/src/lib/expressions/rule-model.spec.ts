import { JsonLogicRule } from './jsonlogic';
import { buildRule, parseRule } from './rule-model';

describe('rule model', () => {
  it('reads comparisons into the visual form and writes them back unchanged', () => {
    const rules: JsonLogicRule[] = [
      { '==': [{ var: 'level' }, 'PREMIUM'] },
      {
        and: [{ '>=': [{ var: 'data.REVENUE' }, 1000000] }, { in: [{ var: 'level' }, ['PREMIUM', 'DIAMOND']] }],
      },
      {
        or: [{ in: ['ICT', { var: 'data.SECTORS' }] }, { '!!': [{ var: 'data.BOARD' }] }, { '!': [{ var: 'data.ISO' }] }],
      },
    ];
    for (const rule of rules) {
      const group = parseRule(rule);
      expect(group).not.toBeNull();
      expect(buildRule(group!)).toEqual(rule);
    }
    expect(parseRule({ or: [{ in: ['ICT', { var: 'data.SECTORS' }] }] })!.conditions[0]).toEqual({
      variable: 'data.SECTORS',
      operator: 'contains',
      value: 'ICT',
    });
  });

  it('leaves anything else to the JSON editor', () => {
    expect(parseRule({ '>': [{ '+': [{ var: 'a' }, 1] }, 2] })).toBeNull();
    expect(parseRule({ and: [{ '==': [{ var: 'a' }, 1] }, { if: [true, 1, 2] }] })).toBeNull();
    expect(parseRule(true)).toBeNull();
  });

  it('treats no rule as no conditions', () => {
    expect(parseRule(null)).toEqual({ match: 'all', conditions: [] });
    expect(buildRule({ match: 'all', conditions: [] })).toBeNull();
    expect(buildRule({ match: 'any', conditions: [{ variable: '', operator: '==', value: 1 }] })).toBeNull();
  });
});
