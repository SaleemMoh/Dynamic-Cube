import { Type } from '@angular/core';
import { FormFieldType, FormOption, JsonLogicRule, LocalizedText } from '@dynamic-cube/shared';

/** A field of a configuration editor. Besides form field types: `json` (nested data) and `refList` (codes → objects). */
export interface ConfigField {
  code: string;
  type: FormFieldType | 'json' | 'refList';
  label: LocalizedText;
  hint?: LocalizedText;
  required?: boolean;
  /** JSONLogic over the form value; when truthy the field is required. */
  requiredWhen?: JsonLogicRule;
  options?: FormOption[];
  /** Options are the rows of another entity type (value = code). */
  ref?: string;
  /** Options are the values of a class. */
  refDomains?: string;
  multiple?: boolean;
  visibleWhen?: JsonLogicRule;
  /** For `refList`: property holding the code in each list item, e.g. `roleCode`. */
  itemKey?: string;
  /** Default for new rows. */
  defaultValue?: unknown;
  /** Options come from an API returning `{ code, nameAr, nameEn }[]` (e.g. the events modules raise). */
  optionsUrl?: string;
  /** Set when the row is created; read-only afterwards (e.g. the service a workflow belongs to). */
  lockedOnEdit?: boolean;
}

/** A link from each row to a related screen (e.g. from a service to its workflow). */
export interface ConfigRowLink {
  icon: string;
  label: LocalizedText;
  route: (row: Record<string, unknown>) => { commands: string[]; queryParams?: Record<string, string> };
  testId: string;
}

export interface ConfigType {
  entityType: string;
  title: LocalizedText;
  /** How the change-item key is formed: the code, `APP/CODE` for app-scoped rows, or a fixed singleton code. */
  key: 'code' | 'appScoped' | { singleton: string };
  /** Extra list columns (payload fields). */
  columns?: string[];
  fields: ConfigField[];
  /** Labels of the name fields, when they hold something else (UI texts). */
  nameLabels?: { ar: LocalizedText; en: LocalizedText };
  /** A dialog that edits rows instead of the generic form (it receives `ConfigEditorData`); loaded when first opened. */
  editor?: () => Promise<Type<unknown>>;
  /** Rows are created elsewhere (e.g. a service's workflow is created with the service); no *New* button. */
  noCreate?: boolean;
  rowLinks?: ConfigRowLink[];
}

export interface ConfigSection {
  path: string;
  title: LocalizedText;
  resource: string;
  types: ConfigType[];
}

const t = (ar: string, en: string): LocalizedText => ({ ar, en });
const opt = (value: string, ar: string, en: string): FormOption => ({ value, label: t(ar, en) });

const appRef: ConfigField = {
  code: 'appCode',
  type: 'select',
  label: t('التطبيق', 'App'),
  ref: 'apps.app',
};
const displayOrder: ConfigField = {
  code: 'displayOrder',
  type: 'number',
  label: t('الترتيب', 'Display order'),
  defaultValue: 0,
};
const effectiveFrom: ConfigField = {
  code: 'effectiveFrom',
  type: 'date',
  label: t('يسري من', 'Effective from'),
};
const effectiveTo: ConfigField = {
  code: 'effectiveTo',
  type: 'date',
  label: t('يسري حتى', 'Effective to'),
};
const actions = ['VIEW', 'CREATE', 'EDIT', 'DELETE', 'APPROVE', 'EXPORT', 'PRINT'].map((a) =>
  opt(a, a, a),
);

