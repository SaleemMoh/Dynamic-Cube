import { FormOption, LocalizedText } from '@dynamic-cube/shared';
import { CONFIG_SECTIONS, ConfigField, ConfigRowLink, ConfigSection } from './config-types';

const t = (ar: string, en: string): LocalizedText => ({ ar, en });
const opt = (value: string, ar: string, en: string): FormOption => ({ value, label: t(ar, en) });
const number = (
  code: string,
  label: LocalizedText,
  defaultValue: number,
  hint?: LocalizedText,
): ConfigField => ({ code, type: 'number', label, defaultValue, hint });
const path = (code: string, label: LocalizedText, hint?: LocalizedText): ConfigField => ({
  code,
  type: 'text',
  label,
  hint,
});
const json = (
  code: string,
  label: LocalizedText,
  hint: LocalizedText,
  defaultValue: unknown = [],
): ConfigField => ({ code, type: 'json', label, hint, defaultValue });

/** From a service or a workflow row to the workflow's versions. */
function workflowVersionsLink(param: 'service' | 'workflowCode'): ConfigRowLink {
  return {
    icon: 'account_tree',
    label: t('مسار العمل وإصداراته', 'Workflow and its versions'),
    route: (row) => ({
      commands: ['/workflows/versions'],
      queryParams: { [param]: String(row['code']) },
    }),
    testId: 'workflow-of',
  };
}

const partyKinds = [
  opt('INDIVIDUAL', 'فرد', 'Individual'),
  opt('ESTABLISHMENT', 'منشأة', 'Establishment'),
];
const outcomes = 'SUCCESS | NOT_FOUND | BUSINESS_ERROR | TECHNICAL_ERROR';
const responseCodesHint = t(
  `[{"code": "001-000001", "outcome": "NOT_FOUND", "messageAr": "…", "messageEn": "…"}] — النتائج: ${outcomes}`,
  `[{"code": "001-000001", "outcome": "NOT_FOUND", "messageAr": "…", "messageEn": "…"}] — outcomes: ${outcomes}`,
);

