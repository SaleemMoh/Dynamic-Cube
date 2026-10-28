import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatListModule } from '@angular/material/list';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { LocalizePipe, TranslatePipe, WorkflowApi } from '@dynamic-cube/shared';

/** Picks who gets a task among its candidates, showing how many open tasks each already has. Closes with the user name. */
@Component({
  selector: 'dc-assign-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatListModule, MatProgressBarModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'tasks.assign' | dcTranslate }}</h2>
    <mat-dialog-content>
      @if (candidates.isLoading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <mat-selection-list [multiple]="false" [(ngModel)]="chosen" data-testid="candidates">
        @for (c of candidates.value() ?? []; track c.userName) {
          <mat-list-option [value]="c.userName" [attr.data-testid]="'candidate-' + c.userName">
            <span matListItemTitle>{{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}</span>
            <span matListItemLine class="dc-muted"
              ><span dir="ltr">{{ c.userName }}</span> · {{ 'tasks.openTasks' | dcTranslate }}: {{ c.openTasks }}</span
            >
          </mat-list-option>
        } @empty {
          @if (!candidates.isLoading()) {
            <p class="dc-muted">{{ 'tasks.noCandidates' | dcTranslate }}</p>
          }
        }
      </mat-selection-list>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close>{{ 'form.cancel' | dcTranslate }}</button>
      <button matButton="filled" [disabled]="!chosen.length" (click)="ref.close(chosen[0])" data-testid="confirm-assign">
        {{ 'tasks.assign' | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
})
export class AssignDialog {
  private readonly taskId = inject<string>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<AssignDialog, string>>(MatDialogRef);
  private readonly api = inject(WorkflowApi);
  protected readonly candidates = rxResource({ stream: () => this.api.candidates(this.taskId) });
  protected chosen: string[] = [];
}
