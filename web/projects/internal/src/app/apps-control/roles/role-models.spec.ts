import { ConfigRow } from '@dynamic-cube/shared';
import { actionLabel, menuChoices, permissionsOf, privilegesOf, roleScreens, screenLists, screenTree, splitGrants } from './role-models';

const row = (code: string, extra: Record<string, unknown> = {}): ConfigRow => ({ code, nameAr: code, nameEn: code, isActive: true, owner: 'ENTITY', ...extra });

describe('role models', () => {
  const resources = [
    row('action-center', { availableActions: ['VIEW'] }),
    row('action-center.supervision', { parentCode: 'action-center', availableActions: ['VIEW', 'EDIT'] }),
    row('apps-control.roles', { availableActions: ['VIEW', 'CREATE', 'EDIT'] }),
    row('reports', { availableActions: ['VIEW', 'EXPORT'] }),
  ];
  const menus = [
    row('m1', { resourceCode: 'action-center', groupCode: 'ACTION_CENTER' }),
    row('m2', { resourceCode: 'apps-control.roles', groupCode: 'APPS_CONTROL' }),
  ];
  const groups = [row('ACTION_CENTER', { nameEn: 'Action Center' }), row('APPS_CONTROL', { nameEn: 'Apps Control' })];

  it('lists top-level screens with their sub-resources, by menu group, screens outside menus last', () => {
    const tree = screenTree(resources, menus, groups);
    expect(tree.map((s) => s.code)).toEqual(['action-center', 'apps-control.roles', 'reports']);
    expect(tree[0].place.en).toBe('Action Center');
    expect(tree[0].children.map((c) => c.code)).toEqual(['action-center.supervision']);
    expect(tree[2].place.en).toBe('Other screens');
  });

  it('reads and writes privileges, keeping field rules and dropping resources without actions', () => {
    const tree = screenTree(resources, menus, groups);
    const permissions = permissionsOf([
      { resourceCode: 'action-center.supervision', actions: ['VIEW'] },
      { resourceCode: 'reports', actions: ['VIEW'], fieldRules: { AMOUNT: 'HIDDEN' } },
    ]);
    expect(roleScreens(tree, permissions)).toEqual(['action-center', 'reports']);
    permissions['apps-control.roles'] = [];
    expect(privilegesOf(permissions, [{ resourceCode: 'reports', actions: ['VIEW'], fieldRules: { AMOUNT: 'HIDDEN' } }])).toEqual([
      { resourceCode: 'action-center.supervision', actions: ['VIEW'], fieldRules: null },
      { resourceCode: 'reports', actions: ['VIEW'], fieldRules: { AMOUNT: 'HIDDEN' } },
    ]);
  });

  it('labels standard actions and makes custom codes readable', () => {
    expect(actionLabel('VIEW').en).toBe('Open the screen');
    expect(actionLabel('EQUIPMENT_REVIEW').en).toBe('Equipment review');
  });

  it('lists the dropdowns a screen uses and the lists the role restricts on it', () => {
    const tree = screenTree(resources, menus, groups);
    const center = tree.find((s) => s.code === 'action-center')!;
    const lovs = [
      row('REGIONS', {
        bindings: [
          { resourceCode: 'action-center.supervision', fieldCode: 'region' },
          { resourceCode: 'action-center.supervision', fieldCode: 'to' },
        ],
      }),
      row('TYPES', { bindings: [{ resourceCode: 'reports', fieldCode: 'type' }] }),
      row('CITIES'),
    ];
    const grants = [{ lovCode: 'CITIES', resourceCode: 'action-center', grantType: 'NONE' as const, grantedValues: [] }];
    expect(screenLists(center, lovs, grants)).toEqual([
      { lovCode: 'REGIONS', name: { ar: 'REGIONS', en: 'REGIONS' }, resourceCode: 'action-center.supervision', fields: ['region', 'to'] },
      { lovCode: 'CITIES', name: { ar: 'CITIES', en: 'CITIES' }, resourceCode: 'action-center', fields: [] },
    ]);
    const split = splitGrants(
      [...grants, { lovCode: 'TYPES', resourceCode: null, grantType: 'ALL', grantedValues: [] }],
      ['action-center', 'action-center.supervision'],
    );
    expect(split.onScreens.map((g) => g.lovCode)).toEqual(['CITIES']);
    expect(split.others.map((g) => g.lovCode)).toEqual(['TYPES']);
  });

  it('offers menu items by group, in display order, items without a group last', () => {
    const choices = menuChoices(
      [
        row('b', { groupCode: 'G', displayOrder: 2 }),
        row('a', { groupCode: 'G', displayOrder: 1 }),
        row('sub', { groupCode: 'G', itemType: 'GROUP' }),
        row('loose'),
      ],
      [row('G', { nameEn: 'Group' }), row('EMPTY')],
    );
    expect(choices.map((g) => [g.code, g.items.map((i) => i.code)])).toEqual([
      ['G', ['a', 'b']],
      ['', ['loose']],
    ]);
  });
});
