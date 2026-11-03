import {
  EnvironmentProviders,
  Injectable,
  inject,
  provideAppInitializer,
  signal,
} from '@angular/core';
import {
  AppsControlApi,
  ConfigRow,
  LocalizedText,
  LogicContexts,
  LogicField,
} from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';

const t = (ar: string, en: string): LocalizedText => ({ ar, en });
const yesNo = 'boolean' as const;

/** What an automation rule reads about an open task (Workflow → Automation rules). */
const TASK: LogicField[] = (
  [
    { path: 'task.step', label: t('رمز الخطوة', 'Step code') },
    { path: 'task.workflow', label: t('رمز مسار العمل', 'Workflow code') },
    { path: 'task.service', label: t('رمز الخدمة', 'Service code') },
    { path: 'task.company', label: t('الشركة', 'Company') },
    { path: 'task.branch', label: t('الفرع', 'Branch') },
    { path: 'task.level', label: t('مستوى الخدمة', 'Service level') },
    { path: 'task.role', label: t('الدور المرشح', 'Candidate role') },
    { path: 'task.assignee', label: t('المكلف بالمهمة', 'Assignee') },
    { path: 'task.assigned', label: t('مسندة', 'Assigned'), type: yesNo },
    { path: 'task.ageHours', label: t('ساعات منذ الإنشاء', 'Hours since created'), type: 'number' },
    {
      path: 'task.unassignedHours',
      label: t('ساعات بلا إسناد', 'Hours unassigned'),
      type: 'number',
    },
    {
      path: 'task.assignedHours',
      label: t('ساعات منذ الإسناد', 'Hours since assigned'),
      type: 'number',
    },
    {
      path: 'task.dueInHours',
      label: t('ساعات حتى الاستحقاق', 'Hours to the due time'),
      type: 'number',
    },
    { path: 'task.overdue', label: t('تجاوزت موعدها', 'Overdue'), type: yesNo },
    {
      path: 'task.sla',
      label: t('حالة المدة', 'SLA state'),
      options: [
        { value: 'ON_TRACK', label: t('في الموعد', 'On track') },
        { value: 'WARNING', label: t('قريبة من الموعد', 'Near the due time') },
        { value: 'BREACHED', label: t('متأخرة', 'Late') },
      ],
    },
    { path: 'task.escalated', label: t('مصعدة', 'Escalated'), type: yesNo },
  ] as LogicField[]
).map((f) => ({ ...f, group: t('المهمة', 'Task') }));

/** What a list's static filter reads about each class value and the signed-in user. */
const CLASS_VALUE: LogicField[] = [
  { path: 'code', label: t('رمز القيمة', 'Value code'), group: t('القيمة', 'Value') },
  {
    path: 'parent',
    label: t('رمز القيمة الأعلى', 'Parent value code'),
    group: t('القيمة', 'Value'),
  },
  { path: 'user.username', label: t('اسم المستخدم', 'User name'), group: t('المستخدم', 'User') },
  {
    path: 'user.roles',
    label: t('أدوار المستخدم', "User's roles"),
    type: 'list',
    group: t('المستخدم', 'User'),
  },
  { path: 'user.orgUnit', label: t('الوحدة التنظيمية', 'Org unit'), group: t('المستخدم', 'User') },
  {
    path: 'user.orgUnits',
    label: t('الوحدات التنظيمية', 'Org units'),
    type: 'list',
    group: t('المستخدم', 'User'),
  },
  {
    path: 'user.positions',
    label: t('المناصب', 'Positions'),
    type: 'list',
    group: t('المستخدم', 'User'),
  },
  { path: 'company', label: t('الشركة الحالية', 'Current company'), group: t('المستخدم', 'User') },
  { path: 'app', label: t('التطبيق', 'App'), group: t('المستخدم', 'User') },
];

/**
 * The internal app's named field lists for the Logic Builder (config screens point at them by name): `task`,
 * `classValue` and `party` (the acting party's facts, with the profile fields loaded on first use).
 */
@Injectable({ providedIn: 'root' })
export class InternalLogicContexts {
  private readonly api = inject(AppsControlApi);
  private readonly contexts = inject(LogicContexts);
  private profile: ReturnType<typeof signal<ConfigRow[]>> | null = null;

  install(): void {
    this.contexts.register('task', () => TASK);
    this.contexts.register('classValue', () => CLASS_VALUE);
    this.contexts.register('party', () => this.partyFields());
  }

  private partyFields(): LogicField[] {
    if (!this.profile) {
      const rows = signal<ConfigRow[]>([]);
      this.profile = rows;
      this.api
        .list('party.profile-field')
        .pipe(catchError(() => of([] as ConfigRow[])))
        .subscribe((r) => rows.set(r));
    }
    const group = t('المنشأة', 'The firm');
    return [
      {
        path: 'party.kind',
        label: t('نوع الطرف', 'Party kind'),
        options: [
          { value: 'INDIVIDUAL', label: t('فرد', 'Individual') },
          { value: 'ESTABLISHMENT', label: t('منشأة', 'Establishment') },
        ],
        group,
      },
      ...this.profile().map((f) => ({
        path: `party.profile.${f.code}`,
        label: { ar: (f.nameAr as string) || f.code, en: (f.nameEn as string) || f.code },
        type: profileType(f['dataType']),
        group,
      })),
    ];
  }
}

/** How a party profile field's data type is compared. */
export function profileType(dataType: unknown): LogicField['type'] {
  switch (String(dataType ?? '').toUpperCase()) {
    case 'NUMBER':
      return 'number';
    case 'DATE':
      return 'date';
    case 'BOOLEAN':
      return 'boolean';
    default:
      return 'text';
  }
}

export function provideLogicContexts(): EnvironmentProviders {
  return provideAppInitializer(() => inject(InternalLogicContexts).install());
}
