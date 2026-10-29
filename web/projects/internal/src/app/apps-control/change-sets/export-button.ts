import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { LocalizedText, TranslatePipe } from '@dynamic-cube/shared';
import { ExportDialogData } from './export-dialog';

/** "Export" for a screen's header: the screen's rows (or the whole environment without a type) as a package file. */
@Component({
  selector: 'dc-export-button',
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button matButton="outlined" type="button" (click)="open()" [attr.data-testid]="'export-' + (entityType() ?? 'environment')">
      <mat-icon fontSet="material-symbols-outlined">download</mat-icon>
      {{ (entityType() ? 'export.button' : 'export.environmentButton') | dcTranslate }}
    </button>
  `,
})
export class ExportButton {
  readonly entityType = input<string | null>(null);
  readonly title = input<LocalizedText | null>(null);
  readonly selected = input<string[]>([]);
  private readonly dialog = inject(MatDialog);

  protected async open(): Promise<void> {
    const { ExportDialog } = await import('./export-dialog');
    const data: ExportDialogData = { entityType: this.entityType() ?? undefined, title: this.title() ?? undefined, selected: this.selected() };
    this.dialog.open(ExportDialog, { data, width: this.entityType() ? '46rem' : '64rem', maxWidth: '96vw' });
  }
}
