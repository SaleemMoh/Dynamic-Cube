import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { I18nService } from '../i18n/i18n.service';
import { LocalizePipe } from '../i18n/translate.pipe';
import { answeredSections, displayAnswer } from './service-form';
import { QuestionSnapshot, ResolvedVersion } from './service.models';

/** The answers of a request by section, each question once. */
@Component({
  selector: 'dc-request-answers',
  imports: [LocalizePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (section of sections(); track section.titleEn) {
      <section class="dc-answers__section">
        <h3>{{ { ar: section.titleAr, en: section.titleEn } | dcLocalize }}</h3>
        <dl class="dc-answers">
          @for (q of section.questions; track q.code) {
            <dt>{{ { ar: q.labelAr, en: q.labelEn } | dcLocalize }}</dt>
            <dd [attr.data-testid]="'answer-' + q.code">{{ text(q) }}</dd>
          }
        </dl>
      </section>
    }
  `,
  styles: `
    .dc-answers__section h3 {
      margin: 1rem 0 0.5rem;
      font-size: 16px;
    }
    .dc-answers {
      display: grid;
      grid-template-columns: minmax(10rem, 1fr) 2fr;
      gap: 0.5rem 1rem;
      margin: 0;
    }
    .dc-answers dt {
      color: var(--dga-text-3, #6c737f);
    }
    .dc-answers dd {
      margin: 0;
    }
  `,
})
export class RequestAnswers {
  readonly version = input.required<ResolvedVersion>();
  readonly data = input.required<Record<string, unknown>>();
  private readonly i18n = inject(I18nService);
  protected readonly sections = computed(() => answeredSections(this.version(), this.data()));

  protected text(question: QuestionSnapshot): string {
    return displayAnswer(question, this.data()[question.code], this.i18n.language());
  }
}
