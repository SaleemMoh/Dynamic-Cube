import { Injectable, effect, inject } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { I18nService } from './i18n.service';

/** Paginator labels in the current language ("1 – 50 of 1,749"), updated when the language changes. */
@Injectable()
export class DcPaginatorIntl extends MatPaginatorIntl {
  private readonly i18n = inject(I18nService);

  constructor() {
    super();
    effect(() => {
      const ar = this.i18n.language() === 'ar';
      this.itemsPerPageLabel = ar ? 'عدد العناصر في الصفحة:' : 'Items per page:';
      this.nextPageLabel = ar ? 'الصفحة التالية' : 'Next page';
      this.previousPageLabel = ar ? 'الصفحة السابقة' : 'Previous page';
      this.firstPageLabel = ar ? 'الصفحة الأولى' : 'First page';
      this.lastPageLabel = ar ? 'الصفحة الأخيرة' : 'Last page';
      this.changes.next();
    });
  }

  override getRangeLabel = (page: number, pageSize: number, length: number): string => {
    const of = this.i18n.language() === 'ar' ? 'من' : 'of';
    if (length === 0 || pageSize === 0) {
      return `0 ${of} ${length}`;
    }
    const start = page * pageSize;
    const end = Math.min(start + pageSize, length);
    return `${start + 1} – ${end} ${of} ${length}`;
  };
}
