import { problemLines, rowKey, toPackage } from './json-package';

describe('JSON import', () => {
  it('keys rows by code, app and code, or the singleton', () => {
    expect(rowKey({ code: 'A' }, 'code')).toBe('A');
    expect(rowKey({ code: 'LANG', appCode: 'EXTERNAL' }, 'appScoped')).toBe('EXTERNAL/LANG');
    expect(rowKey({ code: 'LANG', appCode: null }, 'appScoped')).toBe('LANG');
    expect(rowKey({}, { singleton: 'ENTITY' })).toBe('ENTITY');
    expect(rowKey({ nameAr: 'x' }, 'code')).toBeNull();
  });

  it('updates rows that exist and creates the others', () => {
    const pkg = toPackage([{ code: 'A', nameAr: 'أ' }, { code: 'B' }], { entityType: 'cms.faq', key: 'code' }, new Set(['A']), 'FAQs');
    expect(pkg).toEqual({
      title: 'FAQs',
      items: [
        { entityType: 'cms.faq', entityCode: 'A', operation: 'UPDATE', payload: { code: 'A', nameAr: 'أ' } },
        { entityType: 'cms.faq', entityCode: 'B', operation: 'CREATE', payload: { code: 'B' } },
      ],
    });
  });

  it('passes packages and package items through', () => {
    const items = [{ entityType: 'apps.entity', entityCode: 'ENTITY', operation: 'UPDATE', payload: {} }];
    expect(toPackage({ title: 'Brand', items }, null, new Set(), 'x')).toEqual({ title: 'Brand', items });
    expect(toPackage(items[0], null, new Set(), 'x').items).toEqual(items);
  });

  it('says what is wrong', () => {
    expect(() => toPackage([{ nameAr: 'x' }], { entityType: 't', key: 'code' }, new Set(), 'x')).toThrow('#1: code is required');
    expect(() => toPackage({ code: 'A' }, null, new Set(), 'x')).toThrow('entityType and entityCode are required');
    expect(() => toPackage([], null, new Set(), 'x')).toThrow('empty');
  });

  it('lists every problem as a line', () => {
    expect(problemLines(['items: missing'], [{ index: 1, entityType: 'cms.faq', entityCode: 'A', errors: { answerEn: ['Required.'] } }])).toEqual([
      'items: missing',
      '#2 cms.faq A — answerEn: Required.',
    ]);
  });
});
