import { ConfigRow } from '@dynamic-cube/shared';
import { actionsOf, isChanged, toScreenRow } from './connected-screens-dialog';
import { SignInStatus, signInState } from './connected-sign-in-dialog';

const screen = (extra: Record<string, unknown> = {}): ConfigRow => ({
  code: 'PMS.PROJECTS',
  nameAr: 'المشاريع',
  nameEn: 'Projects',
  isActive: true,
  owner: 'ENTITY',
  businessApp: 'PMS',
  availableActions: ['VIEW', 'EDIT', 'ARCHIVE'],
  ...extra,
});

describe('connected app screens', () => {
  it('splits standard and other actions and joins them back without repeats', () => {
    const row = toScreenRow(screen());
    expect(row.actions).toEqual(['VIEW', 'EDIT']);
    expect(row.other).toBe('ARCHIVE');
    expect(actionsOf({ ...row, other: 'archive, VIEW , close' })).toEqual([
      'VIEW',
      'EDIT',
      'ARCHIVE',
      'CLOSE',
    ]);
  });

  it('notices a change of names, parent or actions only', () => {
    const row = toScreenRow(screen());
    expect(isChanged(row)).toBe(false);
    expect(isChanged({ ...row, nameEn: 'Projects ' })).toBe(false);
    expect(isChanged({ ...row, parentCode: 'PMS.ROOT' })).toBe(true);
    expect(isChanged({ ...row, actions: ['VIEW'] })).toBe(true);
    expect(isChanged({ ...row, row: null })).toBe(false);
  });
});

describe('connected app sign-in', () => {
  const status = (extra: Partial<SignInStatus>): SignInStatus => ({
    appCode: 'PMS',
    clientId: 'dc-app-pms',
    managed: true,
    exists: true,
    enabled: true,
    inSync: true,
    redirectUris: [],
    registeredRedirectUris: [],
    issuer: null,
    audience: 'dynamic-cube-connect-api',
    apiPath: '/api/connect/v1',
    ...extra,
  });

  it('tells whether the client is missing, in line, out of line, disabled or maintained elsewhere', () => {
    expect(signInState(status({ managed: false }))).toBe('unmanaged');
    expect(signInState(status({ exists: false }))).toBe('notSet');
    expect(signInState(status({ enabled: false }))).toBe('disabled');
    expect(signInState(status({}))).toBe('inSync');
    expect(signInState(status({ inSync: false }))).toBe('outOfSync');
  });
});
