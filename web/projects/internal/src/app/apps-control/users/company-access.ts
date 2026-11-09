import { ConfigRow } from '@dynamic-cube/shared';
import { ALL, RoleEntry } from './access-model';

/**
 * The simple Access table, like the legacy Apps Control: the companies a person may enter (one default per
 * organization) and the roles they hold in each, in all its branches.
 */
export interface CompanyRow {
  /** The company's key, `instid/cmp_num`; null until chosen. */
  company: string | null;
  /** The person's default company in its organization (legacy DEFAULT_CMP_FLAG). */
  isDefault: boolean;
  roles: string[];
  /** Kept as stored (legacy DEFAULT_CURRENCY). */
  defaultCurrency: string | null;
}

/** A company the person may enter, as the user row stores it. */
export interface CompanyAdmission {
  companyCode: string;
  isDefault: boolean;
  defaultCurrency?: string | null;
}

/** An organization the person may enter, as the user row stores it. */
export interface InstanceAdmission {
  entityCode: string;
  isDefault: boolean;
}

/** The organization of a company key (`instid/cmp_num` → `instid`). */
export function organizationOf(company: string): string {
  const slash = company.indexOf('/');
  return slash < 0 ? company : company.slice(0, slash);
}

/** A role held in one company, in all its branches, not only within a unit: shown in the simple table. */
export function isSimpleRole(entry: RoleEntry): boolean {
  const branches = entry.branches ?? [ALL];
  return (
    !!entry.companyCode &&
    entry.companyCode !== ALL &&
    branches.length === 1 &&
    branches[0] === ALL &&
    !entry.orgUnitCode
  );
}

/** One line per company: the admitted ones first (as stored), then those only a role opens. */
export function companyRows(admissions: CompanyAdmission[], entries: RoleEntry[]): CompanyRow[] {
  const rows: CompanyRow[] = admissions.map((a) => ({
    company: a.companyCode,
    isDefault: a.isDefault,
    roles: [],
    defaultCurrency: a.defaultCurrency ?? null,
  }));
  for (const entry of entries.filter(isSimpleRole)) {
    let row = rows.find((r) => r.company === entry.companyCode);
    if (!row) {
      row = { company: entry.companyCode!, isDefault: false, roles: [], defaultCurrency: null };
      rows.push(row);
    }
    if (!row.roles.includes(entry.roleCode)) row.roles.push(entry.roleCode);
  }
  return rows;
}

/** The roles shown in the Advanced part: every company, some branches, or only within a unit. */
export function advancedRoles(entries: RoleEntry[]): RoleEntry[] {
  return entries.filter((e) => !isSimpleRole(e));
}

/** The roles of the table, one entry per role and company in all branches, keeping the dates of entries that stay. */
export function tableRoleEntries(rows: CompanyRow[], existing: RoleEntry[]): RoleEntry[] {
  return rows
    .filter((r) => r.company)
    .flatMap((row) =>
      row.roles.map((roleCode) => {
        const before = existing.find(
          (e) => isSimpleRole(e) && e.roleCode === roleCode && e.companyCode === row.company,
        );
        return {
          ...before,
          roleCode,
          companyCode: row.company,
          branches: [ALL],
          orgUnitCode: null,
        };
      }),
    );
}

/** The companies to save: every line of the table. */
export function companyAdmissions(rows: CompanyRow[]): CompanyAdmission[] {
  return rows
    .filter((r): r is CompanyRow & { company: string } => !!r.company)
    .map((r) => ({
      companyCode: r.company,
      isDefault: r.isDefault,
      defaultCurrency: r.defaultCurrency,
    }));
}

/**
 * The organizations to save: those of the table's companies, and those admitted without a company (kept); the default
 * one is the chosen organization, else the only one.
 */
export function instanceAdmissions(
  rows: CompanyRow[],
  stored: InstanceAdmission[],
  defaultOrganization: string | null,
): InstanceAdmission[] {
  const codes = [
    ...new Set([
      ...stored.map((s) => s.entityCode),
      ...rows.filter((r) => r.company).map((r) => organizationOf(r.company!)),
    ]),
  ];
  const chosen =
    defaultOrganization && codes.includes(defaultOrganization) ? defaultOrganization : null;
  return codes.map((entityCode) => ({
    entityCode,
    isDefault: chosen ? entityCode === chosen : codes.length === 1,
  }));
}

/** Makes a line the default company of its organization (the organization's other lines lose the star). */
export function setDefaultCompany(rows: CompanyRow[], index: number): CompanyRow[] {
  const company = rows[index]?.company;
  if (!company) return rows;
  const organization = organizationOf(company);
  return rows.map((r, i) =>
    i === index
      ? { ...r, isDefault: true }
      : r.company && organizationOf(r.company) === organization
        ? { ...r, isDefault: false }
        : r,
  );
}

/**
 * The roles that can be held in a company: its own (`instid/cmp_num/ROLE`) and the roles of no company (a plain code).
 */
export function rolesForCompany(company: string | null, roles: ConfigRow[]): ConfigRow[] {
  if (!company) return [];
  return roles.filter((r) => r.code.startsWith(company + '/') || !r.code.includes('/'));
}

/** What is missing or repeated in the table (translation keys with the line number). */
export function companyProblems(rows: CompanyRow[]): { key: string; row: number }[] {
  return rows.flatMap((row, i) => {
    if (!row.company) return row.roles.length ? [{ key: 'access.needCompany', row: i + 1 }] : [];
    return rows.findIndex((r) => r.company === row.company) < i
      ? [{ key: 'access.companyTwice', row: i + 1 }]
      : [];
  });
}
