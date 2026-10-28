import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslatePipe } from '@dynamic-cube/shared';

export interface ConfirmData {
  title: string;
  message?: string;
  confirmLabel: string;
  /** Ask for a comment; with `commentRequired` the dialog cannot be confirmed without one. */
  comment?: boolean;
  commentRequired?: boolean;
  danger?: boolean;
}

/** Confirmation, optionally with a comment. Closes with the comment (or '') when confirmed, undefined otherwise. */
@Component({
  selector: 'dc-confirm-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      @if (data.message) {
        <p>{{ data.message }}</p>
      }
      @if (data.comment) {
        <mat-form-field class="dc-confirm__comment">
          <mat-label>{{ 'changeSets.comment' | dcTranslate }}</mat-label>
          <textarea matInput rows="3" [(ngModel)]="text" name="comment" [required]="data.commentRequired ?? false" data-testid="confirm-comment"></textarea>
        </mat-form-field>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close>{{ 'form.cancel' | dcTranslate }}</button>
      <button
        matButton="filled"
        [class.dc-danger]="data.danger"
        [disabled]="data.commentRequired && !text.trim()"
        (click)="ref.close(text.trim())"
        data-testid="confirm"
      >
        {{ data.confirmLabel }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-confirm__comment {
      width: 100%;
      min-width: 22rem;
    }
    .dc-danger {
      --mat-button-filled-container-color: var(--mat-sys-error);
      --mat-button-filled-label-text-color: var(--mat-sys-on-error);
    }
  `,
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmData>(MAT_DIALOG_DATA);
  protected readonly ref = inject(MatDialogRef<ConfirmDialog, string>);
  protected text = '';
}
