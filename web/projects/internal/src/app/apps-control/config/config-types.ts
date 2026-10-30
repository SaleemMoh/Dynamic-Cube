import { Type } from '@angular/core';
import { FormFieldType, FormOption, JsonLogicRule, LocalizedText, StructuredShape } from '@dynamic-cube/shared';

/**
 * A field of a configuration editor. Besides form field types: `json` (nested data, edited through a form made for its
 * `structure` — rows, lists, properties — with a JSON view) and `refList` (codes → objects).
 */
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
  /** For `refList`: shown as two lists to move items between (available and chosen) instead of a dropdown. */
  picker?: 'dual';
  /** For `refList`: property holding the code in each list item, e.g. `roleCode`. */
  itemKey?: string;
  /** For `json`: what it holds (without, it is edited as JSON). */
  structure?: StructuredShape;
  /** Default for new rows. */
  defaultValue?: unknown;
  /** Options come from an API returning `{ code, nameAr, nameEn }[]` (e.g. the events modules raise). */
  optionsUrl?: string;
  /** Set when the row is created; read-only afterwards (e.g. the service a workflow belongs to). */
  lockedOnEdit?: boolean;
  /**
   * The label of an "all" option (value `*`) listed first, for choices where all is chosen explicitly and an empty
   * choice is not valid (companies and branches, ADR-0036).
   */
  allOption?: LocalizedText;
}

/** A link from each row to a related screen (e.g. from a service to its workflow). */
export interface ConfigRowLink {
  icon: string;
  label: LocalizedText;
  route: (row: Record<string, unknown>) => { commands: string[]; queryParams?: Record<string, string> };
  testId: string;
}

/** A dialog opened from each row (it receives `ConfigEditorData` with the row), e.g. a user's sign-in account. */
export interface ConfigRowAction {
  icon: string;
  label: LocalizedText;
  testId: string;
  dialog: () => Promise<Type<unknown>>;
  /** Dialog width (default 44rem). */
  width?: string;
}

/** What a bulk dialog receives: the type and the ticked rows. */
export interface ConfigBulkData {
  type: ConfigType;
  rows: Record<string, unknown>[];
}

/** A dialog acting on the ticked rows (it receives `ConfigBulkData`), e.g. adding roles to several users. */
export interface ConfigBulkAction {
  icon: string;
  label: LocalizedText;
  testId: string;
  dialog: () => Promise<Type<unknown>>;
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
  rowActions?: ConfigRowAction[];
  /** Actions on the ticked rows, shown above the table. */
  bulkActions?: ConfigBulkAction[];
  /** A dialog that creates rows instead of the generic form (e.g. a user together with the sign-in account). */
  createDialog?: () => Promise<Type<unknown>>;
  /** A component shown above the table that explains how the screen works on this installation (e.g. where accounts come from). */
  intro?: () => Promise<Type<unknown>>;
  /**
   * The row's level: `full` (default) asks for the entity and business app, `entity` for the entity only (business apps),
   * `none` for rows of the whole installation (entities, channel apps, installation settings).
   */
  scope?: 'full' | 'entity' | 'none';
}

export interface ConfigSection {
  path: string;
  title: LocalizedText;
  resource: string;
  types: ConfigType[];
}

/** The level a type's rows take: singletons and rows marked `none` belong to the whole installation. */
export function scopeOf(type: ConfigType): 'full' | 'entity' | 'none' {
  return type.scope ?? (typeof type.key === 'object' ? 'none' : 'full');
}

const t = (ar: string, en: string): LocalizedText => ({ ar, en });
const opt = (value: string, ar: string, en: string): FormOption => ({ value, label: t(ar, en) });