/** Integration Manager configuration screens (internal system → Integrations). */
export const INTEGRATION_SECTIONS: ConfigSection[] = [
  {
    path: 'providers',
    title: t('الجهات المزودة', 'Providers'),
    resource: 'integration.config',
    types: [
      {
        entityType: 'integ.provider',
        title: t('الجهات المزودة', 'Providers'),
        key: 'code',
        fields: [
          {
            code: 'descriptionAr',
            type: 'textarea',
            label: t('الوصف بالعربية', 'Arabic description'),
          },
          {
            code: 'descriptionEn',
            type: 'textarea',
            label: t('الوصف بالإنجليزية', 'English description'),
          },
        ],
      },
    ],
  },
  {
    path: 'connections',
    title: t('الاتصالات', 'Connections'),
    resource: 'integration.config',
    types: [
      {
        entityType: 'integ.connection',
        title: t('الاتصالات', 'Connections'),
        key: 'code',
        columns: ['providerCode', 'baseUrl', 'authType'],
        fields: [
          {
            code: 'providerCode',
            type: 'select',
            label: t('الجهة المزودة', 'Provider'),
            ref: 'integ.provider',
            required: true,
          },
          {
            code: 'baseUrl',
            type: 'text',
            label: t('العنوان الأساسي', 'Base URL'),
            required: true,
            hint: t(
              'مثال: https://apiservicesstg.balady.gov.sa',
              'e.g. https://apiservicesstg.balady.gov.sa',
            ),
          },
          {
            code: 'authType',
            type: 'select',
            label: t('طريقة التوثيق', 'Authentication'),
            required: true,
            defaultValue: 'OAUTH2_CLIENT_CREDENTIALS',
            options: [
              opt('NONE', 'بدون', 'None'),
              opt('BASIC', 'اسم مستخدم وكلمة مرور', 'Basic'),
              opt('API_KEY', 'مفتاح API', 'API key'),
              opt(
                'OAUTH2_CLIENT_CREDENTIALS',
                'OAuth2 (بيانات العميل)',
                'OAuth2 client credentials',
              ),
            ],
          },
          {
            code: 'secretName',
            type: 'text',
            label: t('اسم بيانات الاعتماد', 'Credentials name'),
            hint: t(
              'القيم تُضبط في مخزن الأسرار: Secrets__الاسم__clientId …، ولا تُحفظ في قاعدة البيانات',
              'Values are set in the secret store (Secrets__NAME__clientId …), never in the database',
            ),
            requiredWhen: { '!=': [{ var: 'authType' }, 'NONE'] },
            visibleWhen: { '!=': [{ var: 'authType' }, 'NONE'] },
          },
          {
            code: 'tokenUrl',
            type: 'text',
            label: t('عنوان الحصول على الرمز', 'Token URL'),
            defaultValue: '/oauth/v1/token',
            requiredWhen: { '==': [{ var: 'authType' }, 'OAUTH2_CLIENT_CREDENTIALS'] },
            visibleWhen: { '==': [{ var: 'authType' }, 'OAUTH2_CLIENT_CREDENTIALS'] },
          },
          {
            code: 'apiKeyHeader',
            type: 'text',
            label: t('ترويسة المفتاح', 'API key header'),
            visibleWhen: { '==': [{ var: 'authType' }, 'API_KEY'] },
          },
          number('timeoutSeconds', t('مهلة الاستجابة (ثانية)', 'Timeout (seconds)'), 30),
          number(
            'retryCount',
            t('عدد مرات إعادة المحاولة', 'Retries'),
            2,
            t('للعمليات الآمنة للتكرار فقط', 'Idempotent operations only'),
          ),
          number(
            'circuitBreakerFailures',
            t('الإخفاقات قبل الإيقاف المؤقت', 'Failures before pausing'),
            5,
          ),
          number('circuitBreakerSeconds', t('مدة الإيقاف المؤقت (ثانية)', 'Pause (seconds)'), 60),
          json(
            'headers',
            t('ترويسات ثابتة', 'Fixed headers'),
            t(
              '[{"name": "Accept-Language", "value": "ar"}]',
              '[{"name": "Accept-Language", "value": "ar"}]',
            ),
          ),
        ],
      },
    ],
  },
  {
    path: 'envelopes',
    title: t('أنماط الاستجابة', 'Response envelopes'),
    resource: 'integration.config',
    types: [
      {
        entityType: 'integ.envelope-profile',
        title: t('أنماط الاستجابة', 'Response envelopes'),
        key: 'code',
        columns: ['statusCodePath', 'businessCodePath'],
        fields: [
          path(
            'statusCodePath',
            t('مسار رمز الحالة', 'Status code path'),
            t('مثال: statusDetails.code', 'e.g. statusDetails.code'),
          ),
          path('statusMessagePath', t('مسار رسالة الحالة', 'Status message path')),
          json(
            'successStatusCodes',
            t('رموز الحالة الناجحة', 'Success status codes'),
            t('["200"]', '["200"]'),
          ),
          path(
            'businessCodePath',
            t('مسار رمز العمل', 'Business code path'),
            t('مثال: data.responseCode', 'e.g. data.responseCode'),
          ),
          path('businessMessagePath', t('مسار رسالة العمل', 'Business message path')),
          json(
            'successBusinessCodes',
            t('رموز العمل الناجحة', 'Success business codes'),
            t('["1"]', '["1"]'),
          ),
          path(
            'resultPath',
            t('مسار البيانات', 'Result path'),
            t('مثال: data.result', 'e.g. data.result'),
          ),
          json(
            'responseCodes',
            t('رموز الاستجابة المشتركة', 'Shared response codes'),
            responseCodesHint,
          ),
        ],
      },
    ],
  },
  {
    path: 'operations',
    title: t('العمليات', 'Operations'),
    resource: 'integration.config',
    types: [
      {
        entityType: 'integ.operation',
        title: t('العمليات', 'Operations'),
        key: 'code',
        columns: ['connectionCode', 'method', 'pathTemplate'],
        fields: [
          {
            code: 'connectionCode',
            type: 'select',
            label: t('الاتصال', 'Connection'),
            ref: 'integ.connection',
            required: true,
          },
          {
            code: 'envelopeProfileCode',
            type: 'select',
            label: t('نمط الاستجابة', 'Response envelope'),
            ref: 'integ.envelope-profile',
          },
          {
            code: 'method',
            type: 'select',
            label: t('الطريقة', 'Method'),
            required: true,
            defaultValue: 'GET',
            options: ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'].map((m) => opt(m, m, m)),
          },
          {
            code: 'pathTemplate',
            type: 'text',
            label: t('المسار', 'Path'),
            required: true,
            hint: t(
              'مثال: /v1/MCI-DIP/CR-Information/{crNumber}',
              'e.g. /v1/MCI-DIP/CR-Information/{crNumber}',
            ),
          },
          {
            code: 'descriptionAr',
            type: 'textarea',
            label: t('الوصف بالعربية', 'Arabic description'),
          },
          {
            code: 'descriptionEn',
            type: 'textarea',
            label: t('الوصف بالإنجليزية', 'English description'),
          },
          json(
            'parameters',
            t('المدخلات', 'Inputs'),
            t(
              '[{"name": "crNumber", "location": "PATH", "isRequired": true, "isMasked": false, "sampleValue": "1010000000"}] — المواقع: PATH | QUERY | HEADER | BODY',
              '[{"name": "crNumber", "location": "PATH", "isRequired": true, "isMasked": false, "sampleValue": "1010000000"}] — locations: PATH | QUERY | HEADER | BODY',
            ),
          ),
          json(
            'outputs',
            t('المخرجات', 'Outputs'),
            t(
              '[{"name": "crNumbers", "path": "Licenses[*].Commercial_NO"}]',
              '[{"name": "crNumbers", "path": "Licenses[*].Commercial_NO"}]',
            ),
          ),
          json('responseCodes', t('رموز الاستجابة', 'Response codes'), responseCodesHint),
          json(
            'maskFields',
            t('حقول تُخفى في السجل', 'Fields masked in the log'),
            t('["Commercial_Name", "phoneNumber"]', '["Commercial_Name", "phoneNumber"]'),
          ),
          number(
            'cacheSeconds',
            t('مدة التخزين المؤقت (ثانية)', 'Cache (seconds)'),
            0,
            t('0 = بدون تخزين', '0 = no caching'),
          ),
          {
            code: 'isIdempotent',
            type: 'checkbox',
            label: t('آمنة لإعادة المحاولة', 'Safe to retry'),
          },
        ],
      },
    ],
  },
];

