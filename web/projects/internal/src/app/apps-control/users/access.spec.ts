import { ConfigRow } from '@dynamic-cube/shared';
import {
  ALL,
  departmentRoles,
  flattenRoles,
  groupRoles,
  personalPayload,
  pickWithAll,
  readPersonal,
  roleAccess,
  rowProblems,
  setPersonalActions,
  setPersonalDenied,
  setPersonalGrant,
  setPersonalPeriod,
} from './access-model';
import { planRoles } from './bulk-roles-dialog';

const row = (code: string, extra: Record<string, unknown> = {}): ConfigRow =>
  ({ code, nameAr: code, nameEn: code, ...extra }) as unknown as ConfigRow;

describe('Access tab', () => {
  const stored = [
    { roleCode: 'ADMIN', companyCode: ALL, branches: [ALL] },
    {
      roleCode: 'MANAGER',
      companyCode: 'MAIN',
      branches: ['JED', 'DMM'],
      effectiveTo: '2027-01-01',
    },
    { roleCode: 'REVIEWER', companyCode: 'MAIN', branches: ['DMM', 'JED'] },
  ];

  it('groups roles by company and branches', () => {
    expect(groupRoles(stored)).toEqual([
      { company: ALL, branches: [ALL], roles: ['ADMIN'], orgUnitCode: null },
      {
        company: 'MAIN',
        branches: ['DMM', 'JED'],
        roles: ['MANAGER', 'REVIEWER'],
        orgUnitCode: null,
      },
    ]);
  });

  it('flattens rows back and keeps the dates of roles that stay', () => {
    const rows = groupRoles(stored);
    rows[1].roles = ['MANAGER'];
    expect(flattenRoles(rows, stored)).toEqual([
      { roleCode: 'ADMIN', companyCode: ALL, branches: [ALL], orgUnitCode: null },
      {
        roleCode: 'MANAGER',
        companyCode: 'MAIN',
        branches: ['DMM', 'JED'],
        orgUnitCode: null,
        effectiveTo: '2027-01-01',
      },
    ]);
  });

  it('holds a role in all branches when it is held in all companies', () => {
    const rows = [{ company: ALL, branches: ['JED'], roles: ['ADMIN'], orgUnitCode: null }];
    expect(flattenRoles(rows, [])[0].branches).toEqual([ALL]);
  });

  it('never reads an empty company or branches as all', () => {
    expect(groupRoles([{ roleCode: 'OLD' }])[0].company).toBeNull();
    const rows = [
      { company: null, branches: [], roles: ['A'], orgUnitCode: null },
      { company: 'MAIN', branches: [], roles: ['B'], orgUnitCode: null },
      { company: 'MAIN', branches: [ALL], roles: ['C'], orgUnitCode: null },
      { company: null, branches: [], roles: [], orgUnitCode: null },
    ];
    expect(rowProblems(rows)).toEqual([
      { key: 'access.needCompany', row: 1 },
      { key: 'access.needBranches', row: 2 },
    ]);
  });

  it('lets "all" and single choices replace each other', () => {
    expect(pickWithAll(['JED'], ['JED', ALL])).toEqual([ALL]);
    expect(pickWithAll([ALL], [ALL, 'JED'])).toEqual(['JED']);
    expect(pickWithAll(['JED'], ['JED', 'DMM'])).toEqual(['JED', 'DMM']);
  });

  it('finds the branch a department serves by looking up the tree', () => {
    const units = [
      row('JED-BRANCH'),
      row('JED-REQUESTS', { parentCode: 'JED-BRANCH', defaultRoles: ['REQUEST_MANAGER'] }),
      row('GM-OFFICE', { defaultRoles: ['GENERAL_MANAGER'] }),
      row('IT'),
    ];
    const branches = [row('JED', { orgUnitCode: 'JED-BRANCH' })];
    const found = departmentRoles(
      [{ orgUnitCode: 'JED-REQUESTS' }, { orgUnitCode: 'GM-OFFICE' }, { orgUnitCode: 'IT' }],
      units,
      branches,
    );
    expect(found.map((d) => [d.unit.code, d.roles, d.branch?.code ?? null])).toEqual([
      ['JED-REQUESTS', ['REQUEST_MANAGER'], 'JED'],
      ['GM-OFFICE', ['GENERAL_MANAGER'], null],
    ]);
  });
});

