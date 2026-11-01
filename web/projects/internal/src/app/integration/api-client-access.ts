import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { AppsControlApi, I18nService, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { ConfigEditorData } from '../apps-control/config/config-editor';
import { CONFIG_SECTIONS } from '../apps-control/config/config-types';
import { PUBLIC_API_TRANSLATIONS } from './public-api-translations';

/**
 * An API client's roles and privileges (ADR-0042): they are its service account's, a user of kind SERVICE. This opens
 * the same user editor as the Users screen on that account (details and access, saved as one change), so the account is
 * kept in one place whichever screen it is opened from.
 */
@Component({
  selector: 'dc-api-client-access',
  imports: [MatButtonModule, MatDialogModule, MatProgressBarModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'pa.access.title' | dcTranslate }}</h2>
    <mat-dialog-content>
      @if (errors().length) {
        <p>{{ 'pa.access.missing' | dcTranslate }}</p>
        <ul class="dc-errors" role="alert" data-testid="pa-access-errors">
          @for (e of errors(); track $index) {
            <li dir="auto">{{ e }}</li>
          }
        </ul>
      } @else {
        <mat-progress-bar mode="indeterminate" />
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      <button matButton type="button" mat-dialog-close>{{ 'pa.close' | dcTranslate }}</button>
    </mat-dialog-actions>
  `,
})
export class ApiClientAccessDialog {
  private readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ApiClientAccessDialog, boolean>);
  private readonly dialog = inject(MatDialog);
  private readonly api = inject(AppsControlApi);
  protected readonly errors = signal<string[]>([]);

  constructor() {
    inject(I18nService).register(PUBLIC_API_TRANSLATIONS);
    void this.open();
  }

  /** Loads the service account and hands over to the user editor. */
  private async open(): Promise<void> {
    const account = String(this.data.row?.['account'] ?? '');
    const type = CONFIG_SECTIONS.flatMap((s) => s.types).find((t) => t.entityType === 'apps.user');
    try {
      const row = await firstValueFrom(this.api.get('apps.user', account));
      if (!type?.editor) return;
      const editor = await type.editor();
      this.ref.close(false);
      this.dialog.open<unknown, ConfigEditorData, boolean>(editor, {
        data: { type, row },
        autoFocus: 'first-tabbable',
        width: '96rem',
        maxWidth: '96vw',
      });
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}
