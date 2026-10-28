/**
 * JSONLogic evaluator (https://jsonlogic.com) with the semantics of the reference JavaScript implementation.
 *
 * It mirrors `DynamicCube.Platform.Expressions.JsonLogicEvaluator` on the server so rules behave identically in the
 * browser (instant feedback) and on the server (authoritative). Both are verified against the shared conformance cases
 * in `tests/shared/jsonlogic-cases.json`.
 */

export type JsonValue = null | boolean | number | string | JsonValue[] | { [key: string]: JsonValue };

/** A JSONLogic rule: any JSON value; objects with exactly one key are operations. */
export type JsonLogicRule = JsonValue;

export type JsonLogicOperation = (args: JsonValue[], data: JsonValue) => JsonValue;

type LazyOperation = (logic: JsonLogic, args: JsonValue[], data: JsonValue) => JsonValue;

export class JsonLogicError extends Error {}

const isObject = (value: unknown): value is Record<string, JsonValue> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

const isLogic = (rule: JsonValue): rule is Record<string, JsonValue> => isObject(rule) && Object.keys(rule).length === 1;

/** JSONLogic truthiness: null, false, 0, "" and [] are falsy. */
export function isTruthy(value: JsonValue | undefined): boolean {
  if (Array.isArray(value)) {
    return value.length > 0;
  }
  return !!value;
}

/** Numbers JSON cannot represent (NaN, ±Infinity) become null, as with JSON.stringify. */
const num = (value: number): JsonValue => (Number.isFinite(value) ? value : null);

const toNumber = (value: JsonValue): number => {
  if (Array.isArray(value)) {
    return value.length === 0 ? 0 : value.length === 1 ? Number(toJsString(value[0] ?? '')) : NaN;
  }
  if (isObject(value)) {
    return NaN;
  }
  if (typeof value === 'string') {
    return value.trim() === '' ? 0 : Number(value.trim());
  }
  return Number(value);
};

const parseFloatJs = (value: JsonValue): number => (typeof value === 'number' ? value : parseFloat(toJsString(value)));

/** JavaScript String(value) for JSON values. */
const toJsString = (value: JsonValue): string => {
  if (value === null) {
    return 'null';
  }
  if (Array.isArray(value)) {
    return value.map((item) => (item === null ? '' : toJsString(item))).join(',');
  }
  if (isObject(value)) {
    return '[object Object]';
  }
  return String(value);
};

const toPrimitive = (value: JsonValue): JsonValue => (Array.isArray(value) || isObject(value) ? toJsString(value) : value);

const strictEquals = (a: JsonValue, b: JsonValue): boolean => a === b;

const looseEquals = (a: JsonValue, b: JsonValue): boolean => {
  if (a === null || b === null) {
    return a === null && b === null;
  }
  const aComposite = Array.isArray(a) || isObject(a);
  const bComposite = Array.isArray(b) || isObject(b);
  if (aComposite || bComposite) {
    if (aComposite && bComposite) {
      return a === b;
    }
    return aComposite ? looseEquals(toJsString(a), b) : looseEquals(a, toJsString(b));
  }
  if (typeof a === typeof b) {
    return a === b;
  }
  if (typeof a === 'boolean') {
    return looseEquals(Number(a), b);
  }
  if (typeof b === 'boolean') {
    return looseEquals(a, Number(b));
  }
  return toNumber(a) === toNumber(b);
};

const compare = (a: JsonValue, b: JsonValue, predicate: (c: number) => boolean): boolean => {
  const pa = toPrimitive(a);
  const pb = toPrimitive(b);
  if (typeof pa === 'string' && typeof pb === 'string') {
    return predicate(pa < pb ? -1 : pa > pb ? 1 : 0);
  }
  const na = toNumber(pa);
  const nb = toNumber(pb);
  if (Number.isNaN(na) || Number.isNaN(nb)) {
    return false;
  }
  return predicate(na < nb ? -1 : na > nb ? 1 : 0);
};

const jsSubstr = (value: string, start: number, length?: number): string => {
  let from = start < 0 ? Math.max(value.length + start, 0) : start;
  from = Math.min(from, value.length);
  const available = value.length - from;
  const count = length === undefined ? available : Math.min(Math.max(length, 0), available);
  return value.substring(from, from + count);
};

const toInteger = (value: JsonValue): number => {
  const n = toNumber(value);
  return Number.isNaN(n) ? 0 : Math.trunc(n);
};

