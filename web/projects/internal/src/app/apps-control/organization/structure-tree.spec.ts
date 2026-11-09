import { ConfigRow } from '@dynamic-cube/shared';
import { buildStructure, matchingOrganizations } from './structure-tree';

const row = (code: string, extra: Record<string, unknown> = {}): ConfigRow =>
  ({ code, nameAr: code, nameEn: code, isActive: true, owner: 'ENTITY', ...extra }) as ConfigRow;

describe('Organization structure', () => {
  const organizations = [
    row('1', { nameEn: 'Group' }),
    row('5', { parentCode: '1' }),
    row('6', { parentCode: '99' }),
    row('7', { parentCode: '8' }),
    row('8', { parentCode: '7' }),
  ];
  const companies = [row('5/2', { nameEn: 'Qimma' }), row('2', { entityCode: '6' })];
  const branches = [row('5-2-HO', { companyCode: '5/2', nameEn: 'Head office' })];
  const apps = [
    row('HR', { companies: ['5/2'] }),
    row('BI', { companies: ['*'] }),
    row('X', { companies: ['6/9'] }),
  ];

  it('puts organizations under their parents, companies under their organization, branches and apps under the company', () => {
    const roots = buildStructure(organizations, companies, branches, apps);
    expect(roots.map((r) => r.organization.code)).toEqual(['1', '6', '7', '8']);
    const five = roots[0].children[0];
    expect(five.organization.code).toBe('5');
    expect(five.companies[0].key).toBe('5/2');
    expect(five.companies[0].branches.map((b) => b.code)).toEqual(['5-2-HO']);
    expect(five.companies[0].apps.map((a) => a.code)).toEqual(['HR', 'BI']);
    expect(roots[1].companies.map((c) => c.key)).toEqual(['6/2']);
  });

  it('finds the organizations above a matching company or branch', () => {
    const roots = buildStructure(organizations, companies, branches, apps);
    expect([...matchingOrganizations(roots, 'head office')].sort()).toEqual(['1', '5']);
    expect(matchingOrganizations(roots, '').size).toBe(0);
  });
});
