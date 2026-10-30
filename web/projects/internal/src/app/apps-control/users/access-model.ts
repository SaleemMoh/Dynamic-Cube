import { ConfigRow } from '@dynamic-cube/shared';

/**
 * The explicit choice of every company or every branch (ADR-0036). An empty choice is never "all": a row without a
 * company or branches is incomplete and cannot be saved.
 */
export const ALL = '*';

/** A role as the user row stores it (one per role and company). */
export interface RoleEntry {
  roleCode: string;
  orgUnitCode?: string | null;
  companyCode?: string | null;
  branches?: string[];
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
}

/** A line of the Access tab: roles held in a company (or all, `*`) and some of its branches (or all, `['*']`). */
export interface AccessRow {
  /** Null until chosen. */
  company: string | null;
  branches: string[];
  roles: string[];
  /** Kept from rows that hold a role only within an org unit. */
  orgUnitCode: string | null;
}

/** Values of a list one person may see on a screen, besides their roles. */
export interface PersonalGrant {
  lovCode: string;
  grantType: 'ALL' | 'VALUES';
  grantedValues: string[];
}

/** Actions and data on a screen given to the person alone, in a company or all of them (`*`). */
export interface PersonalPrivilege {
  companyCode: string;
  resourceCode: string;
  actions: string[];
  grants: PersonalGrant[];
}

/** A department role: what the person receives from a unit they are placed in. */
export interface DepartmentRole {
  unit: ConfigRow;
  roles: string[];
  /** The branch the unit serves (it or a unit above it), where the roles are held; none: the whole organization. */
  branch: ConfigRow | null;
}

/**
 * A multi-choice with an "all" option: picking all clears the others, picking another clears all (the newest choice
 * wins).
 */
export function pickWithAll(previous: string[], next: string[]): string[] {
  if (next.includes(ALL) && !previous.includes(ALL)) return [ALL];
  if (next.includes(ALL) && next.length > 1) return next.filter((v) => v !== ALL);
  return next;
}

/** The user's roles grouped by company, branches and unit, as the Access tab shows them. */
export function groupRoles(entries: RoleEntry[]): AccessRow[] {
  const rows = new Map<string, AccessRow>();
  for (const entry of entries) {
    const branches = [...(entry.branches ?? [])].sort();
    const key = `${entry.companyCode ?? ''}|${branches.join(',')}|${entry.orgUnitCode ?? ''}`;
    const row = rows.get(key) ?? {
      company: entry.companyCode || null,
      branches,
      roles: [],
      orgUnitCode: entry.orgUnitCode ?? null,
    };
    row.roles.push(entry.roleCode);
    rows.set(key, row);
  }
  return [...rows.values()];
}

/** The rows back as one entry per role, keeping the dates of entries that stay. All companies take all branches. */
export function flattenRoles(rows: AccessRow[], existing: RoleEntry[]): RoleEntry[] {
  return rows.flatMap((row) =>
    row.roles.map((code) => {
      const before = existing.find(
        (e) =>
          e.roleCode === code &&
          (e.companyCode || null) === row.company &&
          (e.orgUnitCode ?? null) === row.orgUnitCode,
      );
      return {
        ...before,
        roleCode: code,
        companyCode: row.company,
        branches: row.company === ALL ? [ALL] : row.branches,
        orgUnitCode: row.orgUnitCode,
      };
    }),
  );
}

/** What is missing in the rows before they can be saved (translation keys with the row number). */
export function rowProblems(rows: AccessRow[]): { key: string; row: number }[] {
  return rows.flatMap((row, i) => {
    if (!row.roles.length) return [];
    const problems: { key: string; row: number }[] = [];
    if (!row.company) problems.push({ key: 'access.needCompany', row: i + 1 });
    else if (row.company !== ALL && !row.branches.length)
      problems.push({ key: 'access.needBranches', row: i + 1 });
    return problems;
  });
}

/** The roles of the units a person is placed in, with the branch each unit serves (looking up the tree). */
export function departmentRoles(
  assignments: { orgUnitCode: string }[],
  units: ConfigRow[],
  branches: ConfigRow[],
): DepartmentRole[] {
  const byCode = new Map(units.map((u) => [u.code, u]));
  const served = (code: string): ConfigRow | null => {
    for (
      let unit = byCode.get(code), depth = 0;
      unit && depth < 50;
      unit = byCode.get(unit['parentCode'] as string), depth++
    ) {
      const branch = branches.find((b) => b['orgUnitCode'] === unit!.code);
      if (branch) return branch;
    }
    return null;
  };
  return assignments
    .map((a) => byCode.get(a.orgUnitCode))
    .filter(
      (u): u is ConfigRow => !!u && ((u['defaultRoles'] as string[] | undefined) ?? []).length > 0,
    )
    .map((unit) => ({ unit, roles: unit['defaultRoles'] as string[], branch: served(unit.code) }));
}

/** The stored personal privileges of a user row. */
export function readPersonal(stored: unknown): PersonalPrivilege[] {
  return ((stored as Partial<PersonalPrivilege>[] | undefined) ?? []).map((p) => ({
    companyCode: p.companyCode || ALL,
    resourceCode: p.resourceCode ?? '',
    actions: [...(p.actions ?? [])],
    grants: (p.grants ?? []).map((g) => ({
      lovCode: g.lovCode,
      grantType: g.grantType === 'VALUES' ? 'VALUES' : 'ALL',
      grantedValues: [...(g.grantedValues ?? [])],
    })),
  }));
}

function update(
  list: PersonalPrivilege[],
  company: string,
  resource: string,
  change: (p: PersonalPrivilege) => PersonalPrivilege,
): PersonalPrivilege[] {
  const current = list.find((p) => p.companyCode === company && p.resourceCode === resource) ?? {
    companyCode: company,
    resourceCode: resource,
    actions: [],
    grants: [],
  };
  const next = change(current);
  const others = list.filter((p) => p !== current);
  // A screen with neither actions nor data is no personal privilege any more.
  return next.actions.length || next.grants.length ? [...others, next] : others;
}

/** Sets the actions given on a screen in a company. */
export function setPersonalActions(
  list: PersonalPrivilege[],
  company: string,
  resource: string,
  actions: string[],
): PersonalPrivilege[] {
  return update(list, company, resource, (p) => ({ ...p, actions }));
}

/** Sets (or, with null, removes) the values of a list given on a screen in a company. */
export function setPersonalGrant(
  list: PersonalPrivilege[],
  company: string,
  resource: string,
  lovCode: string,
  grant: Omit<PersonalGrant, 'lovCode'> | null,
): PersonalPrivilege[] {
  return update(list, company, resource, (p) => ({
    ...p,
    grants: [
      ...p.grants.filter((g) => g.lovCode !== lovCode),
      ...(grant ? [{ lovCode, ...grant }] : []),
    ],
  }));
}

/** Personal privileges as the server takes them: complete ones only, in a stable order. */
export function personalPayload(list: PersonalPrivilege[]): PersonalPrivilege[] {
  return list
    .map((p) => ({
      ...p,
      grants: p.grants.filter((g) => g.grantType === 'ALL' || g.grantedValues.length),
    }))
    .filter((p) => p.resourceCode && (p.actions.length || p.grants.length))
    .sort(
      (a, b) =>
        a.companyCode.localeCompare(b.companyCode) || a.resourceCode.localeCompare(b.resourceCode),
    );
}