const clone = <T extends JsonValue>(value: T): T => (value === null || typeof value !== 'object' ? value : structuredClone(value));

const arg = (args: JsonValue[], index: number): JsonValue => (index < args.length ? (args[index] ?? null) : null);

function varOp(args: JsonValue[], data: JsonValue): JsonValue {
  const path = arg(args, 0);
  const notFound = arg(args, 1);
  const pathText = path === null ? '' : toJsString(path);
  if (pathText === '') {
    return clone(data);
  }
  let current: JsonValue | undefined = data;
  for (const segment of pathText.split('.')) {
    if (current === null || current === undefined) {
      return clone(notFound);
    }
    if (isObject(current) && Object.prototype.hasOwnProperty.call(current, segment)) {
      current = current[segment];
    } else if (Array.isArray(current) && /^\d+$/.test(segment) && Number(segment) < current.length) {
      current = current[Number(segment)];
    } else {
      return clone(notFound);
    }
  }
  return clone(current ?? null);
}

function missingOp(args: JsonValue[], data: JsonValue): JsonValue[] {
  const first = arg(args, 0);
  const keys = Array.isArray(first) ? first : args;
  return keys.filter((key) => {
    const value = varOp([key, null], data);
    return value === null || value === '';
  });
}

const builtIns: Record<string, JsonLogicOperation> = {
  var: varOp,
  missing: missingOp,
  missing_some: (args, data) => {
    const need = toNumber(arg(args, 0));
    const keysArg = arg(args, 1);
    const keys = Array.isArray(keysArg) ? keysArg : [];
    const missing = missingOp([keys], data);
    return keys.length - missing.length >= need ? [] : missing;
  },
  '==': (a) => looseEquals(arg(a, 0), arg(a, 1)),
  '===': (a) => strictEquals(arg(a, 0), arg(a, 1)),
  '!=': (a) => !looseEquals(arg(a, 0), arg(a, 1)),
  '!==': (a) => !strictEquals(arg(a, 0), arg(a, 1)),
  '!': (a) => !isTruthy(arg(a, 0)),
  '!!': (a) => isTruthy(arg(a, 0)),
  '>': (a) => compare(arg(a, 0), arg(a, 1), (c) => c > 0),
  '>=': (a) => compare(arg(a, 0), arg(a, 1), (c) => c >= 0),
  '<': (a) =>
    a.length === 3
      ? compare(arg(a, 0), arg(a, 1), (c) => c < 0) && compare(arg(a, 1), arg(a, 2), (c) => c < 0)
      : compare(arg(a, 0), arg(a, 1), (c) => c < 0),
  '<=': (a) =>
    a.length === 3
      ? compare(arg(a, 0), arg(a, 1), (c) => c <= 0) && compare(arg(a, 1), arg(a, 2), (c) => c <= 0)
      : compare(arg(a, 0), arg(a, 1), (c) => c <= 0),
  '+': (a) => num(a.reduce<number>((sum, x) => sum + parseFloatJs(x), 0)),
  '*': (a) => {
    if (a.length === 0) {
      return null;
    }
    if (a.length === 1) {
      return clone(arg(a, 0));
    }
    return num(a.slice(1).reduce<number>((product, x) => product * parseFloatJs(x), parseFloatJs(arg(a, 0))));
  },
  '-': (a) => (a.length === 1 ? num(-toNumber(arg(a, 0))) : num(toNumber(arg(a, 0)) - toNumber(arg(a, 1)))),
  '/': (a) => num(toNumber(arg(a, 0)) / toNumber(arg(a, 1))),
  '%': (a) => num(toNumber(arg(a, 0)) % toNumber(arg(a, 1))),
  max: (a) => (a.length === 0 ? null : num(Math.max(...a.map(toNumber)))),
  min: (a) => (a.length === 0 ? null : num(Math.min(...a.map(toNumber)))),
  cat: (a) => a.map((x) => (x === null ? '' : toJsString(x))).join(''),
  substr: (a) => {
    const source = toJsString(arg(a, 0));
    const start = toInteger(arg(a, 1));
    const temp = jsSubstr(source, start);
    if (a.length < 3 || arg(a, 2) === null) {
      return temp;
    }
    const end = toInteger(arg(a, 2));
    return end < 0 ? jsSubstr(temp, 0, Math.max(0, temp.length + end)) : jsSubstr(source, start, end);
  },
  in: (a) => {
    const needle = arg(a, 0);
    const haystack = arg(a, 1);
    if (Array.isArray(haystack)) {
      return haystack.some((item) => strictEquals(item, needle));
    }
    return typeof haystack === 'string' ? haystack.includes(toJsString(needle)) : false;
  },
  merge: (a) => a.flatMap((x) => (Array.isArray(x) ? x.map(clone) : [clone(x)])),
  log: (a) => clone(arg(a, 0)),
};

