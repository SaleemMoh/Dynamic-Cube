import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OutputRefSubscription,
  Type,
  ViewContainerRef,
  inject,
  viewChild,
} from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { I18nService } from '../i18n/i18n.service';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import type { FormValue } from './dynamic-form';
import { FormSchema } from './form-schema';
import {
  Row,
  RowDialogData,
  RowFormInstance,
  rowFiles,
  rowForForm,
  rowForStorage,
} from './row-form';

/**
 * The form of one row, in a dialog. The form is the shared dynamic form, given by the caller (a rows field gets it from the
 * dynamic form it sits in), so this dialog does not import it. Files uploaded and then cancelled are removed again.
 */
@Component({
  selector: 'dc-row-dialog',
  imports: [MatDialogModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ data.title | dcLocalize }} — {{ (data.row ? 'rows.edit' : 'rows.add') | dcTranslate }}
    </h2>
    <mat-dialog-content>
      <ng-container #host />
    </mat-dialog-content>
  `,
})
export class RowDialog implements AfterViewInit {
  protected readonly data = inject<RowDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<RowDialog, Row>>(MatDialogRef);
  private readonly i18n = inject(I18nService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly host = viewChild.required('host', { read: ViewContainerRef });
  /** The row as typed so far, to find files uploaded before a cancel. */
  private typed: FormValue | null = null;

  constructor() {
    this.ref.backdropClick().subscribe(() => this.cancel());
    this.ref.keydownEvents().subscribe((event) => {
      if (event.key === 'Escape') this.cancel();
    });
  }

  ngAfterViewInit(): void {
    const schema: FormSchema = {
      code: 'row',
      sections: [{ code: 'row', title: { ar: '', en: '' }, fields: this.data.columns }],
    };
    const form = this.host().createComponent(this.data.form as Type<RowFormInstance>);
    form.setInput('schema', schema);
    form.setInput('initialValue', rowForForm(this.data.columns, this.data.row ?? {}));
    form.setInput('context', this.data.context);
    form.setInput('uploader', this.data.uploader);
    form.setInput('submitLabel', this.i18n.translate('rows.save'));
    form.setInput('submitIcon', 'check');
    form.setInput('cancellable', true);
    form.setInput('cancelLabel', this.i18n.translate('rows.cancel'));
    const subscriptions: OutputRefSubscription[] = [
      form.instance.submitted.subscribe((value) => this.ref.close(rowForStorage(value))),
      form.instance.cancelled.subscribe(() => void this.cancel()),
      form.instance.edited.subscribe((value) => (this.typed = value)),
    ];
    this.destroyRef.onDestroy(() => subscriptions.forEach((s) => s.unsubscribe()));
    form.changeDetectorRef.detectChanges();
  }

  protected async cancel(): Promise<void> {
    const kept = new Set(rowFiles(this.data.columns, this.data.row).map((f) => f.id));
    for (const file of rowFiles(this.data.columns, this.typed).filter((f) => !kept.has(f.id))) {
      await this.data.uploader?.remove(file.column, file.id).catch(() => undefined);
    }
    this.ref.close();
  }
}
