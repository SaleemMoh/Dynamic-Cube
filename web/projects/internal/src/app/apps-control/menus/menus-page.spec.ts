import { ConfigRow } from '@dynamic-cube/shared';
import { buildMenuTree, MenuTreeNode } from './menus-page';

const row = (code: string, extra: Record<string, unknown> = {}): ConfigRow => ({
  code,
  nameAr: code,
  nameEn: code,
  isActive: true,
  owner: 'ENTITY',
  ...extra,
});

const shape = (nodes: MenuTreeNode[]): unknown[] =>
  nodes.map((n) => (n.children.length ? { [n.row.code]: shape(n.children) } : n.row.code));

describe('buildMenuTree', () => {
  it('puts items under their group and parents at any depth, in display order, loose items last', () => {
    const groups = [row('M090', { displayOrder: 20 }), row('M010', { displayOrder: 10 })];
    const menus = [
      row('M010-010-020', { parentCode: 'M010-010', displayOrder: 20 }),
      row('M010-010', { groupCode: 'M010', displayOrder: 10 }),
      row('M010-010-010', { parentCode: 'M010-010', displayOrder: 10 }),
      row('M010-010-010-A', { parentCode: 'M010-010-010' }),
      row('M090-010', { groupCode: 'M090' }),
      row('LOOSE'),
    ];

    expect(shape(buildMenuTree(groups, menus))).toEqual([
      { M010: [{ 'M010-010': [{ 'M010-010-010': ['M010-010-010-A'] }, 'M010-010-020'] }] },
      { M090: ['M090-010'] },
      'LOOSE',
    ]);
  });

  it('shows an item whose parent is in no list at the top of its group', () => {
    const tree = buildMenuTree([row('G')], [row('ORPHAN', { groupCode: 'G', parentCode: 'GONE' })]);
    expect(shape(tree)).toEqual([{ G: ['ORPHAN'] }]);
  });
});
