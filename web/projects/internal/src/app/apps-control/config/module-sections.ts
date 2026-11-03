import { FormOption, LocalizedText, StructuredColumn, StructuredShape } from '@dynamic-cube/shared';
import { CONFIG_SECTIONS, ConfigField, ConfigRowLink, ConfigSection } from './config-types';

const t = (ar: string, en: string): LocalizedText => ({ ar, en });
const opt = (value: string, ar: string, en: string): FormOption => ({ value, label: t(ar, en) });
const number = (code: string, label: LocalizedText, defaultValue: number, hint?: LocalizedText): ConfigField => ({
  code,
  type: 'number',
  label,
  defaultValue,
  hint,
});
const path = (code: string, label: LocalizedText, hint?: LocalizedText): ConfigField => ({
  code,
  type: 'text',
  label,
  hint,
});
/** Nested data edited through a form made for its shape (rows, a list, properties), with a JSON view. */
const json = (code: string, label: LocalizedText, structure: StructuredShape, hint?: LocalizedText, defaultValue: unknown = []): ConfigField => ({
  code,
  type: 'json',
  label,
  structure,
  hint,
  defaultValue,
});
const col = (key: string, label: LocalizedText, type: StructuredColumn['type'] = 'text', extra: Partial<StructuredColumn> = {}): StructuredColumn => ({
  key,
  label,
  type,
  ...extra,
});
const table = (...columns: StructuredColumn[]): StructuredShape => ({ kind: 'table', columns });
const list = (item: 'text' | 'number' = 'text'): StructuredShape => ({ kind: 'list', item });
const object = (...properties: StructuredColumn[]): StructuredShape => ({ kind: 'object', properties });
/** A JSONLogic rule: written as JSON. */
const JSON_RULE: StructuredShape = { kind: 'json' };
const REQ: Partial<StructuredColumn> = { required: true };
const LTR: Partial<StructuredColumn> = { ltr: true };
const LTR_REQ: Partial<StructuredColumn> = { ltr: true, required: true };

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

