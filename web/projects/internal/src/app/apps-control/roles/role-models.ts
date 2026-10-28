import { ConfigRow, LocalizedText } from '@dynamic-cube/shared';

export interface PrivilegeRow {
  resourceCode: string;
  actions: string[];
  /** Field code → HIDDEN | READONLY. */
  fieldRules?: Record<string, string> | null;
}

export interface RoleRow extends ConfigRow {
  appCode: string | null;
  referenceCode: string | null;
  privileges: PrivilegeRow[];
  grants: unknown[];
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
