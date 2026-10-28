import { TestBed } from '@angular/core/testing';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { I18nService } from './i18n.service';
import { DcPaginatorIntl } from './paginator-intl';

describe('DcPaginatorIntl', () => {
  it('labels the paginator in the current language', () => {
    TestBed.configureTestingModule({ providers: [{ provide: MatPaginatorIntl, useClass: DcPaginatorIntl }] });
    const intl = TestBed.inject(MatPaginatorIntl);
    const i18n = TestBed.inject(I18nService);
    i18n.setLanguage('ar');
    TestBed.tick();
    expect(intl.itemsPerPageLabel).toBe('عدد العناصر في الصفحة:');
    expect(intl.getRangeLabel(1, 50, 120)).toBe('51 – 100 من 120');
    i18n.setLanguage('en');
    TestBed.tick();
    expect(intl.itemsPerPageLabel).toBe('Items per page:');
    expect(intl.getRangeLabel(0, 50, 0)).toBe('0 of 0');
  });
});
