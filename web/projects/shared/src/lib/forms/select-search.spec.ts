import { matchesSearch } from './select-search';

describe('matchesSearch', () => {
  it('matches every word typed in any of the texts, ignoring case', () => {
    expect(matchesSearch('', 'REGION')).toBe(true);
    expect(matchesSearch('svc req', 'svc.request', null)).toBe(true);
    expect(matchesSearch('svc pay', 'svc.request')).toBe(false);
    expect(matchesSearch('مناطق', 'REGION', 'المناطق الإدارية', 'Administrative regions')).toBe(
      true,
    );
    expect(matchesSearch('  Administrative  ', 'REGION', 'المناطق', 'administrative regions')).toBe(
      true,
    );
  });
});
