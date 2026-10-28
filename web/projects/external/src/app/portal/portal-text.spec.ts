import { TestBed } from '@angular/core/testing';
import { I18nService } from '@dynamic-cube/shared';
import { APP_TRANSLATIONS } from '../translations';
import { CostPipe, DurationPipe } from './portal-text';

describe('Portal wording', () => {
  let cost: CostPipe;
  let duration: DurationPipe;
  let i18n: I18nService;

  beforeEach(() => {
    localStorage.clear();
    i18n = TestBed.inject(I18nService);
    i18n.register(APP_TRANSLATIONS);
    cost = TestBed.runInInjectionContext(() => new CostPipe());
    duration = TestBed.runInInjectionContext(() => new DurationPipe());
  });

  it('says what a service costs: free, fixed, a range or depending on the request', () => {
    i18n.language.set('en');
    expect(cost.transform({ min: 0, max: 0, dependsOnRequest: false })).toBe('Free');
    expect(cost.transform({ min: 500, max: 500, dependsOnRequest: false })).toBe('500 SAR');
    expect(cost.transform({ min: 200, max: 1500, dependsOnRequest: true })).toBe('200 – 1,500 SAR');
    expect(cost.transform({ min: 0, max: null, dependsOnRequest: true })).toBe('Depends on the request');
    expect(cost.transform({ min: 100, max: null, dependsOnRequest: true })).toBe('From 100 SAR');
  });

  it('gives the duration in working days, in Arabic too', () => {
    i18n.language.set('ar');
    expect(duration.transform({ min: 5, max: 5 })).toBe('5 يوم عمل');
    expect(duration.transform({ min: 10, max: 60 })).toBe('10 – 60 يوم عمل');
    expect(duration.transform(null)).toBe('غير محددة');
    expect(cost.transform({ min: 0, max: 0, dependsOnRequest: false })).toBe('مجانية');
  });
});
