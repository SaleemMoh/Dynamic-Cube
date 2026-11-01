import { ApiEndpointDefinition, ApiFunctionInfo } from '@dynamic-cube/shared';
import {
  bindInputs,
  checkEndpoint,
  classDefaults,
  emptyEndpoint,
  endpointPayload,
  fieldFor,
  fromQuery,
  operatorsFor,
  previewUrl,
  syncParameters,
  toQuery,
} from './api-endpoint-model';

const STATUS: ApiFunctionInfo = {
  code: 'REQUEST_STATUS',
  nameAr: 'حالة طلب',
  nameEn: 'Request status',
  descriptionAr: '',
  descriptionEn: '',
  inputs: [{ name: 'number', required: true, descriptionAr: '', descriptionEn: '' }],
  sample: {},
};

describe('api endpoint model', () => {
  it('keeps path inputs in line with the path', () => {
    const one = syncParameters([], '/requests/{number}/status');
    expect(one.map((p) => [p.name, p.location, p.isRequired])).toEqual([['number', 'PATH', true]]);
    const query = { ...one[0], name: 'lang', location: 'QUERY' as const, isRequired: false };
    expect(syncParameters([...one, query], '/requests').map((p) => p.name)).toEqual(['lang']);
  });

  it('round-trips the query with values, inputs and the client', () => {
    const query = fromQuery(null);
    query.from = { kind: 'CLASS', code: 'REGIONS', alias: 'a' };
    query.conditions = [
      { column: 'a.code', op: 'EQ', source: 'PARAM', value: '', param: 'code', user: '' },
      { column: 'a.companies', op: 'IN', source: 'USER', value: '', param: '', user: 'COMPANY' },
      { column: 'a.name_en', op: 'IS_EMPTY', source: 'VALUE', value: 'x', param: '', user: '' },
    ];
    const definition = toQuery(query);
    expect(definition['where']).toEqual({
      match: 'ALL',
      conditions: [
        { column: 'a.code', op: 'EQ', param: 'code' },
        { column: 'a.companies', op: 'IN', user: 'COMPANY' },
        { column: 'a.name_en', op: 'IS_EMPTY' },
      ],
      groups: [],
    });
    const back = fromQuery(definition);
    expect(back.conditions.map((c) => c.source)).toEqual(['PARAM', 'USER', 'VALUE']);
    expect(back.from.code).toBe('REGIONS');
  });

  it('names fields after their columns and offers a class its usual fields', () => {
    expect(fieldFor('a.name_en', []).name).toBe('nameEn');
    expect(fieldFor('b.name_en', [{ name: 'nameEn', column: 'a.name_en' }]).name).toBe('nameEn2');
    const defaults = classDefaults('r');
    expect(defaults.fields.map((f) => f.name)).toEqual(['code', 'nameAr', 'nameEn', 'parentCode']);
    expect(defaults.sort).toEqual([{ column: 'r.display_order', direction: 'ASC' }]);
  });

  it('compares with inputs and the client only in ways that make sense', () => {
    expect(operatorsFor('TEXT', 'USER')).toEqual(['EQ', 'IN']);
    expect(operatorsFor('NUMBER', 'PARAM')).not.toContain('IS_EMPTY');
    expect(operatorsFor('DATE', 'VALUE')).toContain('IS_EMPTY');
  });

  it('binds function inputs to parameters of the same name', () => {
    const params = syncParameters([], '/requests/{number}');
    expect(bindInputs(STATUS, [], params)).toEqual([
      { input: 'number', parameter: 'number', value: null },
    ]);
    expect(bindInputs(STATUS, [{ input: 'number', value: 'X' }], params)).toEqual([
      { input: 'number', value: 'X' },
    ]);
  });

  it('previews the URL and reports what stops saving', () => {
    const def = { ...emptyEndpoint(), nameAr: 'م', nameEn: 'S', path: '/requests/{number}/status' };
    def.parameters = syncParameters([], def.path);
    expect(previewUrl(def, { number: 'CLS 1' }, 'https://api')).toBe(
      'https://api/requests/CLS%201/status',
    );
    expect(checkEndpoint('', def, true, []).map((p) => p.key)).toEqual([
      'pa.problem.code',
      'pa.problem.source',
      'pa.problem.fields',
    ]);
    const fn: ApiEndpointDefinition = {
      ...def,
      kind: 'FUNCTION',
      function: 'REQUEST_STATUS',
      functionInputs: [],
    };
    expect(checkEndpoint('STATUS', fn, true, [STATUS]).map((p) => p.key)).toEqual([
      'pa.problem.input',
    ]);
    fn.functionInputs = [{ input: 'number', parameter: 'number' }];
    expect(checkEndpoint('STATUS', fn, true, [STATUS])).toEqual([]);
    expect(checkEndpoint('X', { ...def, path: 'no-slash' }, true, []).map((p) => p.key)).toContain(
      'pa.problem.path',
    );
  });

  it('saves only the part of the endpoint kind', () => {
    const def = {
      ...emptyEndpoint(),
      kind: 'FUNCTION' as const,
      function: 'REQUEST_STATUS',
      nameAr: ' م ',
      nameEn: 'S',
    };
    def.functionInputs = [{ input: 'number', parameter: '', value: ' X ' }];
    def.fields = [{ name: 'code', column: 'a.code' }];
    const payload = endpointPayload('STATUS', def, fromQuery(null));
    expect(payload.fields).toEqual([]);
    expect(payload.query).toBeNull();
    expect(payload.functionInputs).toEqual([{ input: 'number', parameter: null, value: 'X' }]);
    expect(payload.nameAr).toBe('م');
  });
});
