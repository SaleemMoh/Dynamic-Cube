import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { DynamicForm, FormSchema, FormValue, problemMessages } from '@dynamic-cube/shared';

export interface FormDialogData {
  title: string;
  schema: FormSchema;
  initialValue?: FormValue;
  submitLabel: string;
  /** Runs the action; the dialog stays open and shows the problems when it fails. */
  submit: (value: FormValue) => Promise<unknown>;
}

/** A {@link DynamicForm} in a dialog that runs an action on submit and closes with true when it succeeds. */
@Component({
  selector: 'dc-form-dialog',
  imports: [MatDialogModule, DynamicForm],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (error of errors(); track $index) {
            <li>{{ error }}</li>
          }
        </ul>
      }
      <dc-dynamic-form
        [schema]="data.schema"
        [initialValue]="data.initialValue ?? {}"
        [submitLabel]="data.submitLabel"
        [cancellable]="true"
        (submitted)="run($event)"
        (cancelled)="ref.close()"
      />
    </mat-dialog-content>
  `,
})
export class FormDialog {
  protected readonly data = inject<FormDialogData>(MAT_DIALOG_DATA);
  protected readonly ref = inject(MatDialogRef<FormDialog, boolean>);
  protected readonly errors = signal<string[]>([]);

  protected async run(value: FormValue): Promise<void> {
    this.errors.set([]);
    try {
      await this.data.submit(value);
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}