/** The product channel a row belongs to (internal system, portal, field app). */
const appRef: ConfigField = {
  code: 'appCode',
  type: 'select',
  label: t('القناة', 'Channel'),
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
const actions = ['VIEW', 'CREATE', 'EDIT', 'DELETE', 'APPROVE', 'EXPORT', 'PRINT'].map((a) => opt(a, a, a));

/** Apps Control screens built from field definitions. Nested lists are edited as JSON until visual editors arrive. */
export const CONFIG_SECTIONS: ConfigSection[] = [
  {
    path: 'installation',
    title: t('النظام والجهة', 'Installation & organization'),
    resource: 'apps-control.installation',
    types: [
      {
        entityType: 'apps.entity',
        title: t('الجهات', 'Organizations'),
        key: 'code',
        scope: 'none',
        columns: ['entityKind'],
        fields: [
          {
            code: 'entityKind',
            type: 'select',
            label: t('نوع الجهة', 'Organization kind'),
            refDomains: 'ENTITY_KIND',
          },
          { code: 'unifiedNumber', type: 'text', label: t('الرقم الموحد', 'Unified number') },
          { code: 'crNumber', type: 'text', label: t('السجل التجاري', 'CR number') },
        ],
      },
      {
        entityType: 'apps.company',
        title: t('الشركات', 'Companies'),
        key: 'code',
        scope: 'entity',
        columns: ['crNumber', 'displayOrder'],
        fields: [
          { code: 'crNumber', type: 'text', label: t('السجل التجاري', 'CR number') },
          { code: 'unifiedNumber', type: 'text', label: t('الرقم الموحد', 'Unified number') },
          { code: 'vatNumber', type: 'text', label: t('الرقم الضريبي', 'VAT number') },
          displayOrder,
        ],
      },
      {
        entityType: 'apps.branch',
        title: t('الفروع', 'Branches'),
        key: 'code',
        scope: 'none',
        columns: ['companyCode', 'city', 'isDefault'],
        fields: [
          {
            code: 'companyCode',
            type: 'select',
            label: t('الشركة', 'Company'),
            ref: 'apps.company',
            required: true,
            lockedOnEdit: true,
          },
          {
            code: 'orgUnitCode',
            type: 'select',
            label: t('الوحدة التنظيمية', 'Org unit'),
            ref: 'apps.org-unit',
            hint: t('الوحدة التي تُوجَّه إليها مهام طلبات الفرع', 'The unit that handles the branch’s requests'),
          },
          { code: 'regionCode', type: 'select', label: t('المنطقة', 'Region'), refDomains: 'REGION' },
          { code: 'city', type: 'text', label: t('المدينة', 'City') },
          {
            code: 'isDefault',
            type: 'checkbox',
            label: t('الفرع الافتراضي للشركة', 'Company’s default branch'),
          },
          displayOrder,
        ],
      },
      {
        entityType: 'apps.installation',
        title: t('النظام', 'Installation'),
        key: { singleton: 'INSTALLATION' },
        scope: 'none',
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
            options: [opt('GREGORIAN', 'ميلادي', 'Gregorian'), opt('HIJRI', 'هجري', 'Hijri'), opt('BOTH', 'كلاهما', 'Both')],
          },
          {
            code: 'weekendDays',
            type: 'json',
            label: t('أيام العطلة', 'Weekend days'),
            structure: {
              kind: 'list',
              item: 'number',
              options: [
                opt('1', 'الاثنين', 'Monday'),
                opt('2', 'الثلاثاء', 'Tuesday'),
                opt('3', 'الأربعاء', 'Wednesday'),
                opt('4', 'الخميس', 'Thursday'),
                opt('5', 'الجمعة', 'Friday'),
                opt('6', 'السبت', 'Saturday'),
                opt('7', 'الأحد', 'Sunday'),
              ],
            },
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
        entityType: 'apps.business-app',
        title: t('التطبيقات', 'Apps'),
        key: 'code',
        scope: 'entity',
        columns: ['kind', 'channels', 'companies', 'displayOrder'],
        // The product's own apps (internal system, portal, field app) above the apps it manages.
        intro: () => import('../apps/apps-intro').then((m) => m.AppsIntro),
        fields: [
          {
            code: 'kind',
            type: 'select',
            label: t('نوع التطبيق', 'Kind'),
            options: [
              opt('DYNAMIC_CUBE', 'مبني على ديناميك كيوب', 'Built on Dynamic Cube'),
              opt('CONNECTED', 'نظام مرتبط', 'Connected system'),
            ],
            hint: t(
              'مبني على ديناميك كيوب: خدماته وشاشاته تظهر في تطبيقات ديناميك كيوب (النظام الداخلي، البوابة، التطبيق الميداني). نظام مرتبط: نظام خارجي بشاشاته وقاعدة بياناته يستخدم ديناميك كيوب لتسجيل الدخول والمستخدمين والأدوار والقوائم',
              'Built on Dynamic Cube: its services and screens appear in the Dynamic Cube apps (internal system, portal, field app). Connected system: an outside system with its own screens and database that uses Dynamic Cube for sign-in, users, roles and menus',
            ),
            required: true,
            defaultValue: 'DYNAMIC_CUBE',
          },
          {
            code: 'channels',
            type: 'select',
            multiple: true,
            label: t('يظهر في', 'Appears on'),
            options: [
              opt('INTERNAL', 'النظام الداخلي', 'Internal system'),
              opt('EXTERNAL', 'البوابة الإلكترونية', 'Portal'),
              opt('FIELD', 'التطبيق الميداني', 'Field app'),
            ],
            hint: t('خدمات التطبيق تظهر في البوابة فقط إذا اخترت البوابة', 'The app’s services show on the portal only when the portal is ticked'),
            visibleWhen: { '==': [{ var: 'kind' }, 'DYNAMIC_CUBE'] },
            requiredWhen: { '==': [{ var: 'kind' }, 'DYNAMIC_CUBE'] },
            defaultValue: ['INTERNAL', 'EXTERNAL'],
          },
          {
            code: 'companies',
            type: 'select',
            multiple: true,
            label: t('الشركات', 'Companies'),
            ref: 'apps.company',
            required: true,
            allOption: t('كل الشركات', 'All companies'),
            hint: t('اختر الشركات أو «كل الشركات»', 'Choose companies, or "All companies"'),
          },
          {
            code: 'branches',
            type: 'select',
            multiple: true,
            label: t('الفروع', 'Branches'),
            ref: 'apps.branch',
            required: true,
            allOption: t('كل الفروع', 'All branches'),
            hint: t('اختر الفروع أو «كل الفروع» (فروع الشركات المختارة)', 'Choose branches, or "All branches" (of the chosen companies)'),
          },
          {
            code: 'baseUrl',
            type: 'text',
            label: t('رابط التطبيق', 'App address'),
            visibleWhen: { '==': [{ var: 'kind' }, 'CONNECTED'] },
          },
          { code: 'icon', type: 'icon', label: t('الأيقونة', 'Icon') },
          { code: 'descriptionAr', type: 'textarea', label: t('الوصف بالعربية', 'Arabic description') },
          { code: 'descriptionEn', type: 'textarea', label: t('الوصف بالإنجليزية', 'English description') },
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
            code: 'defaultRoles',
            type: 'select',
            multiple: true,
            label: t('أدوار تلقائية لمن في الوحدة', 'Default roles of people in this unit'),
            ref: 'apps.role',
            defaultValue: [],
          },
          {
            code: 'attributes',
            type: 'json',
            label: t('خصائص (المنطقة، الأمانة…)', 'Attributes (region, amanah…)'),
            structure: { kind: 'map', keyLabel: t('الخاصية', 'Attribute'), valueLabel: t('القيمة', 'Value') },
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
        createDialog: () => import('../users/new-user-dialog').then((m) => m.NewUserDialog),
        // Details and access (roles by company, department roles, personal privileges) in one editor.
        editor: () => import('../users/user-editor').then((m) => m.UserEditor),
        intro: () => import('../users/identity-notice').then((m) => m.IdentityNotice),
        rowActions: [
          {
            icon: 'manage_accounts',
            label: t('حساب الدخول', 'Sign-in account'),
            testId: 'account',
            dialog: () => import('../users/account-dialog').then((m) => m.AccountDialog),
          },
        ],
        bulkActions: [
          {
            icon: 'group_add',
            label: t('إضافة أو إزالة أدوار', 'Add or remove roles'),
            testId: 'bulk-roles',
            dialog: () => import('../users/bulk-roles-dialog').then((m) => m.BulkRolesDialog),
          },
        ],
        fields: [
          { code: 'email', type: 'text', label: t('البريد الإلكتروني', 'Email') },
          { code: 'mobile', type: 'text', label: t('الجوال', 'Mobile') },
          { code: 'nationalId', type: 'text', label: t('رقم الهوية', 'National ID') },
          {
            code: 'status',
            type: 'select',
            label: t('الحالة', 'Status'),
            options: [opt('ACTIVE', 'نشط', 'Active'), opt('LOCKED', 'مقفل', 'Locked'), opt('DISABLED', 'معطل', 'Disabled')],
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
            code: 'assignments',
            type: 'json',
            label: t('التكليفات (الوحدة والمسمى)', 'Assignments (unit and position)'),
            defaultValue: [],
            structure: {
              kind: 'table',
              columns: [
                { key: 'orgUnitCode', label: t('الوحدة', 'Unit'), type: 'select', ref: 'apps.org-unit', required: true },
                { key: 'positionCode', label: t('المسمى', 'Position'), type: 'select', ref: 'apps.position' },
                { key: 'isPrimary', label: t('أساسي', 'Primary'), type: 'checkbox' },
                { key: 'effectiveFrom', label: t('من', 'From'), type: 'date' },
                { key: 'effectiveTo', label: t('إلى', 'To'), type: 'date' },
              ],
            },
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
            structure: {
              kind: 'table',
              columns: [
                { key: 'resourceCode', label: t('الشاشة', 'Screen'), type: 'select', ref: 'apps.resource', required: true },
                { key: 'actions', label: t('الإجراءات', 'Actions'), type: 'tags', ltr: true, defaultValue: ['VIEW'] },
                { key: 'fieldRules', label: t('قواعد الحقول', 'Field rules'), type: 'map' },
              ],
            },
          },
          {
            code: 'grants',
            type: 'json',
            label: t('صلاحيات القيم', 'LOV grants'),
            defaultValue: [],
            structure: {
              kind: 'table',
              columns: [
                { key: 'lovCode', label: t('القائمة', 'List'), type: 'select', ref: 'apps.lov', required: true },
                { key: 'resourceCode', label: t('الشاشة (فارغ: الكل)', 'Screen (empty: all)'), type: 'select', ref: 'apps.resource' },
                {
                  key: 'grantType',
                  label: t('الصلاحية', 'Access'),
                  type: 'select',
                  required: true,
                  defaultValue: 'ALL',
                  options: [
                    opt('ALL', 'الكل', 'All'),
                    opt('VALUES', 'قيم محددة', 'Some values'),
                    opt('RULE', 'حسب قاعدة', 'By a rule'),
                    opt('NONE', 'لا شيء', 'None'),
                  ],
                },
                { key: 'grantedValues', label: t('القيم', 'Values'), type: 'tags', ltr: true },
                { key: 'rule', label: t('القاعدة', 'Rule'), type: 'value' },
              ],
            },
          },
          { code: 'menus', type: 'select', multiple: true, label: t('عناصر القائمة', 'Menu items'), ref: 'apps.menu', defaultValue: [] },
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
            options: ['SCREEN', 'API', 'REPORT', 'SERVICE', 'WORKFLOW_ACTION', 'FIELD'].map((v) => opt(v, v, v)),
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
          { ...appRef, hint: t('فارغ لقوائم التطبيق المرتبط', 'Empty for a connected app’s menus') },
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
            options: ['GROUP', 'SCREEN', 'SERVICE', 'REPORT', 'DASHBOARD', 'QUEUE', 'URL'].map((v) => opt(v, v, v)),
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
          { code: 'icon', type: 'icon', label: t('الأيقونة', 'Icon') },
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
          { ...appRef, hint: t('فارغ لقوائم التطبيق المرتبط', 'Empty for a connected app’s menus') },
          displayOrder,
          { code: 'icon', type: 'icon', label: t('الأيقونة', 'Icon') },
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
            options: [opt('CLASS', 'تصنيف', 'Class'), opt('DATA_OBJECT', 'كائن بيانات', 'Data object'), opt('SQL', 'استعلام مقيد', 'Restricted SQL')],
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
            hint: t('للاستعلام: اختياري، الافتراضي value و display_ar و display_en', 'For SQL: optional; defaults to value, display_ar, display_en'),
          },
          {
            code: 'displayArColumn',
            type: 'text',
            label: t('عمود الاسم العربي', 'Arabic label column'),
            visibleWhen: { '!=': [{ var: 'sourceType' }, 'CLASS'] },
            requiredWhen: { '==': [{ var: 'sourceType' }, 'DATA_OBJECT'] },
            hint: t('للاستعلام: اختياري، الافتراضي value و display_ar و display_en', 'For SQL: optional; defaults to value, display_ar, display_en'),
          },
          {
            code: 'displayEnColumn',
            type: 'text',
            label: t('عمود الاسم الإنجليزي', 'English label column'),
            visibleWhen: { '!=': [{ var: 'sourceType' }, 'CLASS'] },
            requiredWhen: { '==': [{ var: 'sourceType' }, 'DATA_OBJECT'] },
            hint: t('للاستعلام: اختياري، الافتراضي value و display_ar و display_en', 'For SQL: optional; defaults to value, display_ar, display_en'),
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
            structure: { kind: 'json' },
            visibleWhen: { '==': [{ var: 'sourceType' }, 'CLASS'] },
          },
          {
            code: 'searchMode',
            type: 'select',
            label: t('البحث', 'Search'),
            defaultValue: 'FULL',
            options: [opt('FULL', 'قائمة كاملة', 'Full list'), opt('TYPEAHEAD', 'بحث أثناء الكتابة', 'Type-ahead')],
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
            structure: {
              kind: 'table',
              columns: [
                { key: 'resourceCode', label: t('الشاشة', 'Screen'), type: 'select', ref: 'apps.resource', required: true },
                { key: 'fieldCode', label: t('الحقل', 'Field'), type: 'text', ltr: true, required: true },
                {
                  key: 'selectionMode',
                  label: t('الاختيار', 'Selection'),
                  type: 'select',
                  required: true,
                  defaultValue: 'SINGLE',
                  options: [opt('SINGLE', 'قيمة واحدة', 'One value'), opt('MULTI', 'عدة قيم', 'Several values')],
                },
              ],
            },
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
            structure: {
              kind: 'table',
              columns: [
                { key: 'name', label: t('اسم العمود', 'Column name'), type: 'text', ltr: true, required: true },
                { key: 'dataType', label: t('نوع البيانات', 'Data type'), type: 'text', ltr: true },
                { key: 'labelAr', label: t('العنوان بالعربية', 'Arabic label'), type: 'text' },
                { key: 'labelEn', label: t('العنوان بالإنجليزية', 'English label'), type: 'text', ltr: true },
                { key: 'filterable', label: t('للتصفية', 'Filterable'), type: 'checkbox' },
                { key: 'searchable', label: t('للبحث', 'Searchable'), type: 'checkbox' },
              ],
            },
          },
          {
            code: 'scopeBindings',
            type: 'json',
            label: t('نطاقات البيانات', 'Data scopes'),
            defaultValue: [],
            structure: {
              kind: 'table',
              columns: [
                { key: 'columnName', label: t('العمود', 'Column'), type: 'text', ltr: true, required: true },
                { key: 'lovCode', label: t('القائمة', 'List'), type: 'select', ref: 'apps.lov', required: true },
                { key: 'appliesTo', label: t('ينطبق على (فارغ: الكل)', 'Applies to (empty: all)'), type: 'tags', ltr: true },
              ],
            },
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
          {
            code: 'value',
            type: 'json',
            label: t('القيمة', 'Value'),
            structure: { kind: 'json' },
            hint: t('نص بين علامتي تنصيص، أو رقم، أو true/false، أو كائن JSON', 'Text in quotes, a number, true/false, or a JSON object'),
          },
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
