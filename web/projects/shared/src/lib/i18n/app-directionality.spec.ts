import { Directionality } from '@angular/cdk/bidi';
import { TestBed } from '@angular/core/testing';
import { provideDynamicCubeUi } from '../theme/theme';
import { I18nService } from './i18n.service';

describe('AppDirectionality', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideDynamicCubeUi()] });
  });

  it('follows the current language', () => {
    const directionality = TestBed.inject(Directionality);
    const i18n = TestBed.inject(I18nService);
    const changes: string[] = [];
    directionality.change.subscribe((d) => changes.push(d));
    TestBed.tick();
    expect(directionality.value).toBe('rtl');

    i18n.setLanguage('en');
    TestBed.tick();

    expect(directionality.value).toBe('ltr');
    expect(changes).toContain('ltr');
  });
});
