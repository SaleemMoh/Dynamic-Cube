import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  output,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { I18nService } from '../i18n/i18n.service';
import { LocalizedText } from '../i18n/language';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import {
  AnswerChange,
  IntegrationStepRun,
  ResolvedVersion,
  StepDefinition,
} from './service.models';

/** A value shown on an integration step's result card. */
export interface ResultValue {
  code: string;
  label: LocalizedText;
  value: string;
}

/** A value as the applicant reads it (yes/no, lists joined, an option's label rather than its code), or null when empty. */
export function readableValue(
  version: ResolvedVersion,
  question: string | null,
  value: unknown,
  i18n: I18nService,
): string | null {
  const options = question ? (version.questions[question]?.options ?? []) : [];
  const text = (v: unknown): string | null => {
    if (v === null || v === undefined || v === '') return null;
    const option = options.find((o) => o.value === v);
    if (option) return i18n.localize({ ar: option.labelAr, en: option.labelEn });
    if (typeof v === 'boolean') return i18n.translate(v ? 'wizard.yes' : 'wizard.no');
    if (Array.isArray(v)) return v.map((item) => text(item) ?? '').join('، ');
    return typeof v === 'object' ? JSON.stringify(v) : String(v);
  };
  return text(value);
}

/** Answers an integration step filled (its outputs), to show the applicant. */
export function fetchedAnswers(
  version: ResolvedVersion,
  step: StepDefinition,
  data: Record<string, unknown>,
  i18n: I18nService,
): ResultValue[] {
  return (step.integration?.outputs ?? [])
    .map((o) => ({ o, value: readableValue(version, o.question, data[o.question], i18n) }))
    .filter(({ value }) => value !== null)
    .map(({ o, value }) => {
      const q = version.questions[o.question];
      return {
        code: o.question,
        label: q ? { ar: q.labelAr, en: q.labelEn } : { ar: o.question, en: o.question },
        value: value!,
      };
    });
}

/**
 * The outcome of an integration step's call, as a card: success or failure with the provider's message, when it was
 * checked, the values chosen for the card (or the answers the step filled) and a button to call it again.
 */
