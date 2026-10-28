import { ALL_CONFIG_SECTIONS } from '../config/module-sections';
import { buildTemplate, exampleValue, fieldHelp, templateEntities, withoutHelp } from './package-template';

describe('package template', () => {
  const entities = templateEntities(ALL_CONFIG_SECTIONS);

  it('covers the configuration screens and the dedicated types once each', () => {
    const types = entities.map((e) => e.entityType);
    expect(types).toContain('apps.role');
    expect(types).toContain('apps.class');
    expect(types).toContain('apps.domain');
    expect(types).toContain('wf.workflow-version');
    expect(new Set(types).size).toBe(types.length);
  });

  it('gives each item an example payload and explains every property', () => {
    const role = entities.find((e) => e.entityType === 'apps.role')!;
    const template = buildTemplate([role]) as { items: { entityCode: string; payload: Record<string, unknown>; _help: Record<string, string> }[] };
    const item = template.items[0];

    expect(item.entityCode).toBe('EXAMPLE_CODE');
    expect(item.payload['nameAr']).toBe('');
    expect(item.payload['privileges']).toEqual([{ resourceCode: '...', actions: ['VIEW'] }]);
    for (const field of role.fields) {
      expect(item._help[`payload.${field.code}`]).toContain(field.label.en);
    }
    expect(item._help['entityCode']).toContain('Unique code');
  });

  it('describes types, required fields, allowed values and references', () => {
    expect(fieldHelp({ code: 'appType', type: 'select', label: { ar: 'النوع', en: 'Type' }, required: true, options: [{ value: 'INTERNAL', label: { ar: 'د', en: 'I' } }] })).toBe(
      'Type (النوع) — select, required — one of: INTERNAL',
    );
    expect(fieldHelp({ code: 'parentCode', type: 'select', label: { ar: 'أعلى', en: 'Parent' }, ref: 'apps.resource' })).toContain('code of a apps.resource row');
    expect(exampleValue({ code: 'x', type: 'json', label: { ar: '', en: '' }, hint: { ar: 'نص', en: 'not json' } })).toBeUndefined();
  });

  it('removes explanations at every depth before import', () => {
    expect(withoutHelp({ title: 'T', _help: { title: 'x' }, items: [{ entityType: 'a', _help: {}, payload: { nameAr: 'أ', _help: 'y' } }] })).toEqual({
      title: 'T',
      items: [{ entityType: 'a', payload: { nameAr: 'أ' } }],
    });
  });
});
