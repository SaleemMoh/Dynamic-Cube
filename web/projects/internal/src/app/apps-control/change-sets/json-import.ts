import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { I18nService } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { JsonImportData, JsonImportDialog, JsonImportResult } from './json-import-dialog';
import { PackageReviewData, PackageReviewDialog } from './package-review-dialog';

/** Paste or choose JSON, review it on the server, then draft it as change sets. Resolves with the drafts created. */
@Injectable({ providedIn: 'root' })
export class JsonImport {
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(I18nService);

  async run(data: JsonImportData): Promise<number> {
    const pasted = await firstValueFrom(
      this.dialog.open<JsonImportDialog, JsonImportData, JsonImportResult>(JsonImportDialog, { data, width: '56rem', maxWidth: '95vw' }).afterClosed(),
    );
    if (!pasted) return 0;
    const created = await firstValueFrom(
      this.dialog.open<PackageReviewDialog, PackageReviewData, number>(PackageReviewDialog, { data: pasted, width: '64rem', maxWidth: '95vw' }).afterClosed(),
    );
    if (created) {
      this.snackBar.open(this.i18n.translate('changeSets.imported').replace('{count}', String(created)), undefined, { duration: 6000 });
    }
    return created ?? 0;
  }
}
