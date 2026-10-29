import { ConfigRow, LocalizedText } from '@dynamic-cube/shared';

export interface PrivilegeRow {
  resourceCode: string;
  actions: string[];
  /** Field code → HIDDEN | READONLY. */
  fieldRules?: Record<string, string> | null;
}

export type GrantType = 'ALL' | 'VALUES' | 'RULE' | 'NONE';

/** Values of a list a role may see: everywhere (`resourceCode` null) or on one screen. */
export interface GrantRow {
  lovCode: string;
  resourceCode: string | null;
  grantType: GrantType;
  grantedValues: string[];
  rule?: unknown;
  defaultValue?: string | null;
}

export interface RoleRow extends ConfigRow {
  appCode: string | null;
  referenceCode: string | null;
  privileges: PrivilegeRow[];
  grants: GrantRow[];
  /** Menu item codes the role shows. */
  menus?: string[];
}

/** A list (LOV) used on a screen: on the screen itself or on one of its sub-resources. */
export interface ScreenList {
  lovCode: string;
  name: LocalizedText;
  /** Where the grant applies: the resource the list is bound to (or the screen). */
  resourceCode: string;
  /** Fields of the screen that show the list. */
  fields: string[];
}

/** A menu group with its items, for choosing the items a role shows. */
export interface MenuChoiceGroup {
  code: string;
  name: LocalizedText;
  items: { code: string; name: LocalizedText; app: string | null }[];
}

/** A resource as the role editor shows it. */
export interface ScreenNode {
  code: string;
  name: LocalizedText;
  /** Where it appears: the menu group of a menu that opens it, else "Other screens". */
  place: LocalizedText;
  actions: string[];
  /** Its sub-resources (menus and actions inside the screen), at any depth. */
  children: ScreenNode[];
}

/** Granted actions per resource code. */
export type Permissions = Record<string, string[]>;

const OTHER_SCREENS: LocalizedText = { ar: 'شاشات أخرى', en: 'Other screens' };

const ACTIONS: Record<string, LocalizedText> = {
  VIEW: { ar: 'فتح الشاشة', en: 'Open the screen' },
  CREATE: { ar: 'إضافة', en: 'Add' },
  EDIT: { ar: 'تعديل', en: 'Edit' },
  DELETE: { ar: 'حذف', en: 'Delete' },
  APPROVE: { ar: 'اعتماد', en: 'Approve' },
  EXPORT: { ar: 'تصدير', en: 'Export' },
  PRINT: { ar: 'طباعة', en: 'Print' },
};

/** Label of an action: the standard ones translated, custom codes made readable (EQUIPMENT_REVIEW → Equipment review). */
export function actionLabel(action: string): LocalizedText {
  if (ACTIONS[action]) {
    return ACTIONS[action];
  }
  const words = action.toLowerCase().replace(/[_-]+/g, ' ').trim();
  const text = words.charAt(0).toUpperCase() + words.slice(1);
  return { ar: text, en: text };
}

/**
 * Top-level resources with their sub-resources, ordered by where they appear (menu group) and name. A resource whose
 * parent is not listed counts as top-level.
 */
export function screenTree(resources: ConfigRow[], menus: ConfigRow[], groups: ConfigRow[]): ScreenNode[] {
  const codes = new Set(resources.map((r) => r.code));
  const groupNames = new Map(groups.map((g) => [g.code, { ar: g.nameAr, en: g.nameEn } as LocalizedText]));
  const placeOf = new Map<string, LocalizedText>();
  for (const menu of menus) {
    const resource = menu['resourceCode'] as string | null;
    const group = groupNames.get(menu['groupCode'] as string);
    if (resource && group && !placeOf.has(resource)) {
      placeOf.set(resource, group);
    }
  }
  const children = new Map<string, ConfigRow[]>();
  for (const r of resources) {
    const parent = r['parentCode'] as string | null;
    if (parent && codes.has(parent) && parent !== r.code) {
      children.set(parent, [...(children.get(parent) ?? []), r]);
    }
  }
  const node = (r: ConfigRow, place: LocalizedText): ScreenNode => ({
    code: r.code,
    name: { ar: r.nameAr, en: r.nameEn },
    place,
    actions: (r['availableActions'] as string[] | undefined) ?? ['VIEW'],
    children: [],
  });
  const descendants = (code: string, place: LocalizedText, seen: Set<string>): ScreenNode[] =>
    (children.get(code) ?? [])
      .filter((c) => !seen.has(c.code))
      .flatMap((c) => {
        seen.add(c.code);
        return [node(c, place), ...descendants(c.code, place, seen)];
      });
  return resources
    .filter((r) => {
      const parent = r['parentCode'] as string | null;
      return !parent || !codes.has(parent) || parent === r.code;
    })
    .map((r) => {
      const place = placeOf.get(r.code) ?? OTHER_SCREENS;
      return { ...node(r, place), children: descendants(r.code, place, new Set([r.code])) };
    })
    .sort(
      (a, b) =>
        Number(a.place === OTHER_SCREENS) - Number(b.place === OTHER_SCREENS) || a.place.en.localeCompare(b.place.en) || a.name.en.localeCompare(b.name.en),
    );
}

