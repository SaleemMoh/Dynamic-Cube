import { TestBed } from '@angular/core/testing';
import { DOCUMENT } from '@angular/common';
import { I18nService } from './i18n.service';
import { localize } from './language';

describe('I18nService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
  });

  it('defaults to Arabic and right-to-left', () => {
    const i18n = TestBed.inject(I18nService);
    TestBed.tick();

    expect(i18n.language()).toBe('ar');
    expect(TestBed.inject(DOCUMENT).documentElement.dir).toBe('rtl');
    expect(TestBed.inject(DOCUMENT).documentElement.lang).toBe('ar');
  });

  it('switches html direction and remembers the choice', () => {
    const i18n = TestBed.inject(I18nService);
    i18n.toggle();
    TestBed.tick();

    expect(TestBed.inject(DOCUMENT).documentElement.dir).toBe('ltr');
    expect(localStorage.getItem('dc.language')).toBe('en');
  });

  it('translates with app dictionaries overriding shared ones and shows missing keys', () => {
    const i18n = TestBed.inject(I18nService);
    i18n.register({ ar: { 'form.submit': 'تقديم الطلب' }, en: { 'form.submit': 'Submit request' } });

    expect(i18n.translate('form.submit')).toBe('تقديم الطلب');
    expect(i18n.translate('missing.key')).toBe('missing.key');
  });

  it('localizes bilingual values with fallback', () => {
    expect(localize({ ar: 'التشييد والبناء', en: '' }, 'en')).toBe('التشييد والبناء');
    expect(localize({ ar: 'التشييد والبناء', en: 'Construction' }, 'en')).toBe('Construction');
  });
});
