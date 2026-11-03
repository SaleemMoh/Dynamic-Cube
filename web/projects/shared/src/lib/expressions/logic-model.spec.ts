import { JsonLogicRule, jsonLogic } from './jsonlogic';
import {
  LogicCondition,
  LogicWords,
  describeCondition,
  describeValue,
  fullyVisual,
  readCondition,
  readValue,
  writeCondition,
  writeValue,
} from './logic-model';

const words: LogicWords = {
  and: 'and',
  or: 'or',
  not: 'not',
  always: 'always',
  empty: 'empty',
  op: {
    eq: 'is',
    ne: 'is not',
    gt: 'is more than',
    ge: 'is at least',
    lt: 'is less than',
    le: 'is at most',
    between: 'is between',
    in: 'is one of',
    notIn: 'is not one of',
    contains: 'contains',
    startsWith: 'starts with',
    filled: 'is filled',
    empty: 'is empty',
    isTrue: 'is yes',
    isFalse: 'is no',
  },
  quantifier: { some: 'some', all: 'every', none: 'no' },
  rowsOf: 'of',
  where: 'where',
  sumOf: 'sum of',
  countOf: 'number of rows of',
  if: 'if',
  then: 'then',
  otherwise: 'otherwise',
  advanced: '[advanced]',
  yes: 'yes',
  no: 'no',
  joined: 'join',
};

const label = (path: string): string =>
  ({ 'data.OWNER': 'Owner', 'data.VALUE': 'Value', 'data.P': 'Projects' })[path] ??
  path.split('.').pop()!;

