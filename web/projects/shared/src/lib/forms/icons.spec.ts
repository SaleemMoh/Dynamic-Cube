import { ALL_ICONS, ICON_NAME, searchIcons } from './icons';

describe('icons', () => {
  it('lists each icon once and finds them by words of their name', () => {
    expect(new Set(ALL_ICONS).size).toBe(ALL_ICONS.length);
    expect(searchIcons('event')).toContain('event_available');
    expect(searchIcons('account balance')).toEqual(['account_balance', 'account_balance_wallet']);
    expect(searchIcons('', ['a', 'b'])).toEqual(['a', 'b']);
  });

  it('accepts only plausible names', () => {
    expect(ICON_NAME.test('task_alt')).toBe(true);
    expect(ICON_NAME.test('Task Alt')).toBe(false);
  });
});
