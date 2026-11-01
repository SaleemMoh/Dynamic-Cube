import {
  ApiEndpointDefinition,
  ApiEndpointField,
  ApiEndpointParameter,
  ApiFunctionBinding,
  ApiFunctionInfo,
} from '@dynamic-cube/shared';
import {
  OPERATORS,
  QueryJoin,
  QuerySource,
  QuerySourceInfo,
} from '../apps-control/lovs/lov-builder-model';
import { outputName, placeholders } from './operation-designer-model';

/** The kinds of sources a read-data endpoint may read: classes and registered tables (never unregistered ones). */
export type EndpointSourceKind = 'CLASS' | 'DATA_OBJECT';

/** What a condition compares a column with: a fixed value, one of the endpoint's parameters, or the calling client. */
export type ConditionSource = 'VALUE' | 'PARAM' | 'USER';

/** The calling client's facts a condition may compare with (its companies and branches). */
export const CLIENT_FACTS = ['COMPANY', 'BRANCHES'];

export interface EndpointCondition {
  column: string;
  op: string;
  source: ConditionSource;
  value: string;
  param: string;
  user: string;
}

/** The read-data part as the designer edits it: one source, its joins, flat conditions, sort and distinct. */
export interface EndpointQuery {
  from: QuerySource;
  joins: QueryJoin[];
  match: 'ALL' | 'ANY';
  conditions: EndpointCondition[];
  sort: { column: string; direction: 'ASC' | 'DESC' }[];
  distinct: boolean;
}

export interface EndpointProblem {
  part: 'path' | 'data' | 'details';
  key: string;
  params?: Record<string, string | number>;
}

const CODE = /^[A-Z][A-Z0-9_-]{0,50}$/;
const NAME = /^[A-Za-z][A-Za-z0-9_]{0,63}$/;
const PATH = /^(\/([A-Za-z0-9_-]+|\{[A-Za-z][A-Za-z0-9_]{0,63}\}))+$/;
export const RESERVED = ['limit', 'offset'];
export const MAX_PAGE_SIZE = 1000;

export function emptyEndpointQuery(): EndpointQuery {
  return {
    from: { kind: 'CLASS', code: '', alias: 'a' },
    joins: [],
    match: 'ALL',
    conditions: [],
    sort: [],
    distinct: false,
  };
}

/** A new endpoint: a read-data endpoint with a page of 50 rows. */
export function emptyEndpoint(): ApiEndpointDefinition {
  return {
    nameAr: '',
    nameEn: '',
    path: '/',
    kind: 'QUERY',
    descriptionAr: null,
    descriptionEn: null,
    parameters: [],
    query: null,
    fields: [],
    singleRow: false,
    pageSize: 50,
    function: null,
    functionInputs: [],
  };
}

/** A stored endpoint with every list present. */
export function normalizeEndpoint(row: Record<string, unknown>): ApiEndpointDefinition {
  const base = emptyEndpoint();
  const def = { ...base, ...row } as ApiEndpointDefinition;
  return {
    ...def,
    parameters: (def.parameters ?? []).map((p) => ({ ...p })),
    fields: (def.fields ?? []).map((f) => ({ ...f })),
    functionInputs: (def.functionInputs ?? []).map((b) => ({ ...b })),
    pageSize: def.pageSize || 50,
  };
}

/** The builder's state from a stored query definition (conditions of one level; deeper groups are not offered). */
export function fromQuery(definition: unknown): EndpointQuery {
  const query = emptyEndpointQuery();
  const d = definition as Partial<{
    from: QuerySource;
    joins: QueryJoin[];
    where: { match?: string; conditions?: Record<string, unknown>[] } | null;
    sort: EndpointQuery['sort'];
    distinct: boolean;
  }> | null;
  if (!d?.from) return query;
  query.from = { ...d.from };
  query.joins = (d.joins ?? []).map((j) => ({ ...j, on: (j.on ?? []).map((o) => ({ ...o })) }));
  query.match = d.where?.match === 'ANY' ? 'ANY' : 'ALL';
  query.conditions = (d.where?.conditions ?? []).map((c) => ({
    column: String(c['column'] ?? ''),
    op: String(c['op'] ?? 'EQ'),
    source: c['param'] ? 'PARAM' : c['user'] ? 'USER' : 'VALUE',
    value: c['value'] === null || c['value'] === undefined ? '' : String(c['value']),
    param: String(c['param'] ?? ''),
    user: String(c['user'] ?? ''),
  }));
  query.sort = (d.sort ?? []).map((s) => ({ ...s }));
  query.distinct = !!d.distinct;
  return query;
}

