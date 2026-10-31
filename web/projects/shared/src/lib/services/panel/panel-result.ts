import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { I18nService } from '../../i18n/i18n.service';
import { LocalizedText } from '../../i18n/language';
import { LocalizePipe, TranslatePipe } from '../../i18n/translate.pipe';
import { LiveResultView, ResultPanelDefinition } from './panel.models';
import { placeColor, shownItems, shownValue } from './live-result';

/**
 * The live result: the decision step of the service's workflow evaluated on the answers as they are. It shows the headline
 * on the scale (e.g. the expected grade), the configured values and a card per item (e.g. per sector) with its place on the
 * scale drawn as a ladder. It is not recorded and is not the final decision.
 */
@Component({
  selector: 'dc-panel-result',
  imports: [LocalizePipe, MatIconModule, MatProgressBarModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="dc-result" data-testid="panel-result">
      <header class="dc-result__head">
        <h3>
          <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">insights</mat-icon
          >{{ title() }}
        </h3>
        @if (view(); as v) {
          <span class="dc-result__rules" dir="ltr" [attr.title]="v.ruleSetCode + ' v' + v.version"
            >{{ v.ruleSetCode }} · v{{ v.version }}</span
          >
        }
      </header>
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (error(); as e) {
        <p class="dc-result__error" role="alert">{{ e | dcLocalize }}</p>
      }
      @if (view()) {
        @if (headline(); as h) {
          <div class="dc-result__headline" data-testid="result-headline">
            <span class="dc-result__cap">{{ h.label | dcLocalize }}</span>
            <div class="dc-result__big">
              @if (h.place >= 0) {
                <span class="dc-result__badge" [style.background]="color(h.place)" dir="ltr">{{
                  scale()[h.place].value
                }}</span>
              }
              <strong [attr.data-value]="h.value">{{ h.text | dcLocalize }}</strong>
            </div>
            @if (scale().length && h.place >= 0) {
              <ol class="dc-result__ladder" [attr.aria-label]="'panel.result.scale' | dcTranslate">
                @for (s of scale(); track s.value; let i = $index) {
                  <li
                    [class.dc-result__rung--on]="i === h.place"
                    [style.--rung]="color(i)"
                    [attr.aria-current]="i === h.place ? 'true' : null"
                    [attr.title]="{ ar: s.labelAr, en: s.labelEn } | dcLocalize"
                  >
                    <span dir="ltr">{{ s.value }}</span>
                  </li>
                }
              </ol>
            }
          </div>
        }
        @if (values().length) {
          <dl class="dc-result__values">
            @for (v of values(); track $index) {
              <div>
                <dt>{{ v.label | dcLocalize }}</dt>
                <dd>
                  @if (v.place >= 0) {
                    <span class="dc-result__dot" [style.background]="color(v.place)"></span>
                  }
                  {{ v.text | dcLocalize }}
                </dd>
              </div>
            }
          </dl>
        }
        @if (items().length) {
          <div class="dc-result__items">
            @for (item of items(); track $index) {
              <article
                class="dc-result__item"
                [style.--rung]="color(item.result?.place ?? -1)"
                data-testid="result-item"
              >
                <div class="dc-result__item-head">
                  <strong>{{ item.title | dcLocalize }}</strong>
                  @if (item.result; as r) {
                    <span class="dc-result__pill" [class.dc-result__pill--none]="r.place < 0">{{
                      r.text | dcLocalize
                    }}</span>
                  }
                </div>
                @if (item.result && scale().length) {
                  <div class="dc-result__meter" aria-hidden="true">
                    @for (s of scale(); track s.value; let i = $index) {
                      <i
                        [class.dc-result__meter--on]="
                          item.result.place >= 0 && i < scale().length - item.result.place
                        "
                      ></i>
                    }
                  </div>
                }
                @if (item.columns.length) {
                  <dl class="dc-result__cols">
                    @for (c of item.columns; track $index) {
                      <div>
                        <dt>{{ c.label | dcLocalize }}</dt>
                        <dd>{{ c.text | dcLocalize }}</dd>
                      </div>
                    }
                  </dl>
                }
              </article>
            }
          </div>
        }
        <p class="dc-result__hint">{{ 'panel.result.hint' | dcTranslate }}</p>
      } @else if (!loading() && !error()) {
        <p class="dc-result__empty">{{ 'panel.result.empty' | dcTranslate }}</p>
      }
    </section>
  `,
  styles: `
    :host {
      display: block;
    }
    .dc-result {
      display: grid;
      gap: 12px;
      padding: 14px 16px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 14px;
      background: #fff;
    }
    .dc-result__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }
    .dc-result__head h3 {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0;
      font-size: 16px;
    }
    .dc-result__head mat-icon {
      color: var(--dga-primary, #1b8354);
    }
    .dc-result__rules {
      overflow: hidden;
      max-width: 50%;
      padding: 1px 8px;
      border-radius: 999px;
      background: var(--dga-gray-100, #f3f4f6);
      color: var(--dga-gray-600, #4d5761);
      font-family: ui-monospace, monospace;
      font-size: 11px;
      white-space: nowrap;
      text-overflow: ellipsis;
    }
    .dc-result__error {
      margin: 0;
      padding: 8px 10px;
      border-radius: 8px;
      background: #fef3f2;
      color: var(--dga-error-700, #b42318);
      font-size: 13px;
    }
    .dc-result__headline {
      display: grid;
      gap: 6px;
      padding: 12px;
      border-radius: 12px;
      background: var(--dga-gray-50, #f9fafb);
    }
    .dc-result__cap {
      color: var(--dga-gray-600, #4d5761);
      font-size: 12.5px;
    }
    .dc-result__big {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .dc-result__big strong {
      font-size: 20px;
    }
    .dc-result__badge {
      display: grid;
      place-items: center;
      min-width: 40px;
      height: 40px;
      padding: 0 6px;
      border-radius: 10px;
      color: #fff;
      font-size: 20px;
      font-weight: 700;
    }
    .dc-result__ladder {
      display: flex;
      gap: 4px;
      margin: 4px 0 0;
      padding: 0;
      list-style: none;
    }
    .dc-result__ladder li {
      flex: 1;
      display: grid;
      place-items: center;
      height: 24px;
      border-radius: 6px;
      background: color-mix(in srgb, var(--rung) 14%, #fff);
      color: var(--rung);
      font-size: 12px;
      font-weight: 600;
    }
    .dc-result__ladder li.dc-result__rung--on {
      background: var(--rung);
      color: #fff;
      box-shadow:
        0 0 0 2px #fff,
        0 0 0 4px var(--rung);
    }
    .dc-result__values {
      display: grid;
      gap: 6px;
      margin: 0;
    }
    .dc-result__values div,
    .dc-result__cols div {
      display: flex;
      justify-content: space-between;
      gap: 12px;
    }
    .dc-result__values dt,
    .dc-result__cols dt {
      color: var(--dga-gray-600, #4d5761);
    }
    .dc-result__values dd,
    .dc-result__cols dd {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      margin: 0;
      font-weight: 600;
      text-align: end;
    }
    .dc-result__values {
      font-size: 13.5px;
    }
    .dc-result__dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
    }
    .dc-result__items {
      display: grid;
      gap: 8px;
    }
    .dc-result__item {
      padding: 10px 12px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-inline-start: 4px solid var(--rung);
      border-radius: 10px;
    }
    .dc-result__item-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }
    .dc-result__pill {
      padding: 1px 10px;
      border-radius: 999px;
      background: var(--rung);
      color: #fff;
      font-size: 12px;
      font-weight: 600;
      white-space: nowrap;
    }
    .dc-result__pill--none {
      background: var(--dga-gray-100, #f3f4f6);
      color: var(--dga-gray-600, #4d5761);
    }
    .dc-result__meter {
      display: flex;
      gap: 3px;
      margin: 8px 0 2px;
    }
    .dc-result__meter i {
      flex: 1;
      height: 6px;
      border-radius: 3px;
      background: var(--dga-gray-100, #f3f4f6);
    }
    .dc-result__meter i.dc-result__meter--on {
      background: var(--rung);
    }
    .dc-result__cols {
      display: grid;
      gap: 4px;
      margin: 8px 0 0;
      font-size: 12.5px;
    }
    .dc-result__hint,
    .dc-result__empty {
      margin: 0;
      color: var(--dga-gray-500, #6c737f);
      font-size: 12px;
      line-height: 1.6;
    }
  `,
})
export class PanelResult {
  readonly definition = input.required<ResultPanelDefinition>();
  readonly view = input<LiveResultView | null>(null);
  readonly loading = input(false);
  readonly error = input<LocalizedText | null>(null);

  private readonly i18n = inject(I18nService);
  protected readonly scale = computed(() => this.definition().scale);
  protected readonly title = computed(
    () =>
      this.i18n.localize({
        ar: this.definition().titleAr ?? '',
        en: this.definition().titleEn ?? '',
      }) || this.i18n.translate('panel.result.title'),
  );
  protected readonly headline = computed(() => {
    const headline = this.definition().headline;
    return headline?.output ? shownValue(headline, this.view()?.output, this.definition()) : null;
  });
  protected readonly values = computed(() =>
    this.definition()
      .values.filter((v) => v.output)
      .map((v) => shownValue(v, this.view()?.output, this.definition())),
  );
  protected readonly items = computed(() => shownItems(this.definition(), this.view()?.output));

  /** The colour of a place on the scale: the best is green; the meter fills from the item's place to the worst. */
  protected color(place: number): string {
    return placeColor(place, this.scale().length);
  }
}
