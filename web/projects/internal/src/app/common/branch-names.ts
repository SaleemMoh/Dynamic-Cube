import { Injectable, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AppsControlApi, ConfigRow, LocalizedText } from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';

/**
 * Branch names for lists of requests, tasks and documents (ADR-0036 step 6). With a single branch the branch is not shown
 * ("with a single company or branch, that level is hidden").
 */
@Injectable({ providedIn: 'root' })
export class BranchNames {
  private readonly rows = toSignal(
    inject(AppsControlApi)
      .list('apps.branch')
      .pipe(catchError(() => of([] as ConfigRow[]))),
    { initialValue: [] as ConfigRow[] },
  );

  /** Whether there is more than one branch (then lists show the branch). */
  readonly several = computed(() => this.rows().length > 1);

  name(code: string | null | undefined): LocalizedText {
    const row = this.rows().find((r) => r.code === code);
    return row
      ? { ar: row.nameAr as string, en: row.nameEn as string }
      : { ar: code ?? '—', en: code ?? '—' };
  }
}
