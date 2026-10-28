import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { IntegrationOutcome, TranslatePipe } from '@dynamic-cube/shared';

const VARIANTS: Record<IntegrationOutcome, string> = {
  SUCCESS: 'dc-badge--success',
  NOT_FOUND: 'dc-badge--info',
  BUSINESS_ERROR: 'dc-badge--warning',
  TECHNICAL_ERROR: 'dc-badge--danger',
};

/** Integration outcome as a DGA badge. */
@Component({
  selector: 'dc-outcome-chip',
  imports: [TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span
    class="dc-badge"
    [class]="'dc-badge ' + variant()"
    [attr.data-outcome]="outcome()"
    >{{ 'outcome.' + outcome() | dcTranslate }}</span
  >`,
})
export class OutcomeChip {
  readonly outcome = input.required<IntegrationOutcome>();
  protected readonly variant = computed(() => VARIANTS[this.outcome()]);
}
