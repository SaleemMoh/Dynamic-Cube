import { FormSchema } from '@dynamic-cube/shared';

/** Preview schema until the Service Builder exists (Phase 1). Mirrors part of the classification request. */
export const SAMPLE_SCHEMA: FormSchema = {
  code: 'classification-request-preview',
  sections: [
    {
      code: 'establishment',
      title: { ar: 'بيانات المنشأة', en: 'Establishment' },
      fields: [
        { code: 'establishmentName', type: 'text', label: { ar: 'اسم المنشأة', en: 'Establishment name' }, required: true },
        {
          code: 'establishmentKind',
          type: 'select',
          label: { ar: 'نوع المنشأة', en: 'Establishment type' },
          required: true,
          options: [
            { value: 'contractor', label: { ar: 'مقاول', en: 'Contractor' } },
            { value: 'consultancy', label: { ar: 'مكتب أو شركة استشارية هندسية', en: 'Engineering consultancy' } },
          ],
        },
        {
          code: 'crNumber',
          type: 'text',
          label: { ar: 'رقم السجل التجاري', en: 'CR number' },
          visibleWhen: { '==': [{ var: 'establishmentKind' }, 'contractor'] },
          requiredWhen: { '==': [{ var: 'establishmentKind' }, 'contractor'] },
        },
        {
          code: 'engineeringLicense',
          type: 'text',
          label: { ar: 'رقم الترخيص الهندسي', en: 'Engineering license number' },
          visibleWhen: { '==': [{ var: 'establishmentKind' }, 'consultancy'] },
          requiredWhen: { '==': [{ var: 'establishmentKind' }, 'consultancy'] },
        },
        { code: 'employees', type: 'number', label: { ar: 'عدد العاملين', en: 'Number of employees' }, required: true },
      ],
    },
    {
      code: 'declaration',
      title: { ar: 'الإقرار والتعهد', en: 'Declaration' },
      fields: [
        {
          code: 'pledge',
          type: 'checkbox',
          label: { ar: 'أقر بصحة البيانات المدخلة', en: 'I confirm that the entered data is correct' },
          required: true,
        },
      ],
    },
  ],
};
