import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe } from '@dynamic-cube/shared';
import { DraftsService } from './drafts.service';

/**
 * The second way to save an edit in an area that needs approval: send it for approval at once (a change set of its own)
 * instead of adding it to the draft. Shown beside an editor's save button; nothing where saving publishes at once.
 */
@Component({
  selector: 'dc-submit-button',
  imports: [MatButtonModule, MatIconModule, MatTooltipModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (drafts.needsApproval(entityType())) {
      <button
        matButton="outlined"
        type="button"
        (click)="submitted.emit()"
        [disabled]="disabled()"
        [matTooltip]="'config.submitHint' | dcTranslate"
        [attr.data-testid]="testId()"
      >
        <mat-icon fontSet="material-symbols-outlined">send</mat-icon>
        {{ 'config.submitForApproval' | dcTranslate }}
      </button>
    }
  `,
  styles: `
    :host {
      display: contents;
    }
  `,
})
export class SubmitButton {
  protected readonly drafts = inject(DraftsService);
  readonly entityType = input.required<string>();
  readonly disabled = input(false);
  readonly testId = input('submit-for-approval');
  readonly submitted = output<void>();
}
