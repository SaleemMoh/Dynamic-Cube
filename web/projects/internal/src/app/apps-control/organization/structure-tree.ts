import { ConfigRow } from '@dynamic-cube/shared';

/** A company in the tree, with its branches and the apps it uses. */
export interface CompanyNode {
  company: ConfigRow;
  /** `instid/cmp_num`. */
  key: string;
  branches: ConfigRow[];
  apps: ConfigRow[];
}

/** An organization (legacy instance) with the organizations under it and its companies. */
export interface OrganizationNode {
  organization: ConfigRow;
  children: OrganizationNode[];
  companies: CompanyNode[];
}

/** The key of a company row: its organization and number. */
export function companyKey(company: ConfigRow): string {
  const code = company.code;
  if (code.includes('/')) return code;
  const organization = (company['entityCode'] as string | null | undefined) ?? '';
  return organization ? `${organization}/${code}` : code;
}

/**
 * Organizations → their sub-organizations → companies → branches, with the apps each company uses (an app for every
 * company counts for each). An organization whose parent is unknown, or which would sit below itself, is a root.
 */
export function buildStructure(
  organizations: ConfigRow[],
  companies: ConfigRow[],
  branches: ConfigRow[],
  apps: ConfigRow[],
): OrganizationNode[] {
  const byCode = new Map(organizations.map((o) => [o.code, o] as const));
  const parentOf = (o: ConfigRow): string | null => {
    const parent = (o['parentCode'] as string | null | undefined) ?? null;
    if (!parent || !byCode.has(parent)) return null;
    // Never below itself.
    const seen = new Set([o.code]);
    for (let at: string | null = parent; at;) {
      if (seen.has(at)) return null;
      seen.add(at);
      at = (byCode.get(at)?.['parentCode'] as string | null | undefined) ?? null;
      if (at && !byCode.has(at)) break;
    }
    return parent;
  };

  const companyNodes = companies.map((company) => {
    const key = companyKey(company);
    return {
      company,
      key,
      branches: branches.filter((b) => b['companyCode'] === key),
      apps: apps.filter((a) => {
        const list = (a['companies'] as string[] | undefined) ?? [];
        return list.includes(key) || list.includes('*');
      }),
    } satisfies CompanyNode;
  });

  const nodes = new Map<string, OrganizationNode>(
    organizations.map((organization) => [
      organization.code,
      {
        organization,
        children: [],
        companies: companyNodes.filter((c) => c.key.split('/')[0] === organization.code),
      },
    ]),
  );
  const roots: OrganizationNode[] = [];
  for (const organization of organizations) {
    const node = nodes.get(organization.code)!;
    const parent = parentOf(organization);
    if (parent) nodes.get(parent)!.children.push(node);
    else roots.push(node);
  }
  return roots;
}

/** The codes of the organizations whose subtree (names, codes, companies, branches) matches the search. */
export function matchingOrganizations(roots: OrganizationNode[], search: string): Set<string> {
  const term = search.trim().toLowerCase();
  const found = new Set<string>();
  if (!term) return found;
  const matches = (row: ConfigRow) =>
    [row.code, row.nameAr, row.nameEn].some((v) => (v ?? '').toLowerCase().includes(term));
  const visit = (node: OrganizationNode): boolean => {
    let hit =
      matches(node.organization) ||
      node.companies.some(
        (c) => matches(c.company) || c.key.includes(term) || c.branches.some(matches),
      );
    for (const child of node.children) hit = visit(child) || hit;
    if (hit) found.add(node.organization.code);
    return hit;
  };
  roots.forEach(visit);
  return found;
}
