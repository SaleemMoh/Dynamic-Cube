import { JsonLogicRule, JsonValue } from './jsonlogic';

/**
 * The Logic Builder's model of a JSONLogic rule: conditions as nested groups of comparisons, values as fields, fixed
 * values and calculations. Every rule can be read: what the form cannot show is kept as a `raw` piece (edited as JSON
 * under *Advanced*), so reading and writing a rule never changes what it does.
 */

export type CompareOp =
  | 'eq'
  | 'ne'
  | 'gt'
  | 'ge'
  | 'lt'
  | 'le'
  | 'between'
  | 'in'
  | 'notIn'
  | 'contains'
  | 'startsWith'
  | 'filled'
  | 'empty'
  | 'isTrue'
  | 'isFalse';

/** Operators with no value on the right. */
export const NO_VALUE_OPS: readonly CompareOp[] = ['filled', 'empty', 'isTrue', 'isFalse'];

/** Operators whose right side is a list of values. */
export const LIST_OPS: readonly CompareOp[] = ['in', 'notIn'];

export type MathOp = '+' | '-' | '*' | '/' | '%' | 'min' | 'max';

/** A true/false rule. */
export type LogicCondition =
  | { kind: 'group'; match: 'all' | 'any'; not: boolean; items: LogicCondition[] }
  | {
      kind: 'compare';
      left: LogicValue;
      op: CompareOp;
      right: LogicValue | null;
      to?: LogicValue | null;
      /** "Is yes" written as the field alone (`{"var": "x"}`, true when the value is truthy) rather than `== true`. */
      bare?: boolean;
    }
  /** Some, every or no row of a list (a repeating-rows answer, items…) meets a condition on its columns. */
  | { kind: 'rows'; quantifier: 'some' | 'all' | 'none'; list: string; where: LogicCondition }
  | { kind: 'raw'; rule: JsonLogicRule };

/** A rule that gives a value. */
export type LogicValue =
  | { kind: 'field'; path: string }
  | { kind: 'fixed'; value: JsonValue }
  | { kind: 'math'; op: MathOp; args: LogicValue[] }
  /** If … then …, else if …, otherwise …. */
  | { kind: 'choose'; cases: { when: LogicCondition; then: LogicValue }[]; otherwise: LogicValue }
  /** Sum of a column, or count, of the rows of a list (optionally only the rows meeting a condition). */
  | {
      kind: 'aggregate';
      fn: 'sum' | 'count';
      list: string;
      column: string | null;
      where: LogicCondition | null;
    }
  | { kind: 'join'; args: LogicValue[] }
  /** True or false from a condition (e.g. an output "eligible"). */
  | { kind: 'test'; condition: LogicCondition }
  | { kind: 'raw'; rule: JsonLogicRule };

type Obj = Record<string, JsonValue>;

