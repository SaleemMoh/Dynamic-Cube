import cases from '../../../../../../tests/shared/jsonlogic-cases.json';
import { isTruthy, JsonLogic, JsonLogicError, JsonValue } from './jsonlogic';

interface ConformanceCase {
  name: string;
  rule: JsonValue;
  data: JsonValue;
  result: JsonValue;
}

/** Same structural comparison as the .NET tests: numbers compare by value. */
function deepEquals(expected: JsonValue, actual: JsonValue): boolean {
  if (expected === null || actual === null) {
    return expected === actual;
  }
  if (Array.isArray(expected)) {
    return Array.isArray(actual) && expected.length === actual.length && expected.every((e, i) => deepEquals(e, actual[i]));
  }
  if (typeof expected === 'object') {
    if (typeof actual !== 'object' || Array.isArray(actual)) {
      return false;
    }
    const keys = Object.keys(expected);
    return keys.length === Object.keys(actual).length && keys.every((k) => k in actual && deepEquals(expected[k], actual[k]));
  }
  return expected === actual;
}

describe('JsonLogic conformance (shared with the server)', () => {
  const logic = new JsonLogic();

  it.each((cases as ConformanceCase[]).map((c) => [c.name, c] as const))('%s', (_name, testCase) => {
    const result = logic.evaluate(testCase.rule, testCase.data);
    expect(deepEquals(testCase.result, result), `got ${JSON.stringify(result)}`).toBe(true);
  });
});

describe('JsonLogic', () => {
  it('throws for unknown operations', () => {
    expect(() => new JsonLogic().evaluate({ nope: [1] })).toThrow(JsonLogicError);
  });

  it('supports custom operations with evaluated arguments', () => {
    const logic = new JsonLogic().addOperation('double', (args) => (args[0] as number) * 2);
    expect(logic.evaluate({ double: [{ var: 'x' }] }, { x: 21 })).toBe(42);
  });

  it('does not allow replacing built-in operations', () => {
    expect(() => new JsonLogic().addOperation('if', () => null)).toThrow();
  });

  it('does not evaluate skipped branches', () => {
    expect(new JsonLogic().evaluate({ if: [true, 'ok', { nope: [] }] })).toBe('ok');
  });

  it.each([
    [null, false],
    [0, false],
    ['', false],
    [[], false],
    ['0', true],
    [{}, true],
    [[0], true],
  ] as const)('truthiness of %j is %s', (value, expected) => {
    expect(isTruthy(value as JsonValue)).toBe(expected);
  });
});