const partyKinds = [opt('INDIVIDUAL', 'فرد', 'Individual'), opt('ESTABLISHMENT', 'منشأة', 'Establishment')];
/** What a provider's response code means, with the message shown for it. */
const responseCodes = table(
  col('code', t('الرمز', 'Code'), 'text', LTR_REQ),
  col('outcome', t('النتيجة', 'Outcome'), 'select', {
    required: true,
    options: [
      opt('SUCCESS', 'نجاح', 'Success'),
      opt('NOT_FOUND', 'غير موجود', 'Not found'),
      opt('BUSINESS_ERROR', 'خطأ عمل', 'Business error'),
      opt('TECHNICAL_ERROR', 'خطأ فني', 'Technical error'),
    ],
  }),
  col('messageAr', t('الرسالة بالعربية', 'Arabic message')),
  col('messageEn', t('الرسالة بالإنجليزية', 'English message'), 'text', LTR),
);
const parameterLocations = [
  opt('PATH', 'المسار', 'Path'),
  opt('QUERY', 'الاستعلام', 'Query'),
  opt('HEADER', 'الترويسة', 'Header'),
  opt('BODY', 'المحتوى', 'Body'),
];

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
            hint: t('مثال: https://apiservicesstg.balady.gov.sa', 'e.g. https://apiservicesstg.balady.gov.sa'),
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
              opt('OAUTH2_CLIENT_CREDENTIALS', 'OAuth2 (بيانات العميل)', 'OAuth2 client credentials'),
              opt('WS_SECURITY', 'WS-Security — رمز اسم المستخدم (SOAP)', 'WS-Security UsernameToken (SOAP)'),
            ],
          },
          {
            code: 'secretName',
            type: 'text',
            label: t('اسم بيانات الاعتماد', 'Credentials name'),
            hint: t(
              'القيم تُضبط في مخزن الأسرار: Secrets__الاسم__clientId … (أو username وpassword لـ Basic وWS-Security)، ولا تُحفظ في قاعدة البيانات',
              'Values are set in the secret store (Secrets__NAME__clientId …, or username and password for Basic and WS-Security), never in the database',
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
          {
            code: 'passwordDigest',
            type: 'checkbox',
            label: t('إرسال كلمة المرور مختصرة (Digest)', 'Send the password as a digest'),
            hint: t(
              'بدلاً من نص كلمة المرور يُرسل مختصرها مع رقم عشوائي ووقت الطلب، حسب ما تطلبه الجهة',
              'Instead of the password text, a digest with a nonce and the time is sent, as the provider requires',
            ),
            visibleWhen: { '==': [{ var: 'authType' }, 'WS_SECURITY'] },
          },
          number('timeoutSeconds', t('مهلة الاستجابة (ثانية)', 'Timeout (seconds)'), 30),
          number('retryCount', t('عدد مرات إعادة المحاولة', 'Retries'), 2, t('للعمليات الآمنة للتكرار فقط', 'Idempotent operations only')),
          number('circuitBreakerFailures', t('الإخفاقات قبل الإيقاف المؤقت', 'Failures before pausing'), 5),
          number('circuitBreakerSeconds', t('مدة الإيقاف المؤقت (ثانية)', 'Pause (seconds)'), 60),
          json(
            'headers',
            t('ترويسات ثابتة', 'Fixed headers'),
            table(col('name', t('الاسم', 'Name'), 'text', LTR_REQ), col('value', t('القيمة', 'Value'), 'text', LTR)),
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
          path('statusCodePath', t('مسار رمز الحالة', 'Status code path'), t('مثال: statusDetails.code', 'e.g. statusDetails.code')),
          path('statusMessagePath', t('مسار رسالة الحالة', 'Status message path')),
          json('successStatusCodes', t('رموز الحالة الناجحة', 'Success status codes'), list(), t('مثال: 200', 'e.g. 200')),
          path('businessCodePath', t('مسار رمز العمل', 'Business code path'), t('مثال: data.responseCode', 'e.g. data.responseCode')),
          path('businessMessagePath', t('مسار رسالة العمل', 'Business message path')),
          json('successBusinessCodes', t('رموز العمل الناجحة', 'Success business codes'), list(), t('مثال: 1', 'e.g. 1')),
          path('resultPath', t('مسار البيانات', 'Result path'), t('مثال: data.result', 'e.g. data.result')),
          json('responseCodes', t('رموز الاستجابة المشتركة', 'Shared response codes'), responseCodes),
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
        // The operation designer is the way in (ADR-0041); the import reads OpenAPI / Swagger descriptions.
        editor: () => import('../../integration/operation-designer').then((m) => m.OperationDesigner),
        intro: () => import('../../integration/openapi-intro').then((m) => m.OpenApiIntro),
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
            hint: t('مثال: /v1/MCI-DIP/CR-Information/{crNumber}', 'e.g. /v1/MCI-DIP/CR-Information/{crNumber}'),
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
            table(
              col('name', t('الاسم', 'Name'), 'text', LTR_REQ),
              col('location', t('الموقع', 'Location'), 'select', { required: true, defaultValue: 'QUERY', options: parameterLocations }),
              col('target', t('الاسم لدى المزود', 'Name at the provider'), 'text', LTR),
              col('isRequired', t('إلزامي', 'Required'), 'checkbox', { defaultValue: true }),
              col('isMasked', t('يُخفى في السجل', 'Masked in the log'), 'checkbox'),
              col('sampleValue', t('قيمة للتجربة', 'Sample value'), 'text', LTR),
            ),
          ),
          json(
            'outputs',
            t('المخرجات', 'Outputs'),
            table(col('name', t('الاسم', 'Name'), 'text', LTR_REQ), col('path', t('المسار في الاستجابة', 'Path in the response'), 'text', LTR_REQ)),
            t('مثال للمسار: Licenses[*].Commercial_NO', 'Path example: Licenses[*].Commercial_NO'),
          ),
          json('responseCodes', t('رموز الاستجابة', 'Response codes'), responseCodes),
          json('maskFields', t('حقول تُخفى في السجل', 'Fields masked in the log'), list(), t('مثال: phoneNumber', 'e.g. phoneNumber')),
          number('cacheSeconds', t('مدة التخزين المؤقت (ثانية)', 'Cache (seconds)'), 0, t('0 = بدون تخزين', '0 = no caching')),
          {
            code: 'isIdempotent',
            type: 'checkbox',
            label: t('آمنة لإعادة المحاولة', 'Safe to retry'),
          },
        ],
      },
    ],
  },
  {
    path: 'api-endpoints',
    title: t('نقاط الواجهة العامة', 'API endpoints'),
    resource: 'integration.config',
    types: [
      {
        entityType: 'integ.api-endpoint',
        title: t('نقاط الواجهة العامة', 'API endpoints'),
        key: 'code',
        columns: ['path', 'kind', 'function'],
        // Read-only endpoints partners call on the external host (ADR-0042), designed in their own dialog.
        editor: () => import('../../integration/api-endpoint-designer').then((m) => m.ApiEndpointDesigner),
        intro: () => import('../../integration/api-endpoints-intro').then((m) => m.ApiEndpointsIntro),
        fields: [
          { code: 'path', type: 'text', label: t('المسار', 'Path'), required: true },
          {
            code: 'kind',
            type: 'select',
            label: t('ما تفعله', 'What it does'),
            options: [opt('QUERY', 'قراءة بيانات', 'Read data'), opt('FUNCTION', 'وظيفة من النظام', 'System function')],
          },
          { code: 'function', type: 'text', label: t('الوظيفة', 'Function') },
        ],
      },
    ],
  },
  {
    path: 'api-clients',
    title: t('عملاء الواجهة العامة', 'API clients'),
    resource: 'integration.config',
    types: [
      {
        entityType: 'integ.api-client',
        title: t('عملاء الواجهة العامة', 'API clients'),
        key: 'code',
        columns: ['clientId', 'account', 'rateLimitPerMinute'],
        intro: () => import('../../integration/api-endpoints-intro').then((m) => m.ApiEndpointsIntro),
        // What the client may call and see is its service account's (ADR-0042): the same user is edited here and on Users.
        // The partner's sign-in client (client credentials only) and its OpenAPI description.
        rowActions: [
          {
            icon: 'admin_panel_settings',
            label: t('الأدوار والصلاحيات (الحساب الخدمي)', 'Roles and privileges (service account)'),
            testId: 'api-client-access',
            dialog: () => import('../../integration/api-client-access').then((m) => m.ApiClientAccessDialog),
          },
          {
            icon: 'key',
            label: t('الدخول وبيانات الربط', 'Sign-in and credentials'),
            testId: 'api-client-sign-in',
            width: '48rem',
            dialog: () => import('../../integration/api-client-sign-in').then((m) => m.ApiClientSignInDialog),
          },
        ],
        fields: [
          {
            code: 'clientId',
            type: 'text',
            label: t('معرّف العميل (client_id)', 'Client id (client_id)'),
            hint: t('يُترك فارغًا ليُقترح من الرمز (dc-api-…)', 'Leave empty to use one from the code (dc-api-…)'),
          },
          {
            code: 'account',
            type: 'text',
            label: t('الحساب الخدمي', 'Service account'),
            hint: t(
              'يُترك فارغًا ليُنشأ حسابه (svc-…)؛ أدواره تحدد النقاط والشركات والفروع والبيانات',
              'Leave empty to create its own (svc-…); its roles give the endpoints, companies, branches and data',
            ),
            lockedOnEdit: true,
          },
          number('rateLimitPerMinute', t('استدعاءات في الدقيقة', 'Calls per minute'), 60),
          { code: 'contactEmail', type: 'text', label: t('بريد التواصل', 'Contact email') },
          { code: 'descriptionAr', type: 'textarea', label: t('الوصف بالعربية', 'Arabic description') },
          { code: 'descriptionEn', type: 'textarea', label: t('الوصف بالإنجليزية', 'English description') },
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
    : [opt('CR_NUMBER', 'سجل تجاري', 'CR number'), opt('UNIFIED_NUMBER', 'الرقم الموحد', 'Unified number')];

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
            table(col('provider', t('المزود', 'Provider'), 'text', LTR_REQ), col('code', t('الرمز لدى المزود', 'Code at the provider'), 'text', LTR_REQ)),
          ),
        ],
      },
    ],
  },
  {
    path: 'record-lists',
    title: t('قوائم السجلات', 'Record lists'),
    resource: 'party.config',
    types: [
      {
        entityType: 'party.record-list',
        title: t('قوائم السجلات', 'Record lists'),
        key: 'code',
        columns: ['partyKind', 'allowManual'],
        fields: [
          {
            code: 'partyKind',
            type: 'select',
            label: t('نوع الطرف', 'Party kind'),
            options: partyKinds,
            required: true,
            defaultValue: 'ESTABLISHMENT',
          },
          { code: 'descriptionAr', type: 'textarea', label: t('الوصف بالعربية', 'Arabic description') },
          { code: 'descriptionEn', type: 'textarea', label: t('الوصف بالإنجليزية', 'English description') },
          { code: 'icon', type: 'icon', label: t('الأيقونة', 'Icon') },
          {
            code: 'allowManual',
            type: 'checkbox',
            label: t('يضيف مقدم الطلب سجلات بنفسه', 'The applicant adds records by hand'),
            defaultValue: true,
          },
          json(
            'columns',
            t('الأعمدة', 'Columns'),
            table(
              col('code', t('الرمز', 'Code'), 'text', LTR_REQ),
              col('labelAr', t('الاسم بالعربية', 'Arabic label'), 'text', REQ),
              col('labelEn', t('الاسم بالإنجليزية', 'English label'), 'text', LTR_REQ),
              col('type', t('النوع', 'Type'), 'select', {
                required: true,
                defaultValue: 'TEXT',
                options: [
                  opt('TEXT', 'نص', 'Text'),
                  opt('NUMBER', 'رقم', 'Number'),
                  opt('DATE', 'تاريخ', 'Date'),
                  opt('CHECKBOX', 'نعم/لا', 'Yes/no'),
                  opt('SELECT', 'اختيار', 'Choice'),
                ],
              }),
              col('required', t('مطلوب', 'Required'), 'checkbox'),
              col('inTable', t('يظهر في الجدول', 'In the table'), 'checkbox'),
              col('options', t('الخيارات', 'Options'), 'table', {
                columns: [
                  col('value', t('القيمة', 'Value'), 'text', LTR_REQ),
                  col('labelAr', t('الاسم بالعربية', 'Arabic label'), 'text', REQ),
                  col('labelEn', t('الاسم بالإنجليزية', 'English label'), 'text', LTR_REQ),
                ],
              }),
            ),
            t(
              'أسئلة «الصفوف المتكررة» في الخدمات التي تختار من هذه القائمة تأخذ قيمة كل عمود في العمود الذي له الرمز نفسه.',
              'Rows questions of services that pick from this list take each column into the column of the same code.',
            ),
          ),
          json(
            'import',
            t('الجلب من نظام آخر', 'Import from another system'),
            object(
              col('operationCode', t('العملية', 'Operation'), 'select', { ref: 'integ.operation' }),
              col('inputs', t('المدخلات', 'Inputs'), 'table', {
                columns: [
                  col('parameter', t('مدخل العملية', 'Operation input'), 'text', LTR_REQ),
                  col('identifierType', t('المعرّف', 'Identifier'), 'select', {
                    required: true,
                    options: [...identifierTypes('INDIVIDUAL'), ...identifierTypes('ESTABLISHMENT')],
                  }),
                ],
              }),
              col('itemsPath', t('مسار القائمة في النتيجة', 'Path of the items in the result'), 'text', LTR),
              col('keyColumn', t('عمود المفتاح', 'Key column'), 'text', LTR),
              col('fields', t('مواقع الأعمدة', 'Where each column is'), 'table', {
                columns: [col('column', t('العمود', 'Column'), 'text', LTR_REQ), col('path', t('المسار في العنصر', 'Path in an item'), 'text', LTR_REQ)],
              }),
            ),
            t(
              'اختياري. تُستدعى العملية بمعرّف الطرف، وتُقرأ عناصر القائمة من المسار، ويُحدَّث كل سجل مجلوب بقيمة عمود المفتاح؛ وتُحذف السجلات المجلوبة التي لم تعد في النتيجة.',
              "Optional. The operation is called with the party's identifier, the items are read at the path, and each imported record is updated by its key column; imported records no longer returned are removed.",
            ),
            null,
          ),
          number('displayOrder', t('الترتيب', 'Display order'), 0),
        ],
      },
    ],
  },
  {
    path: 'authority-types',
    title: t('أنواع التفويض', 'Authority types'),
    resource: 'party.config',
    types: [
      {
        entityType: 'party.authority-type',
        title: t('أنواع التفويض', 'Authority types'),
        key: 'code',
        columns: ['relationshipCode', 'operationCode', 'requiresExpiry'],
        fields: [
          { code: 'descriptionAr', type: 'textarea', label: t('الوصف بالعربية', 'Arabic description') },
          { code: 'descriptionEn', type: 'textarea', label: t('الوصف بالإنجليزية', 'English description') },
          {
            code: 'relationshipCode',
            type: 'select',
            label: t('العلاقة التي تُسجل', 'Relationship recorded'),
            refDomains: 'PARTY_RELATIONSHIP_TYPE',
            required: true,
            hint: t('يجب أن تسمح بالتعامل نيابة عن المنشأة', 'Must allow acting for the firm'),
          },
          {
            code: 'principalIdentifierType',
            type: 'select',
            label: t('معرّف المنشأة', "The firm's identifier"),
            options: identifierTypes('ESTABLISHMENT'),
            defaultValue: 'CR_NUMBER',
            required: true,
          },
          { code: 'numberLabelAr', type: 'text', label: t('اسم الرقم بالعربية', 'Number label (Arabic)'), required: true },
          { code: 'numberLabelEn', type: 'text', label: t('اسم الرقم بالإنجليزية', 'Number label (English)'), required: true },
          { code: 'requiresExpiry', type: 'checkbox', label: t('يُطلب تاريخ الانتهاء', 'Expiry date required') },
          {
            code: 'services',
            type: 'select',
            label: t('الخدمات المشمولة (فارغ: كل الخدمات)', 'Services covered (empty: all)'),
            ref: 'svc.service',
            multiple: true,
          },
          {
            code: 'operationCode',
            type: 'select',
            label: t('عملية التحقق (اختياري)', 'Check operation (optional)'),
            ref: 'integ.operation',
          },
          json(
            'inputs',
            t('مدخلات التحقق', 'Check inputs'),
            table(
              col('parameter', t('مُدخل العملية', 'Operation parameter'), 'text', LTR_REQ),
              col('from', t('القيمة', 'Value'), 'select', {
                required: true,
                options: [
                  opt('NUMBER', 'رقم التفويض', 'Authority number'),
                  opt('PRINCIPAL', 'معرّف المنشأة', "The firm's identifier"),
                  opt('INDIVIDUAL', 'هوية مقدم الطلب', "The applicant's ID number"),
                  opt('EXPIRY', 'تاريخ الانتهاء', 'Expiry date'),
                ],
              }),
            ),
          ),
          path('validOutput', t('مخرج الصلاحية (يجب أن يكون صحيحًا)', 'Validity output (must be true)')),
          path('expiryOutput', t('مخرج تاريخ الانتهاء', 'Expiry output')),
          path('principalNameArOutput', t('مخرج اسم المنشأة بالعربية', "Firm's Arabic name output")),
          path('principalNameEnOutput', t('مخرج اسم المنشأة بالإنجليزية', "Firm's English name output")),
          number('displayOrder', t('الترتيب', 'Display order'), 0),
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
          path('identifierPath', t('مسار المعرّف', 'Identifier path'), t('مثال: Commercial_NO', 'e.g. Commercial_NO')),
          path('nameArPath', t('مسار الاسم بالعربية', 'Arabic name path'), t('مثال: Commercial_Name', 'e.g. Commercial_Name')),
          path('nameEnPath', t('مسار الاسم بالإنجليزية', 'English name path')),
          path('relationCodePath', t('مسار رمز الصفة', 'Relation code path'), t('مثال: RelationTypeInfo.Id', 'e.g. RelationTypeInfo.Id')),
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
          json('activeStatuses', t('الحالات المقبولة', 'Accepted statuses'), list(), t('فارغ: كل الحالات. مثال: Active', 'Empty: every status. e.g. Active')),
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
              table(
                col('parameter', t('مدخل العملية', 'Operation input'), 'text', LTR_REQ),
                col('identifierType', t('المعرّف', 'Identifier'), 'select', {
                  required: true,
                  options: [...identifierTypes('INDIVIDUAL'), ...identifierTypes('ESTABLISHMENT')],
                }),
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
            label: t('نمط الخدمة', 'Archetype'),
            required: true,
            defaultValue: 'ISSUANCE',
            ref: 'svc.archetype',
            hint: t(
              'تبدأ الخدمة الجديدة بمسار العمل الافتراضي للنمط عند نشرها، ويفتح منشئ الخدمات قالبه.',
              "A new service starts with the archetype's default workflow when published, and the builder opens its template.",
            ),
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
            code: 'companyCode',
            type: 'select',
            label: t('الشركة التي تعالج الطلبات', 'Company that handles the requests'),
            ref: 'apps.company',
            hint: t('فارغ: الشركة الأولى.', 'Empty: the first company.'),
          },
          {
            code: 'branchQuestion',
            type: 'select',
            label: t('سؤال يحدد الفرع', 'Question that picks the branch'),
            ref: 'svc.question',
            hint: t(
              'تُطابق الإجابة رمز الفرع أو منطقته أو مدينته؛ دون سؤال أو تطابق: الفرع الافتراضي.',
              "The answer is matched to a branch's code, region or city; no question or no match: the default branch.",
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
            options: [opt('CONTINUE', 'تكمل على إصدارها', 'Continue on their version'), opt('RESTART', 'تنتقل للإصدار الجديد', 'Move to the new version')],
          },
        ],
      },
    ],
  },
  {
    path: 'archetypes',
    title: t('أنماط الخدمات', 'Service archetypes'),
    resource: 'services.config',
    types: [
      {
        entityType: 'svc.archetype',
        title: t('أنماط الخدمات', 'Service archetypes'),
        key: 'code',
        columns: ['displayOrder'],
        rowLinks: [
          {
            icon: 'construction',
            label: t('تصميم قالب الخدمة', 'Design the service template'),
            route: (row) => ({ commands: ['/services/builder'], queryParams: { archetype: String(row['code']) } }),
            testId: 'archetype-template',
          },
          {
            icon: 'account_tree',
            label: t('تصميم مسار العمل الافتراضي', 'Design the default workflow'),
            route: (row) => ({ commands: ['/workflows/designer'], queryParams: { archetype: String(row['code']) } }),
            testId: 'archetype-workflow',
          },
        ],
        fields: [
          { code: 'descriptionAr', type: 'textarea', label: t('الوصف بالعربية', 'Arabic description') },
          { code: 'descriptionEn', type: 'textarea', label: t('الوصف بالإنجليزية', 'English description') },
          { code: 'icon', type: 'icon', label: t('الأيقونة', 'Icon') },
          number('displayOrder', t('الترتيب', 'Display order'), 0),
          {
            code: 'templateFrom',
            type: 'select',
            label: t('نسخ قالب الخدمة من خدمة', 'Copy the service template from a service'),
            ref: 'svc.service',
            hint: t('اختياري: يصبح القالب نسخة من آخر إصدار للخدمة.', "Optional: the template becomes a copy of the service's latest version."),
          },
          {
            code: 'workflowFrom',
            type: 'select',
            label: t('نسخ مسار العمل الافتراضي من مسار', 'Copy the default workflow from a workflow'),
            ref: 'wf.workflow',
            hint: t('اختياري: يصبح المسار نسخة من آخر إصدار منشور للمسار.', "Optional: it becomes a copy of the workflow's latest published version."),
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
        columns: ['type', 'serviceCode', 'fill'],
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
            refDomains: 'SVC_QUESTION_TYPE',
            options: [
              opt('TEXT', 'نص', 'Text'),
              opt('TEXTAREA', 'نص طويل', 'Long text'),
              opt('NUMBER', 'رقم', 'Number'),
              opt('DATE', 'تاريخ', 'Date'),
              opt('SELECT', 'اختيار', 'Choice'),
              opt('CHECKBOX', 'نعم/لا', 'Yes/no'),
              opt('FILE', 'رفع ملفات', 'File upload'),
              opt('ROWS', 'صفوف متكررة', 'Repeating rows'),
            ],
          },
          { code: 'hintAr', type: 'text', label: t('تلميح بالعربية', 'Arabic hint') },
          {
            ...json(
              'columns',
              t('أعمدة الصف', 'Columns of a row'),
              table(
                col('code', t('الرمز', 'Code'), 'text', LTR_REQ),
                col('labelAr', t('الاسم بالعربية', 'Arabic label'), 'text', REQ),
                col('labelEn', t('الاسم بالإنجليزية', 'English label'), 'text', LTR_REQ),
                col('type', t('نوع الإجابة', 'Answer type'), 'select', {
                  required: true,
                  defaultValue: 'TEXT',
                  options: [
                    opt('TEXT', 'نص', 'Text'),
                    opt('TEXTAREA', 'نص طويل', 'Long text'),
                    opt('NUMBER', 'رقم', 'Number'),
                    opt('DATE', 'تاريخ', 'Date'),
                    opt('SELECT', 'اختيار', 'Choice'),
                    opt('CHECKBOX', 'نعم/لا', 'Yes/no'),
                    opt('FILE', 'رفع ملفات', 'File upload'),
                  ],
                }),
                col('required', t('مطلوب', 'Required'), 'checkbox'),
                col('inTable', t('يظهر في الجدول', 'In the table'), 'checkbox'),
                col('lov', t('قائمة القيم', 'List of values'), 'select', { ref: 'apps.lov' }),
                col('options', t('الخيارات', 'Options'), 'table', {
                  columns: [
                    col('value', t('القيمة', 'Value'), 'text', LTR_REQ),
                    col('labelAr', t('الاسم بالعربية', 'Arabic label'), 'text', REQ),
                    col('labelEn', t('الاسم بالإنجليزية', 'English label'), 'text', LTR_REQ),
                  ],
                }),
                col('multiple', t('متعدد', 'Several'), 'checkbox'),
                col('validation', t('القيود', 'Limits'), 'object', {
                  columns: [
                    col('min', t('أقل قيمة', 'Minimum'), 'number'),
                    col('max', t('أعلى قيمة', 'Maximum'), 'number'),
                    col('maxLength', t('أقصى طول', 'Longest text'), 'number'),
                    col('pattern', t('النمط', 'Pattern'), 'text', LTR),
                    col('extensions', t('أنواع الملفات', 'File types'), 'tags', LTR),
                    col('maxSizeMb', t('أكبر حجم (ميجابايت)', 'Largest file (MB)'), 'number'),
                    col('maxFiles', t('أقصى عدد للملفات', 'Most files'), 'number'),
                  ],
                }),
                col('visibleWhen', t('يظهر عندما', 'Shown when'), 'value', LTR),
                col('requiredWhen', t('مطلوب عندما', 'Required when'), 'value', LTR),
              ),
              t(
                'كل صف يُعبّأ بنموذج من هذه الأعمدة. شرطا الظهور والإلزام (JSONLogic) يريان قيم الصف برموز الأعمدة فوق إجابات الطلب، مثل {"==": [{"var": "OWNER"}, "GOV"]}. «يظهر في الجدول»: الأعمدة المعروضة في جدول الصفوف (أول أربعة إن لم يُحدَّد شيء).',
                'Each row is filled through a form of these columns. "Shown when" and "Required when" (JSONLogic) see the row\'s values by column code on top of the request\'s answers, e.g. {"==": [{"var": "OWNER"}, "GOV"]}. "In the table": the columns shown in the table of rows (the first four when none is ticked).',
              ),
              [],
            ),
            visibleWhen: { '==': [{ var: 'type' }, 'ROWS'] },
          },
          {
            code: 'recordList',
            type: 'select',
            label: t('الاختيار من قائمة سجلات', 'Pick from a record list'),
            ref: 'party.record-list',
            hint: t(
              'اختياري: يختار مقدم الطلب صفوفًا من سجلات جهته في هذه القائمة (بحسب رموز الأعمدة).',
              "Optional: the applicant picks rows from their party's records in this list (by column code).",
            ),
            visibleWhen: { '==': [{ var: 'type' }, 'ROWS'] },
          },
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
              table(
                col('value', t('القيمة', 'Value'), 'text', LTR_REQ),
                col('labelAr', t('الاسم بالعربية', 'Arabic label'), 'text', REQ),
                col('labelEn', t('الاسم بالإنجليزية', 'English label'), 'text', LTR_REQ),
              ),
              t('عند عدم استخدام قائمة قيم', 'When no list of values is used'),
            ),
            visibleWhen: { '==': [{ var: 'type' }, 'SELECT'] },
          },
          {
            code: 'multiple',
            type: 'checkbox',
            label: t('اختيار متعدد / عدة ملفات', 'Several choices / several files'),
            visibleWhen: { in: [{ var: 'type' }, ['SELECT', 'FILE']] },
          },
          json(
            'validation',
            t('القيود', 'Limits'),
            object(
              col('min', t('أقل قيمة', 'Minimum'), 'number'),
              col('max', t('أعلى قيمة', 'Maximum'), 'number'),
              col('maxLength', t('أقصى طول', 'Longest text'), 'number'),
              col('pattern', t('النمط (تعبير نمطي)', 'Pattern (regular expression)'), 'text', LTR),
              col('extensions', t('أنواع الملفات المقبولة', 'Accepted file types'), 'tags', LTR),
              col('maxSizeMb', t('أكبر حجم للملف (ميجابايت)', 'Largest file (MB)'), 'number'),
              col('maxFiles', t('أقصى عدد للملفات', 'Most files'), 'number'),
              col('minRows', t('أقل عدد من الصفوف', 'Fewest rows'), 'number'),
              col('maxRows', t('أكثر عدد من الصفوف', 'Most rows'), 'number'),
            ),
            t(
              'لأسئلة رفع الملفات: الأنواع مثل pdf, jpg (فارغ: المستندات والصور المعتادة)، والحجم (افتراضيًا 10)، والعدد مع «عدة ملفات» (افتراضيًا 5). لطلب ملف عند إجابة معينة استخدم «مطلوب عندما» في منشئ الخدمات.',
              'File questions: types such as pdf, jpg (empty: the usual documents and images), size (10 by default) and, with "several files", how many (5 by default). To ask for a file only for a given answer, use "Required when" in the service builder.',
            ),
            {},
          ),
          {
            code: 'serviceCode',
            type: 'select',
            label: t('السؤال لـ', 'Question of'),
            ref: 'svc.service',
            required: true,
            defaultValue: '*',
            allOption: t('بنك الأسئلة (مشترك بين الخدمات)', 'The question bank (shared by every service)'),
            hint: t(
              'سؤال لخدمة واحدة لا تستخدمه إلا إصداراتها؛ سؤال البنك تستخدمه كل الخدمات.',
              'A question of one service is used by its versions only; a bank question by every service.',
            ),
          },
          {
            code: 'fill',
            type: 'select',
            label: t('مصدر الإجابة', 'Where the answer comes from'),
            required: true,
            defaultValue: 'NONE',
            options: [
              opt('NONE', 'يجيب مقدم الطلب', 'The applicant answers'),
              opt('PARTY', 'من بيانات المستفيد', "From the beneficiary's data"),
              opt('INTEGRATION', 'من تكامل مع جهة أخرى', 'From an integration'),
            ],
          },
          {
            code: 'prefill',
            type: 'select',
            label: t('حقل بيانات المستفيد', "Field of the beneficiary's data"),
            optionsUrl: '/api/party/v1/fact-fields',
            visibleWhen: { '==': [{ var: 'fill' }, 'PARTY'] },
            requiredWhen: { '==': [{ var: 'fill' }, 'PARTY'] },
          },
          {
            ...json(
              'integration',
              t('التكامل', 'Integration'),
              object(
                col('operation', t('العملية', 'Operation'), 'select', { ref: 'integ.operation', required: true }),
                col('inputs', t('المدخلات', 'Inputs'), 'table', {
                  columns: [
                    col('parameter', t('المعامل', 'Parameter'), 'text', LTR_REQ),
                    col('from', t('القيمة من', 'Value from'), 'select', { optionsUrl: '/api/services/v1/admin/fill-sources', required: true }),
                  ],
                }),
                col('output', t('المخرج المستخدم', 'Output used'), 'text', {
                  ...LTR_REQ,
                }),
              ),
              t(
                'المخرج: اسم مخرج معرّف في العملية، أو مسار في نتيجتها مثل capital أو data.capital.',
                'Output: an output defined in the operation, or a path in its result such as capital or data.capital.',
              ),
              null,
            ),
            visibleWhen: { '==': [{ var: 'fill' }, 'INTEGRATION'] },
          },
          {
            code: 'allowManualEntry',
            type: 'checkbox',
            label: t('يمكن لمقدم الطلب إدخال الإجابة أو تعديلها', 'The applicant may type or change the answer'),
            defaultValue: true,
            visibleWhen: { '!=': [{ var: 'fill' }, 'NONE'] },
            hint: t(
              'عند عدم السماح تكون الإجابة دائمًا القيمة المجلوبة وتُعرض للقراءة فقط.',
              'When not allowed, the answer is always the fetched value and is shown read-only.',
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
    path: 'automation-rules',
    title: t('قواعد الأتمتة', 'Automation rules'),
    resource: 'workflows.config',
    types: [
      {
        entityType: 'wf.automation-rule',
        title: t('قواعد الأتمتة', 'Automation rules'),
        key: 'code',
        columns: ['action', 'workflows', 'repeatHours'],
        intro: () => import('../../workflows/automation').then((m) => m.AutomationIntro),
        rowActions: [
          {
            icon: 'play_circle',
            label: t('اختبار القاعدة', 'Test the rule'),
            testId: 'automation-test',
            width: '44rem',
            dialog: () => import('../../workflows/automation').then((m) => m.AutomationTest),
          },
        ],
        fields: [
          { code: 'descriptionAr', type: 'textarea', label: t('الوصف بالعربية', 'Arabic description') },
          { code: 'descriptionEn', type: 'textarea', label: t('الوصف بالإنجليزية', 'English description') },
          {
            code: 'workflows',
            type: 'select',
            label: t('مسارات العمل (فارغ: كلها)', 'Workflows (empty: all)'),
            ref: 'wf.workflow',
            multiple: true,
          },
          json(
            'steps',
            t('الخطوات (فارغ: كلها)', 'Steps (empty: all)'),
            list(),
            t('رموز خطوات المهام، مثل REVIEW', 'Codes of the task steps, e.g. REVIEW'),
          ),
          json(
            'when',
            t('متى تعمل (JSONLogic)', 'When it acts (JSONLogic)'),
            JSON_RULE,
            t(
              'شرط على المهمة، مثل {">=": [{"var": "task.unassignedHours"}, 4]}. انظر ما يقرؤه الشرط أعلى الصفحة.',
              'A condition on the task, e.g. {">=": [{"var": "task.unassignedHours"}, 4]}. See what the condition reads above.',
            ),
            { '>=': [{ var: 'task.unassignedHours' }, 4] },
          ),
          {
            code: 'action',
            type: 'select',
            label: t('الإجراء', 'Action'),
            required: true,
            defaultValue: 'ASSIGN_LEAST_LOADED',
            options: [
              opt('ASSIGN_LEAST_LOADED', 'إسناد للموظف الأقل عبئًا (المهام غير المسندة)', 'Assign to the least loaded employee (unassigned tasks)'),
              opt('REMIND', 'تذكير المكلف بالمهمة', 'Remind whoever has the task'),
              opt('ESCALATE', 'تصعيد إلى دور', 'Escalate to a role'),
            ],
          },
          {
            code: 'escalateToRole',
            type: 'select',
            label: t('دور التصعيد', 'Escalate to role'),
            ref: 'apps.role',
            visibleWhen: { '==': [{ var: 'action' }, 'ESCALATE'] },
            requiredWhen: { '==': [{ var: 'action' }, 'ESCALATE'] },
          },
          {
            code: 'repeatHours',
            type: 'number',
            label: t('التكرار كل (ساعات)', 'Repeat every (hours)'),
            hint: t('فارغ: مرة واحدة لكل مهمة', 'Empty: once per task'),
          },
          number('displayOrder', t('الترتيب', 'Display order'), 0),
        ],
      },
    ],
  },
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

/** Labels of a document's data (or line-item) fields. */
const documentFields = table(
  col('code', t('الرمز', 'Code'), 'text', LTR_REQ),
  col('nameAr', t('الاسم بالعربية', 'Arabic name'), 'text', REQ),
  col('nameEn', t('الاسم بالإنجليزية', 'English name'), 'text', LTR_REQ),
  col('isPublic', t('يظهر في التحقق العام', 'Shown on public verification'), 'checkbox'),
);
const documentFieldsHint = t(
  '«يظهر في التحقق العام»: يُعرض في صفحة التحقق من المستند لأي زائر.',
  '"Shown on public verification": anyone checking the document sees it.',
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
            label: t('مستند واحد لكل صاحب (الإصدار مرة أخرى يعدّله)', 'One per holder (issuing again amends it)'),
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
          json('alertDaysBefore', t('أيام التذكير قبل الانتهاء', 'Reminder days before expiry'), list('number'), t('مثال: 90 ثم 30', 'e.g. 90 then 30')),
          json('fields', t('حقول البيانات', 'Data fields'), documentFields, documentFieldsHint),
          json('lineFields', t('حقول البنود', 'Line-item fields'), documentFields, documentFieldsHint),
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
        editor: () => import('../../documents/template-editor').then((m) => m.DocumentTemplateEditor),
        fields: [],
      },
    ],
  },
];

/** How a public rule set is shown as a calculator on the portal (fields, results, starting values from the firm's data). */
function calculatorFields(): ConfigField[] {
  const shown = { '==': [{ var: 'isPublic' }, true] };
  const text = (code: string, label: LocalizedText, type: 'text' | 'textarea' | 'icon' = 'text'): ConfigField => ({ code, type, label, visibleWhen: shown });
  return [
    text('calculatorTitleAr', t('عنوان الحاسبة بالعربية', 'Calculator title (Arabic)')),
    text('calculatorTitleEn', t('عنوان الحاسبة بالإنجليزية', 'Calculator title (English)')),
    text('calculatorIntroAr', t('مقدمة الحاسبة بالعربية', 'Calculator intro (Arabic)'), 'textarea'),
    text('calculatorIntroEn', t('مقدمة الحاسبة بالإنجليزية', 'Calculator intro (English)'), 'textarea'),
    text('calculatorIcon', t('أيقونة الحاسبة', 'Calculator icon'), 'icon'),
    {
      ...json(
        'calculatorInputs',
        t('حقول الحاسبة', 'Calculator fields'),
        table(
          col('code', t('الرمز (input.الرمز)', 'Code (input.code)'), 'text', LTR_REQ),
          col('labelAr', t('الاسم بالعربية', 'Arabic label'), 'text', REQ),
          col('labelEn', t('الاسم بالإنجليزية', 'English label'), 'text', REQ),
          col('type', t('النوع', 'Type'), 'select', {
            required: true,
            defaultValue: 'NUMBER',
            options: [
              opt('NUMBER', 'رقم', 'Number'),
              opt('TEXT', 'نص', 'Text'),
              opt('SELECT', 'قائمة اختيار', 'Choice list'),
              opt('CHECKBOX', 'مربع اختيار', 'Tick box'),
              opt('DATE', 'تاريخ', 'Date'),
            ],
          }),
          col('required', t('مطلوب', 'Required'), 'checkbox'),
          col('min', t('الحد الأدنى', 'Least'), 'number'),
          col('max', t('الحد الأعلى', 'Most'), 'number'),
          col('hintAr', t('تلميح بالعربية', 'Arabic hint')),
          col('hintEn', t('تلميح بالإنجليزية', 'English hint')),
          col('options', t('الخيارات', 'Choices'), 'table', {
            columns: [
              col('value', t('القيمة', 'Value'), 'text', LTR_REQ),
              col('labelAr', t('بالعربية', 'Arabic'), 'text', REQ),
              col('labelEn', t('بالإنجليزية', 'English'), 'text', REQ),
            ],
          }),
          col('prefill', t('القيمة من بيانات المنشأة (قاعدة)', 'Value from the firm\'s data (rule)'), 'value', LTR),
        ),
        t(
          'ما يدخله الزائر؛ تقرأه القواعد باسم input.الرمز. القيمة من بيانات المنشأة قاعدة مثل {"var": "party.profile.CR_CAPITAL"} تُعبأ للمسجل دخوله.',
          'What the visitor types; the rules read it as input.code. The value from the firm\'s data is a rule such as {"var": "party.profile.CR_CAPITAL"}, filled in for a signed-in applicant.',
        ),
      ),
      visibleWhen: shown,
    },
    {
      ...json(
        'calculatorResults',
        t('نتائج الحاسبة', 'Calculator results'),
        table(
          col('path', t('المسار في النتيجة', 'Path in the output'), 'text', LTR_REQ),
          col('labelAr', t('الاسم بالعربية', 'Arabic label'), 'text', REQ),
          col('labelEn', t('الاسم بالإنجليزية', 'English label'), 'text', REQ),
          col('format', t('العرض', 'Shown as'), 'select', {
            defaultValue: 'TEXT',
            options: [opt('TEXT', 'نص', 'Text'), opt('NUMBER', 'رقم', 'Number'), opt('MONEY', 'مبلغ', 'Amount'), opt('YES_NO', 'نعم / لا', 'Yes / no')],
          }),
          col('headline', t('النتيجة الرئيسة', 'Main result'), 'checkbox'),
        ),
      ),
      visibleWhen: shown,
    },
    text('calculatorNoteAr', t('ملاحظة تحت النتيجة بالعربية', 'Note under the result (Arabic)'), 'textarea'),
    text('calculatorNoteEn', t('ملاحظة تحت النتيجة بالإنجليزية', 'Note under the result (English)'), 'textarea'),
  ];
}

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
            label: t('حاسبة عامة في البوابة (بدون تسجيل دخول)', 'Public calculator on the portal (no sign-in)'),
          },
          ...calculatorFields(),
        ],
      },
    ],
  },
];

