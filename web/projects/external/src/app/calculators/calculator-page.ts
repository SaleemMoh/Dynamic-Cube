import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import {
  ActingPartyService,
  AuthService,
  DynamicForm,
  FormSchema,
  FormValue,
  I18nService,
  LocalizePipe,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { catchError, firstValueFrom, of } from 'rxjs';
import { PageHero } from '../portal/page-hero';
import { CalculatorResult, CalculatorView, CalculatorsApi } from './calculators-api';

/** A result line as shown: its label, the value as text and whether it is the main answer. */
interface ShownResult {
  label: string;
  value: string;
  headline: boolean;
  path: string;
}

/**
 * One public calculator: its form (starting from the firm's latest data for a signed-in applicant), worked out by the
 * rule set without recording anything, and the results with the note that it is an estimate.
 */
@Component({
  selector: 'dc-calculator-page',
  imports: [
    DecimalPipe,
    MatIconModule,
    RouterLink,
    DynamicForm,
    LocalizePipe,
    TranslatePipe,
    PageHero,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (calculator.value(); as c) {
      <dc-page-hero
        [title]="{ ar: c.titleAr, en: c.titleEn } | dcLocalize"
        [text]="{ ar: c.introAr ?? '', en: c.introEn ?? '' } | dcLocalize"
      />
      <div class="dc-portal-block dc-page-body dc-calc">
        <a routerLink="/calculators" class="dc-calc__back">
          <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">arrow_forward</mat-icon>
          {{ 'calculators.all' | dcTranslate }}
        </a>
        <div class="dc-calc__grid">
          <section class="dc-card">
            @if (prefilled()) {
              <p class="dc-calc__prefilled" data-testid="calculator-prefilled">
                <mat-icon fontSet="material-symbols-outlined" aria-hidden="true"
                  >auto_awesome</mat-icon
                >
                {{ 'calculators.prefilled' | dcTranslate }}
              </p>
            }
            @if (ready()) {
              <dc-dynamic-form
                [schema]="schema()"
                [initialValue]="start()"
                [submitLabel]="'calculators.calculate' | dcTranslate"
                submitIcon="calculate"
                (submitted)="calculate($event)"
              />
            }
          </section>
          <section
            class="dc-card dc-calc__result"
            aria-live="polite"
            data-testid="calculator-result"
          >
            <h2>{{ 'calculators.result' | dcTranslate }}</h2>
            @if (errors().length) {
              <ul class="dc-errors" role="alert">
                @for (e of errors(); track $index) {
                  <li>{{ e }}</li>
                }
              </ul>
            }
            @if (shown(); as lines) {
              @for (r of lines; track r.path) {
                @if (r.headline) {
                  <div class="dc-calc__headline" [attr.data-testid]="'result-' + r.path">
                    <span>{{ r.label }}</span>
                    <strong>{{ r.value }}</strong>
                  </div>
                } @else {
                  <div class="dc-calc__line" [attr.data-testid]="'result-' + r.path">
                    <span>{{ r.label }}</span>
                    <strong>{{ r.value }}</strong>
                  </div>
                }
              }
              @if (c.noteAr || c.noteEn) {
                <p class="dc-calc__note">
                  {{ { ar: c.noteAr ?? '', en: c.noteEn ?? '' } | dcLocalize }}
                </p>
              }
            } @else {
              <p class="dc-muted">{{ 'calculators.fillIn' | dcTranslate }}</p>
            }
          </section>
        </div>
      </div>
    } @else if (calculator.error()) {
      <div class="dc-portal-block dc-page-body">
        <p class="dc-errors" role="alert">{{ 'calculators.notFound' | dcTranslate }}</p>
      </div>
    }
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }
    .dc-calc__back {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      margin-bottom: 12px;
      color: var(--dga-gray-600, #4d5761);
      text-decoration: none;
    }
    :host-context([dir='ltr']) .dc-calc__back mat-icon {
      transform: scaleX(-1);
    }
    .dc-calc__grid {
      display: grid;
      grid-template-columns: minmax(0, 3fr) minmax(18rem, 2fr);
      gap: 16px;
      align-items: start;
    }
    @media (max-width: 960px) {
      .dc-calc__grid {
        grid-template-columns: 1fr;
      }
    }
    .dc-calc__prefilled {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0 0 12px;
      padding: 8px 12px;
      border-radius: 8px;
      background: var(--dga-primary-50, #ecfdf3);
      color: var(--dga-primary-700, #14573a);
    }
    .dc-calc__result h2 {
      margin: 0 0 12px;
      font-size: 18px;
    }
    .dc-calc__headline {
      display: grid;
      gap: 4px;
      margin-bottom: 12px;
      padding: 16px;
      border-radius: 12px;
      background: var(--dga-primary-50, #ecfdf3);
    }
    .dc-calc__headline strong {
      font-size: 32px;
      color: var(--dga-primary-700, #14573a);
    }
    .dc-calc__line {
      display: flex;
      justify-content: space-between;
      gap: 12px;
      padding: 8px 0;
      border-bottom: 1px solid var(--dga-gray-200, #e5e7eb);
    }
    .dc-calc__note {
      margin: 12px 0 0;
      font-size: 13px;
      color: var(--dga-gray-600, #4d5761);
    }
  `,
})
export class CalculatorPage {
  readonly code = input.required<string>();
  private readonly api = inject(CalculatorsApi);
  private readonly auth = inject(AuthService);
  private readonly acting = inject(ActingPartyService);
  private readonly i18n = inject(I18nService);
  private readonly decimal = new DecimalPipe('en-US');

  protected readonly calculator = rxResource({
    params: () => this.code(),
    stream: ({ params }) => this.api.get(params),
  });
  /** Starting values for a signed-in applicant (none for visitors, or when the data cannot be read). */
  protected readonly prefill = rxResource({
    params: () => ({
      code: this.code(),
      signedIn: this.auth.isAuthenticated(),
      party: this.acting.current()?.partyId ?? null,
    }),
    stream: ({ params }) =>
      params.signedIn ? this.api.prefill(params.code).pipe(catchError(() => of({}))) : of({}),
  });
  protected readonly ready = computed(() => !!this.calculator.value() && !this.prefill.isLoading());
  protected readonly start = computed<FormValue>(() => (this.prefill.value() ?? {}) as FormValue);
  protected readonly prefilled = computed(() => Object.keys(this.prefill.value() ?? {}).length > 0);
  protected readonly output = signal<Record<string, unknown> | null>(null);
  protected readonly errors = signal<string[]>([]);

  protected readonly schema = computed<FormSchema>(() => {
    const c = this.calculator.value();
    return {
      code: c?.code ?? 'calculator',
      sections: [
        {
          code: 'inputs',
          title: { ar: '', en: '' },
          columns: 2,
          fields: (c?.inputs ?? []).map((i) => ({
            code: i.code,
            type:
              i.type === 'NUMBER'
                ? 'number'
                : i.type === 'SELECT'
                  ? 'select'
                  : i.type === 'CHECKBOX'
                    ? 'checkbox'
                    : i.type === 'DATE'
                      ? 'date'
                      : 'text',
            label: { ar: i.labelAr, en: i.labelEn },
            hint: i.hintAr || i.hintEn ? { ar: i.hintAr ?? '', en: i.hintEn ?? '' } : undefined,
            required: i.required,
            options: i.options.map((o) => ({
              value: o.value,
              label: { ar: o.labelAr, en: o.labelEn },
            })),
            min: i.min ?? undefined,
            max: i.max ?? undefined,
          })),
        },
      ],
    };
  });

  protected readonly shown = computed<ShownResult[] | null>(() => {
    const output = this.output();
    const c = this.calculator.value();
    if (!output || !c) return null;
    return c.results.map((r) => ({
      path: r.path,
      headline: r.headline,
      label: this.label(r),
      value: this.format(r, read(output, r.path)),
    }));
  });

  protected async calculate(value: FormValue): Promise<void> {
    this.errors.set([]);
    try {
      this.output.set(
        await firstValueFrom(this.api.calculate(this.code(), value as Record<string, unknown>)),
      );
    } catch (error) {
      this.output.set(null);
      this.errors.set(problemMessages(error));
    }
  }

  private label(result: CalculatorResult): string {
    return this.i18n.language() === 'ar' ? result.labelAr : result.labelEn;
  }

  private format(result: CalculatorResult, value: unknown): string {
    if (value === null || value === undefined || value === '') return '—';
    const ar = this.i18n.language() === 'ar';
    switch (result.format) {
      case 'YES_NO':
        return value ? (ar ? 'نعم' : 'Yes') : ar ? 'لا' : 'No';
      case 'MONEY':
        return `${this.decimal.transform(Number(value), '1.0-2') ?? value} ${ar ? 'ريال' : 'SAR'}`;
      case 'NUMBER':
        return this.decimal.transform(Number(value), '1.0-2') ?? String(value);
      default:
        return typeof value === 'object' ? JSON.stringify(value) : String(value);
    }
  }
}

/** A value at a dot path of the output (e.g. `grade` or `items.0.grade`). */
function read(output: Record<string, unknown>, path: string): unknown {
  return path
    .split('.')
    .reduce<unknown>(
      (at, key) =>
        at && typeof at === 'object' ? (at as Record<string, unknown>)[key] : undefined,
      output,
    );
}

export type { CalculatorView };
