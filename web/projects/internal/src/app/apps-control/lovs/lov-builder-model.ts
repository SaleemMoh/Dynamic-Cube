/** The visual query builder's model of a list of values (source type QUERY) and its conversions. */

export type SourceKind = 'CLASS' | 'DATA_OBJECT';
export type ColumnCategory = 'TEXT' | 'NUMBER' | 'DATE' | 'BOOL' | 'JSON';

export interface SourceColumn {
  name: string;
  dataType: string;
  category: ColumnCategory;
  labelAr: string | null;
  labelEn: string | null;
}

export interface QuerySourceInfo {
  kind: SourceKind;
  code: string;
  nameAr: string;
  nameEn: string;
  schema: string;
  dbObject: string;
  columns: SourceColumn[];
}

export interface QuerySource {
  kind: SourceKind;
  code: string;
  alias: string;
}

export interface QueryJoin extends QuerySource {
  type: 'INNER' | 'LEFT';
  on: { left: string; right: string }[];
}

export interface QueryCondition {
  column: string;
  op: string;
  value?: unknown;
  user?: string | null;
}

export interface QueryGroup {
  match: 'ALL' | 'ANY';
  conditions: QueryCondition[];
  groups: QueryGroup[];
}

export interface QueryDefinition {
  from: QuerySource;
  joins: QueryJoin[];
  map: {
    value: string | null;
    labelAr: string | null;
    labelEn: string | null;
    parent: string | null;
  };
  where: QueryGroup | null;
  sort: { column: string; direction: 'ASC' | 'DESC' }[];
  distinct: boolean;
}

/** The builder's working state: the filter (Filter step) and the conditions on the user's data (Access step) apart. */
export interface BuilderQuery {
  from: QuerySource;
  joins: QueryJoin[];
  map: QueryDefinition['map'];
  filter: QueryGroup;
  userConditions: QueryCondition[];
  sort: QueryDefinition['sort'];
  distinct: boolean;
}

export const OPERATORS: Record<ColumnCategory, string[]> = {
  TEXT: [
    'EQ',
    'NE',
    'CONTAINS',
    'STARTS_WITH',
    'ENDS_WITH',
    'IN',
    'NOT_IN',
    'IS_EMPTY',
    'IS_NOT_EMPTY',
  ],
  NUMBER: ['EQ', 'NE', 'GT', 'GE', 'LT', 'LE', 'IN', 'NOT_IN', 'IS_EMPTY', 'IS_NOT_EMPTY'],
  DATE: ['EQ', 'NE', 'GT', 'GE', 'LT', 'LE', 'IS_EMPTY', 'IS_NOT_EMPTY'],
  BOOL: ['EQ', 'NE', 'IS_EMPTY', 'IS_NOT_EMPTY'],
  JSON: ['IS_EMPTY', 'IS_NOT_EMPTY'],
};

/** Operators that take no value. */
export const NO_VALUE = ['IS_EMPTY', 'IS_NOT_EMPTY'];

/** Facts of the signed-in user a condition may compare with. */
export const USER_FACTS = [
  'COMPANY',
  'BRANCHES',
  'ORG_UNIT',
  'ORG_UNITS',
  'SUBTREE_ORG_UNITS',
  'USERNAME',
  'APP',
  'ROLES',
];

/** What a role's grant may use as "values from the user's data" (JSONLogic over the user context). */
export const RULE_FACTS = [
  'user.orgUnit',
  'user.orgUnits',
  'user.subtreeOrgUnits',
  'user.roles',
  'user.positions',
  'user.username',
  'company',
];

export function emptyGroup(match: 'ALL' | 'ANY' = 'ALL'): QueryGroup {
  return { match, conditions: [], groups: [] };
}

export function emptyQuery(): BuilderQuery {
  return {
    from: { kind: 'CLASS', code: '', alias: 'a' },
    joins: [],
    map: { value: null, labelAr: null, labelEn: null, parent: null },
    filter: emptyGroup(),
    userConditions: [],
    sort: [],
    distinct: false,
  };
}

/** The next free alias: a, b, c… */
export function nextAlias(query: BuilderQuery): string {
  const used = new Set([query.from.alias, ...query.joins.map((j) => j.alias)]);
  for (let i = 0; i < 26; i++) {
    const alias = String.fromCharCode(97 + i);
    if (!used.has(alias)) return alias;
  }
  return 's' + used.size;
}

/**
 * The definition saved on the list. Conditions on the user's data always hold (top-level ALL), whatever the filter's
 * own match, so a filter of "any" can never widen what a user may see.
 */
export function toDefinition(query: BuilderQuery): QueryDefinition {
  const filter = pruneGroup(query.filter);
  const users = query.userConditions
    .filter((c) => c.column && c.user)
    .map((c) => ({ column: c.column, op: c.op, user: c.user }));
  const where: QueryGroup | null =
    users.length || filter
      ? { match: 'ALL', conditions: users, groups: filter ? [filter] : [] }
      : null;
  return {
    from: query.from,
    joins: query.joins,
    map: {
      value: query.map.value || null,
      labelAr: query.map.labelAr || null,
      labelEn: query.map.labelEn || null,
      parent: query.map.parent || null,
    },
    where,
    sort: query.sort.filter((s) => s.column),
    distinct: query.distinct,
  };
}