describe('personal privileges', () => {
  it('sets actions and data per company and screen, and drops a screen left with neither', () => {
    let list = readPersonal([{ companyCode: 'MAIN', resourceCode: 'cases', actions: ['VIEW'] }]);
    list = setPersonalActions(list, ALL, 'reports', ['VIEW', 'EXPORT']);
    list = setPersonalGrant(list, 'MAIN', 'cases', 'REGIONS', {
      grantType: 'VALUES',
      grantedValues: ['RIYADH'],
    });
    expect(list.find((p) => p.resourceCode === 'cases')).toEqual({
      companyCode: 'MAIN',
      resourceCode: 'cases',
      actions: ['VIEW'],
      deniedActions: [],
      grants: [{ lovCode: 'REGIONS', grantType: 'VALUES', grantedValues: ['RIYADH'] }],
      effectiveFrom: null,
      effectiveTo: null,
    });
    list = setPersonalActions(list, ALL, 'reports', []);
    expect(list.map((p) => p.resourceCode)).toEqual(['cases']);
  });

  it('keeps data alone on a screen (no actions) and leaves out values not chosen yet', () => {
    let list = setPersonalGrant([], 'MAIN', 'cases', 'REGIONS', {
      grantType: 'ALL',
      grantedValues: [],
    });
    list = setPersonalGrant(list, 'MAIN', 'cases', 'SECTORS', {
      grantType: 'VALUES',
      grantedValues: [],
    });
    list = setPersonalGrant(list, ALL, 'reports', 'SECTORS', {
      grantType: 'VALUES',
      grantedValues: [],
    });
    expect(personalPayload(list)).toEqual([
      {
        companyCode: 'MAIN',
        resourceCode: 'cases',
        actions: [],
        deniedActions: [],
        grants: [{ lovCode: 'REGIONS', grantType: 'ALL', grantedValues: [] }],
        effectiveFrom: null,
        effectiveTo: null,
      },
    ]);
  });

  it('removes an action for one person, never both giving and removing it, and keeps a period', () => {
    let list = setPersonalActions([], ALL, 'cases', ['EDIT', 'EXPORT']);
    list = setPersonalDenied(list, ALL, 'cases', ['EDIT']);
    expect(list[0].actions).toEqual(['EXPORT']);
    expect(list[0].deniedActions).toEqual(['EDIT']);
    list = setPersonalActions(list, ALL, 'cases', ['EXPORT', 'EDIT']);
    expect(list[0].deniedActions).toEqual([]);
    list = setPersonalPeriod(list, list[0], '2026-11-01', '2026-12-31');
    expect(list[0]).toMatchObject({ effectiveFrom: '2026-11-01', effectiveTo: '2026-12-31' });
    // Removing only (no action given) is still a personal privilege.
    expect(setPersonalDenied([], 'MAIN', 'cases', ['DELETE'])).toHaveLength(1);
  });
});

describe('what roles give', () => {
  const role = (code: string, extra: object) => ({ code, nameAr: code, nameEn: code, ...extra });
  const roles = new Map([
    [
      'CLERK',
      role('CLERK', {
        privileges: [{ resourceCode: 'cases', actions: ['VIEW', 'EDIT'] }],
        menus: ['M_REPORTS'],
      }),
    ],
    [
      'MANAGER',
      role('MANAGER', {
        privileges: [{ resourceCode: 'cases', actions: ['APPROVE'] }],
        overrides: [
          {
            companyCode: 'SUB',
            privileges: [{ resourceCode: 'audit', actions: ['VIEW'] }],
            menus: [],
          },
        ],
      }),
    ],
  ]);
  const menus = new Map([['M_REPORTS', 'reports']]);
  const held = [
    { roleCode: 'CLERK', company: ALL },
    { roleCode: 'MANAGER', company: 'SUB' },
  ];

  it('counts roles held in all companies everywhere and others only in their company', () => {
    const everywhere = roleAccess(ALL, held, roles, menus);
    expect([...everywhere.get('cases')!.actions]).toEqual(['VIEW', 'EDIT']);
    expect(everywhere.get('reports')!.actions.has('VIEW')).toBe(true);
    expect(everywhere.has('audit')).toBe(false);
  });

  it("uses the company's version of a role that differs there", () => {
    const sub = roleAccess('SUB', held, roles, menus);
    expect([...sub.get('cases')!.actions]).toEqual(['VIEW', 'EDIT']);
    expect(sub.get('audit')!.roles.map((r) => r.code)).toEqual(['MANAGER']);
    const main = roleAccess('MAIN', held, roles, menus);
    expect(main.get('cases')!.actions.has('APPROVE')).toBe(false);
  });
});

describe('bulk roles', () => {
  const user = row('clerk', {
    roles: [
      { roleCode: 'VIEWER', companyCode: 'MAIN', branches: [ALL] },
      { roleCode: 'ADMIN', companyCode: ALL, branches: [ALL] },
    ],
  });

  it('adds only roles the user does not hold in that company', () => {
    const plan = planRoles(user, 'ADD', 'MAIN', ['JED'], ['VIEWER', 'MANAGER']);
    expect(plan.added).toEqual(['MANAGER']);
    expect(plan.unchanged).toEqual(['VIEWER']);
    expect(plan.roles).toContainEqual({
      roleCode: 'MANAGER',
      companyCode: 'MAIN',
      branches: ['JED'],
    });
    expect(plan.roles).toHaveLength(3);
  });

  it('keeps a role held in all companies when adding it for one company', () => {
    const plan = planRoles(user, 'ADD', 'MAIN', [ALL], ['ADMIN']);
    expect(plan.added).toEqual(['ADMIN']);
    expect(plan.roles.filter((r) => r.roleCode === 'ADMIN')).toHaveLength(2);
  });

  it('adds a role in all companies with all branches', () => {
    const plan = planRoles(user, 'ADD', ALL, ['JED'], ['MANAGER']);
    expect(plan.roles).toContainEqual({ roleCode: 'MANAGER', companyCode: ALL, branches: [ALL] });
  });

  it('removes only the role in the chosen company and reports what was not held', () => {
    const plan = planRoles(user, 'REMOVE', ALL, [], ['ADMIN', 'VIEWER']);
    expect(plan.removed).toEqual(['ADMIN']);
    expect(plan.unchanged).toEqual(['VIEWER']);
    expect(plan.roles).toEqual([{ roleCode: 'VIEWER', companyCode: 'MAIN', branches: [ALL] }]);
  });

  it('does not change the user row it was given', () => {
    planRoles(user, 'REMOVE', ALL, [], ['ADMIN']);
    expect((user['roles'] as unknown[]).length).toBe(2);
  });
});