function filterItems(logic: JsonLogic, args: JsonValue[], data: JsonValue): JsonValue[] {
  const items = logic.evaluate(arg(args, 0), data);
  return Array.isArray(items) ? items.filter((item) => isTruthy(logic.evaluate(arg(args, 1), item))).map(clone) : [];
}

const lazyOperations: Record<string, LazyOperation> = {
  if: ifOp,
  '?:': ifOp,
  and: (logic, args, data) => {
    let current: JsonValue = null;
    for (const a of args) {
      current = logic.evaluate(a, data);
      if (!isTruthy(current)) {
        return current;
      }
    }
    return current;
  },
  or: (logic, args, data) => {
    let current: JsonValue = null;
    for (const a of args) {
      current = logic.evaluate(a, data);
      if (isTruthy(current)) {
        return current;
      }
    }
    return current;
  },
  map: (logic, args, data) => {
    const items = logic.evaluate(arg(args, 0), data);
    return Array.isArray(items) ? items.map((item) => clone(logic.evaluate(arg(args, 1), item))) : [];
  },
  filter: filterItems,
  reduce: (logic, args, data) => {
    let accumulator = logic.evaluate(arg(args, 2), data);
    const items = logic.evaluate(arg(args, 0), data);
    if (!Array.isArray(items)) {
      return accumulator;
    }
    for (const item of items) {
      accumulator = logic.evaluate(arg(args, 1), { current: clone(item), accumulator: clone(accumulator) });
    }
    return accumulator;
  },
  all: (logic, args, data) => {
    const items = logic.evaluate(arg(args, 0), data);
    return Array.isArray(items) && items.length > 0 && items.every((item) => isTruthy(logic.evaluate(arg(args, 1), item)));
  },
  none: (logic, args, data) => filterItems(logic, args, data).length === 0,
  some: (logic, args, data) => filterItems(logic, args, data).length > 0,
};

function ifOp(logic: JsonLogic, args: JsonValue[], data: JsonValue): JsonValue {
  let i = 0;
  for (; i < args.length - 1; i += 2) {
    if (isTruthy(logic.evaluate(arg(args, i), data))) {
      return logic.evaluate(arg(args, i + 1), data);
    }
  }
  return args.length === i + 1 ? logic.evaluate(arg(args, i), data) : null;
}

export class JsonLogic {
  private readonly custom: Record<string, JsonLogicOperation> = {};

  /**
   * Registers a platform-specific operation. Arguments are evaluated before the call.
   * The same operation must be registered in the .NET evaluator.
   */
  addOperation(name: string, operation: JsonLogicOperation): this {
    if (name in lazyOperations || name in builtIns) {
      throw new Error(`'${name}' is a built-in JSONLogic operation and cannot be replaced.`);
    }
    this.custom[name] = operation;
    return this;
  }

  evaluate(rule: JsonLogicRule, data: JsonValue = null): JsonValue {
    if (Array.isArray(rule)) {
      return rule.map((item) => clone(this.evaluate(item, data)));
    }
    if (!isLogic(rule)) {
      return clone(rule);
    }
    const [name] = Object.keys(rule);
    const raw = rule[name];
    const args: JsonValue[] = Array.isArray(raw) ? raw : [raw ?? null];

    const lazy = lazyOperations[name];
    if (lazy) {
      return lazy(this, args, data);
    }
    const operation = builtIns[name] ?? this.custom[name];
    if (!operation) {
      throw new JsonLogicError(`Unrecognized JSONLogic operation: '${name}'.`);
    }
    return operation(
      args.map((a) => this.evaluate(a, data)),
      data,
    );
  }

  evaluateTruthy(rule: JsonLogicRule, data: JsonValue = null): boolean {
    return isTruthy(this.evaluate(rule, data));
  }
}

/** Shared default evaluator. */
export const jsonLogic = new JsonLogic();
