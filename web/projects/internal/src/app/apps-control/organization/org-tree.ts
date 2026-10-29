import { ConfigRow } from '@dynamic-cube/shared';

/** An org unit with its sub-units, as the tree shows it. */
export interface OrgNode {
  row: ConfigRow;
  children: OrgNode[];
  /** Units under this one, at any depth. */
  descendants: number;
}

/**
 * Builds the org tree from the flat list of units (each names its parent by code). Units whose parent is missing
 * become roots, so nothing is hidden; siblings are sorted by Arabic name, then code.
 */
export function buildOrgTree(rows: ConfigRow[]): OrgNode[] {
  const nodes = new Map(rows.map((row) => [row.code, { row, children: [] as OrgNode[], descendants: 0 }]));
  const roots: OrgNode[] = [];
  for (const node of nodes.values()) {
    const parent = node.row['parentCode'] as string | null | undefined;
    const parentNode = parent && parent !== node.row.code ? nodes.get(parent) : undefined;
    (parentNode ? parentNode.children : roots).push(node);
  }
  const order = (a: OrgNode, b: OrgNode) => String(a.row.nameAr ?? '').localeCompare(String(b.row.nameAr ?? ''), 'ar') || a.row.code.localeCompare(b.row.code);
  const finish = (list: OrgNode[], seen: Set<string>): number => {
    list.sort(order);
    let count = 0;
    for (const node of list) {
      if (seen.has(node.row.code)) continue;
      seen.add(node.row.code);
      node.descendants = finish(node.children, seen);
      count += 1 + node.descendants;
    }
    return count;
  };
  finish(roots, new Set());
  return roots;
}

/** Codes of the units on the way to `code` (its ancestors, root first). */
export function pathTo(rows: ConfigRow[], code: string): string[] {
  const byCode = new Map(rows.map((r) => [r.code, r]));
  const path: string[] = [];
  let current = byCode.get(code)?.['parentCode'] as string | null | undefined;
  while (current && !path.includes(current)) {
    path.unshift(current);
    current = byCode.get(current)?.['parentCode'] as string | null | undefined;
  }
  return path;
}

/** Units matching the search (code or either name) and their ancestors, so matches show in place. */
export function matchingCodes(rows: ConfigRow[], term: string): Set<string> | null {
  const t = term.trim().toLowerCase();
  if (!t) return null;
  const result = new Set<string>();
  for (const row of rows) {
    if (`${row.code} ${row.nameAr ?? ''} ${row.nameEn ?? ''}`.toLowerCase().includes(t)) {
      result.add(row.code);
      for (const code of pathTo(rows, row.code)) result.add(code);
    }
  }
  return result;
}
