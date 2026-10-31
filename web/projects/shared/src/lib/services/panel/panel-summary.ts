import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { I18nService } from '../../i18n/i18n.service';
import { LocalizedText } from '../../i18n/language';
import { LocalizePipe, TranslatePipe } from '../../i18n/translate.pipe';
import { displayAnswer } from '../service-form';
import { FeeLine, ResolvedVersion } from '../service.models';
import { headlineText } from './live-result';
import { LiveResultView, ResultPanelDefinition, SummaryPanelDefinition } from './panel.models';
import { Readiness } from './readiness';

/** The request summary: the service, version, number, level, answers given, attachments, fees, the expected result and chosen answers. */
@Component({
  selector: 'dc-panel-summary',
  imports: [DecimalPipe, LocalizePipe, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="dc-summary" data-testid="panel-summary">
      <h3>
        <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">summarize</mat-icon
        >{{ 'panel.summary.title' | dcTranslate }}
      </h3>
      <dl>
        <dt>{{ 'panel.summary.service' | dcTranslate }}</dt>
        <dd>{{ service() | dcLocalize }}</dd>
        <dt>{{ 'panel.summary.version' | dcTranslate }}</dt>
        <dd dir="ltr">{{ version().number }}</dd>
        @if (number()) {
          <dt>{{ 'panel.summary.number' | dcTranslate }}</dt>
          <dd dir="ltr">{{ number() }}</dd>
        }
        @if (version().definition.levels.length) {
          <dt>{{ 'panel.summary.level' | dcTranslate }}</dt>
          <dd>{{ level() ? (level()! | dcLocalize) : ('panel.summary.notYet' | dcTranslate) }}</dd>
        }
        <dt>{{ 'panel.summary.answered' | dcTranslate }}</dt>
        <dd dir="ltr" data-testid="summary-answered">
          {{ readiness().answered }} / {{ readiness().asked }}
        </dd>
        @if (hasFiles()) {
          <dt>{{ 'panel.summary.files' | dcTranslate }}</dt>
          <dd dir="ltr">{{ readiness().files }}</dd>
        }
        @if (fees().length) {
          <dt>{{ 'panel.summary.fees' | dcTranslate }}</dt>
          <dd>{{ feeTotal() | number: '1.0-2' }} {{ 'fees.currency' | dcTranslate }}</dd>
        }
        @if (headline(); as h) {
          <dt>{{ h.label | dcLocalize }}</dt>
          <dd data-testid="summary-result">{{ h.text | dcLocalize }}</dd>
        }
        @for (a of answers(); track a.code) {
          <dt>{{ a.label | dcLocalize }}</dt>
          <dd>{{ a.text }}</dd>
        }
      </dl>
    </section>
  `,
  styles: `
    :host {
      display: block;
    }
    .dc-summary {
      padding: 14px 16px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 14px;
      background: #fff;
    }
    h3 {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0 0 10px;
      font-size: 16px;
    }
    h3 mat-icon {
      color: var(--dga-primary, #1b8354);
    }
    dl {
      display: grid;
      grid-template-columns: max-content minmax(0, 1fr);
      gap: 6px 14px;
      margin: 0;
      font-size: 13.5px;
    }
    dt {
      color: var(--dga-gray-600, #4d5761);
    }
    dd {
      margin: 0;
      font-weight: 600;
      overflow-wrap: anywhere;
    }
  `,
})
export class PanelSummary {
  readonly definition = input.required<SummaryPanelDefinition>();
  readonly version = input.required<ResolvedVersion>();
  readonly service = input.required<LocalizedText>();
  readonly data = input<Record<string, unknown>>({});
  readonly levelCode = input<string | null>(null);
  readonly number = input<string | null>(null);
  readonly readiness = input.required<Readiness>();
  readonly fees = input<FeeLine[]>([]);
  /** Uploaded files, for the names of file answers. */
  readonly files = input<{ id: string; fileName: string }[]>([]);
  readonly result = input<ResultPanelDefinition | null>(null);
  readonly live = input<LiveResultView | null>(null);

  private readonly i18n = inject(I18nService);
  protected readonly level = computed<LocalizedText | null>(() => {
    const level = this.version().definition.levels.find((l) => l.code === this.levelCode());
    return level ? { ar: level.nameAr, en: level.nameEn } : null;
  });
  protected readonly hasFiles = computed(() =>
    Object.values(this.version().questions).some((q) => q.type === 'FILE'),
  );
  protected readonly feeTotal = computed(() => this.fees().reduce((sum, l) => sum + l.total, 0));
  protected readonly headline = computed(() => headlineText(this.result(), this.live()));
  protected readonly answers = computed(() =>
    this.definition()
      .questions.map((code) => this.version().questions[code])
      .filter((q) => !!q)
      .map((q) => ({
        code: q.code,
        label: { ar: q.labelAr, en: q.labelEn },
        text: displayAnswer(q, this.data()[q.code], this.i18n.language(), this.files()),
      })),
  );
}