const identifierTypes = (kind: 'INDIVIDUAL' | 'ESTABLISHMENT') =>
  kind === 'INDIVIDUAL'
    ? [
        opt('NATIONAL_ID', 'هوية وطنية', 'National ID'),
        opt('IQAMA', 'إقامة', 'Iqama'),
        opt('GCC_ID', 'هوية خليجية', 'GCC ID'),
        opt('PASSPORT', 'جواز سفر', 'Passport'),
      ]
    : [
        opt('CR_NUMBER', 'سجل تجاري', 'CR number'),
        opt('UNIFIED_NUMBER', 'الرقم الموحد', 'Unified number'),
      ];

/** Party Registry configuration screens (internal system → Party registry). */
export const PARTY_SECTIONS: ConfigSection[] = [
  {
    path: 'relationship-types',
    title: t('أنواع العلاقات', 'Relationship types'),
    resource: 'party.config',
    types: [
      {
        entityType: 'party.relationship-type',
        title: t('أنواع العلاقات', 'Relationship types'),
        key: 'code',
        columns: ['canAct', 'displayOrder'],
        fields: [
          {
            code: 'canAct',
            type: 'checkbox',
            label: t('يحق له التمثيل في البوابة', 'May act for the party in the portal'),
          },
          number('displayOrder', t('الترتيب', 'Display order'), 0),
          json(
            'externalCodes',
            t('رموز الجهات الخارجية', 'Provider codes'),
            t('[{"provider": "MCI", "code": "1"}]', '[{"provider": "MCI", "code": "1"}]'),
          ),
        ],
      },
    ],
  },
  {
    path: 'context-sources',
    title: t('مصادر صفة التمثيل', 'Acting context sources'),
    resource: 'party.config',
    types: [
      {
        entityType: 'party.context-source',
        title: t('مصادر صفة التمثيل', 'Acting context sources'),
        key: 'code',
        columns: ['operationCode', 'principalKind', 'refreshMinutes'],
        fields: [
          {
            code: 'operationCode',
            type: 'select',
            label: t('عملية التكامل', 'Integration operation'),
            ref: 'integ.operation',
            required: true,
          },
          {
            code: 'inputParameter',
            type: 'text',
            label: t('مُدخل رقم الهوية', 'ID number input'),
            required: true,
            defaultValue: 'idNumber',
          },
          path('itemsPath', t('مسار القائمة', 'Items path'), t('مثال: Licenses', 'e.g. Licenses')),
          {
            code: 'principalKind',
            type: 'select',
            label: t('نوع الطرف الممثَّل', 'Represented party kind'),
            options: partyKinds,
            required: true,
            defaultValue: 'ESTABLISHMENT',
          },
          {
            code: 'principalIdentifierType',
            type: 'select',
            label: t('نوع المعرّف', 'Identifier type'),
            options: identifierTypes('ESTABLISHMENT'),
            defaultValue: 'CR_NUMBER',
          },
          path(
            'identifierPath',
            t('مسار المعرّف', 'Identifier path'),
            t('مثال: Commercial_NO', 'e.g. Commercial_NO'),
          ),
          path(
            'nameArPath',
            t('مسار الاسم بالعربية', 'Arabic name path'),
            t('مثال: Commercial_Name', 'e.g. Commercial_Name'),
          ),
          path('nameEnPath', t('مسار الاسم بالإنجليزية', 'English name path')),
          path(
            'relationCodePath',
            t('مسار رمز الصفة', 'Relation code path'),
            t('مثال: RelationTypeInfo.Id', 'e.g. RelationTypeInfo.Id'),
          ),
          {
            code: 'relationProvider',
            type: 'text',
            label: t('جهة رموز الصفة', 'Relation codes provider'),
            hint: t('مثال: MCI', 'e.g. MCI'),
          },
          {
            code: 'defaultRelationshipCode',
            type: 'select',
            label: t('نوع العلاقة الافتراضي', 'Default relationship type'),
            ref: 'party.relationship-type',
          },
          path('statusPath', t('مسار الحالة', 'Status path'), t('مثال: Status', 'e.g. Status')),
          json(
            'activeStatuses',
            t('الحالات المقبولة', 'Accepted statuses'),
            t('["Active"]', '["Active"]'),
          ),
          path('validFromPath', t('مسار تاريخ البداية', 'Valid-from path')),
          path('validToPath', t('مسار تاريخ النهاية', 'Valid-to path')),
          path('servicesPath', t('مسار الخدمات المفوضة', 'Delegated services path')),
          number('refreshMinutes', t('التحديث كل (دقيقة)', 'Refresh every (minutes)'), 60),
          number('displayOrder', t('الترتيب', 'Display order'), 0),
        ],
      },
    ],
  },
  {
    path: 'profile-fields',
    title: t('بيانات الملف', 'Profile fields'),
    resource: 'party.config',
    types: [
      {
        entityType: 'party.profile-field',
        title: t('بيانات الملف', 'Profile fields'),
        key: 'code',
        columns: ['partyKind', 'dataType', 'operationCode'],
        fields: [
          {
            code: 'partyKind',
            type: 'select',
            label: t('نوع الطرف', 'Party kind'),
            options: partyKinds,
            required: true,
            defaultValue: 'ESTABLISHMENT',
          },
          {
            code: 'dataType',
            type: 'select',
            label: t('نوع البيانات', 'Data type'),
            required: true,
            defaultValue: 'TEXT',
            options: [
              opt('TEXT', 'نص', 'Text'),
              opt('NUMBER', 'رقم', 'Number'),
              opt('DATE', 'تاريخ', 'Date'),
              opt('BOOL', 'نعم/لا', 'Yes/no'),
              opt('JSON', 'بيانات مركبة', 'Structured'),
            ],
          },
          {
            code: 'operationCode',
            type: 'select',
            label: t('عملية التكامل', 'Integration operation'),
            ref: 'integ.operation',
            hint: t('اتركه فارغاً للإدخال اليدوي', 'Leave empty for manual entry'),
          },
          {
            code: 'outputName',
            type: 'text',
            label: t('المُخرج', 'Output'),
            requiredWhen: { '!!': [{ var: 'operationCode' }] },
            visibleWhen: { '!!': [{ var: 'operationCode' }] },
          },
          {
            ...json(
              'inputs',
              t('المدخلات', 'Inputs'),
              t(
                '[{"parameter": "crNumber", "identifierType": "CR_NUMBER"}]',
                '[{"parameter": "crNumber", "identifierType": "CR_NUMBER"}]',
              ),
            ),
            visibleWhen: { '!!': [{ var: 'operationCode' }] },
          },
          number('refreshHours', t('التحديث كل (ساعة)', 'Refresh every (hours)'), 24),
          number('displayOrder', t('الترتيب', 'Display order'), 0),
        ],
      },
    ],
  },
  {
    path: 'credential-types',
    title: t('أنواع الشهادات', 'Credential types'),
    resource: 'party.config',
    types: [
      {
        entityType: 'party.credential-type',
        title: t('أنواع الشهادات', 'Credential types'),
        key: 'code',
        columns: ['partyKind', 'issuerAr'],
        fields: [
          {
            code: 'partyKind',
            type: 'select',
            label: t('نوع الطرف', 'Party kind'),
            options: partyKinds,
            required: true,
            defaultValue: 'ESTABLISHMENT',
          },
          { code: 'issuerAr', type: 'text', label: t('جهة الإصدار بالعربية', 'Issuer (Arabic)') },
          {
            code: 'issuerEn',
            type: 'text',
            label: t('جهة الإصدار بالإنجليزية', 'Issuer (English)'),
          },
          {
            code: 'hasExpiry',
            type: 'checkbox',
            label: t('لها تاريخ انتهاء', 'Has an expiry date'),
            defaultValue: true,
          },
        ],
      },
    ],
  },
  {
    path: 'sanction-types',
    title: t('أنواع الجزاءات', 'Sanction types'),
    resource: 'party.config',
    types: [
      {
        entityType: 'party.sanction-type',
        title: t('أنواع الجزاءات', 'Sanction types'),
        key: 'code',
        columns: ['blocksApplications', 'defaultDays'],
        fields: [
          {
            code: 'blocksApplications',
            type: 'checkbox',
            label: t('يمنع تقديم الطلبات', 'Blocks new requests'),
            defaultValue: true,
          },
          {
            code: 'defaultDays',
            type: 'number',
            label: t('المدة الافتراضية (يوم)', 'Default duration (days)'),
          },
        ],
      },
    ],
  },
];

