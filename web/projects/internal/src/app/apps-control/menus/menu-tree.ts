import { ConfigRow } from '@dynamic-cube/shared';

/** A node of the tree: a group (first level) or an item, with the items under it. */
export interface MenuTreeNode {
  row: ConfigRow;
  group: boolean;
  children: MenuTreeNode[];
}

const order = (r: ConfigRow) => Number(r['displayOrder'] ?? 0);
const byOrder = (a: ConfigRow, b: ConfigRow) => order(a) - order(b) || a.code.localeCompare(b.code);

/** Groups with their items, items under their parents at any depth; items without a group or parent come last. */
export function buildMenuTree(groups: ConfigRow[], menus: ConfigRow[]): MenuTreeNode[] {
  const codes = new Set(menus.map((m) => m.code));
  const itemsUnder = (parent: string): MenuTreeNode[] =>
    menus
      .filter((m) => m['parentCode'] === parent)
      .sort(byOrder)
      .map((m) => ({ row: m, group: false, children: itemsUnder(m.code) }));
  const tops = (group: string | null) =>
    menus
      .filter(
        (m) =>
          !(m['parentCode'] && codes.has(m['parentCode'] as string)) &&
          ((m['groupCode'] as string | null) ?? null) === group,
      )
      .sort(byOrder)
      .map((m) => ({ row: m, group: false, children: itemsUnder(m.code) }));
  const known = new Set(groups.map((g) => g.code));
  const loose = menus
    .filter(
      (m) =>
        !(m['parentCode'] && codes.has(m['parentCode'] as string)) &&
        (!m['groupCode'] || !known.has(m['groupCode'] as string)),
    )
    .sort(byOrder)
    .map((m) => ({ row: m, group: false, children: itemsUnder(m.code) }));
  return [
    ...[...groups].sort(byOrder).map((g) => ({ row: g, group: true, children: tops(g.code) })),
    ...loose,
  ];
}
