import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import {
  IntegrationApi,
  LocalizePipe,
  OperationDefinition,
  TestOperationResult,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { OutcomeChip } from '../common/outcome-chip';

/**
 * Runs the operation as it is being designed (not saved) on its published connection, with the inputs typed here
 * (their sample values by default), and shows the outcome, the request sent and the provider's raw response.
 */
@Component({
  selector: 'dc-operation-try',
  imports: [
    FormsModule,
    LocalizePipe,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    OutcomeChip,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h3>
      <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">play_circle</mat-icon
      >{{ 'od.try' | dcTranslate }}
    </h3>
    <p class="ot-muted">{{ 'od.tryHint' | dcTranslate }}</p>
    @for (param of definition().parameters; track param.name) {
      <label class="ot-input">
        <span
          ><b dir="ltr">{{ param.name }}</b>
          <small
            >{{ 'od.location.' + param.location | dcTranslate }}
            @if (param.isRequired) {
              · *
            }
          </small></span
        >
        <input
          dir="ltr"
          [ngModel]="values()[param.name] ?? ''"
          (ngModelChange)="set(param.name, $event)"
          [attr.data-try-input]="param.name"
        />
      </label>
    }
    <button
      matButton="filled"
      type="button"
      (click)="run()"
      [disabled]="busy() || disabled()"
      data-testid="od-try-run"
    >
      <mat-icon fontSet="material-symbols-outlined">send</mat-icon>{{ 'od.tryRun' | dcTranslate }}
    </button>
    @if (busy()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (e of errors(); track $index) {
          <li>{{ e }}</li>
        }
      </ul>
    }
    @if (result(); as r) {
      <div class="ot-result" data-testid="od-result">
        <div class="ot-head">
          <dc-outcome-chip [outcome]="r.outcome" />
          @if (r.trace.httpStatus) {
            <span dir="ltr">HTTP {{ r.trace.httpStatus }}</span>
          }
          <span dir="ltr">{{ r.trace.durationMs }} ms</span>
          @if (r.code) {
            <code dir="ltr">{{ r.code }}</code>
          }
        </div>
        @if (r.messageAr || r.messageEn) {
          <p>{{ { ar: r.messageAr ?? '', en: r.messageEn ?? '' } | dcLocalize }}</p>
        }
        <p class="ot-url" dir="ltr">
          <b>{{ r.trace.method }}</b> {{ r.trace.url }}
        </p>
        @if (r.trace.requestBody) {
          <pre dir="ltr">{{ r.trace.requestBody }}</pre>
        }
        <details open>
          <summary>{{ 'od.rawResponse' | dcTranslate }}</summary>
          <pre class="ot-raw" dir="ltr">{{
            raw(r.trace.responseBody) ?? r.trace.error ?? '—'
          }}</pre>
        </details>
        <button matButton type="button" (click)="pickOutputs.emit()">
          <mat-icon fontSet="material-symbols-outlined">account_tree</mat-icon
          >{{ 'od.pickOutputs' | dcTranslate }}
        </button>
      </div>
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: 10px;
      padding: 14px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 12px;
      background: var(--dga-gray-50, #f9fafb);
    }
    h3 {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0;
      font-size: 15px;
    }
    h3 mat-icon {
      color: var(--dynamic-cube-gold, #ca9f45);
    }
    :host > button,
    .ot-result > button {
      justify-self: start;
    }
    .ot-muted {
      margin: 0;
      color: var(--dga-text-3, #6c737f);
      font-size: 13px;
    }
    .ot-input {
      display: grid;
      gap: 4px;
      font-size: 13px;
    }
    .ot-input small {
      color: var(--dga-text-3, #6c737f);
    }
    .ot-input input {
      padding: 6px 8px;
      border: 1px solid var(--dga-gray-300, #d2d6db);
      border-radius: 6px;
      font: inherit;
      box-shadow: none;
    }
    .ot-result {
      display: grid;
      gap: 8px;
      padding-top: 8px;
      border-top: 1px solid var(--dga-gray-200, #e5e7eb);
    }
    .ot-result p {
      margin: 0;
    }
    .ot-head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 10px;
      font-size: 13px;
    }
    .ot-url,
    pre,
    code {
      font-family: ui-monospace, monospace;
      font-size: 12px;
      overflow-wrap: anywhere;
    }
    pre {
      margin: 0;
      padding: 10px 12px;
      border-radius: 8px;
      background: #0f172a;
      color: #e2e8f0;
      white-space: pre-wrap;
    }
    .ot-raw {
      max-height: 18rem;
      overflow: auto;
    }
    summary {
      cursor: pointer;
      font-size: 13px;
    }
  `,
})
export class OperationTry {
  private readonly api = inject(IntegrationApi);
  /** The operation as designed (not saved). */
  readonly definition = input.required<OperationDefinition>();
  readonly disabled = input(false);
  /** The outcome of the last try (null when it failed to run). */
  readonly tried = output<TestOperationResult | null>();
  /** Go and pick outputs from the response. */
  readonly pickOutputs = output<void>();

  private readonly typed = signal<Record<string, string>>({});
  protected readonly values = computed<Record<string, string | null>>(() =>
    Object.fromEntries(
      this.definition().parameters.map((p) => [
        p.name,
        this.typed()[p.name] ?? p.sampleValue ?? null,
      ]),
    ),
  );
  protected readonly busy = signal(false);
  protected readonly result = signal<TestOperationResult | null>(null);
  protected readonly errors = signal<string[]>([]);

  /** The response as received, laid out when it is JSON (escaped characters shown as they are). */
  protected raw(body: string | null): string | null {
    if (!body) return body;
    try {
      return JSON.stringify(JSON.parse(body), null, 2);
    } catch {
      return body;
    }
  }

  protected set(name: string, value: string): void {
    this.typed.update((v) => ({ ...v, [name]: value }));
  }

  protected async run(): Promise<void> {
    this.errors.set([]);
    this.busy.set(true);
    try {
      const inputs = Object.fromEntries(
        Object.entries(this.values()).filter(([, v]) => v !== null && v !== ''),
      );
      const result = await firstValueFrom(this.api.tryOperation(this.definition(), inputs));
      this.result.set(result);
      this.tried.emit(result);
    } catch (error) {
      this.result.set(null);
      this.errors.set(problemMessages(error));
      this.tried.emit(null);
    } finally {
      this.busy.set(false);
    }
  }
}
