import { ConfigRow } from '@dynamic-cube/shared';
import { actionLabel, permissionsOf, privilegesOf, roleScreens, screenTree } from './role-models';

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
});
