import { GrantRow, MenuChoiceGroup, Permissions, ScreenNode, screenCodes } from './role-models';

/**
 * What a role gives through its menus, as the legacy Apps Control: a ticked menu item is shown and opens its screen; the
 * screen's actions and the values of its lists are set under the item.
 */
export interface MenuAccess {
  menus: string[];
  permissions: Permissions;
  /** Grants on screens (per screen: the same list can give all values on one screen and some on another). */
  grants: GrantRow[];
}

/** Screens opened by at least one menu item: edited under their menu item, not in "Other screens". */
export function screensWithMenus(groups: MenuChoiceGroup[]): Set<string> {
  return new Set(
    groups.flatMap((g) => g.items.map((i) => i.resource).filter((r): r is string => !!r)),
  );
}

/**
 * Ticks or clears a menu item. Ticking shows the item and opens its screen (VIEW); clearing hides it and, when no other
 * ticked item opens the same screen, takes away the screen, its sub-resources' actions and its data.
 */
export function tickMenu(
  access: MenuAccess,
  groups: MenuChoiceGroup[],
  screens: ScreenNode[],
  item: string,
  checked: boolean,
): MenuAccess {
  const resourceOf = new Map(
    groups.flatMap((g) => g.items.map((i) => [i.code, i.resource ?? null] as const)),
  );
  const resource = resourceOf.get(item) ?? null;
  const menus = checked
    ? [...new Set([...access.menus, item])]
    : access.menus.filter((m) => m !== item);
  if (!resource) return { ...access, menus };

  if (checked) {
    const actions = access.permissions[resource] ?? [];
    return {
      ...access,
      menus,
      permissions: {
        ...access.permissions,
        [resource]: actions.includes('VIEW') ? actions : ['VIEW', ...actions],
      },
    };
  }

  // Another ticked item still opens the screen: it stays.
  if (menus.some((m) => resourceOf.get(m) === resource)) return { ...access, menus };
  const screen = screens.find((s) => s.code === resource);
  const codes = screen ? screenCodes(screen) : [resource];
  const permissions = Object.fromEntries(
    Object.entries(access.permissions).filter(([code]) => !codes.includes(code)),
  );
  const grants = access.grants.filter((g) => !g.resourceCode || !codes.includes(g.resourceCode));
  return { menus, permissions, grants };
}

/** Ticks or clears every item of a group. */
export function tickGroup(
  access: MenuAccess,
  groups: MenuChoiceGroup[],
  screens: ScreenNode[],
  group: MenuChoiceGroup,
  checked: boolean,
): MenuAccess {
  return group.items.reduce(
    (current, item) => tickMenu(current, groups, screens, item.code, checked),
    access,
  );
}

/** The groups and items whose name or code contains the search (a matching group keeps all its items). */
export function filterGroups(groups: MenuChoiceGroup[], search: string): MenuChoiceGroup[] {
  const term = search.trim().toLowerCase();
  if (!term) return groups;
  const has = (code: string, name: { ar: string; en: string }) =>
    `${code} ${name.ar} ${name.en}`.toLowerCase().includes(term);
  return groups
    .map((g) =>
      has(g.code, g.name) ? g : { ...g, items: g.items.filter((i) => has(i.code, i.name)) },
    )
    .filter((g) => g.items.length);
}