/** The query definition the server compiles (the query builder's, without a value or labels: fields are chosen instead). */
export function toQuery(query: EndpointQuery): Record<string, unknown> {
  const conditions = query.conditions
    .filter((c) => c.column && c.op)
    .map((c) => {
      const condition: Record<string, unknown> = { column: c.column, op: c.op };
      if (c.op === 'IS_EMPTY' || c.op === 'IS_NOT_EMPTY') return condition;
      if (c.source === 'PARAM') condition['param'] = c.param;
      else if (c.source === 'USER') condition['user'] = c.user;
      else condition['value'] = c.value;
      return condition;
    });
  return {
    from: query.from,
    joins: query.joins,
    where: conditions.length ? { match: query.match, conditions, groups: [] } : null,
    sort: query.sort.filter((s) => s.column),
    distinct: query.distinct,
  };
}

/** Operators a condition on a column of this category may use (comparisons with the client: equals, in). */
export function operatorsFor(category: string | undefined, source: ConditionSource): string[] {
  if (source === 'USER') return ['EQ', 'IN'];
  const all = OPERATORS[(category as keyof typeof OPERATORS) ?? 'TEXT'] ?? OPERATORS.TEXT;
  return source === 'PARAM' ? all.filter((o) => o !== 'IS_EMPTY' && o !== 'IS_NOT_EMPTY') : all;
}

/** Every column of the query's sources as alias.column. */
export function endpointColumns(
  query: EndpointQuery,
  sources: QuerySourceInfo[],
): { ref: string; name: string; category: string; source: QuerySourceInfo }[] {
  const result: { ref: string; name: string; category: string; source: QuerySourceInfo }[] = [];
  for (const s of [query.from, ...query.joins]) {
    const source = sources.find((x) => x.kind === s.kind && x.code === s.code);
    for (const column of source?.columns ?? [])
      result.push({
        ref: `${s.alias}.${column.name}`,
        name: column.name,
        category: column.category,
        source: source!,
      });
  }
  return result;
}

/** A field reading the column, named after it (name_en → nameEn), unique among the fields. */
export function fieldFor(column: string, fields: ApiEndpointField[]): ApiEndpointField {
  const name = column.split('.').pop() ?? column;
  const camel = name.replace(/_([a-z0-9])/g, (_, c: string) => c.toUpperCase());
  return {
    name: outputName(
      camel,
      fields.map((f) => f.name),
    ),
    column,
    descriptionAr: null,
    descriptionEn: null,
  };
}

/** The class's usual fields: code, names and parent, in display order. */
export function classDefaults(alias: string): {
  fields: ApiEndpointField[];
  sort: EndpointQuery['sort'];
} {
  const fields: ApiEndpointField[] = [];
  for (const column of ['code', 'name_ar', 'name_en', 'parent_code'])
    fields.push(fieldFor(`${alias}.${column}`, fields));
  return { fields, sort: [{ column: `${alias}.display_order`, direction: 'ASC' }] };
}

/** Keeps the path parameters in line with the path: a new placeholder gets its parameter, a removed one loses it. */
export function syncParameters(
  parameters: ApiEndpointParameter[],
  path: string,
): ApiEndpointParameter[] {
  const names = placeholders(path).filter((n) => NAME.test(n));
  const kept = parameters.filter((p) => p.location !== 'PATH' || names.includes(p.name));
  const added = names
    .filter((n) => !kept.some((p) => p.location === 'PATH' && p.name === n))
    .map<ApiEndpointParameter>((name) => ({
      name,
      location: 'PATH',
      type: 'TEXT',
      isRequired: true,
      isMasked: false,
      descriptionAr: null,
      descriptionEn: null,
      sample: null,
    }));
  return [...kept, ...added];
}

/** The function's inputs, each bound as before, or to the parameter of the same name, or left for a value. */
export function bindInputs(
  fn: ApiFunctionInfo | undefined,
  current: ApiFunctionBinding[],
  parameters: ApiEndpointParameter[],
): ApiFunctionBinding[] {
  if (!fn) return [];
  return fn.inputs.map((input) => {
    const existing = current.find((b) => b.input === input.name);
    if (existing) return { ...existing };
    const parameter = parameters.find((p) => p.name === input.name);
    return { input: input.name, parameter: parameter?.name ?? null, value: null };
  });
}