/** The builder's state from a saved definition (see {@link toDefinition}). */
export function fromDefinition(
  definition: Partial<QueryDefinition> | null | undefined,
): BuilderQuery {
  const query = emptyQuery();
  if (!definition?.from) return query;
  query.from = { ...definition.from };
  query.joins = (definition.joins ?? []).map((j) => ({
    ...j,
    on: (j.on ?? []).map((p) => ({ ...p })),
  }));
  query.map = { value: null, labelAr: null, labelEn: null, parent: null, ...definition.map };
  query.sort = (definition.sort ?? []).map((s) => ({ ...s }));
  query.distinct = !!definition.distinct;
  const where = definition.where;
  if (where) {
    const top = normalizeGroup(where);
    if (top.match === 'ALL') {
      query.userConditions = top.conditions.filter((c) => c.user);
      const plain = top.conditions.filter((c) => !c.user);
      query.filter =
        !plain.length && top.groups.length === 1
          ? top.groups[0]
          : { match: 'ALL', conditions: plain, groups: top.groups };
    } else {
      query.filter = top;
    }
  }
  return query;
}

/** A list made before the builder (a class, a data object), as a query; null for SQL lists. */
export function fromLegacy(row: Record<string, unknown>): BuilderQuery | null {
  const query = emptyQuery();
  switch (row['sourceType']) {
    case 'QUERY':
      return fromDefinition(row['query'] as QueryDefinition | null);
    case 'CLASS':
      if (!row['classCode']) return query;
      query.from = { kind: 'CLASS', code: String(row['classCode']), alias: 'a' };
      query.map = {
        value: 'a.code',
        labelAr: 'a.name_ar',
        labelEn: 'a.name_en',
        parent: 'a.parent_code',
      };
      query.sort = [
        { column: 'a.display_order', direction: 'ASC' },
        { column: 'a.name_ar', direction: 'ASC' },
      ];
      return query;
    case 'DATA_OBJECT': {
      if (!row['dataObjectCode']) return query;
      query.from = { kind: 'DATA_OBJECT', code: String(row['dataObjectCode']), alias: 'a' };
      const column = (key: string) => (row[key] ? `a.${row[key]}` : null);
      query.map = {
        value: column('valueColumn'),
        labelAr: column('displayArColumn'),
        labelEn: column('displayEnColumn'),
        parent: column('parentColumn'),
      };
      return query;
    }
    default:
      return row['sourceType'] ? null : query;
  }
}

/** Every column of the query's sources as `alias.column`, with its source and category. */
export function columnsOf(
  query: BuilderQuery,
  sources: QuerySourceInfo[],
): { ref: string; alias: string; source: QuerySourceInfo; column: SourceColumn }[] {
  const result: { ref: string; alias: string; source: QuerySourceInfo; column: SourceColumn }[] =
    [];
  for (const s of [query.from, ...query.joins]) {
    const source = sources.find((x) => x.kind === s.kind && x.code === s.code);
    if (!source) continue;
    for (const column of source.columns)
      result.push({ ref: `${s.alias}.${column.name}`, alias: s.alias, source, column });
  }
  return result;
}

/** A role's own grant on the list, as the builder edits it. */
export interface RoleGrantRow {
  roleCode: string;
  nameAr: string;
  nameEn: string;
  grantType: 'ALL' | 'VALUES' | 'RULE' | 'NONE' | null;
  grantedValues: string[];
  rule: unknown;
  defaultValue: string | null;
  otherGrants: number;
}

/** The role's grants with its own grant on the list replaced (screen and company grants kept as they are). */
export function withGrant(
  grants: Record<string, unknown>[] | undefined,
  lovCode: string,
  row: RoleGrantRow,
): Record<string, unknown>[] {
  const kept = (grants ?? []).filter((g) => !(g['lovCode'] === lovCode && !g['resourceCode']));
  if (!row.grantType) return kept;
  return [
    ...kept,
    {
      lovCode,
      resourceCode: null,
      grantType: row.grantType,
      grantedValues: row.grantType === 'VALUES' ? row.grantedValues : [],
      rule: row.grantType === 'RULE' ? row.rule : null,
      defaultValue: row.defaultValue,
    },
  ];
}

/** Which step of the builder a problem path belongs to. */
export function stepOf(field: string): number {
  if (field.startsWith('query.from')) return 0;
  if (field.startsWith('query.joins')) return 1;
  if (field.startsWith('query.map')) return 2;
  if (field.startsWith('query.where.conditions')) return 5;
  if (field.startsWith('query.where')) return 3;
  if (field.startsWith('query.sort')) return 4;
  if (field === 'query') return 0;
  return 6;
}

function pruneGroup(group: QueryGroup): QueryGroup | null {
  const conditions = group.conditions
    .filter((c) => c.column && c.op)
    .map((c) => ({ column: c.column, op: c.op, value: NO_VALUE.includes(c.op) ? null : c.value }));
  const groups = group.groups.map(pruneGroup).filter((g): g is QueryGroup => g !== null);
  return conditions.length || groups.length ? { match: group.match, conditions, groups } : null;
}

function normalizeGroup(group: Partial<QueryGroup>): QueryGroup {
  return {
    match: group.match === 'ANY' ? 'ANY' : 'ALL',
    conditions: (group.conditions ?? []).map((c) => ({ ...c })),
    groups: (group.groups ?? []).map(normalizeGroup),
  };
}
