import { answerField, hasChoices, operatorsFor } from './logic-fields';

describe('logic fields', () => {
  it('offers the comparisons that fit a field', () => {
    expect(operatorsFor({ path: 'n', label: { ar: '', en: '' }, type: 'number' })).toContain(
      'between',
    );
    expect(operatorsFor({ path: 't', label: { ar: '', en: '' }, type: 'text' })).not.toContain(
      'gt',
    );
    expect(operatorsFor({ path: 'b', label: { ar: '', en: '' }, type: 'boolean' })[0]).toBe(
      'isTrue',
    );
    expect(operatorsFor({ path: 't', label: { ar: '', en: '' }, type: 'text' }, 'gt')).toContain(
      'gt',
    );
    expect(operatorsFor(undefined).length).toBe(15);
  });

  it('turns questions into fields with their choices and row columns', () => {
    const field = answerField(
      'data.P',
      {
        label: { ar: 'المشاريع', en: 'Projects' },
        type: 'ROWS',
        columns: [
          {
            code: 'OWNER',
            labelAr: 'المالك',
            labelEn: 'Owner',
            type: 'SELECT',
            options: [{ value: 'GOV', labelAr: 'حكومي', labelEn: 'Government' }],
          },
          { code: 'REGION', labelAr: 'المنطقة', labelEn: 'Region', type: 'SELECT', lov: 'REGIONS' },
          { code: 'VALUE', labelAr: 'القيمة', labelEn: 'Value', type: 'NUMBER' },
        ],
      },
      { ar: 'الإجابات', en: 'Answers' },
    );
    expect(field.type).toBe('rows');
    expect(field.group?.en).toBe('Answers');
    expect(field.columns?.map((c) => c.type)).toEqual(['text', 'text', 'number']);
    expect(hasChoices(field.columns?.[0])).toBe(true);
    expect(field.columns?.[1].lovCode).toBe('REGIONS');
    expect(
      answerField('data.S', { label: { ar: '', en: '' }, type: 'SELECT', multiple: true }).type,
    ).toBe('list');
    expect(
      answerField('data.C', {
        label: { ar: '', en: '' },
        type: 'CHECKBOX',
        options: [{ value: 'true', label: { ar: '', en: '' } }],
      }).options,
    ).toBeUndefined();
  });
});