/** Services configuration screens (internal system → Services); versions have their own screen. */
export const SERVICE_SECTIONS: ConfigSection[] = [
  {
    path: 'catalog',
    title: t('الخدمات', 'Services'),
    resource: 'services.config',
    types: [
      {
        entityType: 'svc.service',
        title: t('الخدمات', 'Services'),
        key: 'code',
        columns: ['archetype', 'numberPrefix', 'isListed'],
        // A service's workflow is created with it (completing requests without review until a version adds steps).
        rowLinks: [
          {
            icon: 'construction',
            label: t('فتح في منشئ الخدمات', 'Open in the service builder'),
            route: (row) => ({ commands: ['/services/builder'], queryParams: { service: String(row['code']) } }),
            testId: 'builder-of',
          },
          workflowVersionsLink('service'),
        ],
        fields: [
          {
            code: 'descriptionAr',
            type: 'textarea',
            label: t('الوصف بالعربية', 'Arabic description'),
          },
          {
            code: 'descriptionEn',
            type: 'textarea',
            label: t('الوصف بالإنجليزية', 'English description'),
          },
          {
            code: 'archetype',
            type: 'select',
            label: t('النمط', 'Archetype'),
            required: true,
            defaultValue: 'ISSUANCE',
            options: [
              opt('ISSUANCE', 'إصدار', 'Issuance'),
              opt('AMENDMENT', 'تعديل', 'Amendment'),
              opt('VISIT_REQUEST', 'طلب زيارة', 'Visit request'),
              opt('ATTESTATION', 'تصديق', 'Attestation'),
            ],
          },
          {
            code: 'partyKinds',
            type: 'select',
            label: t('من يحق له التقديم', 'Who may apply'),
            options: partyKinds,
            multiple: true,
            required: true,
            defaultValue: ['ESTABLISHMENT'],
          },
          {
            code: 'categoryCode',
            type: 'select',
            label: t('التصنيف', 'Category'),
            ref: 'svc.category',
          },
          {
            code: 'icon',
            type: 'icon',
            label: t('الأيقونة', 'Icon'),
          },
          number('displayOrder', t('الترتيب', 'Display order'), 0),
          {
            code: 'isListed',
            type: 'checkbox',
            label: t('تظهر في دليل الخدمات', 'Shown in the service catalog'),
            defaultValue: true,
          },
          {
            code: 'numberPrefix',
            type: 'text',
            label: t('بادئة رقم الطلب', 'Request number prefix'),
            hint: t('مثال: CLS ← CLS-2026-000001', 'e.g. CLS → CLS-2026-000001'),
          },
          {
            code: 'documentType',
            type: 'select',
            label: t('الوثيقة التي تُقدَّم عليها الخدمة', 'Document the service acts on'),
            ref: 'doc.document-type',
            hint: t(
              'للخدمات اللاحقة كالتجديد والتمديد: يُقدَّم الطلب على إحدى وثائق المستفيد من هذا النوع',
              "For follow-up services such as renewal or extension: each request is made for one of the beneficiary's documents of this type",
            ),
          },
          {
            code: 'draftsAllowed',
            type: 'checkbox',
            label: t('السماح بحفظ المسودات', 'Drafts allowed'),
            defaultValue: true,
          },
          number('draftExpiryDays', t('صلاحية المسودة (يوم)', 'Draft expiry (days)'), 30),
          {
            code: 'onNewVersion',
            type: 'select',
            label: t('المسودات عند نشر إصدار جديد', 'Drafts when a new version is published'),
            defaultValue: 'CONTINUE',
            options: [
              opt('CONTINUE', 'تكمل على إصدارها', 'Continue on their version'),
              opt('RESTART', 'تنتقل للإصدار الجديد', 'Move to the new version'),
            ],
          },
        ],
      },
    ],
  },
  {
    path: 'categories',
    title: t('تصنيفات الخدمات', 'Service categories'),
    resource: 'services.config',
    types: [
      {
        entityType: 'svc.category',
        title: t('تصنيفات الخدمات', 'Service categories'),
        key: 'code',
        columns: ['icon', 'displayOrder'],
        // The portal shows a tab per category on its home page and a filter in its search.
        fields: [
          {
            code: 'descriptionAr',
            type: 'textarea',
            label: t('الوصف بالعربية', 'Arabic description'),
          },
          {
            code: 'descriptionEn',
            type: 'textarea',
            label: t('الوصف بالإنجليزية', 'English description'),
          },
          {
            code: 'icon',
            type: 'icon',
            label: t('الأيقونة', 'Icon'),
          },
          number('displayOrder', t('الترتيب', 'Display order'), 0),
        ],
      },
    ],
  },
  {
    path: 'questions',
    title: t('بنك الأسئلة', 'Question bank'),
    resource: 'services.config',
    types: [
      {
        entityType: 'svc.question',
        title: t('بنك الأسئلة', 'Question bank'),
        key: 'code',
        columns: ['type', 'lov', 'prefill'],
        nameLabels: {
          ar: t('نص السؤال بالعربية', 'Arabic label'),
          en: t('نص السؤال بالإنجليزية', 'English label'),
        },
        fields: [
          {
            code: 'type',
            type: 'select',
            label: t('نوع الإجابة', 'Answer type'),
            required: true,
            defaultValue: 'TEXT',
            options: [
              opt('TEXT', 'نص', 'Text'),
              opt('TEXTAREA', 'نص طويل', 'Long text'),
              opt('NUMBER', 'رقم', 'Number'),
              opt('DATE', 'تاريخ', 'Date'),
              opt('SELECT', 'اختيار', 'Choice'),
              opt('CHECKBOX', 'نعم/لا', 'Yes/no'),
            ],
          },
          { code: 'hintAr', type: 'text', label: t('تلميح بالعربية', 'Arabic hint') },
          { code: 'hintEn', type: 'text', label: t('تلميح بالإنجليزية', 'English hint') },
          {
            code: 'lov',
            type: 'select',
            label: t('قائمة القيم', 'List of values'),
            ref: 'apps.lov',
            visibleWhen: { '==': [{ var: 'type' }, 'SELECT'] },
          },
          {
            code: 'lovParentQuestion',
            type: 'select',
            label: t('السؤال الأب', 'Parent question'),
            ref: 'svc.question',
            visibleWhen: { '!!': [{ var: 'lov' }] },
          },
          {
            ...json(
              'options',
              t('الخيارات', 'Options'),
              t(
                '[{"value": "ICT", "labelAr": "…", "labelEn": "…"}] — عند عدم استخدام قائمة قيم',
                '[{"value": "ICT", "labelAr": "…", "labelEn": "…"}] — when no list of values is used',
              ),
            ),
            visibleWhen: { '==': [{ var: 'type' }, 'SELECT'] },
          },
          {
            code: 'multiple',
            type: 'checkbox',
            label: t('اختيار متعدد', 'Several choices'),
            visibleWhen: { '==': [{ var: 'type' }, 'SELECT'] },
          },
          json(
            'validation',
            t('القيود', 'Limits'),
            t(
              '{"min": 0, "max": 100, "maxLength": 200, "pattern": "^[0-9]{10}$"}',
              '{"min": 0, "max": 100, "maxLength": 200, "pattern": "^[0-9]{10}$"}',
            ),
            {},
          ),
          {
            code: 'prefill',
            type: 'text',
            label: t('تعبئة من بيانات المتعامل', 'Prefill from the party'),
            hint: t(
              'مثال: party.identifiers.CR_NUMBER أو party.profile.CR_CAPITAL',
              'e.g. party.identifiers.CR_NUMBER or party.profile.CR_CAPITAL',
            ),
          },
        ],
      },
    ],
  },
];