function isObject(value: unknown): value is Obj {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function single(rule: JsonValue): [string, JsonValue] | null {
  if (!isObject(rule)) return null;
  const keys = Object.keys(rule);
  return keys.length === 1 ? [keys[0], rule[keys[0]]] : null;
}

function argsOf(value: JsonValue): JsonValue[] {
  return Array.isArray(value) ? value : [value];
}

/** The variable path of `{"var": "x"}` (or `{"var": ["x"]}`), else null. */
export function fieldPath(rule: JsonValue | undefined): string | null {
  if (rule === undefined) return null;
  const s = single(rule);
  if (!s || s[0] !== 'var') return null;
  const v = s[1];
  if (typeof v === 'string') return v;
  if (Array.isArray(v) && v.length === 1 && typeof v[0] === 'string') return v[0];
  return null;
}

function isScalar(value: JsonValue | undefined): boolean {
  return value === null || (value !== undefined && typeof value !== 'object');
}

function isFixed(value: JsonValue | undefined): boolean {
  if (value === undefined) return false;
  return isScalar(value) || (Array.isArray(value) && value.every(isScalar));
}

// ---------------------------------------------------------------- reading

const COMPARE: Record<string, CompareOp> = {
  '==': 'eq',
  '!=': 'ne',
  '>': 'gt',
  '>=': 'ge',
  '<': 'lt',
  '<=': 'le',
};

/** Reads a true/false rule. Empty (null) is an empty "all" group, which is always true. */
export function readCondition(rule: JsonLogicRule | null | undefined): LogicCondition {
  if (rule === null || rule === undefined)
    return { kind: 'group', match: 'all', not: false, items: [] };
  return condition(rule as JsonValue);
}

function condition(rule: JsonValue): LogicCondition {
  const bare = fieldPath(rule);
  if (bare !== null)
    return {
      kind: 'compare',
      left: { kind: 'field', path: bare },
      op: 'isTrue',
      right: null,
      bare: true,
    };
  const s = single(rule);
  if (!s) return { kind: 'raw', rule };
  const [op, raw] = s;
  if (op === 'and' || op === 'or') {
    if (!Array.isArray(raw)) return { kind: 'raw', rule };
    return {
      kind: 'group',
      match: op === 'and' ? 'all' : 'any',
      not: false,
      items: raw.map(condition),
    };
  }
  if (op === '!' || op === '!!') {
    const inner = argsOf(raw)[0];
    const path = fieldPath(inner);
    if (path !== null)
      return compare({ kind: 'field', path }, op === '!!' ? 'filled' : 'empty', null);
    if (op === '!') {
      const innerCondition = condition(inner);
      if (innerCondition.kind === 'group' && !innerCondition.not)
        return { ...innerCondition, not: true };
      if (innerCondition.kind === 'compare' && innerCondition.op === 'in')
        return { ...innerCondition, op: 'notIn' };
      if (innerCondition.kind !== 'raw')
        return { kind: 'group', match: 'all', not: true, items: [innerCondition] };
    }
    return { kind: 'raw', rule };
  }
  if (op === 'some' || op === 'all' || op === 'none') {
    const [list, where] = argsOf(raw);
    const path = fieldPath(list);
    if (path !== null && where !== undefined)
      return { kind: 'rows', quantifier: op, list: path, where: condition(where) };
    return { kind: 'raw', rule };
  }
  const args = argsOf(raw);
  if (op === 'in' && args.length === 2) {
    const [needle, haystack] = args;
    if (Array.isArray(haystack) && isFixed(haystack))
      return compare(value(needle), 'in', { kind: 'fixed', value: haystack });
    if (
      !Array.isArray(haystack) &&
      !(isFixed(needle) && fieldPath(haystack) === null && isFixed(haystack))
    ) {
      return compare(value(haystack), 'contains', value(needle));
    }
    return { kind: 'raw', rule };
  }
  if ((op === '<' || op === '<=') && args.length === 3) {
    if (op === '<=')
      return {
        kind: 'compare',
        left: value(args[1]),
        op: 'between',
        right: value(args[0]),
        to: value(args[2]),
      };
    return { kind: 'raw', rule };
  }
  if (op in COMPARE && args.length === 2) {
    const left = value(args[0]);
    // starts with: {"==": [{"substr": [x, 0, n]}, "text of n letters"]}
    if (op === '==' && typeof args[1] === 'string') {
      const sub = single(args[0]);
      if (sub && sub[0] === 'substr') {
        const subArgs = argsOf(sub[1]);
        if (subArgs.length === 3 && subArgs[1] === 0 && subArgs[2] === args[1].length) {
          return compare(value(subArgs[0]), 'startsWith', { kind: 'fixed', value: args[1] });
        }
      }
    }
    if (op === '==' && (args[1] === true || args[1] === false))
      return compare(left, args[1] ? 'isTrue' : 'isFalse', null);
    return compare(left, COMPARE[op], value(args[1]));
  }
  return { kind: 'raw', rule };
}

function compare(left: LogicValue, op: CompareOp, right: LogicValue | null): LogicCondition {
  return { kind: 'compare', left, op, right };
}

const MATH: readonly string[] = ['+', '-', '*', '/', '%', 'min', 'max'];

/** Reads a rule that gives a value. Empty (null) is an empty fixed value. */
export function readValue(rule: JsonLogicRule | null | undefined): LogicValue {
  if (rule === undefined) return { kind: 'fixed', value: null };
  return value(rule as JsonValue);
}

function value(rule: JsonValue): LogicValue {
  if (isFixed(rule)) return { kind: 'fixed', value: rule };
  const path = fieldPath(rule);
  if (path !== null) return { kind: 'field', path };
  const s = single(rule);
  if (!s) return { kind: 'raw', rule };
  const [op, raw] = s;
  const args = argsOf(raw);
  if (MATH.includes(op) && args.length >= 1)
    return { kind: 'math', op: op as MathOp, args: args.map(value) };
  if (op === 'cat') return { kind: 'join', args: args.map(value) };
  if (op === 'if' || op === '?:') {
    if (args.length < 3 || args.length % 2 === 0) return { kind: 'raw', rule };
    const cases: { when: LogicCondition; then: LogicValue }[] = [];
    for (let i = 0; i + 1 < args.length; i += 2)
      cases.push({ when: condition(args[i]), then: value(args[i + 1]) });
    return { kind: 'choose', cases, otherwise: value(args[args.length - 1]) };
  }
  if (op === 'reduce') {
    const aggregate = readAggregate(args);
    if (aggregate) return aggregate;
  }
  const asCondition = condition(rule);
  if (asCondition.kind !== 'raw') return { kind: 'test', condition: asCondition };
  return { kind: 'raw', rule };
}

/**
 * Sum: `{"reduce": [LIST, {"+": [{"var": "accumulator"}, {"var": "current.COL"}]}, 0]}`;
 * count: `{"reduce": [LIST, {"+": [{"var": "accumulator"}, 1]}, 0]}`; LIST may be `{"filter": [list, condition]}`.
 */
function readAggregate(args: JsonValue[]): LogicValue | null {
  if (args.length !== 3 || args[2] !== 0) return null;
  let list = fieldPath(args[0]);
  let where: LogicCondition | null = null;
  if (list === null) {
    const f = single(args[0]);
    if (!f || f[0] !== 'filter') return null;
    const [inner, cond] = argsOf(f[1]);
    list = fieldPath(inner);
    if (list === null || cond === undefined) return null;
    where = condition(cond);
  }
  const step = single(args[1]);
  if (!step || step[0] !== '+') return null;
  const parts = argsOf(step[1]);
  if (parts.length !== 2 || fieldPath(parts[0]) !== 'accumulator') return null;
  if (parts[1] === 1) return { kind: 'aggregate', fn: 'count', list, column: null, where };
  const current = fieldPath(parts[1]);
  if (current === null || !current.startsWith('current.')) return null;
  return { kind: 'aggregate', fn: 'sum', list, column: current.slice('current.'.length), where };
}

// ---------------------------------------------------------------- writing

/** Writes a condition as JSONLogic; null when it has nothing in it (always true). */
export function writeCondition(node: LogicCondition): JsonLogicRule | null {
  const rule = conditionRule(node);
  return rule === undefined ? null : rule;
}

function conditionRule(node: LogicCondition): JsonValue | undefined {
  switch (node.kind) {
    case 'raw':
      return node.rule as JsonValue;
    case 'group': {
      const items = node.items.map(conditionRule).filter((r): r is JsonValue => r !== undefined);
      if (items.length === 0) return node.not ? { '!': [true] } : undefined;
      const inner =
        items.length === 1 ? items[0] : { [node.match === 'all' ? 'and' : 'or']: items };
      return node.not ? { '!': [inner] } : inner;
    }
    case 'rows': {
      const where = conditionRule(node.where);
      return { [node.quantifier]: [{ var: node.list }, where === undefined ? true : where] };
    }
    case 'compare':
      return compareRule(node);
  }
}

function compareRule(node: Extract<LogicCondition, { kind: 'compare' }>): JsonValue | undefined {
  if (node.left.kind === 'field' && node.left.path.trim() === '') return undefined;
  const left = valueRule(node.left);
  const right = node.right ? valueRule(node.right) : null;
  switch (node.op) {
    case 'filled':
      return { '!!': [left] };
    case 'empty':
      return { '!': [left] };
    case 'isTrue':
      return node.bare ? left : { '==': [left, true] };
    case 'isFalse':
      return { '==': [left, false] };
    case 'in':
      return { in: [left, Array.isArray(right) ? right : right === null ? [] : [right]] };
    case 'notIn':
      return {
        '!': [{ in: [left, Array.isArray(right) ? right : right === null ? [] : [right]] }],
      };
    case 'contains':
      return { in: [right, left] };
    case 'startsWith': {
      const text = typeof right === 'string' ? right : String(right ?? '');
      return { '==': [{ substr: [left, 0, text.length] }, text] };
    }
    case 'between':
      return { '<=': [right, left, node.to ? valueRule(node.to) : null] };
    default: {
      const op = Object.entries(COMPARE).find(([, v]) => v === node.op)![0];
      return { [op]: [left, right] };
    }
  }
}

/** Writes a value rule as JSONLogic. */
export function writeValue(node: LogicValue): JsonLogicRule | null {
  return valueRule(node) as JsonLogicRule | null;
}

function valueRule(node: LogicValue): JsonValue {
  switch (node.kind) {
    case 'field':
      return { var: node.path };
    case 'fixed':
      return node.value;
    case 'raw':
      return node.rule as JsonValue;
    case 'math':
      return { [node.op]: node.args.map(valueRule) };
    case 'join':
      return { cat: node.args.map(valueRule) };
    case 'test':
      return conditionRule(node.condition) ?? true;
    case 'choose': {
      const args: JsonValue[] = [];
      for (const c of node.cases) args.push(conditionRule(c.when) ?? true, valueRule(c.then));
      args.push(valueRule(node.otherwise));
      return { if: args };
    }
    case 'aggregate': {
      const where = node.where ? conditionRule(node.where) : undefined;
      const list: JsonValue =
        where === undefined ? { var: node.list } : { filter: [{ var: node.list }, where] };
      const step: JsonValue =
        node.fn === 'count'
          ? { '+': [{ var: 'accumulator' }, 1] }
          : { '+': [{ var: 'accumulator' }, { var: `current.${node.column ?? ''}` }] };
      return { reduce: [list, step, 0] };
    }
  }
}

// ---------------------------------------------------------------- describing

/** Words used to describe a rule in a sentence, in the user's language. */
export interface LogicWords {
  and: string;
  or: string;
  not: string;
  always: string;
  empty: string;
  op: Record<CompareOp, string>;
  quantifier: Record<'some' | 'all' | 'none', string>;
  rowsOf: string;
  where: string;
  sumOf: string;
  countOf: string;
  if: string;
  then: string;
  otherwise: string;
  advanced: string;
  yes: string;
  no: string;
  joined: string;
  /** The name of a value of a field with choices (e.g. a region's name for its code), when known. */
  valueName?: (path: string, value: string) => string | undefined;
}

/** Describes a condition in plain words, e.g. "Owner is Government and Value is more than 10". */
export function describeCondition(
  node: LogicCondition,
  words: LogicWords,
  label: (path: string) => string,
  depth = 0,
): string {
  switch (node.kind) {
    case 'raw':
      return words.advanced;
    case 'rows':
      return `${words.quantifier[node.quantifier]} ${words.rowsOf} ${label(node.list)} ${words.where} ${describeCondition(node.where, words, (p) => label(`${node.list}.${p}`), depth + 1)}`;
    case 'compare': {
      const left = describeValue(node.left, words, label, depth + 1);
      if (NO_VALUE_OPS.includes(node.op)) return `${left} ${words.op[node.op]}`;
      const right = !node.right
        ? '…'
        : node.left.kind === 'field' && node.right.kind === 'fixed' && words.valueName
          ? named(node.right.value, node.left.path, words)
          : describeValue(node.right, words, label, depth + 1);
      if (node.op === 'between') {
        const to = node.to ? describeValue(node.to, words, label, depth + 1) : '…';
        return `${left} ${words.op.between} ${right} ${words.and} ${to}`;
      }
      return `${left} ${words.op[node.op]} ${right}`;
    }
    case 'group': {
      const parts = node.items
        .map((i) => describeCondition(i, words, label, depth + 1))
        .filter((p) => p);
      let text =
        parts.length === 0
          ? words.always
          : parts.join(` ${node.match === 'all' ? words.and : words.or} `);
      if (parts.length > 1 && depth > 0) text = `(${text})`;
      return node.not
        ? `${words.not} ${parts.length > 1 && depth === 0 ? `(${text})` : text}`
        : text;
    }
  }
}

/** A fixed value said by the names of the field's choices ("Makkah region" rather than "MAKKAH"). */
function named(value: JsonValue, path: string, words: LogicWords): string {
  const one = (v: JsonValue) =>
    v === null || typeof v === 'object' ? undefined : words.valueName?.(path, String(v));
  if (Array.isArray(value)) return value.map((v) => one(v) ?? String(v)).join('، ');
  return one(value) ?? describeValue({ kind: 'fixed', value }, words, (p) => p);
}

export function describeValue(
  node: LogicValue,
  words: LogicWords,
  label: (path: string) => string,
  depth = 0,
): string {
  switch (node.kind) {
    case 'field':
      return node.path ? label(node.path) : '…';
    case 'fixed':
      if (node.value === null || node.value === '') return words.empty;
      if (node.value === true) return words.yes;
      if (node.value === false) return words.no;
      if (Array.isArray(node.value)) return node.value.map((v) => String(v)).join('، ');
      return typeof node.value === 'string' ? `"${node.value}"` : String(node.value);
    case 'raw':
      return words.advanced;
    case 'math': {
      const sign = node.op === '*' ? '×' : node.op === '/' ? '÷' : node.op;
      if (node.op === 'min' || node.op === 'max')
        return `${node.op}(${node.args.map((a) => describeValue(a, words, label, depth + 1)).join(', ')})`;
      const text = node.args
        .map((a) => describeValue(a, words, label, depth + 1))
        .join(` ${sign} `);
      return depth > 0 && node.args.length > 1 ? `(${text})` : text;
    }
    case 'join':
      return `${words.joined}(${node.args.map((a) => describeValue(a, words, label, depth + 1)).join(', ')})`;
    case 'test':
      return describeCondition(node.condition, words, label, depth + 1);
    case 'aggregate': {
      const head =
        node.fn === 'count'
          ? `${words.countOf} ${label(node.list)}`
          : `${words.sumOf} ${node.column ? label(`${node.list}.${node.column}`) : '…'} ${words.rowsOf} ${label(node.list)}`;
      return node.where
        ? `${head} ${words.where} ${describeCondition(node.where, words, (p) => label(`${node.list}.${p}`), depth + 1)}`
        : head;
    }
    case 'choose': {
      const cases = node.cases.map(
        (c) =>
          `${words.if} ${describeCondition(c.when, words, label, depth + 1)} ${words.then} ${describeValue(c.then, words, label, depth + 1)}`,
      );
      return `${cases.join('، ')}، ${words.otherwise} ${describeValue(node.otherwise, words, label, depth + 1)}`;
    }
  }
}

/** True when the form shows the whole rule (no piece is kept as JSON). */
export function fullyVisual(node: LogicCondition | LogicValue): boolean {
  switch (node.kind) {
    case 'raw':
      return false;
    case 'group':
      return node.items.every(fullyVisual);
    case 'rows':
      return fullyVisual(node.where);
    case 'compare':
      return (
        fullyVisual(node.left) &&
        (node.right === null || fullyVisual(node.right)) &&
        (!node.to || fullyVisual(node.to))
      );
    case 'math':
    case 'join':
      return node.args.every(fullyVisual);
    case 'test':
      return fullyVisual(node.condition);
    case 'choose':
      return (
        node.cases.every((c) => fullyVisual(c.when) && fullyVisual(c.then)) &&
        fullyVisual(node.otherwise)
      );
    case 'aggregate':
      return node.where === null || fullyVisual(node.where);
    default:
      return true;
  }
}