describe('logic model', () => {
  it('reads conditions into the form and writes them back unchanged', () => {
    const rules: JsonLogicRule[] = [
      { '==': [{ var: 'level' }, 'PREMIUM'] },
      {
        and: [
          { '>=': [{ var: 'data.REVENUE' }, 1000000] },
          { in: [{ var: 'level' }, ['PREMIUM', 'DIAMOND']] },
        ],
      },
      {
        or: [
          { in: ['ICT', { var: 'data.SECTORS' }] },
          { '!!': [{ var: 'data.BOARD' }] },
          { '!': [{ var: 'data.ISO' }] },
        ],
      },
      { and: [{ var: 'task.assigned' }, { in: [{ var: 'task.sla' }, ['WARNING', 'BREACHED']] }] }
        .and[1] as JsonLogicRule,
      { '==': [{ substr: [{ var: 'task.service' }, 0, 14] }, 'CLASSIFICATION'] },
      { '<=': [10, { var: 'data.VALUE' }, 50] },
      { '!': [{ in: [{ var: 'level' }, ['A', 'B']] }] },
      { '!': [{ or: [{ '>': [{ var: 'x' }, 1] }, { '<': [{ var: 'y' }, 2] }] }] },
      {
        and: [
          { '==': [{ var: 'a' }, 1] },
          { or: [{ '==': [{ var: 'b' }, 2] }, { '!=': [{ var: 'c' }, { var: 'd' }] }] },
        ],
      },
      { some: [{ var: 'data.P' }, { '==': [{ var: 'OWNER' }, 'GOV'] }] },
      { '==': [{ var: 'task.escalated' }, false] },
      {
        '>=': [
          {
            reduce: [
              { var: 'data.P' },
              { '+': [{ var: 'accumulator' }, { var: 'current.VALUE' }] },
              0,
            ],
          },
          1,
        ],
      },
    ];
    for (const rule of rules) {
      const node = readCondition(rule);
      expect(fullyVisual(node), JSON.stringify(rule)).toBe(true);
      expect(writeCondition(node), JSON.stringify(rule)).toEqual(rule);
    }
  });

  it('keeps what it cannot show as JSON, unchanged, inside the form', () => {
    const rule: JsonLogicRule = {
      and: [{ '==': [{ var: 'a' }, 1] }, { missing_some: [1, ['x', 'y']] }],
    };
    const node = readCondition(rule);
    expect(fullyVisual(node)).toBe(false);
    expect(node.kind).toBe('group');
    expect((node as Extract<LogicCondition, { kind: 'group' }>).items[1].kind).toBe('raw');
    expect(writeCondition(node)).toEqual(rule);
  });

  it('reads and writes calculations', () => {
    const rules: JsonLogicRule[] = [
      {
        '*': [
          {
            reduce: [
              { var: 'data.P' },
              { '+': [{ var: 'accumulator' }, { var: 'current.VALUE' }] },
              0,
            ],
          },
          1000,
        ],
      },
      {
        if: [
          { and: [{ '>=': [{ var: 'vars.total' }, 50] }, { '>=': [{ var: 'vars.quality' }, 80] }] },
          'A',
          { '>=': [{ var: 'vars.quality' }, 50] },
          'C',
          'NONE',
        ],
      },
      {
        reduce: [
          { filter: [{ var: 'data.P' }, { '==': [{ var: 'OWNER' }, 'GOV'] }] },
          { '+': [{ var: 'accumulator' }, 1] },
          0,
        ],
      },
      { cat: [{ var: 'party.nameAr' }, ' — ', { var: 'subject.number' }] },
      { '!=': [{ var: 'vars.band' }, 'NONE'] },
      { var: 'party.profile.CR_CAPITAL' },
      42,
      'TEXT',
      { max: [{ var: 'a' }, 0] },
    ];
    for (const rule of rules) {
      const node = readValue(rule);
      expect(fullyVisual(node), JSON.stringify(rule)).toBe(true);
      expect(writeValue(node), JSON.stringify(rule)).toEqual(rule);
    }
    expect(readValue({ '!=': [{ var: 'vars.band' }, 'NONE'] }).kind).toBe('test');
    expect(
      readValue({ reduce: [{ var: 'data.P' }, { '+': [{ var: 'accumulator' }, 1] }, 0] }),
    ).toEqual({
      kind: 'aggregate',
      fn: 'count',
      list: 'data.P',
      column: null,
      where: null,
    });
  });

  it('writes rules that give the same result as typed JSON', () => {
    const data = {
      data: {
        P: [
          { OWNER: 'GOV', VALUE: 4 },
          { OWNER: 'PRIVATE', VALUE: 7 },
        ],
        VALUE: 20,
      },
    };
    const sum = writeValue({
      kind: 'aggregate',
      fn: 'sum',
      list: 'data.P',
      column: 'VALUE',
      where: null,
    });
    expect(jsonLogic.evaluate(sum, data)).toBe(11);
    const govCount = writeValue({
      kind: 'aggregate',
      fn: 'count',
      list: 'data.P',
      column: null,
      where: {
        kind: 'compare',
        left: { kind: 'field', path: 'OWNER' },
        op: 'eq',
        right: { kind: 'fixed', value: 'GOV' },
      },
    });
    expect(jsonLogic.evaluate(govCount, data)).toBe(1);
    const between = writeCondition({
      kind: 'compare',
      left: { kind: 'field', path: 'data.VALUE' },
      op: 'between',
      right: { kind: 'fixed', value: 10 },
      to: { kind: 'fixed', value: 30 },
    });
    expect(jsonLogic.evaluate(between, data)).toBe(true);
    expect(
      jsonLogic.evaluate(
        writeCondition({
          kind: 'compare',
          left: { kind: 'field', path: 'data.VALUE' },
          op: 'notIn',
          right: { kind: 'fixed', value: [20] },
        }),
        data,
      ),
    ).toBe(false);
  });

  it('treats an empty condition as always true and leaves out unfinished rows', () => {
    expect(writeCondition(readCondition(null))).toBeNull();
    expect(
      writeCondition({
        kind: 'group',
        match: 'all',
        not: false,
        items: [
          { kind: 'compare', left: { kind: 'field', path: '' }, op: 'eq', right: null },
          {
            kind: 'compare',
            left: { kind: 'field', path: 'level' },
            op: 'eq',
            right: { kind: 'fixed', value: 'A' },
          },
        ],
      }),
    ).toEqual({ '==': [{ var: 'level' }, 'A'] });
  });

  it('describes rules in plain words', () => {
    expect(
      describeCondition(
        readCondition({
          and: [{ '==': [{ var: 'data.OWNER' }, 'GOV'] }, { '>': [{ var: 'data.VALUE' }, 10] }],
        }),
        words,
        label,
      ),
    ).toBe('Owner is "GOV" and Value is more than 10');
    expect(
      describeCondition(
        readCondition({ some: [{ var: 'data.P' }, { '==': [{ var: 'OWNER' }, 'GOV'] }] }),
        words,
        label,
      ),
    ).toBe('some of Projects where OWNER is "GOV"');
    expect(
      describeValue(
        readValue({ if: [{ '>=': [{ var: 'data.VALUE' }, 50] }, 'A', 'B'] }),
        words,
        label,
      ),
    ).toBe('if Value is at least 50 then "A"، otherwise "B"');
    expect(describeCondition(readCondition(null), words, label)).toBe('always');
  });
});