/**
 * Workflow configuration screens (internal system → Action Center → Workflows); versions have their own screen. Every
 * service gets its workflow when it is created, so workflows are not added here.
 */
export const WORKFLOW_SECTIONS: ConfigSection[] = [
  {
    path: 'workflows',
    title: t('مسارات العمل', 'Workflows'),
    resource: 'workflows.config',
    types: [
      {
        entityType: 'wf.workflow',
        title: t('مسارات العمل', 'Workflows'),
        key: 'code',
        columns: ['serviceCode'],
        noCreate: true,
        rowLinks: [workflowVersionsLink('workflowCode')],
        fields: [
          {
            code: 'serviceCode',
            type: 'select',
            label: t('الخدمة', 'Service'),
            ref: 'svc.service',
            required: true,
            lockedOnEdit: true,
          },
          {
            code: 'descriptionAr',
            type: 'textarea',
            label: t('الوصف بالعربية', 'Arabic description'),
          },
          {
            code: 'descriptionEn',
            type: 'textarea',
            label: t('الوصف بالإنجليزية', 'English description'),
          },
        ],
      },
    ],
  },
];

const fieldLabelsHint = t(
  '[{"code": "city", "nameAr": "المدينة", "nameEn": "City", "isPublic": true}] — isPublic: يظهر في صفحة التحقق العامة',
  '[{"code": "city", "nameAr": "المدينة", "nameEn": "City", "isPublic": true}] — isPublic: shown on the public verification page',
);

