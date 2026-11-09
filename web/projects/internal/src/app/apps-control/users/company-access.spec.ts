import { ConfigRow } from '@dynamic-cube/shared';
import { ALL } from './access-model';
import {
  advancedRoles,
  companyAdmissions,
  companyProblems,
  companyRows,
  instanceAdmissions,
  rolesForCompany,
  setDefaultCompany,
  tableRoleEntries,
} from './company-access';

const row = (code: string): ConfigRow =>
  ({ code, nameAr: code, nameEn: code }) as unknown as ConfigRow;

describe('Companies and roles table', () => {
  const stored = [
    { roleCode: '5/2/CLERK', companyCode: '5/2', branches: [ALL], effectiveTo: '2027-01-01' },
    { roleCode: 'AUDITOR', companyCode: '6/1', branches: [ALL] },
    { roleCode: 'ADMIN', companyCode: ALL, branches: [ALL] },
    { roleCode: 'MANAGER', companyCode: '5/2', branches: ['JED'] },
  ];
  const admissions = [
    { companyCode: '5/2', isDefault: true, defaultCurrency: '6' },
    { companyCode: '5/3', isDefault: false },
  ];

  it('shows one line per company: admitted ones, then those a role opens', () => {
    expect(companyRows(admissions, stored)).toEqual([
      { company: '5/2', isDefault: true, roles: ['5/2/CLERK'], defaultCurrency: '6' },
      { company: '5/3', isDefault: false, roles: [], defaultCurrency: null },
      { company: '6/1', isDefault: false, roles: ['AUDITOR'], defaultCurrency: null },
    ]);
  });

  it('keeps roles of every company, of some branches or of a unit for the Advanced part', () => {
    expect(advancedRoles(stored).map((r) => r.roleCode)).toEqual(['ADMIN', 'MANAGER']);
  });

  it('saves the lines back as companies and roles in all branches, keeping dates', () => {
    const rows = companyRows(admissions, stored);
    expect(tableRoleEntries(rows, stored)).toEqual([
      {
        roleCode: '5/2/CLERK',
        companyCode: '5/2',
        branches: [ALL],
        effectiveTo: '2027-01-01',
        orgUnitCode: null,
      },
      { roleCode: 'AUDITOR', companyCode: '6/1', branches: [ALL], orgUnitCode: null },
    ]);
    expect(companyAdmissions(rows).map((a) => a.companyCode)).toEqual(['5/2', '5/3', '6/1']);
  });

  it('admits the organizations of the companies, with the chosen default', () => {
    const rows = companyRows(admissions, stored);
    expect(instanceAdmissions(rows, [{ entityCode: '9', isDefault: false }], '6')).toEqual([
      { entityCode: '9', isDefault: false },
      { entityCode: '5', isDefault: false },
      { entityCode: '6', isDefault: true },
    ]);
    expect(instanceAdmissions(rows.slice(0, 1), [], null)).toEqual([
      { entityCode: '5', isDefault: true },
    ]);
  });

  it('keeps one default company in each organization', () => {
    const rows = setDefaultCompany(companyRows(admissions, stored), 1);
    expect(rows.map((r) => r.isDefault)).toEqual([false, true, false]);
  });

  it('offers the company’s own roles and the roles of no company', () => {
    const roles = [row('5/2/CLERK'), row('5/3/CLERK'), row('AUDITOR')];
    expect(rolesForCompany('5/2', roles).map((r) => r.code)).toEqual(['5/2/CLERK', 'AUDITOR']);
    expect(rolesForCompany(null, roles)).toEqual([]);
  });

  it('reports a line without a company and a company given twice', () => {
    expect(
      companyProblems([
        { company: null, isDefault: false, roles: ['X'], defaultCurrency: null },
        { company: '5/2', isDefault: false, roles: [], defaultCurrency: null },
        { company: '5/2', isDefault: false, roles: [], defaultCurrency: null },
        { company: null, isDefault: false, roles: [], defaultCurrency: null },
      ]),
    ).toEqual([
      { key: 'access.needCompany', row: 1 },
      { key: 'access.companyTwice', row: 3 },
    ]);
  });
});
