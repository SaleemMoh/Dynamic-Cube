import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import {
  ApiEndpointDefinition,
  AppsControlApi,
  ConfigRow,
  LocalizePipe,
  PublicApiAdmin,
  TranslatePipe,
  TryEndpointResult,
  problemMessages,
} from '@dynamic-cube/shared';
import { catchError, firstValueFrom, of } from 'rxjs';

/**
 * Calls the endpoint as it is being designed (not saved) the way a partner would (ADR-0042): with the inputs typed here
 * (their samples by default), as an API client's service account (its companies, branches and data; optionally in a named
 * company, or for a person the client acts for) or with no limits, and shows the status, the URL called and the body.
 */
@Component({
  selector: 'dc-api-endpoint-try',
  imports: [
    FormsModule,
    LocalizePipe,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h3>
      <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">play_circle</mat-icon
      >{{ 'pa.try' | dcTranslate }}
    </h3>
    <p class="et-muted">{{ 'pa.tryHint' | dcTranslate }}</p>
    @for (param of definition().parameters; track param.name) {
      <label class="et-input">
        <span
          ><b dir="ltr">{{ param.name }}</b>
          <small
            >{{ 'pa.location.' + param.location | dcTranslate
            }}{{ param.isRequired || param.location === 'PATH' ? ' · *' : '' }}</small
          ></span
        >
        <input
          dir="ltr"
          [ngModel]="values()[param.name] ?? ''"
          (ngModelChange)="set(param.name, $event)"
          [attr.data-try-input]="param.name"
        />
      </label>
    }
    @if (paged()) {
      <div class="et-pair">
        <label class="et-input"
          ><span dir="ltr">{{ 'pa.limit' | dcTranslate }}</span>
          <input
            type="number"
            min="1"
            dir="ltr"
            [ngModel]="limit()"
            (ngModelChange)="limit.set($event)"
        /></label>
        <label class="et-input"
          ><span dir="ltr">{{ 'pa.offset' | dcTranslate }}</span>
          <input
            type="number"
            min="0"
            dir="ltr"
            [ngModel]="offset()"
            (ngModelChange)="offset.set($event)"
        /></label>
      </div>
    }
    <label class="et-input">
      <span>{{ 'pa.tryAs' | dcTranslate }}</span>
      <select [ngModel]="client()" (ngModelChange)="client.set($event)" data-testid="pa-try-client">
        <option [ngValue]="null">{{ 'pa.tryAsNone' | dcTranslate }}</option>
        @for (c of clients(); track c.code) {
          <option [ngValue]="c.code">
            {{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }} ({{ c.code }})
          </option>
        }
      </select>
    </label>
    @if (client()) {
      <div class="et-pair">
        <label class="et-input"
          ><span>{{ 'pa.tryCompany' | dcTranslate }}</span>
          <input
            dir="ltr"
            [placeholder]="'pa.tryCompanyHint' | dcTranslate"
            [ngModel]="company()"
            (ngModelChange)="company.set($event)"
            data-testid="pa-try-company"
        /></label>
        @if (definition().actsForUser) {
          <label class="et-input"
            ><span>{{ 'pa.tryActingUser' | dcTranslate }}</span>
            <input
              dir="ltr"
              [placeholder]="'pa.tryActingUserHint' | dcTranslate"
              [ngModel]="actingUser()"
              (ngModelChange)="actingUser.set($event)"
              data-testid="pa-try-acting-user"
          /></label>
        }
      </div>
    }
    <button
      matButton="filled"
      type="button"
      (click)="run()"
      [disabled]="busy() || disabled()"
      data-testid="pa-try-run"
    >
      <mat-icon fontSet="material-symbols-outlined">send</mat-icon>{{ 'pa.tryRun' | dcTranslate }}
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
      <div class="et-result" data-testid="pa-try-result">
        <div class="et-head">
          <span
            class="dc-pill"
            [class.dc-pill--green]="r.status < 300"
            [class.dc-pill--gold]="r.status >= 400 && r.status < 500"
            >HTTP {{ r.status }}</span
          >
          <span dir="ltr">{{ r.durationMs }} ms</span>
          @if (r.rows !== null) {
            <span>{{ r.rows }} {{ 'pa.rows' | dcTranslate }}</span>
          }
        </div>
        <p class="et-url" dir="ltr"><b>GET</b> {{ r.url }}</p>
        <pre dir="ltr">{{ pretty(r.body) }}</pre>
      </div>
    }
  `,
  styles: `
    :host {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: 10px;
      min-width: 0;
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
    :host > button {
      justify-self: start;
    }
    .et-muted {
      margin: 0;
      color: var(--dga-text-3, #6c737f);
      font-size: 13px;
    }
    .et-pair {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
      gap: 8px;
    }
    .et-input {
      display: grid;
      gap: 4px;
      font-size: 13px;
    }
    .et-input small {
      margin-inline-start: 6px;
      color: var(--dga-text-3, #6c737f);
    }
    .et-input input,
    .et-input select {
      width: 100%;
      min-width: 0;
      box-sizing: border-box;
      padding: 6px 8px;
      border: 1px solid var(--dga-gray-300, #d2d6db);
      border-radius: 6px;
      font: inherit;
    }
    .et-result {
      display: grid;
      gap: 8px;
      padding-top: 8px;
      border-top: 1px solid var(--dga-gray-200, #e5e7eb);
    }
    .et-head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 10px;
      font-size: 13px;
    }
    .et-url,
    pre {
      margin: 0;
      font-family: ui-monospace, monospace;
      font-size: 12px;
      overflow-wrap: anywhere;
    }
    pre {
      text-align: left;
      max-height: 22rem;
      overflow: auto;
      padding: 10px 12px;
      border-radius: 8px;
      background: #0f172a;
      color: #e2e8f0;
      white-space: pre-wrap;
    }
  `,
})
export class ApiEndpointTry {
  private readonly api = inject(PublicApiAdmin);
  private readonly config = inject(AppsControlApi);
  /** The endpoint as designed (not saved). */
  readonly definition = input.required<ApiEndpointDefinition>();
  readonly disabled = input(false);

  protected readonly clients = toSignal(
    this.config.list('integ.api-client').pipe(catchError(() => of([] as ConfigRow[]))),
    { initialValue: [] as ConfigRow[] },
  );
  private readonly typed = signal<Record<string, string>>({});
  protected readonly values = computed<Record<string, string | null>>(() =>
    Object.fromEntries(
      this.definition().parameters.map((p) => [p.name, this.typed()[p.name] ?? p.sample ?? null]),
    ),
  );
  protected readonly paged = computed(
    () => this.definition().kind === 'QUERY' && !this.definition().singleRow,
  );
  protected readonly client = signal<string | null>(null);
  /** The company (X-Company) and the person acted for (X-Acting-User) the try sends with a client. */
  protected readonly company = signal('');
  protected readonly actingUser = signal('');
  protected readonly limit = signal<number | null>(null);
  protected readonly offset = signal<number | null>(null);
  protected readonly busy = signal(false);
  protected readonly result = signal<TryEndpointResult | null>(null);
  protected readonly errors = signal<string[]>([]);

  protected set(name: string, value: string): void {
    this.typed.update((v) => ({ ...v, [name]: value }));
  }

  /** The body laid out as JSON. */
  protected pretty(body: unknown): string {
    return body === null || body === undefined ? '—' : JSON.stringify(body, null, 2);
  }

  protected async run(): Promise<void> {
    this.errors.set([]);
    this.busy.set(true);
    try {
      const inputs = Object.fromEntries(
        Object.entries(this.values()).filter(([, v]) => v !== null && v !== ''),
      );
      const paged = this.paged();
      this.result.set(
        await firstValueFrom(
          this.api.tryEndpoint(
            this.definition(),
            inputs,
            this.client(),
            paged ? this.limit() || null : null,
            paged ? this.offset() || null : null,
            this.client() ? this.company().trim() || null : null,
            this.client() && this.definition().actsForUser
              ? this.actingUser().trim() || null
              : null,
          ),
        ),
      );
    } catch (error) {
      this.result.set(null);
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }
}
