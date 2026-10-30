import { ConfigRow } from '@dynamic-cube/shared';
import { departmentRoles, flattenRoles, groupRoles } from './access-dialog';
import { planRoles } from './bulk-roles-dialog';

const row = (code: string, extra: Record<string, unknown> = {}): ConfigRow =>
  ({ code, nameAr: code, nameEn: code, ...extra }) as unknown as ConfigRow;

describe('Access tab', () => {
  const stored = [
    { roleCode: 'ADMIN' },
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
      { company: null, branches: [], roles: ['ADMIN'], orgUnitCode: null },
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
      { roleCode: 'ADMIN', companyCode: null, branches: [], orgUnitCode: null },
      {
        roleCode: 'MANAGER',
        companyCode: 'MAIN',
        branches: ['DMM', 'JED'],
        orgUnitCode: null,
        effectiveTo: '2027-01-01',
      },
    ]);
  });

  it('drops branches when the row has no company', () => {
    const rows = [{ company: null, branches: ['JED'], roles: ['ADMIN'], orgUnitCode: null }];
    expect(flattenRoles(rows, [])[0].branches).toEqual([]);
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

describe('bulk roles', () => {
  const user = row('clerk', {
    roles: [{ roleCode: 'VIEWER', companyCode: 'MAIN', branches: [] }, { roleCode: 'ADMIN' }],
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

  it('keeps a role held in every company when adding it for one company', () => {
    const plan = planRoles(user, 'ADD', 'MAIN', [], ['ADMIN']);
    expect(plan.added).toEqual(['ADMIN']);
    expect(plan.roles.filter((r) => r.roleCode === 'ADMIN')).toHaveLength(2);
  });

  it('removes only the role in the chosen company and reports what was not held', () => {
    const plan = planRoles(user, 'REMOVE', null, [], ['ADMIN', 'VIEWER']);
    expect(plan.removed).toEqual(['ADMIN']);
    expect(plan.unchanged).toEqual(['VIEWER']);
    expect(plan.roles).toEqual([{ roleCode: 'VIEWER', companyCode: 'MAIN', branches: [] }]);
  });

  it('does not change the user row it was given', () => {
    planRoles(user, 'REMOVE', null, [], ['ADMIN']);
    expect((user['roles'] as unknown[]).length).toBe(2);
  });
});
