import { filterGroups, screensWithMenus, tickGroup, tickMenu } from './role-access';
import { MenuChoiceGroup, ScreenNode } from './role-models';

const name = (code: string) => ({ ar: code, en: code });

describe('Role menus, screens and data in one place', () => {
  const groups: MenuChoiceGroup[] = [
    {
      code: '1/HR',
      name: name('HR'),
      items: [
        { code: '1/EMP', name: name('Employees'), app: null, resource: 'HR.EMP' },
        { code: '1/EMP2', name: name('Employees (copy)'), app: null, resource: 'HR.EMP' },
        { code: '1/LEAVE', name: name('Leaves'), app: null, resource: 'HR.LEAVE' },
        { code: '1/HELP', name: name('Help'), app: null, resource: null },
      ],
    },
  ];
  const screens: ScreenNode[] = [
    {
      code: 'HR.EMP',
      name: name('Employees'),
      place: name('HR'),
      actions: ['VIEW', 'EDIT'],
      children: [
        {
          code: 'HR.EMP.PAY',
          name: name('Pay'),
          place: name('HR'),
          actions: ['VIEW'],
          children: [],
        },
      ],
    },
  ];
  const empty = { menus: [], permissions: {}, grants: [] };

  it('ticking a menu item shows it and opens its screen', () => {
    const access = tickMenu(empty, groups, screens, '1/EMP', true);
    expect(access.menus).toEqual(['1/EMP']);
    expect(access.permissions).toEqual({ 'HR.EMP': ['VIEW'] });
  });

  it('keeps the screen while another ticked item opens it, then takes the screen and its data away', () => {
    let access = tickMenu(empty, groups, screens, '1/EMP', true);
    access = tickMenu(access, groups, screens, '1/EMP2', true);
    access = {
      ...access,
      permissions: { ...access.permissions, 'HR.EMP.PAY': ['VIEW'] },
      grants: [
        { lovCode: 'GRADE', resourceCode: 'HR.EMP', grantType: 'ALL', grantedValues: [] },
        { lovCode: 'GRADE', resourceCode: 'HR.LEAVE', grantType: 'VALUES', grantedValues: ['G1'] },
      ],
    };
    access = tickMenu(access, groups, screens, '1/EMP', false);
    expect(access.permissions['HR.EMP']).toEqual(['VIEW']);
    access = tickMenu(access, groups, screens, '1/EMP2', false);
    expect(access.menus).toEqual([]);
    expect(access.permissions).toEqual({});
    expect(access.grants.map((g) => g.resourceCode)).toEqual(['HR.LEAVE']);
  });

  it('ticks a whole group, and an item that opens no screen only shows', () => {
    const access = tickGroup(empty, groups, screens, groups[0], true);
    expect(access.menus).toEqual(['1/EMP', '1/EMP2', '1/LEAVE', '1/HELP']);
    expect(Object.keys(access.permissions)).toEqual(['HR.EMP', 'HR.LEAVE']);
  });

  it('knows which screens have menus and finds items by name', () => {
    expect([...screensWithMenus(groups)]).toEqual(['HR.EMP', 'HR.LEAVE']);
    expect(filterGroups(groups, 'leave')[0].items.map((i) => i.code)).toEqual(['1/LEAVE']);
    expect(filterGroups(groups, 'hr')[0].items.length).toBe(4);
  });
});
