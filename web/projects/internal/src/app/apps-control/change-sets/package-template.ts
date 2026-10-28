import { LocalizedText } from '@dynamic-cube/shared';
import { ConfigField, ConfigSection, ConfigType } from '../config/config-types';

/** An entity type a package can carry: its section, how its key is formed and its payload fields. */
export interface TemplateEntity {
  entityType: string;
  section: LocalizedText;
  title: LocalizedText;
  key: ConfigType['key'] | 'domain' | 'version';
  fields: ConfigField[];
}

const t = (ar: string, en: string): LocalizedText => ({ ar, en });
const json = (code: string, label: LocalizedText, example: string, extra?: Partial<ConfigField>): ConfigField => ({
  code,
  type: 'json',
  label,
  hint: { ar: example, en: example },
  ...extra,
});

/** Types edited on their own screens rather than the generic editor. */
export const EXTRA_TEMPLATE_ENTITIES: TemplateEntity[] = [
  {
    entityType: 'apps.class',
    section: t('التصنيفات والقيم', 'Classes & domains'),
    title: t('التصنيف', 'Class'),
    key: 'code',
    fields: [
      { code: 'descriptionAr', type: 'text', label: t('الوصف بالعربية', 'Arabic description') },
      { code: 'descriptionEn', type: 'text', label: t('الوصف بالإنجليزية', 'English description') },
      { code: 'parentClassCode', type: 'text', label: t('التصنيف الأعلى', 'Parent class'), ref: 'apps.class' },
      { code: 'isHierarchical', type: 'checkbox', label: t('قيم هرمية داخل التصنيف', 'Values form a hierarchy within the class') },
      json(
        'attributes',
        t('الخصائص', 'Attributes'),
        '[{"code": "MIN_CAPITAL", "nameAr": "رأس المال", "nameEn": "Minimum capital", "dataType": "NUMBER", "isRequired": false, "displayOrder": 10}]',
        { defaultValue: [] },
      ),
    ],
  },
  {
    entityType: 'apps.domain',
    section: t('التصنيفات والقيم', 'Classes & domains'),
    title: t('قيمة التصنيف', 'Class value'),
    key: 'domain',
    fields: [
      { code: 'parentCode', type: 'text', label: t('القيمة الأعلى', 'Parent value') },
      { code: 'displayOrder', type: 'number', label: t('ترتيب العرض', 'Display order') },
      { code: 'isDefault', type: 'checkbox', label: t('القيمة الافتراضية', 'Default value') },
      json('attributes', t('قيم الخصائص', 'Attribute values'), '{"MIN_CAPITAL": 50000}', { defaultValue: {} }),
      { code: 'effectiveFrom', type: 'date', label: t('يسري من', 'Effective from') },
      { code: 'effectiveTo', type: 'date', label: t('يسري حتى', 'Effective to') },
      json('externalCodes', t('رموز الأنظمة الأخرى', 'External codes'), '[{"providerCode": "MCI", "externalCode": "17", "direction": "BOTH"}]', { defaultValue: [] }),
    ],
  },
  {
    entityType: 'svc.service-version',
    section: t('الخدمات', 'Services'),
    title: t('إصدار خدمة', 'Service version'),
    key: 'version',
    fields: [
      json(
        'definition',
        t('تعريف الإصدار', 'Version definition'),
        '{"levels": [], "steps": [{"code": "MAIN", "type": "FORM", "titleAr": "البيانات", "titleEn": "Details", "sections": [{"code": "S1", "titleAr": "القسم", "titleEn": "Section", "questions": [{"code": "QUESTION_CODE", "required": true}]}]}], "eligibility": [], "validations": [], "fees": [], "workflow": null}',
      ),
      { code: 'isRetired', type: 'checkbox', label: t('موقوف', 'Retired') },
    ],
  },
  {
    entityType: 'wf.workflow-version',
    section: t('مسارات العمل', 'Workflows'),
    title: t('إصدار مسار العمل', 'Workflow version'),
    key: 'version',
    fields: [
      json(
        'definition',
        t('تعريف المسار', 'Workflow graph'),
        '{"slaWorkingDays": 30, "nodes": [{"code": "START", "type": "START", "nameAr": "بداية", "nameEn": "Start", "next": "REVIEW"}, {"code": "REVIEW", "type": "HUMAN_TASK", "nameAr": "المراجعة", "nameEn": "Review", "assignment": {"target": "ROLE", "role": "REVIEWER", "mode": "QUEUE"}, "actions": [{"code": "APPROVE", "kind": "APPROVE", "nameAr": "اعتماد", "nameEn": "Approve", "to": "APPROVED"}, {"code": "RETURN", "kind": "RETURN", "nameAr": "إعادة للتعديل", "nameEn": "Return for correction"}], "sla": {"workingDays": 5}}, {"code": "APPROVED", "type": "END", "nameAr": "معتمد", "nameEn": "Approved", "outcome": "APPROVED"}]}',
      ),
      { code: 'isRetired', type: 'checkbox', label: t('موقوف', 'Retired') },
    ],
  },
];