/** Apps Control screens built from field definitions. Nested lists are edited as JSON until visual editors arrive. */
export const CONFIG_SECTIONS: ConfigSection[] = [
  {
    path: 'installation',
    title: t('النظام والجهة', 'Installation & entity'),
    resource: 'apps-control.installation',
    types: [
      {
        entityType: 'apps.entity',
        title: t('الجهة', 'Entity'),
        key: { singleton: 'ENTITY' },
        columns: ['entityKind'],
        fields: [
          {
            code: 'entityKind',
            type: 'select',
            label: t('نوع الجهة', 'Entity kind'),
            refDomains: 'ENTITY_KIND',
          },
          { code: 'unifiedNumber', type: 'text', label: t('الرقم الموحد', 'Unified number') },
          { code: 'crNumber', type: 'text', label: t('السجل التجاري', 'CR number') },
        ],
      },
      {
        entityType: 'apps.installation',
        title: t('النظام', 'Installation'),
        key: { singleton: 'INSTALLATION' },
        columns: ['defaultLanguage', 'timeZone'],
        fields: [
          {
            code: 'systemType',
            type: 'text',
            label: t('نوع البيئة', 'System type'),
            required: true,
          },
          {
            code: 'defaultLanguage',
            type: 'select',
            label: t('اللغة الافتراضية', 'Default language'),
            options: [opt('ar', 'العربية', 'Arabic'), opt('en', 'الإنجليزية', 'English')],
            required: true,
          },
          {
            code: 'timeZone',
            type: 'text',
            label: t('المنطقة الزمنية', 'Time zone'),
            required: true,
          },
          {
            code: 'calendarDisplay',
            type: 'select',
            label: t('عرض التقويم', 'Calendar display'),
            options: [
              opt('GREGORIAN', 'ميلادي', 'Gregorian'),
              opt('HIJRI', 'هجري', 'Hijri'),
              opt('BOTH', 'كلاهما', 'Both'),
            ],
          },
          {
            code: 'weekendDays',
            type: 'json',
            label: t('أيام العطلة (1=الاثنين … 7=الأحد)', 'Weekend days (1=Mon … 7=Sun)'),
          },
          { code: 'copyright', type: 'text', label: t('حقوق النشر', 'Copyright') },
        ],
      },
      {
        entityType: 'apps.holiday',
        title: t('العطل الرسمية', 'Holidays'),
        key: 'code',
        columns: ['fromDate', 'toDate'],
        fields: [
          { code: 'fromDate', type: 'date', label: t('من', 'From'), required: true },
          {
            code: 'toDate',
            type: 'date',
            label: t('إلى', 'To'),
            hint: t('فارغ ليوم واحد', 'Empty for one day'),
          },
        ],
      },
      {
        entityType: 'apps.app',
        title: t('التطبيقات', 'Apps'),
        key: 'code',
        columns: ['appType'],
        fields: [
          {
            code: 'appType',
            type: 'select',
            label: t('النوع', 'Type'),
            options: [
              opt('INTERNAL', 'داخلي', 'Internal'),
              opt('EXTERNAL', 'خارجي', 'External'),
              opt('FIELD', 'ميداني', 'Field'),
              opt('OTHER', 'أخرى', 'Other'),
            ],
            required: true,
          },
          { code: 'baseUrl', type: 'text', label: t('الرابط', 'Base URL') },
          displayOrder,
        ],
      },
    ],
  },
  {
    path: 'organization',
    title: t('الهيكل التنظيمي', 'Organization structure'),
    resource: 'apps-control.organization',
    types: [
      {
        entityType: 'apps.org-unit',
        title: t('الوحدات التنظيمية', 'Org units'),
        key: 'code',
        columns: ['typeCode', 'parentCode'],
        fields: [
          {
            code: 'typeCode',
            type: 'select',
            label: t('نوع الوحدة', 'Unit type'),
            ref: 'apps.org-unit-type',
            required: true,
          },
          {
            code: 'parentCode',
            type: 'select',
            label: t('الوحدة الأعلى', 'Parent unit'),
            ref: 'apps.org-unit',
          },
          {
            code: 'attributes',
            type: 'json',
            label: t('خصائص (المنطقة، الأمانة…)', 'Attributes (region, amanah…)'),
            defaultValue: {},
          },
          effectiveFrom,
          effectiveTo,
        ],
      },
      {
        entityType: 'apps.org-unit-type',
        title: t('أنواع الوحدات', 'Unit types'),
        key: 'code',
        columns: ['allowedParentTypes'],
        fields: [
          displayOrder,
          {
            code: 'allowedParentTypes',
            type: 'select',
            multiple: true,
            label: t('الأنواع المسموح بها كأعلى', 'Allowed parent types'),
            ref: 'apps.org-unit-type',
          },
        ],
      },
      { entityType: 'apps.position', title: t('المسميات', 'Positions'), key: 'code', fields: [] },
    ],
  },
  {
    path: 'users',
    title: t('المستخدمون', 'Users'),
    resource: 'apps-control.users',
    types: [
      {
        entityType: 'apps.user',
        title: t('المستخدمون', 'Users'),
        key: 'code',
        columns: ['status', 'email'],
        fields: [
          { code: 'email', type: 'text', label: t('البريد الإلكتروني', 'Email') },
          { code: 'mobile', type: 'text', label: t('الجوال', 'Mobile') },
          { code: 'nationalId', type: 'text', label: t('رقم الهوية', 'National ID') },
          {
            code: 'status',
            type: 'select',
            label: t('الحالة', 'Status'),
            options: [
              opt('ACTIVE', 'نشط', 'Active'),
              opt('LOCKED', 'مقفل', 'Locked'),
              opt('DISABLED', 'معطل', 'Disabled'),
            ],
            required: true,
            defaultValue: 'ACTIVE',
          },
          {
            code: 'preferredLanguage',
            type: 'select',
            label: t('اللغة', 'Language'),
            options: [opt('ar', 'العربية', 'Arabic'), opt('en', 'الإنجليزية', 'English')],
          },
          {
            code: 'roles',
            type: 'refList',
            multiple: true,
            itemKey: 'roleCode',
            label: t('الأدوار', 'Roles'),
            ref: 'apps.role',
          },
          {
            code: 'assignments',
            type: 'json',
            label: t('التكليفات (الوحدة والمسمى)', 'Assignments (unit and position)'),
            defaultValue: [],
            hint: t(
              '[{"orgUnitCode": "...", "positionCode": "...", "isPrimary": true}]',
              '[{"orgUnitCode": "...", "positionCode": "...", "isPrimary": true}]',
            ),
          },
        ],
      },
    ],
  },
  {
    path: 'roles',
    title: t('الأدوار والصلاحيات', 'Roles & privileges'),
    resource: 'apps-control.roles',
    types: [
      {
        entityType: 'apps.role',
        title: t('الأدوار', 'Roles'),
        key: 'code',
        columns: ['appCode'],
        fields: [
          appRef,
          { code: 'referenceCode', type: 'text', label: t('الرمز المرجعي', 'Reference code') },
          {
            code: 'privileges',
            type: 'json',
            label: t('الصلاحيات', 'Privileges'),
            defaultValue: [],
            hint: t(
              '[{"resourceCode": "...", "actions": ["VIEW"]}]',
              '[{"resourceCode": "...", "actions": ["VIEW"]}]',
            ),
          },
          {
            code: 'grants',
            type: 'json',
            label: t('صلاحيات القيم', 'LOV grants'),
            defaultValue: [],
            hint: t(
              '[{"lovCode": "...", "grantType": "VALUES", "grantedValues": ["..."]}]',
              '[{"lovCode": "...", "grantType": "VALUES", "grantedValues": ["..."]}]',
            ),
          },
        ],
      },
    ],
  },
  {
    path: 'resources',
    title: t('الموارد', 'Resources'),
    resource: 'apps-control.resources',
    types: [
      {
        entityType: 'apps.resource',
        title: t('الموارد', 'Resources'),
        key: 'code',
        columns: ['appCode', 'resourceType'],
        fields: [
          appRef,
          {
            code: 'resourceType',
            type: 'select',
            label: t('النوع', 'Type'),
            required: true,
            defaultValue: 'SCREEN',
            options: ['SCREEN', 'API', 'REPORT', 'SERVICE', 'WORKFLOW_ACTION', 'FIELD'].map((v) =>
              opt(v, v, v),
            ),
          },
          {
            code: 'parentCode',
            type: 'select',
            label: t('المورد الأعلى', 'Parent resource'),
            ref: 'apps.resource',
          },
          {
            code: 'availableActions',
            type: 'select',
            multiple: true,
            label: t('الإجراءات المتاحة', 'Available actions'),
            options: actions,
            defaultValue: ['VIEW'],
          },
        ],
      },
    ],
  },
  {
    path: 'menus',
    title: t('القوائم', 'Menus'),
    resource: 'apps-control.menus',
    types: [
      {
        entityType: 'apps.menu',
        title: t('عناصر القائمة', 'Menu items'),
        key: 'code',
        columns: ['appCode', 'groupCode', 'target'],
        fields: [
          { ...appRef, required: true },
          {
            code: 'groupCode',
            type: 'select',
            label: t('المجموعة', 'Group'),
            ref: 'apps.menu-group',
          },
          {
            code: 'parentCode',
            type: 'select',
            label: t('العنصر الأعلى', 'Parent item'),
            ref: 'apps.menu',
          },
          {
            code: 'itemType',
            type: 'select',
            label: t('النوع', 'Type'),
            required: true,
            defaultValue: 'SCREEN',
            options: ['GROUP', 'SCREEN', 'SERVICE', 'REPORT', 'DASHBOARD', 'QUEUE', 'URL'].map(
              (v) => opt(v, v, v),
            ),
          },
          {
            code: 'target',
            type: 'text',
            label: t('الوجهة (مسار، رمز، رابط)', 'Target (route, code, URL)'),
            visibleWhen: { '!=': [{ var: 'itemType' }, 'GROUP'] },
          },
          {
            code: 'resourceCode',
            type: 'select',
            label: t('المورد (صلاحية العرض)', 'Resource (view privilege)'),
            ref: 'apps.resource',
          },
          { code: 'icon', type: 'text', label: t('الأيقونة', 'Icon') },
          displayOrder,
          { code: 'isVisible', type: 'checkbox', label: t('ظاهر', 'Visible'), defaultValue: true },
          {
            code: 'descriptionAr',
            type: 'textarea',
            label: t('الوصف بالعربية', 'Description (Arabic)'),
          },
          {
            code: 'descriptionEn',
            type: 'textarea',
            label: t('الوصف بالإنجليزية', 'Description (English)'),
          },
        ],
      },
      {
        entityType: 'apps.menu-group',
        title: t('مجموعات القوائم', 'Menu groups'),
        key: 'code',
        columns: ['appCode'],
        fields: [
          { ...appRef, required: true },
          displayOrder,
          { code: 'icon', type: 'text', label: t('الأيقونة', 'Icon') },
        ],
      },
    ],
  },
  {
    path: 'lovs',
    title: t('قوائم القيم', 'Lists of values'),
    resource: 'apps-control.lovs',
    types: [
      {
        entityType: 'apps.lov',
        title: t('قوائم القيم', 'Lists of values'),
        key: 'code',
        columns: ['sourceType', 'classCode'],
        fields: [
          {
            code: 'sourceType',
            type: 'select',
            label: t('المصدر', 'Source'),
            required: true,
            defaultValue: 'CLASS',
            options: [
              opt('CLASS', 'تصنيف', 'Class'),
              opt('DATA_OBJECT', 'كائن بيانات', 'Data object'),
              opt('SQL', 'استعلام مقيد', 'Restricted SQL'),
            ],
          },
          {
            code: 'classCode',
            type: 'select',
            label: t('التصنيف', 'Class'),
            ref: 'apps.class',
            visibleWhen: { '==': [{ var: 'sourceType' }, 'CLASS'] },
            required: true,
          },
          {
            code: 'dataObjectCode',
            type: 'select',
            label: t('كائن البيانات', 'Data object'),
            ref: 'apps.data-object',
            visibleWhen: { '==': [{ var: 'sourceType' }, 'DATA_OBJECT'] },
            required: true,
          },
          {
            code: 'valueColumn',
            type: 'text',
            label: t('عمود القيمة', 'Value column'),
            visibleWhen: { '!=': [{ var: 'sourceType' }, 'CLASS'] },
            requiredWhen: { '==': [{ var: 'sourceType' }, 'DATA_OBJECT'] },
            hint: t(
              'للاستعلام: اختياري، الافتراضي value و display_ar و display_en',
              'For SQL: optional; defaults to value, display_ar, display_en',
            ),
          },
          {
            code: 'displayArColumn',
            type: 'text',
            label: t('عمود الاسم العربي', 'Arabic label column'),
            visibleWhen: { '!=': [{ var: 'sourceType' }, 'CLASS'] },
            requiredWhen: { '==': [{ var: 'sourceType' }, 'DATA_OBJECT'] },
            hint: t(
              'للاستعلام: اختياري، الافتراضي value و display_ar و display_en',
              'For SQL: optional; defaults to value, display_ar, display_en',
            ),
          },
          {
            code: 'displayEnColumn',
            type: 'text',
            label: t('عمود الاسم الإنجليزي', 'English label column'),
            visibleWhen: { '!=': [{ var: 'sourceType' }, 'CLASS'] },
            requiredWhen: { '==': [{ var: 'sourceType' }, 'DATA_OBJECT'] },
            hint: t(
              'للاستعلام: اختياري، الافتراضي value و display_ar و display_en',
              'For SQL: optional; defaults to value, display_ar, display_en',
            ),
          },
          {
            code: 'parentColumn',
            type: 'text',
            label: t('عمود القيمة الأعلى', 'Parent column'),
            visibleWhen: { '!=': [{ var: 'sourceType' }, 'CLASS'] },
          },
          {
            code: 'sqlText',
            type: 'textarea',
            label: t('الاستعلام', 'SQL'),
            visibleWhen: { '==': [{ var: 'sourceType' }, 'SQL'] },
            required: true,
            hint: t(
              'أي استعلام SELECT؛ حدد أعمدة القيمة والأسماء إن اختلفت عن الافتراضي',
              'Any SELECT; name its value and label columns if they differ from the defaults',
            ),
          },
          {
            code: 'staticFilter',
            type: 'json',
            label: t('شرط ثابت (JSONLogic)', 'Static filter (JSONLogic)'),
            visibleWhen: { '==': [{ var: 'sourceType' }, 'CLASS'] },
          },
          {
            code: 'searchMode',
            type: 'select',
            label: t('البحث', 'Search'),
            defaultValue: 'FULL',
            options: [
              opt('FULL', 'قائمة كاملة', 'Full list'),
              opt('TYPEAHEAD', 'بحث أثناء الكتابة', 'Type-ahead'),
            ],
          },
          {
            code: 'maxRows',
            type: 'number',
            label: t('الحد الأقصى للصفوف', 'Max rows'),
            defaultValue: 500,
          },
          {
            code: 'requiresGrant',
            type: 'checkbox',
            label: t('تتطلب صلاحية قيم', 'Requires a grant'),
          },
          {
            code: 'isPublic',
            type: 'checkbox',
            label: t('متاحة للبوابة الخارجية', 'Available to the external portal'),
          },
          {
            code: 'bindings',
            type: 'json',
            label: t('الربط بالشاشات', 'Screen bindings'),
            defaultValue: [],
          },
        ],
      },
      {
        entityType: 'apps.data-object',
        title: t('كائنات البيانات', 'Data objects'),
        key: 'code',
        columns: ['dbSchema', 'dbObject'],
        fields: [
          { code: 'dbSchema', type: 'text', label: t('المخطط', 'Schema'), required: true },
          {
            code: 'dbObject',
            type: 'text',
            label: t('الجدول أو العرض', 'Table or view'),
            required: true,
          },
          {
            code: 'objectType',
            type: 'select',
            label: t('النوع', 'Type'),
            defaultValue: 'VIEW',
            options: [opt('VIEW', 'عرض', 'View'), opt('TABLE', 'جدول', 'Table')],
          },
          {
            code: 'columns',
            type: 'json',
            label: t('الأعمدة', 'Columns'),
            defaultValue: [],
            hint: t(
              '[{"name": "code", "searchable": true}]',
              '[{"name": "code", "searchable": true}]',
            ),
          },
          {
            code: 'scopeBindings',
            type: 'json',
            label: t('نطاقات البيانات', 'Data scopes'),
            defaultValue: [],
            hint: t(
              '[{"columnName": "sector_code", "lovCode": "LOV_SECTOR"}]',
              '[{"columnName": "sector_code", "lovCode": "LOV_SECTOR"}]',
            ),
          },
        ],
      },
    ],
  },
  {
    path: 'settings',
    title: t('الإعدادات', 'Settings'),
    resource: 'apps-control.settings',
    types: [
      {
        entityType: 'apps.setting',
        title: t('الإعدادات', 'Settings'),
        key: 'appScoped',
        columns: ['appCode', 'dataType', 'value'],
        fields: [
          appRef,
          {
            code: 'dataType',
            type: 'select',
            label: t('النوع', 'Type'),
            required: true,
            defaultValue: 'TEXT',
            options: ['TEXT', 'NUMBER', 'BOOL', 'JSON'].map((v) => opt(v, v, v)),
          },
          { code: 'value', type: 'json', label: t('القيمة (JSON)', 'Value (JSON)') },
          { code: 'description', type: 'textarea', label: t('الوصف', 'Description') },
          { code: 'isSecret', type: 'checkbox', label: t('سري', 'Secret') },
          {
            code: 'isPublic',
            type: 'checkbox',
            label: t('متاح للبوابة الخارجية', 'Available to the external portal'),
          },
        ],
      },
    ],
  },
  {
    path: 'ui-texts',
    title: t('نصوص الواجهة', 'UI texts'),
    resource: 'apps-control.ui-texts',
    types: [
      {
        entityType: 'apps.ui-text',
        title: t('نصوص الواجهة', 'UI texts'),
        key: 'appScoped',
        columns: ['appCode'],
        nameLabels: {
          ar: t('النص بالعربية', 'Arabic text'),
          en: t('النص بالإنجليزية', 'English text'),
        },
        fields: [appRef],
      },
    ],
  },
  {
    path: 'approval-policy',
    title: t('سياسة الاعتماد', 'Approval policy'),
    resource: 'apps-control.approval-policy',
    types: [
      {
        entityType: 'apps.change-area',
        title: t('مجالات الإعدادات', 'Configuration areas'),
        key: 'code',
        columns: ['requiresApproval'],
        fields: [
          {
            code: 'requiresApproval',
            type: 'checkbox',
            label: t('يتطلب اعتماد مستخدم آخر', 'Needs approval by a second user'),
          },
        ],
      },
    ],
  },
];