const recipientKinds = [opt('PARTY', 'المتعامل', 'Party'), opt('ROLE', 'حاملو دور', 'Holders of a role'), opt('USER', 'موظف', 'Employee')];

const channels = [opt('IN_APP', 'داخل المنصة', 'In-app'), opt('EMAIL', 'البريد الإلكتروني', 'Email'), opt('SMS', 'رسالة نصية', 'SMS')];

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
            refDomains: 'COM_CHANNEL',
            options: channels,
            multiple: true,
            required: true,
            defaultValue: ['IN_APP'],
          },
          json(
            'recipients',
            t('المستلمون', 'Recipients'),
            table(col('kind', t('المستلم', 'Recipient'), 'select', { required: true, options: recipientKinds }), col('value', t('القيمة', 'Value'), 'value')),
            t(
              'المتعامل: صاحب الطلب (افتراضياً). الدور: رمز الدور. الموظف: اسم المستخدم أو تعبير مثل {"var": "task.assignee"}.',
              'Party: the applicant (by default). Role: the role code. User: a user name or an expression such as {"var": "task.assignee"}.',
            ),
            [{ kind: 'PARTY' }],
          ),
          json(
            'condition',
            t('الشرط (JSONLogic)', 'Condition (JSONLogic)'),
            JSON_RULE,
            t('فارغ: دائماً. مثال: {"==": [{"var": "document.alertDays"}, 90]}', 'Empty: always. e.g. {"==": [{"var": "document.alertDays"}, 90]}'),
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
        editor: () => import('../../communication/message-template-editor').then((m) => m.MessageTemplateEditor),
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
            hint: t('اتركه فارغاً للسداد اليدوي (الصندوق)', 'Leave empty for manual payment (cashier)'),
          },
          path('billNumberOutput', t('رقم الفاتورة في المخرجات', 'Bill number in the output'), t('مثال: sadadNumber', 'e.g. sadadNumber')),
          { code: 'billerCode', type: 'text', label: t('رمز المفوتر', 'Biller code') },
          number('dueDays', t('مدة السداد (يوم)', 'Days to pay'), 7),
          {
            code: 'payUrlTemplate',
            type: 'text',
            label: t('رابط السداد', 'Payment page URL'),
            hint: t('مثال: https://…/pay/{billNumber}?amount={amount}', 'e.g. https://…/pay/{billNumber}?amount={amount}'),
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
            hint: t('اسم السر في مخزن الأسرار وليس قيمته', "The secret's name in the secret store, never its value"),
          },
          json(
            'callback',
            t('حقول إشعار السداد', 'Payment notification fields'),
            object(
              col('billNumber', t('رقم الفاتورة', 'Bill number'), 'text', LTR),
              col('status', t('الحالة', 'Status'), 'text', LTR),
              col('paidValue', t('قيمة الحالة عند السداد', 'Status value when paid'), 'text', LTR),
              col('amount', t('المبلغ', 'Amount'), 'text', LTR),
              col('reference', t('مرجع السداد', 'Payment reference'), 'text', LTR),
              col('paidAt', t('وقت السداد', 'Paid at'), 'text', LTR),
            ),
            t('أسماء الحقول في إشعار المزود', "Field names in the provider's notification"),
            {},
          ),
          { code: 'isDefault', type: 'checkbox', label: t('المزود الافتراضي', 'Default provider') },
        ],
      },
    ],
  },
];