/** Document configuration screens (internal system → Documents); the print template editor shows a live preview. */
export const DOCUMENT_SECTIONS: ConfigSection[] = [
  {
    path: 'document-types',
    title: t('أنواع المستندات', 'Document types'),
    resource: 'documents.config',
    types: [
      {
        entityType: 'doc.document-type',
        title: t('أنواع المستندات', 'Document types'),
        key: 'code',
        columns: ['numberPrefix', 'validityMonths', 'onePerHolder', 'templateCode'],
        fields: [
          {
            code: 'descriptionAr',
            type: 'textarea',
            label: t('الوصف بالعربية', 'Arabic description'),
          },
          {
            code: 'descriptionEn',
            type: 'textarea',
            label: t('الوصف بالإنجليزية', 'English description'),
          },
          {
            code: 'numberPrefix',
            type: 'text',
            label: t('بادئة الرقم', 'Number prefix'),
            required: true,
            hint: t('مثال: CC ← CC-2026-000001', 'e.g. CC → CC-2026-000001'),
          },
          {
            code: 'validityMonths',
            type: 'number',
            label: t('مدة الصلاحية (شهر)', 'Validity (months)'),
            hint: t('فارغ: لا ينتهي', 'Empty: does not expire'),
          },
          {
            code: 'onePerHolder',
            type: 'checkbox',
            label: t(
              'مستند واحد لكل صاحب (الإصدار مرة أخرى يعدّله)',
              'One per holder (issuing again amends it)',
            ),
          },
          {
            code: 'amendResetsValidity',
            type: 'checkbox',
            label: t('التعديل يبدأ الصلاحية من جديد', 'An amendment restarts the validity'),
            visibleWhen: { '!!': [{ var: 'onePerHolder' }] },
          },
          {
            code: 'templateCode',
            type: 'select',
            label: t('قالب الطباعة', 'Print template'),
            ref: 'doc.template',
            required: true,
          },
          json(
            'alertDaysBefore',
            t('أيام التذكير قبل الانتهاء', 'Reminder days before expiry'),
            t('مثال: [90, 30]', 'e.g. [90, 30]'),
          ),
          json('fields', t('حقول البيانات', 'Data fields'), fieldLabelsHint),
          json('lineFields', t('حقول البنود', 'Line-item fields'), fieldLabelsHint),
        ],
      },
    ],
  },
  {
    path: 'document-templates',
    title: t('قوالب الطباعة', 'Print templates'),
    resource: 'documents.config',
    types: [
      {
        entityType: 'doc.template',
        title: t('قوالب الطباعة', 'Print templates'),
        key: 'code',
        columns: ['pageSize', 'landscape'],
        editor: () =>
          import('../../documents/template-editor').then((m) => m.DocumentTemplateEditor),
        fields: [],
      },
    ],
  },
];