/** Every entity type a package can carry, from the configuration screens plus the dedicated ones. */
export function templateEntities(sections: ConfigSection[]): TemplateEntity[] {
  const fromSections = sections.flatMap((s) =>
    s.types.map((type) => ({ entityType: type.entityType, section: s.title, title: type.title, key: type.key, fields: type.fields }) as TemplateEntity),
  );
  const known = new Set(fromSections.map((e) => e.entityType));
  return [...fromSections, ...EXTRA_TEMPLATE_ENTITIES.filter((e) => !known.has(e.entityType))];
}

const both = (text: LocalizedText) => (text.ar === text.en ? text.en : `${text.en} (${text.ar})`);

/** How the entity code of an item is written. */
export function codeHelp(entity: TemplateEntity): string {
  const key = entity.key;
  if (typeof key === 'object') {
    return `Always "${key.singleton}" (one row only).`;
  }
  switch (key) {
    case 'appScoped':
      return 'APP/CODE: the app (e.g. INTERNAL) and the code, e.g. "INTERNAL/MY_CODE".';
    case 'domain':
      return 'CLASS/CODE: the class code and the value code, e.g. "REGION/RIYADH".';
    case 'version':
      return 'PARENT/N: the parent code and the next version number, e.g. "MY_SERVICE/1". Published versions never change.';
    default:
      return 'Unique code of the row: upper-case letters, digits, _ and - for most types.';
  }
}

/** One line explaining a payload field: meaning, type, whether required, allowed values and references. */
export function fieldHelp(field: ConfigField): string {
  const parts = [both(field.label)];
  const type = field.type === 'json' ? 'structured JSON' : field.type === 'refList' ? 'list' : field.type;
  parts.push(`${type}${field.multiple ? ' (several values)' : ''}${field.required ? ', required' : ', optional'}`);
  if (field.options?.length) {
    parts.push(`one of: ${field.options.map((o) => o.value).join(', ')}`);
  }
  if (field.ref) {
    parts.push(`code of a ${field.ref} row`);
  }
  if (field.refDomains) {
    parts.push(`value code of class ${field.refDomains}`);
  }
  if (field.type === 'date') {
    parts.push('format yyyy-MM-dd');
  }
  if (field.hint && field.type === 'json') {
    parts.push(`e.g. ${field.hint.en}`);
  } else if (field.hint) {
    parts.push(both(field.hint));
  }
  return parts.join(' — ');
}

/** An example value for a field, or undefined when the field is best left out (the server default applies). */
export function exampleValue(field: ConfigField): unknown {
  // Structured fields: the example in the hint teaches more than an empty default.
  if (field.type === 'json') {
    const text = field.hint?.en.split(' — ')[0];
    try {
      return text ? JSON.parse(text) : field.defaultValue;
    } catch {
      return field.defaultValue;
    }
  }
  if (field.defaultValue !== undefined) {
    return field.defaultValue;
  }
  switch (field.type) {
    case 'number':
      return 0;
    case 'checkbox':
      return false;
    case 'date':
      return '2026-01-01';
    case 'refList':
      return [];
    case 'select':
      return field.multiple ? [] : (field.options?.[0]?.value ?? '');
    default:
      return '';
  }
}

function exampleCode(entity: TemplateEntity): string {
  const key = entity.key;
  if (typeof key === 'object') {
    return key.singleton;
  }
  return { appScoped: 'INTERNAL/EXAMPLE_CODE', domain: 'CLASS_CODE/VALUE_CODE', version: 'PARENT_CODE/1' }[key as string] ?? 'EXAMPLE_CODE';
}

/** A package with one example item per entity type; every property is explained under `_help` (ignored on import). */
export function buildTemplate(entities: TemplateEntity[]): object {
  return {
    title: 'Package title',
    _help: {
      _help: 'Explanations like this one are ignored on import; you may delete them.',
      title: 'Required. Becomes the title of the draft change sets the package creates (one per configuration area).',
      items: 'Required. 1 to 500 changes, applied in order; an item may refer to rows created by earlier items.',
      'items[].entityType': 'Required. What the item changes (see each example item).',
      'items[].entityCode': 'Required. The key of the row (its format is explained in each item).',
      'items[].operation': 'Required. CREATE a new row, UPDATE an existing one (the payload replaces it), or DELETE it (no payload).',
      'items[].payload': 'Required for CREATE and UPDATE: the row\'s fields, explained in each item\'s _help.',
    },
    items: entities.map((entity) => {
      const help: Record<string, string> = {
        _: `${both(entity.title)} — ${both(entity.section)}`,
        entityCode: codeHelp(entity),
        'payload.nameAr': 'Arabic name (الاسم بالعربية) — text, required',
        'payload.nameEn': 'English name (الاسم بالإنجليزية) — text, required',
        'payload.isActive': 'Active (فعال) — true or false, optional (default true)',
      };
      const payload: Record<string, unknown> = { nameAr: '', nameEn: '', isActive: true };
      for (const field of entity.fields) {
        help[`payload.${field.code}`] = fieldHelp(field);
        const value = exampleValue(field);
        if (value !== undefined) {
          payload[field.code] = value;
        }
      }
      return { entityType: entity.entityType, entityCode: exampleCode(entity), operation: 'CREATE', _help: help, payload };
    }),
  };
}

/** Removes `_help` explanations at any depth before a package is checked or imported. */
export function withoutHelp(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map(withoutHelp);
  }
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).filter(([key]) => key !== '_help').map(([key, v]) => [key, withoutHelp(v)]));
  }
  return value;
}
