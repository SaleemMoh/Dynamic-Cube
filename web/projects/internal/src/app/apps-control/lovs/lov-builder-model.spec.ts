import {
  BuilderQuery,
  QuerySourceInfo,
  dataObjectCode,
  emptyGroup,
  emptyQuery,
  fromDefinition,
  fromLegacy,
  nextAlias,
  registerTables,
  stepOf,
  toDefinition,
  withGrant,
} from './lov-builder-model';

describe('LOV builder model', () => {
  const query = (): BuilderQuery => ({
    ...emptyQuery(),
    from: { kind: 'CLASS', code: 'SECTOR', alias: 'a' },
    joins: [
      {
        kind: 'DATA_OBJECT',
        code: 'BRANCHES',
        alias: 'b',
        type: 'LEFT',
        on: [{ left: 'a.code', right: 'b.sector' }],
      },
    ],
    map: { value: 'a.code', labelAr: 'a.name_ar', labelEn: 'b.name_en', parent: null },
    filter: {
      match: 'ANY',
      conditions: [
        { column: 'a.active_since', op: 'GE', value: 2000 },
        { column: '', op: 'EQ' },
      ],
      groups: [],
    },
    userConditions: [{ column: 'b.branch', op: 'IN', user: 'BRANCHES' }],
    sort: [{ column: 'a.name_ar', direction: 'ASC' }],
  });

  it('keeps conditions on the user data outside an "any" filter, so they always hold', () => {
    const definition = toDefinition(query());
    expect(definition.where).toEqual({
      match: 'ALL',
      conditions: [{ column: 'b.branch', op: 'IN', user: 'BRANCHES' }],
      groups: [
        {
          match: 'ANY',
          conditions: [{ column: 'a.active_since', op: 'GE', value: 2000 }],
          groups: [],
        },
      ],
    });
  });

  it('reads back what it saves', () => {
    const back = fromDefinition(toDefinition(query()));
    expect(back.userConditions).toEqual([{ column: 'b.branch', op: 'IN', user: 'BRANCHES' }]);
    expect(back.filter.match).toBe('ANY');
    expect(back.filter.conditions).toHaveLength(1);
    expect(back.joins[0].on[0]).toEqual({ left: 'a.code', right: 'b.sector' });
  });

  it('saves no condition block when there is nothing to filter', () => {
    expect(toDefinition({ ...query(), filter: emptyGroup(), userConditions: [] }).where).toBeNull();
  });

  it('opens class and data object lists as queries, and leaves SQL lists to the SQL editor', () => {
    const cls = fromLegacy({ sourceType: 'CLASS', classCode: 'REGION' })!;
    expect(cls.from).toEqual({ kind: 'CLASS', code: 'REGION', alias: 'a' });
    expect(cls.map).toEqual({
      value: 'a.code',
      labelAr: 'a.name_ar',
      labelEn: 'a.name_en',
      parent: 'a.parent_code',
    });
    const data = fromLegacy({
      sourceType: 'DATA_OBJECT',
      dataObjectCode: 'X',
      valueColumn: 'id',
      displayArColumn: 'ar',
      displayEnColumn: 'en',
    })!;
    expect(data.map.value).toBe('a.id');
    expect(data.map.parent).toBeNull();
    expect(fromLegacy({ sourceType: 'SQL', sqlText: 'select 1' })).toBeNull();
  });

  it('replaces only the role’s own grant on the list', () => {
    const grants = [
      { lovCode: 'L', resourceCode: null, grantType: 'ALL' },
      { lovCode: 'L', resourceCode: 'screen', grantType: 'NONE' },
      { lovCode: 'OTHER', resourceCode: null, grantType: 'ALL' },
    ];
    const row = {
      roleCode: 'R',
      nameAr: '',
      nameEn: '',
      grantType: 'VALUES' as const,
      grantedValues: ['A'],
      rule: null,
      defaultValue: null,
      otherGrants: 1,
    };
    expect(withGrant(grants, 'L', row)).toEqual([
      { lovCode: 'L', resourceCode: 'screen', grantType: 'NONE' },
      { lovCode: 'OTHER', resourceCode: null, grantType: 'ALL' },
      {
        lovCode: 'L',
        resourceCode: null,
        grantType: 'VALUES',
        grantedValues: ['A'],
        rule: null,
        defaultValue: null,
      },
    ]);
    expect(withGrant(grants, 'L', { ...row, grantType: null })).toHaveLength(2);
  });

  it('names new sources and points problems at their step', () => {
    expect(nextAlias(query())).toBe('c');
    expect(stepOf('query.joins[0].on[0].left')).toBe(1);
    expect(stepOf('query.where.groups[0].conditions[1].value')).toBe(3);
    expect(stepOf('query.where.conditions[0].user')).toBe(5);
    expect(stepOf('nameAr')).toBe(6);
  });

  it('registers the tables a list uses with the list, once each, under a free data object code', () => {
    const table = (schema: string, name: string, objectType = 'TABLE'): QuerySourceInfo => ({
      kind: 'TABLE',
      code: `${schema}.${name}`,
      nameAr: `${schema}.${name}`,
      nameEn: `${schema}.${name}`,
      schema,
      dbObject: name,
      objectType,
      columns: [{ name: 'code', dataType: 'TEXT', category: 'TEXT', labelAr: null, labelEn: null }],
    });
    const sources: QuerySourceInfo[] = [
      { ...table('apps', 'branch'), kind: 'DATA_OBJECT', code: 'PARTY_PARTY' },
      table('party', 'party'),
      table('svc', 'request', 'VIEW'),
    ];
    const definition = toDefinition({
      ...emptyQuery(),
      from: { kind: 'TABLE', code: 'party.party', alias: 'p' },
      joins: [
        {
          kind: 'TABLE',
          code: 'svc.request',
          alias: 'r',
          type: 'LEFT',
          on: [{ left: 'p.code', right: 'r.code' }],
        },
        {
          kind: 'TABLE',
          code: 'party.party',
          alias: 'q',
          type: 'INNER',
          on: [{ left: 'p.code', right: 'q.code' }],
        },
        {
          kind: 'CLASS',
          code: 'REGION',
          alias: 'g',
          type: 'LEFT',
          on: [{ left: 'p.code', right: 'g.code' }],
        },
      ],
    });

    const { definition: saved, registrations } = registerTables(definition, sources, 'ENTITY');

    expect(registrations.map((r) => r.code)).toEqual(['PARTY_PARTY_2', 'SVC_REQUEST']);
    expect(saved.from).toEqual({ kind: 'DATA_OBJECT', code: 'PARTY_PARTY_2', alias: 'p' });
    expect(saved.joins.map((j) => `${j.kind}:${j.code}`)).toEqual([
      'DATA_OBJECT:SVC_REQUEST',
      'DATA_OBJECT:PARTY_PARTY_2',
      'CLASS:REGION',
    ]);
    expect(registrations[1].payload).toMatchObject({
      dbSchema: 'svc',
      dbObject: 'request',
      objectType: 'VIEW',
      entityCode: 'ENTITY',
      businessApp: '',
    });
    expect((registrations[0].payload['columns'] as unknown[]).length).toBe(1);
    expect(dataObjectCode('com', 'outbound-message', new Set())).toBe('COM_OUTBOUND_MESSAGE');
  });
});