/** Decision rule configuration screens (internal system → Decisions); versions have their own screen. */
export const DECISION_SECTIONS: ConfigSection[] = [
  {
    path: 'rule-sets',
    title: t('مجموعات القواعد', 'Rule sets'),
    resource: 'decisions.config',
    types: [
      {
        entityType: 'dec.rule-set',
        title: t('مجموعات القواعد', 'Rule sets'),
        key: 'code',
        columns: ['engine', 'isPublic'],
        fields: [
          {
            code: 'descriptionAr',
            type: 'textarea',
            label: t('الوصف بالعربية', 'Arabic description'),
          },
          {
            code: 'descriptionEn',
            type: 'textarea',
            label: t('الوصف بالإنجليزية', 'English description'),
          },
          {
            code: 'engine',
            type: 'select',
            label: t('محرك القواعد', 'Engine'),
            required: true,
            defaultValue: 'RULES',
            optionsUrl: '/api/decisions/v1/engines',
          },
          {
            code: 'isPublic',
            type: 'checkbox',
            label: t(
              'حاسبة عامة في البوابة (بدون تسجيل دخول)',
              'Public calculator on the portal (no sign-in)',
            ),
          },
        ],
      },
    ],
  },
];

const channels = [
  opt('IN_APP', 'داخل المنصة', 'In-app'),
  opt('EMAIL', 'البريد الإلكتروني', 'Email'),
  opt('SMS', 'رسالة نصية', 'SMS'),
];

/** Notification screens (internal system → Notifications); the message template editor previews on an event's sample. */
export const COMMUNICATION_SECTIONS: ConfigSection[] = [
  {
    path: 'notification-rules',
    title: t('قواعد التنبيه', 'Notification rules'),
    resource: 'communication.config',
    types: [
      {
        entityType: 'com.rule',
        title: t('قواعد التنبيه', 'Notification rules'),
        key: 'code',
        columns: ['eventCode', 'templateCode', 'channels'],
        editor: () => import('../../communication/notification-rule-editor').then((m) => m.NotificationRuleEditor),
        fields: [
          {
            code: 'eventCode',
            type: 'select',
            label: t('الحدث', 'Event'),
            required: true,
            optionsUrl: '/api/communication/v1/events',
          },
          {
            code: 'templateCode',
            type: 'select',
            label: t('قالب الرسالة', 'Message template'),
            ref: 'com.template',
            required: true,
          },
          {
            code: 'channels',
            type: 'select',
            label: t('القنوات', 'Channels'),
            options: channels,
            multiple: true,
            required: true,
            defaultValue: ['IN_APP'],
          },
          json(
            'recipients',
            t('المستلمون', 'Recipients'),
            t(
              '[{"kind": "PARTY"}, {"kind": "ROLE", "value": "CLS_MANAGER"}, {"kind": "USER", "value": {"var": "task.assignee"}}] — PARTY: المتعامل (افتراضياً party.id)',
              '[{"kind": "PARTY"}, {"kind": "ROLE", "value": "CLS_MANAGER"}, {"kind": "USER", "value": {"var": "task.assignee"}}] — PARTY: the party (party.id by default)',
            ),
            [{ kind: 'PARTY' }],
          ),
          json(
            'condition',
            t('الشرط (JSONLogic)', 'Condition (JSONLogic)'),
            t(
              'فارغ: دائماً. مثال: {"==": [{"var": "document.alertDays"}, 90]}',
              'Empty: always. e.g. {"==": [{"var": "document.alertDays"}, 90]}',
            ),
            null,
          ),
          {
            code: 'language',
            type: 'select',
            label: t('لغة البريد والرسائل النصية', 'Language of email and SMS'),
            defaultValue: 'AR',
            options: [opt('AR', 'العربية', 'Arabic'), opt('EN', 'الإنجليزية', 'English')],
          },
        ],
      },
    ],
  },
  {
    path: 'message-templates',
    title: t('قوالب الرسائل', 'Message templates'),
    resource: 'communication.config',
    types: [
      {
        entityType: 'com.template',
        title: t('قوالب الرسائل', 'Message templates'),
        key: 'code',
        editor: () =>
          import('../../communication/message-template-editor').then(
            (m) => m.MessageTemplateEditor,
          ),
        fields: [],
      },
    ],
  },
];