/** Granted actions from a role's privileges. */
export function permissionsOf(privileges: PrivilegeRow[]): Permissions {
  return Object.fromEntries(privileges.filter((p) => p.actions.length).map((p) => [p.resourceCode, [...p.actions]]));
}

/** Top-level screens a role touches: granted on the screen itself or on one of its sub-resources. */
export function roleScreens(tree: ScreenNode[], permissions: Permissions): string[] {
  return tree.filter((s) => permissions[s.code]?.length || s.children.some((c) => permissions[c.code]?.length)).map((s) => s.code);
}

/** Privileges to save: resources with at least one action, keeping the field rules they had. */
export function privilegesOf(permissions: Permissions, existing: PrivilegeRow[]): PrivilegeRow[] {
  const rules = new Map(existing.map((p) => [p.resourceCode, p.fieldRules ?? null]));
  return Object.entries(permissions)
    .filter(([, actions]) => actions.length)
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([resourceCode, actions]) => ({ resourceCode, actions, fieldRules: rules.get(resourceCode) ?? null }));
}

/** Codes of a screen and its sub-resources. */
export function screenCodes(screen: ScreenNode): string[] {
  return [screen.code, ...screen.children.map((c) => c.code)];
}

/**
 * The lists a screen uses: the LOVs bound to its fields (or to its sub-resources'), and lists the role already restricts
 * on it.
 */
export function screenLists(screen: ScreenNode, lovs: ConfigRow[], grants: GrantRow[]): ScreenList[] {
  const codes = screenCodes(screen);
  const names = new Map(lovs.map((l) => [l.code, { ar: l.nameAr, en: l.nameEn } as LocalizedText]));
  const lists = new Map<string, ScreenList>();
  for (const lov of lovs) {
    for (const binding of (lov['bindings'] as { resourceCode: string; fieldCode: string }[] | undefined) ?? []) {
      if (!codes.includes(binding.resourceCode)) continue;
      const key = `${lov.code}|${binding.resourceCode}`;
      const list = lists.get(key) ?? { lovCode: lov.code, name: names.get(lov.code)!, resourceCode: binding.resourceCode, fields: [] };
      list.fields.push(binding.fieldCode);
      lists.set(key, list);
    }
  }
  for (const grant of grants) {
    if (!grant.resourceCode || !codes.includes(grant.resourceCode)) continue;
    const key = `${grant.lovCode}|${grant.resourceCode}`;
    if (!lists.has(key)) {
      lists.set(key, {
        lovCode: grant.lovCode,
        name: names.get(grant.lovCode) ?? { ar: grant.lovCode, en: grant.lovCode },
        resourceCode: grant.resourceCode,
        fields: [],
      });
    }
  }
  return [...lists.values()];
}

/** Menu items by group (groups in display order, items without a group last), for the role's menu choice. */
export function menuChoices(menus: ConfigRow[], groups: ConfigRow[]): MenuChoiceGroup[] {
  const order = (r: ConfigRow) => Number(r['displayOrder'] ?? 0);
  const sorted = [...groups].sort((a, b) => order(a) - order(b) || a.code.localeCompare(b.code));
  const item = (m: ConfigRow) => ({ code: m.code, name: { ar: m.nameAr, en: m.nameEn } as LocalizedText, app: (m['appCode'] as string | null) ?? null });
  const items = (group: string | null) =>
    menus
      .filter((m) => ((m['groupCode'] as string | null) ?? null) === group && m['itemType'] !== 'GROUP')
      .sort((a, b) => order(a) - order(b) || a.code.localeCompare(b.code))
      .map(item);
  const result = sorted.map((g) => ({ code: g.code, name: { ar: g.nameAr, en: g.nameEn } as LocalizedText, items: items(g.code) }));
  const known = new Set(groups.map((g) => g.code));
  const orphans = menus.filter((m) => m['groupCode'] && !known.has(m['groupCode'] as string) && m['itemType'] !== 'GROUP').map(item);
  const loose = [...items(null), ...orphans];
  if (loose.length) result.push({ code: '', name: { ar: 'عناصر أخرى', en: 'Other items' }, items: loose });
  return result.filter((g) => g.items.length);
}

/** Grants on the given resources, and the others (everywhere, or on screens not shown). */
export function splitGrants(grants: GrantRow[], resources: string[]): { onScreens: GrantRow[]; others: GrantRow[] } {
  const on = (g: GrantRow) => !!g.resourceCode && resources.includes(g.resourceCode);
  return { onScreens: grants.filter(on), others: grants.filter((g) => !on(g)) };
}
