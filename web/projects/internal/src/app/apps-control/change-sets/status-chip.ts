import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ChangeSetStatus, TranslatePipe } from '@dynamic-cube/shared';

const VARIANTS: Record<ChangeSetStatus, string> = {
  DRAFT: '',
  SUBMITTED: 'dc-badge--warning',
  APPROVED: 'dc-badge--info',
  REJECTED: 'dc-badge--danger',
  PUBLISHED: 'dc-badge--success',
};

/** Change-set status as a DGA badge. */
@Component({
  selector: 'dc-status-chip',
  imports: [TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span
    class="dc-badge"
    [class]="'dc-badge ' + variant()"
    [attr.data-status]="status()"
    >{{ 'status.' + status() | dcTranslate }}</span
  >`,
})
export class StatusChip {
  readonly status = input.required<ChangeSetStatus>();
  protected readonly variant = computed(() => VARIANTS[this.status()]);
}
