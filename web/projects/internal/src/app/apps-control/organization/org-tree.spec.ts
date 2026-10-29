import { ConfigRow } from '@dynamic-cube/shared';
import { employeesOf } from './org-employees';
import { buildOrgTree, descendantsOf, matchingCodes, pathTo } from './org-tree';

const row = (code: string, parentCode: string | null, nameAr = code): ConfigRow => ({ code, nameAr, nameEn: code, parentCode, isActive: true, owner: 'ENTITY' }) as ConfigRow;
const rows = [row('HQ', null, 'أ'), row('ICT', 'HQ', 'ب'), row('DEV', 'ICT'), row('OPS', 'ICT'), row('LOST', 'MISSING'), row('FIN', 'HQ', 'ت')];

describe('org tree', () => {
  it('nests units under their parents and counts what is under each', () => {
    const tree = buildOrgTree(rows);
    expect(tree.map((n) => n.row.code)).toEqual(['HQ', 'LOST']);
    expect(tree[0].children.map((n) => n.row.code)).toEqual(['ICT', 'FIN']);
    expect(tree[0].descendants).toBe(4);
    expect(tree[0].children[0].children.map((n) => n.row.code)).toEqual(['DEV', 'OPS']);
  });

  it('finds the way to a unit and keeps the ancestors of matches', () => {
    expect(pathTo(rows, 'DEV')).toEqual(['HQ', 'ICT']);
    expect([...matchingCodes(rows, 'ops')!].sort()).toEqual(['HQ', 'ICT', 'OPS']);
    expect(matchingCodes(rows, ' ')).toBeNull();
  });

  it('lists the units under a unit and the employees placed in them', () => {
    expect(descendantsOf(rows, 'HQ')).toEqual(['ICT', 'FIN', 'DEV', 'OPS']);
    const users = [
      { code: 'sara', nameAr: 'سارة', nameEn: 'Sara', isActive: true, owner: 'ENTITY', assignments: [{ orgUnitCode: 'DEV', positionCode: 'ANALYST', isPrimary: true }] },
      { code: 'ali', nameAr: 'علي', nameEn: 'Ali', isActive: true, owner: 'ENTITY', assignments: [{ orgUnitCode: 'ICT', positionCode: 'MANAGER' }, { orgUnitCode: 'FIN' }] },
    ] as unknown as ConfigRow[];
    expect(employeesOf(users, ['ICT']).map((e) => e.userName)).toEqual(['ali']);
    expect(employeesOf(users, ['ICT', ...descendantsOf(rows, 'ICT')]).map((e) => `${e.userName}@${e.unitCode}`)).toEqual(['ali@ICT', 'sara@DEV']);
  });
});
