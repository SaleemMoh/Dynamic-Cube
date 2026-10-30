import { ConfigRow } from '@dynamic-cube/shared';
import {
  ALL,
  departmentRoles,
  flattenRoles,
  groupRoles,
  personalPayload,
  pickWithAll,
  readPersonal,
  rowProblems,
  setPersonalActions,
  setPersonalGrant,
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
      grants: [{ lovCode: 'REGIONS', grantType: 'VALUES', grantedValues: ['RIYADH'] }],
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
        grants: [{ lovCode: 'REGIONS', grantType: 'ALL', grantedValues: [] }],
      },
    ]);
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
