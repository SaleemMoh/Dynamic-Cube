import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '../i18n/translate.pipe';
import { SlaState } from './workflow.models';

/** Deadline badge: green on track, amber due soon, red overdue, with the due date. */
@Component({
  selector: 'dc-sla-chip',
  imports: [DatePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (dueAt()) {
      <span
        class="dc-badge"
        [class.dc-badge--success]="sla() === 'ON_TRACK'"
        [class.dc-badge--warning]="sla() === 'WARNING'"
        [class.dc-badge--danger]="sla() === 'BREACHED'"
        [attr.data-sla]="sla()"
      >
        {{ 'sla.' + sla() | dcTranslate }} ·
        <span dir="ltr">{{ dueAt() | date: 'yyyy-MM-dd' }}</span>
      </span>
    } @else {
      <span class="dc-muted">—</span>
    }
  `,
})
export class SlaChip {
  readonly sla = input.required<SlaState>();
  readonly dueAt = input<string | null>(null);
}
