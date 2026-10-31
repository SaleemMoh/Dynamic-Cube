import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { DynamicForm, FormValue } from '../forms/dynamic-form';
import { FileUploader } from '../forms/file-input';
import { I18nService } from '../i18n/i18n.service';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { FeeTable } from './fee-table';
import { RequestAnswers } from './request-answers';
import { fromAnswers, stepSchema, toAnswers } from './service-form';
import { FeeLine, FieldError, ResolvedVersion, StepDefinition } from './service.models';

/** Moving between steps: the answers so far, the chosen level, the step being left and the step to show. */
export interface WizardMove {
  data: Record<string, unknown>;
  levelCode: string | null;
  from: string;
  to: string;
  /** Moving on (the step being left must be valid) rather than back. */
  forward: boolean;
}

/**
 * Renders a service version as a wizard: form steps (via {@link DynamicForm}), service level, fees, review and
 * declaration. It holds no request state: the page saves each move and passes back the server's answers, current step,
 * fees and problems.
 */
@Component({
  selector: 'dc-service-wizard',
  imports: [
    DecimalPipe,
    NgTemplateOutlet,
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatIconModule,
    MatProgressBarModule,
    DynamicForm,
    FeeTable,
    LocalizePipe,
    RequestAnswers,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ol class="dc-steps" aria-label="steps">
      @for (s of steps(); track s.code; let i = $index) {
        <li class="dc-steps__item" [class.dc-steps__item--current]="i === index()" [class.dc-steps__item--done]="i < index()">
          <button
            type="button"
            class="dc-steps__button"
            [disabled]="i >= index() || busy()"
            (click)="go(s.code)"
            [attr.aria-current]="i === index() ? 'step' : null"
          >
            <span class="dc-steps__number">{{ i + 1 }}</span>
            <span>{{ { ar: s.titleAr, en: s.titleEn } | dcLocalize }}</span>
          </button>
        </li>
      }
    </ol>
    @if (busy()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @if (errors().length) {
      <ul class="dc-errors" role="alert" data-testid="wizard-errors">
        @for (e of errors(); track $index) {
          <li>
            {{ { ar: e.messageAr, en: e.messageEn } | dcLocalize }}
            @if (e.field && label(e.field); as l) {
              — {{ l }}
            }
            @if (e.step && e.step !== step()?.code) {
              <button matButton type="button" (click)="go(e.step)">{{ 'wizard.goToStep' | dcTranslate }}</button>
            }
          </li>
        }
      </ul>
    }

    @if (step(); as s) {
      <section class="dc-wizard__step" [attr.data-step]="s.code">
        <h2>{{ { ar: s.titleAr, en: s.titleEn } | dcLocalize }}</h2>
        @switch (s.type) {
          @case ('FORM') {
            <dc-dynamic-form
              [schema]="schema()!"
              [initialValue]="formValue()"
              [context]="data()"
              [submitLabel]="'form.next' | dcTranslate"
              submitIcon="check"
              [cancellable]="index() > 0"
              [cancelLabel]="'form.back' | dcTranslate"
              [uploader]="uploader()"
              (submitted)="next(s, $event)"
              (cancelled)="back()"
            />
          }
          @case ('LEVEL') {
            <div class="dc-levels" role="radiogroup">
              @for (level of version().definition.levels; track level.code) {
                <button
                  type="button"
                  role="radio"
                  class="dc-level"
                  [class.dc-level--chosen]="level.code === chosenLevel()"
                  [attr.aria-checked]="level.code === chosenLevel()"
                  [disabled]="resubmission() && level.code !== chosenLevel()"
                  (click)="levelChoice.set(level.code)"
                  [attr.data-testid]="'level-' + level.code"
                >
                  <strong>{{ { ar: level.nameAr, en: level.nameEn } | dcLocalize }}</strong>
                  @if (level.descriptionAr || level.descriptionEn) {
                    <span class="dc-muted">{{ { ar: level.descriptionAr ?? '', en: level.descriptionEn ?? '' } | dcLocalize }}</span>
                  }
                  @if (level.slaWorkingDays) {
                    <span>{{ 'wizard.duration' | dcTranslate }} {{ level.slaWorkingDays }} {{ 'wizard.workingDays' | dcTranslate }}</span>
                  }
                  @if (feesByLevel()[level.code]; as lines) {
                    <span class="dc-level__fee">{{
                      levelTotal(lines) ? (levelTotal(lines) | number: '1.0-2') + ' ' + ('fees.currency' | dcTranslate) : ('fees.free' | dcTranslate)
                    }}</span>
                  }
                </button>
              }
            </div>
            <ng-container *ngTemplateOutlet="nav" />
          }
          @case ('FEES') {
            <dc-fee-table [lines]="fees()" />
            <ng-container *ngTemplateOutlet="nav" />
          }
          @case ('REVIEW') {
            <dc-request-answers [version]="version()" [data]="data()" [files]="uploader()?.files() ?? []" (openFile)="uploader()?.open($event.id)" />
            <ng-container *ngTemplateOutlet="nav" />
          }
          @case ('INTEGRATION') {
            <div class="dc-integration" data-testid="integration-step">
              <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">sync_alt</mat-icon>
              <div>
                @if (s.textAr || s.textEn) {
                  <p>{{ { ar: s.textAr ?? '', en: s.textEn ?? '' } | dcLocalize }}</p>
                }
                <p class="dc-muted">{{ (s.integration?.required === false ? 'wizard.integrationOptional' : 'wizard.integrationHint') | dcTranslate }}</p>
                @if (fetched(s).length) {
                  <dl class="dc-integration__values">
                    @for (f of fetched(s); track f.code) {
                      <dt>{{ f.label | dcLocalize }}</dt>
                      <dd>{{ f.value }}</dd>
                    }
                  </dl>
                }
              </div>
            </div>
            <ng-container *ngTemplateOutlet="nav" />
          }
          @case ('DECLARATION') {
            <p class="dc-declaration">{{ { ar: s.textAr ?? '', en: s.textEn ?? '' } | dcLocalize }}</p>
            <mat-checkbox [(ngModel)]="accepted" data-testid="declaration-accept">{{ 'wizard.accept' | dcTranslate }}</mat-checkbox>
            <div class="dc-wizard__actions">
              @if (index() > 0) {
                <button matButton type="button" (click)="back()">{{ 'form.back' | dcTranslate }}</button>
              }
              <button
                matButton="filled"
                type="button"
                [disabled]="!accepted() || busy() || preview()"
                (click)="submitted.emit(true)"
                data-testid="submit-request"
              >
                <mat-icon fontSet="material-symbols-outlined">send</mat-icon>
                {{ (resubmission() ? 'wizard.resubmit' : 'wizard.submit') | dcTranslate }}
              </button>
            </div>
          }
        }
      </section>
    }

    <ng-template #nav>
      <div class="dc-wizard__actions">
        @if (index() > 0) {
          <button matButton type="button" (click)="back()">{{ 'form.back' | dcTranslate }}</button>
        }
        @if (isLast()) {
          <button matButton="filled" type="button" [disabled]="busy() || preview()" (click)="submitted.emit(false)" data-testid="submit-request">
            <mat-icon fontSet="material-symbols-outlined">send</mat-icon>
            {{ (resubmission() ? 'wizard.resubmit' : 'wizard.submit') | dcTranslate }}
          </button>
        } @else {
          <button matButton="filled" type="button" [disabled]="busy()" (click)="nextFrom(step()!)" data-testid="wizard-next">
            <mat-icon fontSet="material-symbols-outlined">check</mat-icon>
            {{ 'form.next' | dcTranslate }}
          </button>
        }
      </div>
    </ng-template>
  `,
  styles: `
    .dc-integration {
      display: flex;
      gap: 12px;
      align-items: flex-start;
      padding: 14px 16px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 10px;
      margin-bottom: 12px;
    }
    .dc-integration p {
      margin: 0 0 6px;
    }
    .dc-integration__values {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 4px 16px;
      margin: 8px 0 0;
    }
    .dc-integration__values dt {
      color: var(--dga-gray-600, #4d5761);
    }
    .dc-integration__values dd {
      margin: 0;
      font-weight: 600;
    }
    .dc-steps {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      margin: 0 0 1rem;
      padding: 0;
      list-style: none;
    }
    .dc-steps__button {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.375rem 0.75rem;
      border: 1px solid var(--dga-border, #d2d6db);
      border-radius: 9999px;
      background: #fff;
      font: inherit;
      color: var(--dga-text-3, #6c737f);
    }
    .dc-steps__button:not(:disabled) {
      cursor: pointer;
    }
    .dc-steps__item--current .dc-steps__button {
      border-color: var(--dga-primary, #1b8354);
      color: var(--dga-primary-dark, #074d31);
      font-weight: 600;
    }
    .dc-steps__item--done .dc-steps__button {
      color: var(--dga-primary, #1b8354);
    }
    .dc-steps__number {
      display: inline-grid;
      place-items: center;
      width: 1.5rem;
      height: 1.5rem;
      border-radius: 9999px;
      background: var(--dga-gray-100, #f3f4f6);
      font-size: 13px;
    }
    .dc-steps__item--current .dc-steps__number,
    .dc-steps__item--done .dc-steps__number {
      background: var(--dga-primary, #1b8354);
      color: #fff;
    }
    .dc-levels {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr));
      gap: 0.75rem;
    }
    .dc-level {
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
      padding: 1rem;
      border: 1px solid var(--dga-border, #d2d6db);
      border-radius: 12px;
      background: #fff;
      font: inherit;
      text-align: start;
      cursor: pointer;
    }
    .dc-level:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
    .dc-level--chosen {
      border-color: var(--dga-primary, #1b8354);
      box-shadow: inset 0 0 0 1px var(--dga-primary, #1b8354);
    }
    .dc-level__fee {
      font-weight: 700;
      color: var(--dga-primary-dark, #074d31);
    }
    .dc-wizard__actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.5rem;
      margin-top: 1.5rem;
    }
    .dc-declaration {
      padding: 1rem;
      border-radius: 8px;
      background: var(--dga-gray-50, #f9fafb);
      line-height: 1.8;
    }
  `,
})
export class ServiceWizard {
  readonly version = input.required<ResolvedVersion>();
  readonly data = input<Record<string, unknown>>({});
  readonly levelCode = input<string | null>(null);
  readonly currentStep = input<string | null>(null);
  readonly fees = input<FeeLine[]>([]);
  readonly feesByLevel = input<Record<string, FeeLine[]>>({});
  readonly errors = input<FieldError[]>([]);
  readonly busy = input(false);
  /** Correcting a request returned by the employee: the level stays as chosen and the request is sent back. */
  readonly resubmission = input(false);
  /** Authoring preview: nothing can be submitted. */
  readonly preview = input(false);
  /** Where file questions upload (the request's files on the portal, the browser in a preview). */
  readonly uploader = input<FileUploader | null>(null);
  readonly moved = output<WizardMove>();
  /** Submission requested; true when the declaration was accepted. */
  readonly submitted = output<boolean>();

  private readonly i18n = inject(I18nService);
  protected readonly accepted = signal(false);
  protected readonly levelChoice = signal<string | null>(null);

  protected readonly steps = computed(() => this.version().definition.steps);
  protected readonly index = computed(() =>
    Math.max(
      0,
      this.steps().findIndex((s) => s.code === this.currentStep()),
    ),
  );
  protected readonly step = computed<StepDefinition | undefined>(() => this.steps()[this.index()]);
  protected readonly schema = computed(() => (this.step()?.type === 'FORM' ? stepSchema(this.version(), this.step()!) : null));
  protected readonly formValue = computed<FormValue>(() => fromAnswers(this.version(), this.data()));
  protected readonly isLast = computed(() => this.index() === this.steps().length - 1);
  protected readonly chosenLevel = computed(() => this.levelChoice() ?? this.levelCode());

  protected label(code: string): string | null {
    const q = this.version().questions[code];
    return q ? this.i18n.localize({ ar: q.labelAr, en: q.labelEn }) : null;
  }

  /** Answers an integration step fetched so far (its outputs), to show the applicant. */
  protected fetched(step: StepDefinition): { code: string; label: { ar: string; en: string }; value: string }[] {
    const data = this.data();
    return (step.integration?.outputs ?? [])
      .filter((o) => data[o.question] !== undefined && data[o.question] !== null && data[o.question] !== '')
      .map((o) => {
        const q = this.version().questions[o.question];
        const value = data[o.question];
        return {
          code: o.question,
          label: q ? { ar: q.labelAr, en: q.labelEn } : { ar: o.question, en: o.question },
          value: typeof value === 'object' ? JSON.stringify(value) : String(value),
        };
      });
  }

  protected levelTotal(lines: FeeLine[]): number {
    return lines.reduce((sum, l) => sum + l.total, 0);
  }

  protected next(step: StepDefinition, value: FormValue): void {
    this.emitMove(step.code, this.steps()[this.index() + 1]?.code ?? step.code, { ...this.data(), ...toAnswers(value) });
  }

  protected nextFrom(step: StepDefinition): void {
    this.emitMove(step.code, this.steps()[this.index() + 1]?.code ?? step.code, this.data());
  }

  protected back(): void {
    const previous = this.steps()[this.index() - 1];
    if (previous) this.go(previous.code);
  }

  protected go(code: string): void {
    this.moved.emit({ data: this.data(), levelCode: this.chosenLevel(), from: this.step()!.code, to: code, forward: false });
  }

  private emitMove(from: string, to: string, data: Record<string, unknown>): void {
    this.moved.emit({ data, levelCode: this.chosenLevel(), from, to, forward: true });
  }
}