@Component({
  selector: 'dc-integration-result',
  imports: [DatePipe, MatButtonModule, MatIconModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let r = run();
    <article
      class="dc-result"
      [class.dc-result--failed]="!r.success"
      data-testid="integration-result"
      [attr.data-success]="r.success"
    >
      <header class="dc-result__head">
        <span class="dc-result__badge" aria-hidden="true">
          <mat-icon fontSet="material-symbols-outlined">{{
            r.success ? 'verified' : 'error'
          }}</mat-icon>
        </span>
        <div class="dc-result__title">
          <strong>{{
            (r.success ? 'wizard.resultOk' : 'wizard.resultFailed') | dcTranslate
          }}</strong>
          @if (r.messageAr || r.messageEn) {
            <span>{{ { ar: r.messageAr ?? '', en: r.messageEn ?? '' } | dcLocalize }}</span>
          } @else if (!r.success) {
            <span>{{
              (required() ? 'wizard.resultRequired' : 'wizard.integrationOptional') | dcTranslate
            }}</span>
          }
        </div>
        <span class="dc-result__time">
          <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">schedule</mat-icon>
          {{ 'wizard.checkedAt' | dcTranslate }} <span dir="ltr">{{ r.at | date: 'short' }}</span>
        </span>
      </header>
      @if (values().length) {
        <dl class="dc-result__values">
          @for (v of values(); track v.code) {
            <div class="dc-result__value" [attr.data-output]="v.code">
              <dt>{{ v.label | dcLocalize }}</dt>
              <dd>{{ v.value }}</dd>
            </div>
          }
        </dl>
      }
      <footer class="dc-result__foot">
        @if (!r.success && required()) {
          <span class="dc-result__note">{{ 'wizard.resultBlocked' | dcTranslate }}</span>
        }
        <button
          matButton
          type="button"
          [disabled]="disabled()"
          (click)="retry.emit()"
          data-testid="integration-retry"
        >
          <mat-icon fontSet="material-symbols-outlined">refresh</mat-icon>
          {{ (r.success ? 'wizard.checkAgain' : 'wizard.tryAgain') | dcTranslate }}
        </button>
      </footer>
    </article>
  `,
  styles: `
    .dc-result {
      --result: var(--dga-primary, #1b8354);
      --result-soft: var(--dga-primary-50, #ecfdf3);
      overflow: hidden;
      margin-bottom: 12px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-top: 4px solid var(--result);
      border-radius: 12px;
      background: #fff;
    }
    .dc-result--failed {
      --result: var(--dga-error-600, #d92d20);
      --result-soft: var(--dga-error-50, #fef3f2);
    }
    .dc-result__head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 12px;
      padding: 14px 16px;
      background: var(--result-soft);
    }
    .dc-result__badge {
      display: grid;
      place-items: center;
      width: 40px;
      height: 40px;
      border-radius: 50%;
      background: var(--result);
      color: #fff;
    }
    .dc-result__title {
      flex: 1;
      display: grid;
      gap: 2px;
      min-width: 12rem;
    }
    .dc-result__title strong {
      color: var(--result);
      font-size: 16px;
    }
    .dc-result__time {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      color: var(--dga-gray-500, #667085);
      font-size: 12px;
    }
    .dc-result__time mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
    .dc-result__values {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(12rem, 1fr));
      gap: 10px;
      margin: 0;
      padding: 14px 16px;
    }
    .dc-result__value {
      padding: 10px 12px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 8px;
      background: var(--dga-gray-50, #f9fafb);
    }
    .dc-result__value dt {
      color: var(--dga-gray-500, #667085);
      font-size: 12px;
    }
    .dc-result__value dd {
      margin: 2px 0 0;
      font-weight: 600;
      overflow-wrap: anywhere;
    }
    .dc-result__foot {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: flex-end;
      gap: 8px;
      padding: 8px 12px;
      border-top: 1px solid var(--dga-gray-200, #e5e7eb);
    }
    .dc-result__note {
      flex: 1;
      color: var(--dga-error-600, #d92d20);
      font-size: 13px;
    }
  `,
})
export class IntegrationResult {
  readonly version = input.required<ResolvedVersion>();
  readonly data = input<Record<string, unknown>>({});
  readonly step = input.required<StepDefinition>();
  readonly run = input.required<IntegrationStepRun>();
  readonly disabled = input(false);
  readonly retry = output<void>();

  private readonly i18n = inject(I18nService);
  protected readonly required = computed(() => this.step().integration?.required !== false);
  /** The outputs chosen for the card, or else (after a success) the answers the step filled. */
  protected readonly values = computed<ResultValue[]>(() => {
    const run = this.run();
    const display = this.step().integration?.display ?? [];
    if (!display.length)
      return run.success ? fetchedAnswers(this.version(), this.step(), this.data(), this.i18n) : [];
    return display
      .filter((d) => run.values && d.output in run.values)
      .map((d) => ({
        code: d.output,
        label: { ar: d.labelAr || d.labelEn, en: d.labelEn || d.labelAr },
        value: readableValue(this.version(), null, run.values![d.output], this.i18n) ?? '—',
      }));
  });
}

/** Answers that changed when the draft's integrations were read again: each answer from → to, until closed. */
@Component({
  selector: 'dc-answer-changes',
  imports: [MatButtonModule, MatIconModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!dismissed()) {
      <div class="dc-changes" role="status" data-testid="wizard-changes">
        <mat-icon fontSet="material-symbols-outlined" aria-hidden="true"
          >published_with_changes</mat-icon
        >
        <div class="dc-changes__body">
          <strong>{{ 'wizard.changedTitle' | dcTranslate }}</strong>
          <span class="dc-changes__hint">{{ 'wizard.changedHint' | dcTranslate }}</span>
          <ul>
            @for (c of shown(); track c.question) {
              <li [attr.data-question]="c.question">
                <span class="dc-changes__label">{{ c.label | dcLocalize }}</span>
                <span class="dc-changes__from">{{
                  c.from ?? ('wizard.noValue' | dcTranslate)
                }}</span>
                <mat-icon
                  fontSet="material-symbols-outlined"
                  class="dc-changes__arrow"
                  aria-hidden="true"
                  >arrow_forward</mat-icon
                >
                <strong class="dc-changes__to">{{
                  c.to ?? ('wizard.noValue' | dcTranslate)
                }}</strong>
              </li>
            }
          </ul>
        </div>
        <button
          matIconButton
          type="button"
          (click)="dismissed.set(true)"
          [attr.aria-label]="'wizard.changedClose' | dcTranslate"
          data-testid="wizard-changes-close"
        >
          <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
        </button>
      </div>
    }
  `,
  styles: `
    .dc-changes {
      display: flex;
      gap: 12px;
      align-items: flex-start;
      padding: 12px 14px;
      margin-bottom: 14px;
      border: 1px solid var(--dga-warning-300, #fec84b);
      border-inline-start-width: 4px;
      border-radius: 10px;
      background: var(--dga-warning-50, #fffaeb);
    }
    .dc-changes > mat-icon {
      color: var(--dga-warning-600, #dc6803);
    }
    .dc-changes__body {
      flex: 1;
      display: grid;
      gap: 4px;
      min-width: 0;
    }
    .dc-changes__hint {
      color: var(--dga-gray-600, #4d5761);
    }
    .dc-changes ul {
      display: grid;
      gap: 6px;
      margin: 6px 0 0;
      padding: 0;
      list-style: none;
    }
    .dc-changes li {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
    }
    .dc-changes__label {
      font-weight: 600;
    }
    .dc-changes__label::after {
      content: ':';
    }
    .dc-changes__from,
    .dc-changes__to {
      padding: 1px 8px;
      border-radius: 6px;
      background: #fff;
    }
    .dc-changes__from {
      color: var(--dga-gray-500, #667085);
      text-decoration: line-through;
    }
    .dc-changes__to {
      color: var(--dga-primary-dark, #074d31);
    }
    .dc-changes__arrow {
      font-size: 18px;
      width: 18px;
      height: 18px;
      color: var(--dga-gray-500, #667085);
    }
    :host-context([dir='rtl']) .dc-changes__arrow {
      transform: scaleX(-1);
    }
  `,
})
export class AnswerChanges {
  readonly version = input.required<ResolvedVersion>();
  readonly changes = input.required<AnswerChange[]>();

  private readonly i18n = inject(I18nService);
  /** Closed by the applicant; shown again when new changes come. */
  protected readonly dismissed = linkedSignal({ source: this.changes, computation: () => false });
  protected readonly shown = computed(() =>
    this.changes().map((c) => ({
      question: c.question,
      label: { ar: c.labelAr || c.labelEn, en: c.labelEn || c.labelAr },
      from: readableValue(this.version(), c.question, c.from, this.i18n),
      to: readableValue(this.version(), c.question, c.to, this.i18n),
    })),
  );
}