/** Payment configuration screens (internal system → Payments). */
export const PAYMENT_SECTIONS: ConfigSection[] = [
  {
    path: 'payment-providers',
    title: t('مزودو الدفع', 'Payment providers'),
    resource: 'payments.config',
    types: [
      {
        entityType: 'pay.provider',
        title: t('مزودو الدفع', 'Payment providers'),
        key: 'code',
        columns: ['createOperation', 'billerCode', 'dueDays', 'isDefault'],
        fields: [
          {
            code: 'createOperation',
            type: 'select',
            label: t('عملية إنشاء الفاتورة', 'Bill creation operation'),
            ref: 'integ.operation',
            hint: t(
              'اتركه فارغاً للسداد اليدوي (الصندوق)',
              'Leave empty for manual payment (cashier)',
            ),
          },
          path(
            'billNumberOutput',
            t('رقم الفاتورة في المخرجات', 'Bill number in the output'),
            t('مثال: sadadNumber', 'e.g. sadadNumber'),
          ),
          { code: 'billerCode', type: 'text', label: t('رمز المفوتر', 'Biller code') },
          number('dueDays', t('مدة السداد (يوم)', 'Days to pay'), 7),
          {
            code: 'payUrlTemplate',
            type: 'text',
            label: t('رابط السداد', 'Payment page URL'),
            hint: t(
              'مثال: https://…/pay/{billNumber}?amount={amount}',
              'e.g. https://…/pay/{billNumber}?amount={amount}',
            ),
          },
          {
            code: 'instructionsAr',
            type: 'textarea',
            label: t('طريقة السداد بالعربية', 'How to pay (Arabic)'),
          },
          {
            code: 'instructionsEn',
            type: 'textarea',
            label: t('طريقة السداد بالإنجليزية', 'How to pay (English)'),
          },
          {
            code: 'callbackSecret',
            type: 'text',
            label: t('اسم سر توقيع الإشعارات', 'Callback signing secret name'),
            hint: t(
              'اسم السر في مخزن الأسرار وليس قيمته',
              "The secret's name in the secret store, never its value",
            ),
          },
          json(
            'callback',
            t('حقول إشعار السداد', 'Payment notification fields'),
            t(
              '{"billNumber": "sadadNumber", "status": "status", "paidValue": "PAID", "amount": "amount", "reference": "paymentReference"}',
              '{"billNumber": "sadadNumber", "status": "status", "paidValue": "PAID", "amount": "amount", "reference": "paymentReference"}',
            ),
            {},
          ),
          { code: 'isDefault', type: 'checkbox', label: t('المزود الافتراضي', 'Default provider') },
        ],
      },
    ],
  },
];

const contentEditor = () => import('../../content/content-editor').then((m) => m.ContentEditor);
const contentOrder = number('order', t('الترتيب', 'Order'), 0);

export const CONTENT_SECTIONS: ConfigSection[] = [
  {
    path: 'content-faqs',
    title: t('الأسئلة الشائعة', 'FAQs'),
    resource: 'content.config',
    types: [
      {
        entityType: 'cms.faq',
        title: t('الأسئلة الشائعة', 'FAQs'),
        key: 'code',
        columns: ['topicAr', 'serviceCode', 'order'],
        nameLabels: { ar: t('السؤال بالعربية', 'Question (Arabic)'), en: t('السؤال بالإنجليزية', 'Question (English)') },
        editor: contentEditor,
        fields: [
          { code: 'answerAr', type: 'textarea', label: t('الإجابة بالعربية', 'Answer (Arabic)'), required: true },
          { code: 'answerEn', type: 'textarea', label: t('الإجابة بالإنجليزية', 'Answer (English)'), required: true },
          { code: 'topicAr', type: 'text', label: t('الموضوع بالعربية', 'Topic (Arabic)') },
          { code: 'topicEn', type: 'text', label: t('الموضوع بالإنجليزية', 'Topic (English)') },
          { code: 'serviceCode', type: 'select', label: t('الخدمة', 'Service'), ref: 'svc.service' },
          contentOrder,
        ],
      },
    ],
  },
  {
    path: 'content-pages',
    title: t('صفحات البوابة', 'Portal pages'),
    resource: 'content.config',
    types: [
      {
        entityType: 'cms.page',
        title: t('صفحات البوابة', 'Portal pages'),
        key: 'code',
        columns: ['showInFooter', 'order'],
        nameLabels: { ar: t('العنوان بالعربية', 'Title (Arabic)'), en: t('العنوان بالإنجليزية', 'Title (English)') },
        editor: contentEditor,
        fields: [
          { code: 'bodyAr', type: 'textarea', label: t('المحتوى بالعربية (Markdown)', 'Content in Arabic (Markdown)'), required: true },
          { code: 'bodyEn', type: 'textarea', label: t('المحتوى بالإنجليزية (Markdown)', 'Content in English (Markdown)'), required: true },
          { code: 'showInFooter', type: 'checkbox', label: t('رابط في تذييل البوابة', 'Link in the portal footer') },
          contentOrder,
        ],
      },
    ],
  },
];

export const ALL_CONFIG_SECTIONS: ConfigSection[] = [
  ...CONFIG_SECTIONS,
  ...INTEGRATION_SECTIONS,
  ...PARTY_SECTIONS,
  ...SERVICE_SECTIONS,
  ...WORKFLOW_SECTIONS,
  ...DOCUMENT_SECTIONS,
  ...DECISION_SECTIONS,
  ...COMMUNICATION_SECTIONS,
  ...PAYMENT_SECTIONS,
  ...CONTENT_SECTIONS,
];
