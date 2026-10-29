import { homeProblems, normalizeHome } from './home-model';

describe('portal home setting', () => {
  it('fills what a stored value lacks and keeps counted and fixed tiles apart', () => {
    const home = normalizeHome({
      heroTitleAr: 'بوابة',
      statistics: [
        { metric: 'SERVICES', labelAr: 'خدمة', labelEn: 'Services' },
        { value: 12, metric: 'NOPE', labelAr: 'فرع', labelEn: 'Branches' },
      ],
    });
    expect(home.heroTitleEn).toBe('');
    expect(home.help).toEqual({ phone: null, email: null, contactUrl: null, accessibilityUrl: null });
    expect(home.statistics).toEqual([
      { metric: 'SERVICES', icon: undefined, labelAr: 'خدمة', labelEn: 'Services' },
      { value: 12, icon: undefined, labelAr: 'فرع', labelEn: 'Branches' },
    ]);
  });

  it('reports what keeps it from being saved', () => {
    const home = normalizeHome({
      heroTitleAr: 'بوابة',
      heroTitleEn: 'Portal',
      bannerUrl: 'javascript:alert(1)',
      statistics: [{ value: -1, icon: 'Bad Icon', labelAr: 'x', labelEn: '' }],
      help: { email: 'not-an-email', contactUrl: '/contact' },
    });
    expect(homeProblems(home)).toEqual([
      'portalHome.problem.banner',
      'portalHome.problem.tileLabel',
      'portalHome.problem.tileValue',
      'portalHome.problem.icon',
      'portalHome.problem.email',
    ]);
    expect(homeProblems(normalizeHome({ heroTitleAr: 'ب', heroTitleEn: 'P', bannerUrl: 'https://x.sa/b.png' }))).toEqual([]);
  });
});
