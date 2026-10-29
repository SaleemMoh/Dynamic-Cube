import { orderTypes, referencesOf, typeDependencies } from './export-order';
import { TemplateEntity } from './package-template';

const t = { ar: '', en: '' };
const entity = (entityType: string, fields: TemplateEntity['fields'] = [], key: TemplateEntity['key'] = 'code'): TemplateEntity => ({
  entityType,
  section: t,
  title: t,
  key,
  fields,
});

describe('export order', () => {
  const entities = [
    entity('integ.operation', [{ code: 'connectionCode', type: 'select', label: t, ref: 'integ.connection' }]),
    entity('integ.connection', [{ code: 'providerCode', type: 'select', label: t, ref: 'integ.provider' }]),
    entity('integ.provider'),
    entity('apps.domain', [], 'domain'),
    entity('apps.class', [{ code: 'parentClassCode', type: 'text', label: t, ref: 'apps.class' }]),
    entity('apps.user', [{ code: 'roles', type: 'refList', multiple: true, itemKey: 'roleCode', label: t, ref: 'apps.role' }]),
    entity('apps.menu', [{ code: 'parentCode', type: 'select', label: t, ref: 'apps.menu' }], 'appScoped'),
  ];
  const deps = typeDependencies(entities);

  it('puts referenced types first, values after their classes, and unknown types last', () => {
    expect(orderTypes(['x.unknown', 'integ.operation', 'apps.domain', 'integ.provider', 'apps.class', 'integ.connection'], deps)).toEqual([
      'apps.class',
      'apps.domain',
      'integ.provider',
      'integ.connection',
      'integ.operation',
      'x.unknown',
    ]);
  });

  it('follows declared references, lists and parents', () => {
    const keys = new Map(entities.map((e) => [e.entityType, e.key]));
    expect(referencesOf({ code: 'U1', roles: [{ roleCode: 'MAKER' }, { roleCode: 'CHECKER' }] }, entities[5], keys)).toEqual([
      { entityType: 'apps.role', code: 'MAKER' },
      { entityType: 'apps.role', code: 'CHECKER' },
    ]);
    expect(referencesOf({ code: 'INTERNAL/CHILD', appCode: 'INTERNAL', parentCode: 'TOP' }, entities[6], keys)).toEqual([
      { entityType: 'apps.menu', code: 'INTERNAL/TOP' },
    ]);
    expect(referencesOf({ code: 'REGION/RIYADH' }, entities[3], keys)).toEqual([{ entityType: 'apps.class', code: 'REGION' }]);
  });
});