/** The URL a partner calls, with the given values (or the placeholders) and the query parameters given. */
export function previewUrl(
  def: ApiEndpointDefinition,
  values: Record<string, string | null>,
  apiUrl = '',
): string {
  let path = def.path;
  for (const p of def.parameters.filter((x) => x.location === 'PATH')) {
    const value = values[p.name];
    path = path.replaceAll(`{${p.name}}`, value ? encodeURIComponent(value) : `{${p.name}}`);
  }
  const query = def.parameters
    .filter((p) => p.location === 'QUERY' && values[p.name])
    .map((p) => `${p.name}=${encodeURIComponent(values[p.name]!)}`);
  return `${apiUrl}${path}${query.length ? '?' + query.join('&') : ''}`;
}

/** The checks the server makes that can be made here (the server check covers the query and the rest). */
export function checkEndpoint(
  code: string,
  def: ApiEndpointDefinition,
  isNew: boolean,
  functions: ApiFunctionInfo[],
): EndpointProblem[] {
  const problems: EndpointProblem[] = [];
  const p = (part: EndpointProblem['part'], key: string, params?: EndpointProblem['params']) =>
    problems.push({ part, key, params });
  if (isNew && !CODE.test(code)) p('details', 'pa.problem.code');
  if (!def.nameAr?.trim() || !def.nameEn?.trim()) p('details', 'pa.problem.names');
  const path = def.path?.trim() ?? '';
  if (!PATH.test(path) || path.length > 300) p('path', 'pa.problem.path');
  const names = new Set<string>();
  def.parameters.forEach((param, i) => {
    const n = i + 1;
    if (!NAME.test(param.name)) p('path', 'pa.problem.paramName', { n });
    else if (names.has(param.name)) p('path', 'pa.problem.paramTwice', { name: param.name });
    else if (RESERVED.includes(param.name.toLowerCase()))
      p('path', 'pa.problem.paramReserved', { name: param.name });
    names.add(param.name);
  });
  if (def.kind === 'QUERY') {
    const query = fromQuery(def.query);
    if (!query.from.code) p('data', 'pa.problem.source');
    if (!def.fields.length) p('data', 'pa.problem.fields');
    const fieldNames = new Set<string>();
    def.fields.forEach((f, i) => {
      if (!NAME.test(f.name)) p('data', 'pa.problem.fieldName', { n: i + 1 });
      else if (fieldNames.has(f.name)) p('data', 'pa.problem.fieldTwice', { name: f.name });
      fieldNames.add(f.name);
    });
    query.conditions.forEach((c, i) => {
      if (c.source === 'PARAM' && !names.has(c.param))
        p('data', 'pa.problem.conditionParam', { n: i + 1 });
    });
    if (!(def.pageSize >= 1 && def.pageSize <= MAX_PAGE_SIZE))
      p('data', 'pa.problem.pageSize', { max: MAX_PAGE_SIZE });
  } else {
    const fn = functions.find((f) => f.code === def.function);
    if (!fn) p('data', 'pa.problem.function');
    else {
      for (const input of fn.inputs) {
        const binding = def.functionInputs.find((b) => b.input === input.name);
        const given =
          binding && (binding.parameter ? names.has(binding.parameter) : !!binding.value?.trim());
        if (input.required && !given) p('data', 'pa.problem.input', { name: input.name });
      }
    }
  }
  return problems;
}

/** The payload saved (and tried): trimmed, and only the part of the endpoint's kind. */
export function endpointPayload(
  code: string,
  def: ApiEndpointDefinition,
  query: EndpointQuery,
): ApiEndpointDefinition {
  const isQuery = def.kind === 'QUERY';
  return {
    ...def,
    code: code.trim() || null,
    nameAr: def.nameAr.trim(),
    nameEn: def.nameEn.trim(),
    path: def.path.trim(),
    parameters: def.parameters.map((p) => ({
      ...p,
      isRequired: p.location === 'PATH' || p.isRequired,
      sample: p.sample?.trim() || null,
    })),
    query: isQuery ? toQuery(query) : null,
    fields: isQuery ? def.fields : [],
    singleRow: isQuery && def.singleRow,
    function: isQuery ? null : def.function,
    functionInputs: isQuery
      ? []
      : def.functionInputs.map((b) => ({
          input: b.input,
          parameter: b.parameter || null,
          value: b.parameter ? null : b.value?.trim() || null,
        })),
  };
}
