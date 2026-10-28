import { JsonPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute } from '@angular/router';
import { AppsControlApi, IntegrationApi, LocalizePipe, TestOperationResult, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { OutcomeChip } from '../common/outcome-chip';

interface OperationParameter {
  name: string;
  location: string;
  isRequired: boolean;
  sampleValue?: string | null;
}

interface OperationRow {
  code: string;
  nameAr: string;
  nameEn: string;
  method: string;
  pathTemplate: string;
  parameters: OperationParameter[];
}

/** Runs a published operation with sample inputs and shows the outcome, mapped outputs and the provider's raw response. */
@Component({
  selector: 'dc-test-console',
  imports: [
    FormsModule,
    JsonPipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    LocalizePipe,
    OutcomeChip,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'integration.testConsole' | dcTranslate }}</h1>
    </header>
    <p class="dc-muted">{{ 'integration.testHint' | dcTranslate }}</p>

    <section class="dc-card dc-console">
      <label class="dc-field-label" for="dc-operation">{{ 'integration.operation' | dcTranslate }}</label>
      <mat-form-field subscriptSizing="dynamic">
        <mat-select id="dc-operation" [ngModel]="selectedCode()" (ngModelChange)="choose($event)" data-testid="operation-select">
          @for (op of operations(); track op.code) {
            <mat-option [value]="op.code">{{ { ar: op.nameAr, en: op.nameEn } | dcLocalize }} ({{ op.code }})</mat-option>
          }
        </mat-select>
      </mat-form-field>

      @if (selected(); as op) {
        <p class="dc-code" dir="ltr">{{ op.method }} {{ op.pathTemplate }}</p>
        @for (p of op.parameters; track p.name) {
          <label class="dc-field-label" [for]="'dc-in-' + p.name">{{ p.name }} <span class="dc-muted">({{ p.location }})</span>{{ p.isRequired ? ' *' : '' }}</label>
          <mat-form-field subscriptSizing="dynamic">
            <input matInput dir="ltr" [id]="'dc-in-' + p.name" [ngModel]="inputs()[p.name]" (ngModelChange)="setInput(p.name, $event)" [attr.data-testid]="'input-' + p.name" />
          </mat-form-field>
        }
        <div class="dc-console__actions">
          <button matButton="filled" (click)="run()" [disabled]="running()" data-testid="run-operation">
            <mat-icon fontSet="material-symbols-outlined">play_arrow</mat-icon>
            {{ 'integration.run' | dcTranslate }}
          </button>
        </div>
      }
      @if (running()) {
        <mat-progress-bar mode="indeterminate" />
      }
    </section>

    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (error of errors(); track $index) {
          <li>{{ error }}</li>
        }
      </ul>
    }

    @if (result(); as r) {
      <section class="dc-card dc-result" data-testid="test-result">
        <div class="dc-result__head">
          <dc-outcome-chip [outcome]="r.outcome" />
          <span class="dc-code" dir="ltr">{{ r.code }}</span>
          <span class="dc-muted">HTTP {{ r.trace.httpStatus ?? '—' }} · {{ r.trace.durationMs }} ms · {{ 'integration.attempts' | dcTranslate }} {{ r.trace.attempts }}</span>
        </div>
        @if (r.messageAr || r.messageEn) {
          <p>{{ { ar: r.messageAr ?? '', en: r.messageEn ?? '' } | dcLocalize }}</p>
        }
        <p class="dc-code" dir="ltr">{{ r.trace.method }} {{ r.trace.url }}</p>
        @if (r.trace.error) {
          <p class="dc-muted" dir="ltr">{{ r.trace.error }}</p>
        }
        <h2>{{ 'integration.outputs' | dcTranslate }}</h2>
        <pre dir="ltr" data-testid="test-outputs">{{ r.outputs | json }}</pre>
        <h2>{{ 'integration.rawResponse' | dcTranslate }}</h2>
        <pre dir="ltr">{{ pretty(r.trace.responseBody) }}</pre>
      </section>
    }
  `,
  styles: `
    .dc-console {
      display: grid;
      gap: 0.25rem;
      max-width: 40rem;
    }
    .dc-console__actions {
      margin-top: 0.75rem;
    }
    .dc-result {
      margin-top: 1rem;
    }
    .dc-result__head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.75rem;
    }
    pre {
      max-height: 24rem;
      overflow: auto;
      padding: 0.75rem;
      border-radius: 8px;
      background: var(--dga-gray-50, #f9fafb);
      font-size: 13px;
    }
  `,
})
export class TestConsole {
  private readonly api = inject(AppsControlApi);
  private readonly integration = inject(IntegrationApi);
  private readonly list = rxResource({ stream: () => this.api.list('integ.operation') });

  protected readonly operations = computed(() => (this.list.hasValue() ? (this.list.value() as unknown as OperationRow[]) : []));
  protected readonly selectedCode = signal<string | null>(inject(ActivatedRoute).snapshot.queryParamMap.get('operation'));
  protected readonly selected = computed(() => this.operations().find((o) => o.code === this.selectedCode()) ?? null);
  protected readonly inputs = signal<Record<string, string | null>>({});
  protected readonly running = signal(false);
  protected readonly result = signal<TestOperationResult | null>(null);
  protected readonly errors = signal<string[]>([]);

  protected choose(code: string): void {
    this.selectedCode.set(code);
    const op = this.operations().find((o) => o.code === code);
    this.inputs.set(Object.fromEntries((op?.parameters ?? []).map((p) => [p.name, p.sampleValue ?? null])));
    this.result.set(null);
  }

  protected setInput(name: string, value: string): void {
    this.inputs.update((current) => ({ ...current, [name]: value }));
  }

  protected async run(): Promise<void> {
    const op = this.selected();
    if (!op) return;
    this.running.set(true);
    this.errors.set([]);
    this.result.set(null);
    try {
      const inputs = Object.fromEntries(Object.entries(this.inputs()).filter(([, v]) => v !== null && v !== ''));
      this.result.set(await firstValueFrom(this.integration.test(op.code, inputs)));
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.running.set(false);
    }
  }

  protected pretty(body: string | null): string {
    if (!body) return '';
    try {
      return JSON.stringify(JSON.parse(body), null, 2);
    } catch {
      return body;
    }
  }
}
