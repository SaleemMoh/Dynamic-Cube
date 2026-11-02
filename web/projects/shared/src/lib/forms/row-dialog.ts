import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { DynamicForm, FormValue } from './dynamic-form';
import { FormSchema } from './form-schema';
import { Row, RowDialogData, rowFiles, rowForForm, rowForStorage } from './rows-input';

/** The form of one row, in a dialog. Files uploaded and then cancelled are removed again. */
@Component({
  selector: 'dc-row-dialog',
  imports: [MatDialogModule, MatButtonModule, DynamicForm, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ data.title | dcLocalize }} — {{ (data.row ? 'rows.edit' : 'rows.add') | dcTranslate }}
    </h2>
    <mat-dialog-content>
      <dc-dynamic-form
        [schema]="schema"
        [initialValue]="initial"
        [context]="data.context"
        [uploader]="data.uploader"
        [submitLabel]="'rows.save' | dcTranslate"
        submitIcon="check"
        [cancellable]="true"
        [cancelLabel]="'rows.cancel' | dcTranslate"
        (submitted)="save($event)"
        (cancelled)="cancel()"
        (edited)="typed = $event"
      />
    </mat-dialog-content>
  `,
})
export class RowDialog {
  protected readonly data = inject<RowDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<RowDialog, Row>>(MatDialogRef);
  protected readonly schema: FormSchema = {
    code: 'row',
    sections: [{ code: 'row', title: { ar: '', en: '' }, fields: this.data.columns }],
  };
  protected readonly initial = rowForForm(this.data.columns, this.data.row ?? {});
  /** The row as typed so far, to find files uploaded before a cancel. */
  protected typed: FormValue | null = null;

  constructor() {
    this.ref.backdropClick().subscribe(() => this.cancel());
    this.ref.keydownEvents().subscribe((event) => {
      if (event.key === 'Escape') this.cancel();
    });
  }

  protected save(value: FormValue): void {
    this.ref.close(rowForStorage(value));
  }

  protected async cancel(): Promise<void> {
    const kept = new Set(rowFiles(this.data.columns, this.data.row).map((f) => f.id));
    for (const file of rowFiles(this.data.columns, this.typed).filter((f) => !kept.has(f.id))) {
      await this.data.uploader?.remove(file.column, file.id).catch(() => undefined);
    }
    this.ref.close();
  }
}
