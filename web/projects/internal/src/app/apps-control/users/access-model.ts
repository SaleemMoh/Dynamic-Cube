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

/**
 * Actions and data on a screen given to the person alone, in a company or all of them (`*`), or actions their roles give
 * that are taken away from them; optionally for a period (ISO dates).
 */
export interface PersonalPrivilege {
  companyCode: string;
  resourceCode: string;
  actions: string[];
  deniedActions: string[];
  grants: PersonalGrant[];
  effectiveFrom: string | null;
  effectiveTo: string | null;
}

/** A role the person holds (directly or from a department) and where: a company or all of them (`*`). */
export interface HeldRole {
  roleCode: string;
  company: string;
}

/** What a role contains, as its full row has it (its own contents and each company where it differs). */
export interface RoleContents {
  code: string;
  nameAr: string;
  nameEn: string;
  privileges?: { resourceCode: string; actions: string[] }[];
  menus?: string[];
  overrides?: {
    companyCode: string;
    privileges: { resourceCode: string; actions: string[] }[];
    menus: string[];
  }[];
}

/** Actions roles give on a screen, with the roles giving them. */
export interface RoleGiven {
  actions: Set<string>;
  roles: RoleContents[];
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
    deniedActions: [...(p.deniedActions ?? [])],
    effectiveFrom: p.effectiveFrom ?? null,
    effectiveTo: p.effectiveTo ?? null,
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
    deniedActions: [],
    grants: [],
    effectiveFrom: null,
    effectiveTo: null,
  };
  const next = change(current);
  const others = list.filter((p) => p !== current);
  // A screen with no action given or removed and no data is no personal privilege any more.
  return next.actions.length || next.deniedActions.length || next.grants.length
    ? [...others, next]
    : others;
}

/** Sets the actions given on a screen in a company (an action given is no longer removed). */
export function setPersonalActions(
  list: PersonalPrivilege[],
  company: string,
  resource: string,
  actions: string[],
): PersonalPrivilege[] {
  return update(list, company, resource, (p) => ({
    ...p,
    actions,
    deniedActions: p.deniedActions.filter((a) => !actions.includes(a)),
  }));
}

/** Sets the actions taken away from the person on a screen in a company (an action removed is no longer given). */
export function setPersonalDenied(
  list: PersonalPrivilege[],
  company: string,
  resource: string,
  deniedActions: string[],
): PersonalPrivilege[] {
  return update(list, company, resource, (p) => ({
    ...p,
    deniedActions,
    actions: p.actions.filter((a) => !deniedActions.includes(a)),
  }));
}

/** Sets the period of a personal privilege (null: no limit on that side). */
export function setPersonalPeriod(
  list: PersonalPrivilege[],
  privilege: PersonalPrivilege,
  effectiveFrom: string | null,
  effectiveTo: string | null,
): PersonalPrivilege[] {
  return list.map((p) => (p === privilege ? { ...p, effectiveFrom, effectiveTo } : p));
}

/**
 * What the person's roles give in a company (`*`: roles held in all companies): per screen, the actions and the roles
 * giving them. A role held in the company where it differs gives that company's version; a menu item gives VIEW on its
 * screen.
 */
export function roleAccess(
  company: string,
  held: HeldRole[],
  roles: Map<string, RoleContents>,
  menuScreens: Map<string, string>,
): Map<string, RoleGiven> {
  const given = new Map<string, RoleGiven>();
  const add = (resource: string, actions: string[], role: RoleContents) => {
    const entry = given.get(resource) ?? { actions: new Set<string>(), roles: [] };
    for (const action of actions) entry.actions.add(action);
    if (!entry.roles.includes(role)) entry.roles.push(role);
    given.set(resource, entry);
  };
  const applying = held.filter(
    (h) => h.company === ALL || (company !== ALL && h.company === company),
  );
  for (const code of new Set(applying.map((h) => h.roleCode))) {
    const role = roles.get(code);
    if (!role) continue;
    const version =
      company !== ALL ? role.overrides?.find((o) => o.companyCode === company) : undefined;
    for (const privilege of (version ?? role).privileges ?? []) {
      if (privilege.actions.length) add(privilege.resourceCode, privilege.actions, role);
    }
    for (const menu of (version ?? role).menus ?? []) {
      const resource = menuScreens.get(menu);
      if (resource) add(resource, ['VIEW'], role);
    }
  }
  return given;
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
    .filter(
      (p) => p.resourceCode && (p.actions.length || p.deniedActions.length || p.grants.length),
    )
    .sort(
      (a, b) =>
        a.companyCode.localeCompare(b.companyCode) || a.resourceCode.localeCompare(b.resourceCode),
    );
}
