import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ChangeSetStatus, TranslatePipe } from '@dynamic-cube/shared';

@Component({
  selector: 'dc-status-chip',
  imports: [TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="dc-status" [attr.data-status]="status()">{{ 'status.' + status() | dcTranslate }}</span>`,
  styles: `
    .dc-status {
      display: inline-block;
      padding: 0.125rem 0.625rem;
      border-radius: 999px;
      font: var(--mat-sys-label-medium);
      background: var(--mat-sys-surface-container-high);
    }
    [data-status='SUBMITTED'] {
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }
    [data-status='PUBLISHED'],
    [data-status='APPROVED'] {
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }
    [data-status='REJECTED'] {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
  `,
})
export class StatusChip {
  readonly status = input.required<ChangeSetStatus>();
}
