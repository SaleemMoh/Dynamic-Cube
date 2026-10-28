import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { IntegrationApi, TranslatePipe } from '@dynamic-cube/shared';
import { OutcomeChip } from '../common/outcome-chip';

/** One call: request and response as logged (masked). */
@Component({
  selector: 'dc-call-log-detail',
  imports: [DatePipe, MatButtonModule, MatDialogModule, MatProgressBarModule, OutcomeChip, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title dir="ltr">{{ entry.value()?.operationCode }}</h2>
    <mat-dialog-content>
      @if (entry.value(); as e) {
        <dl class="dc-facts">
          <dt>{{ 'integration.outcome' | dcTranslate }}</dt>
          <dd><dc-outcome-chip [outcome]="e.outcome" /> <span dir="ltr">{{ e.code }}</span></dd>
          <dt>{{ 'integration.time' | dcTranslate }}</dt>
          <dd dir="ltr">{{ e.startedAt | date: 'yyyy-MM-dd HH:mm:ss' }} · {{ e.durationMs }} ms · HTTP {{ e.httpStatus ?? '—' }}</dd>
          <dt>{{ 'integration.request' | dcTranslate }}</dt>
          <dd dir="ltr" class="dc-code">{{ e.method }} {{ e.url }}</dd>
          <dt>{{ 'integration.user' | dcTranslate }}</dt>
          <dd dir="ltr">{{ e.userId }} · {{ e.appCode }} · {{ e.correlationId }}</dd>
          @if (e.message) {
            <dt>{{ 'integration.message' | dcTranslate }}</dt>
            <dd>{{ e.message }}</dd>
          }
          @if (e.error) {
            <dt>{{ 'integration.error' | dcTranslate }}</dt>
            <dd dir="ltr">{{ e.error }}</dd>
          }
        </dl>
        @if (e.requestBody) {
          <h3>{{ 'integration.requestBody' | dcTranslate }}</h3>
          <pre dir="ltr">{{ pretty(e.requestBody) }}</pre>
        }
        <h3>{{ 'integration.rawResponse' | dcTranslate }}</h3>
        <pre dir="ltr">{{ pretty(e.responseBody) }}</pre>
      } @else {
        <mat-progress-bar mode="indeterminate" />
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close>{{ 'common.close' | dcTranslate }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-facts {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 0.5rem 1rem;
    }
    .dc-facts dd {
      margin: 0;
    }
    pre {
      max-height: 20rem;
      overflow: auto;
      padding: 0.75rem;
      border-radius: 8px;
      background: var(--dga-gray-50, #f9fafb);
      font-size: 13px;
    }
  `,
})
export class CallLogDetail {
  private readonly id = inject<string>(MAT_DIALOG_DATA);
  private readonly api = inject(IntegrationApi);
  protected readonly entry = rxResource({ stream: () => this.api.callLog(this.id) });

  protected pretty(body: string | null): string {
    if (!body) return '—';
    try {
      return JSON.stringify(JSON.parse(body), null, 2);
    } catch {
      return body;
    }
  }
}
