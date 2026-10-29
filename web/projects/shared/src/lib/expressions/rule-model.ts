import { JsonLogicRule, JsonValue } from './jsonlogic';

/** Comparisons the visual editor writes; anything else stays as JSON. */
export const RULE_OPERATORS = ['==', '!=', '>', '>=', '<', '<=', 'in', 'contains', 'filled', 'empty'] as const;
export type RuleOperator = (typeof RULE_OPERATORS)[number];

/** Operators that take no value. */
export const UNARY_OPERATORS: readonly RuleOperator[] = ['filled', 'empty'];

export interface RuleCondition {
  variable: string;
  operator: RuleOperator;
  /** A list for `in`, otherwise one value. */
  value: JsonValue;
}

/** A condition as the visual editor shows it: all or any of a list of comparisons. */
export interface RuleGroup {
  match: 'all' | 'any';
  conditions: RuleCondition[];
}

function isObject(value: unknown): value is Record<string, JsonValue> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** The variable path of `{"var": "x"}` (or `{"var": ["x"]}`), else null. */
export function varPath(rule: JsonValue | undefined): string | null {
  if (!isObject(rule) || Object.keys(rule).length !== 1 || !('var' in rule)) return null;
  const v = rule['var'];
  if (typeof v === 'string') return v;
  if (Array.isArray(v) && typeof v[0] === 'string' && v.length === 1) return v[0];
  return null;
}

function isLiteral(value: JsonValue | undefined): boolean {
  if (value === undefined) return false;
  if (Array.isArray(value)) return value.every((v) => v === null || typeof v !== 'object');
  return value === null || typeof value !== 'object';
}

function condition(rule: JsonValue): RuleCondition | null {
  if (!isObject(rule) || Object.keys(rule).length !== 1) return null;
  const [op] = Object.keys(rule);
  const args = rule[op];
  if (op === '!!' || op === '!') {
    const inner = Array.isArray(args) ? args[0] : args;
    const variable = varPath(inner);
    return variable !== null ? { variable, operator: op === '!!' ? 'filled' : 'empty', value: null } : null;
  }
  if (!Array.isArray(args) || args.length !== 2) return null;
  if (op === 'in') {
    const left = varPath(args[0]);
    if (left !== null && Array.isArray(args[1]) && isLiteral(args[1])) return { variable: left, operator: 'in', value: args[1] };
    const right = varPath(args[1]);
    if (right !== null && isLiteral(args[0]) && !Array.isArray(args[0])) return { variable: right, operator: 'contains', value: args[0] };
    return null;
  }
  if (!(['==', '!=', '>', '>=', '<', '<='] as string[]).includes(op)) return null;
  const variable = varPath(args[0]);
  if (variable === null || !isLiteral(args[1]) || Array.isArray(args[1])) return null;
  return { variable, operator: op as RuleOperator, value: args[1] };
}

/**
 * Reads a rule into the visual form when it is one comparison, or all / any of comparisons of a variable with a
 * value; null when it is something else (it is then edited as JSON). An empty rule is an empty group.
 */
export function parseRule(rule: JsonLogicRule | null | undefined): RuleGroup | null {
  if (rule === null || rule === undefined) return { match: 'all', conditions: [] };
  if (isObject(rule) && Object.keys(rule).length === 1 && ('and' in rule || 'or' in rule)) {
    const list = rule['and'] ?? rule['or'];
    if (!Array.isArray(list)) return null;
    const conditions = list.map(condition);
    return conditions.every((c) => c !== null) ? { match: 'and' in rule ? 'all' : 'any', conditions: conditions as RuleCondition[] } : null;
  }
  const single = condition(rule);
  return single ? { match: 'all', conditions: [single] } : null;
}

function write(c: RuleCondition): JsonLogicRule {
  const v = { var: c.variable };
  switch (c.operator) {
    case 'filled':
      return { '!!': [v] };
    case 'empty':
      return { '!': [v] };
    case 'in':
      return { in: [v, Array.isArray(c.value) ? c.value : [c.value]] };
    case 'contains':
      return { in: [c.value, v] };
    default:
      return { [c.operator]: [v, c.value] };
  }
}

/** Writes the visual form back as JSONLogic; null when there are no conditions. */
export function buildRule(group: RuleGroup): JsonLogicRule | null {
  const complete = group.conditions.filter((c) => c.variable.trim() !== '');
  if (complete.length === 0) return null;
  if (complete.length === 1) return write(complete[0]);
  return { [group.match === 'all' ? 'and' : 'or']: complete.map(write) };
}