const outcomeKinds = [opt('PASS', 'مطابق', 'Pass'), opt('FAIL', 'غير مطابق', 'Fail'), opt('OTHER', 'أخرى', 'Other')];
const visitFieldTypes = [
  opt('TEXT', 'نص', 'Text'),
  opt('TEXTAREA', 'نص طويل', 'Long text'),
  opt('NUMBER', 'رقم', 'Number'),
  opt('DATE', 'تاريخ', 'Date'),
  opt('SELECT', 'اختيار', 'Choice'),
  opt('CHECKBOX', 'نعم/لا', 'Yes/no'),
];

/** Visit configuration screens (internal system → Field visits). */
export const VISIT_SECTIONS: ConfigSection[] = [
  {
    path: 'visit-types',
    title: t('أنواع الزيارات', 'Visit types'),
    resource: 'visits.config',
    types: [
      {
        entityType: 'vis.visit-type',
        title: t('أنواع الزيارات', 'Visit types'),
        key: 'code',
        columns: ['mode', 'inspectorRole', 'dueDays'],
        editor: () => import('../../visits/visit-type-editor').then((m) => m.VisitTypeEditor),
        fields: [
          {
            code: 'mode',
            type: 'select',
            label: t('من يجري الزيارة', 'Who carries it out'),
            required: true,
            refDomains: 'VIS_MODE',
            options: [opt('FIELD', 'ميدانية', 'Field'), opt('EXTERNAL', 'جهة معاينة خارجية', 'External body'), opt('MANUAL', 'تسجيل يدوي', 'Manual')],
          },
          { code: 'descriptionAr', type: 'textarea', label: t('الوصف بالعربية', 'Description (Arabic)') },
          { code: 'descriptionEn', type: 'textarea', label: t('الوصف بالإنجليزية', 'Description (English)') },
          json(
            'outcomes',
            t('النتائج', 'Outcomes'),
            table(
              col('code', t('الرمز', 'Code'), 'text', LTR_REQ),
              col('nameAr', t('الاسم بالعربية', 'Arabic name'), 'text', REQ),
              col('nameEn', t('الاسم بالإنجليزية', 'English name'), 'text', LTR_REQ),
              col('kind', t('النوع', 'Kind'), 'select', { required: true, refDomains: 'VIS_OUTCOME_KIND', options: outcomeKinds }),
            ),
          ),
          json(
            'fields',
            t('حقول المفتش', 'Inspector fields'),
            table(
              col('code', t('الرمز', 'Code'), 'text', LTR_REQ),
              col('labelAr', t('الاسم بالعربية', 'Arabic label'), 'text', REQ),
              col('labelEn', t('الاسم بالإنجليزية', 'English label'), 'text', LTR_REQ),
              col('type', t('النوع', 'Type'), 'select', { required: true, defaultValue: 'TEXT', options: visitFieldTypes }),
              col('required', t('إلزامي', 'Required'), 'checkbox'),
              col('options', t('الخيارات', 'Options'), 'table', {
                columns: [
                  col('code', t('الرمز', 'Code'), 'text', LTR_REQ),
                  col('labelAr', t('بالعربية', 'Arabic'), 'text', REQ),
                  col('labelEn', t('بالإنجليزية', 'English'), 'text', LTR_REQ),
                ],
              }),
            ),
          ),
          number('photosRequired', t('أقل عدد من الصور', 'Fewest photos'), 0),
          { code: 'requireLocation', type: 'checkbox', label: t('تسجيل الموقع إلزامي', 'Position required') },
          { code: 'requireSignature', type: 'checkbox', label: t('التوقيع إلزامي', 'Signature required') },
          { code: 'inspectorRole', type: 'select', label: t('دور المفتشين', 'Inspectors role'), ref: 'apps.role' },
          number('dueDays', t('مدة الإتمام (يوم)', 'Days to complete'), 5),
          json(
            'external',
            t('جهة المعاينة الخارجية', 'External inspection body'),
            object(
              col('createOperation', t('عملية إنشاء الزيارة', 'Create operation'), 'select', { ref: 'integ.operation' }),
              col('referenceOutput', t('مخرج المرجع', 'Reference output'), 'text', LTR),
              col('resultOperation', t('عملية قراءة النتيجة', 'Result operation'), 'select', { ref: 'integ.operation' }),
              col('pollMinutes', t('الاستعلام كل (دقيقة)', 'Poll every (minutes)'), 'number'),
              col('callbackSecret', t('سر توقيع الإشعار', 'Callback signing secret'), 'text', LTR),
              col('inputs', t('مدخلات العملية', 'Operation inputs'), 'map'),
              col('result', t('حقول النتيجة', 'Result fields'), 'object', {
                columns: [
                  col('reference', t('المرجع', 'Reference'), 'text', LTR),
                  col('status', t('الحالة', 'Status'), 'text', LTR),
                  col('completedValue', t('قيمة الإنجاز', 'Completed value'), 'text', LTR),
                  col('outcome', t('النتيجة', 'Outcome'), 'text', LTR),
                  col('data', t('البيانات', 'Data'), 'text', LTR),
                  col('report', t('التقرير', 'Report'), 'text', LTR),
                ],
              }),
              col('outcomeMap', t('ترجمة النتائج', 'Outcome translation'), 'map'),
            ),
            t('للزيارات لدى جهة معاينة خارجية', 'For visits by an external inspection body'),
            null,
          ),
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
        rowLinks: [
          {
            icon: 'dashboard_customize',
            label: t('تصميم الصفحة (السحب والإفلات، الإصدارات والنشر)', 'Design the page (drag and drop, versions, publishing)'),
            route: (row) => ({ commands: ['/content/pages/designer'], queryParams: { page: String(row['code']) } }),
            testId: 'design-page',
          },
        ],
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

/** Reports (ADR-0045): report definitions (their own designer) and scheduled email delivery. */
export const REPORT_SECTIONS: ConfigSection[] = [
  {
    path: 'reports-definitions',
    title: t('تصميم التقارير', 'Report design'),
    resource: 'reports.config',
    types: [
      {
        entityType: 'rpt.report',
        title: t('التقارير', 'Reports'),
        key: 'code',
        columns: ['categoryAr', 'pageSize'],
        // The query builder, columns and totals, filters, tiles and chart with a live preview, in their own dialog.
        editor: () => import('../../reports/report-designer').then((m) => m.ReportDesigner),
        rowLinks: [
          {
            icon: 'open_in_new',
            label: t('فتح التقرير', 'Open the report'),
            route: (row) => ({ commands: ['/reports/view', String(row['code'])] }),
            testId: 'report-open',
          },
        ],
        fields: [
          { code: 'categoryAr', type: 'text', label: t('المجموعة بالعربية', 'Arabic group') },
          { code: 'categoryEn', type: 'text', label: t('المجموعة بالإنجليزية', 'English group') },
          number('pageSize', t('صفوف في الصفحة', 'Rows a page'), 50),
        ],
      },
    ],
  },
  {
    path: 'reports-dashboard-definitions',
    title: t('تصميم لوحات المؤشرات', 'Dashboard design'),
    resource: 'reports.dashboards',
    types: [
      {
        entityType: 'rpt.dashboard',
        title: t('لوحات المؤشرات', 'Dashboards'),
        key: 'code',
        columns: ['descriptionAr'],
        // Tiles, charts and tables of several reports with shared filters, in their own dialog (ADR-0047).
        editor: () => import('../../reports/dashboard-editor').then((m) => m.DashboardEditor),
        rowLinks: [
          {
            icon: 'open_in_new',
            label: t('فتح اللوحة', 'Open the dashboard'),
            route: (row) => ({ commands: ['/reports/dashboards', String(row['code'])] }),
            testId: 'dashboard-open',
          },
        ],
        fields: [
          { code: 'descriptionAr', type: 'text', label: t('الوصف بالعربية', 'Arabic description') },
          { code: 'descriptionEn', type: 'text', label: t('الوصف بالإنجليزية', 'English description') },
        ],
      },
    ],
  },
  {
    path: 'reports-schedules',
    title: t('جدولة التقارير', 'Report schedules'),
    resource: 'reports.schedules',
    types: [
      {
        entityType: 'rpt.schedule',
        title: t('جدولة التقارير', 'Report schedules'),
        key: 'code',
        columns: ['reportCode', 'frequency', 'time', 'format', 'lastRunAt'],
        // Each recipient gets the report run with their own access; the log shows who got it.
        rowActions: [
          {
            icon: 'send',
            label: t('إرسال الآن', 'Send now'),
            testId: 'report-send-now',
            width: '40rem',
            dialog: () => import('../../reports/report-deliveries').then((m) => m.ReportSendNow),
          },
        ],
        rowLinks: [
          {
            icon: 'outgoing_mail',
            label: t('سجل الإرسال', 'Delivery log'),
            route: () => ({ commands: ['/reports/deliveries'] }),
            testId: 'report-deliveries',
          },
        ],
        fields: [
          { code: 'reportCode', type: 'select', label: t('التقرير', 'Report'), ref: 'rpt.report', required: true },
          {
            code: 'frequency',
            type: 'select',
            label: t('التكرار', 'How often'),
            required: true,
            defaultValue: 'DAILY',
            options: [opt('DAILY', 'يوميًا', 'Daily'), opt('WEEKLY', 'أسبوعيًا', 'Weekly'), opt('MONTHLY', 'شهريًا', 'Monthly')],
          },
          {
            code: 'time',
            type: 'text',
            label: t('الوقت', 'Time'),
            required: true,
            defaultValue: '07:00',
            hint: t('بتوقيت الرياض، بصيغة 07:00', 'Riyadh time, as 07:00'),
          },
          {
            code: 'dayOfWeek',
            type: 'number',
            label: t('يوم الأسبوع', 'Day of the week'),
            hint: t('0 الأحد … 6 السبت', '0 Sunday … 6 Saturday'),
            visibleWhen: { '==': [{ var: 'frequency' }, 'WEEKLY'] },
          },
          {
            code: 'dayOfMonth',
            type: 'number',
            label: t('يوم الشهر', 'Day of the month'),
            hint: t('من 1 إلى 28', '1 to 28'),
            visibleWhen: { '==': [{ var: 'frequency' }, 'MONTHLY'] },
          },
          {
            code: 'format',
            type: 'select',
            label: t('صيغة الملف', 'File format'),
            required: true,
            defaultValue: 'XLSX',
            options: [opt('XLSX', 'Excel', 'Excel'), opt('CSV', 'CSV', 'CSV')],
          },
          {
            code: 'language',
            type: 'select',
            label: t('لغة الملف والبريد', 'Language of the file and email'),
            required: true,
            defaultValue: 'AR',
            options: [opt('AR', 'العربية', 'Arabic'), opt('EN', 'الإنجليزية', 'English')],
          },
          { code: 'users', type: 'select', multiple: true, label: t('المستخدمون', 'Users'), ref: 'apps.user', defaultValue: [] },
          {
            code: 'roles',
            type: 'select',
            multiple: true,
            label: t('الأدوار', 'Roles'),
            ref: 'apps.role',
            defaultValue: [],
            hint: t('كل من يحمل الدور يصله التقرير بصلاحياته', 'Everyone holding the role gets the report with their own access'),
          },
          {
            code: 'filterValues',
            type: 'json',
            label: t('قيم المرشحات', 'Filter values'),
            structure: { kind: 'map', keyLabel: t('المرشح', 'Filter'), valueLabel: t('القيمة', 'Value') },
            hint: t('فارغ: القيم الافتراضية للتقرير (مثل TODAY-30)', 'Empty: the report’s defaults (e.g. TODAY-30)'),
            defaultValue: {},
          },
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
  ...VISIT_SECTIONS,
  ...CONTENT_SECTIONS,
  ...REPORT_SECTIONS,
];
