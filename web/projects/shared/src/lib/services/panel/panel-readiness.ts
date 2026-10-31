import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { LocalizePipe, TranslatePipe } from '../../i18n/translate.pipe';
import { ResolvedVersion } from '../service.models';
import { Readiness, ReadinessItem, openGroups } from './readiness';

/** More open items than this: the details start folded. */
const FOLD_ABOVE = 6;

/**
 * The readiness check: a ring with the share of the requirements met, the counts, and what is still open grouped by the step
 * that completes it (a group opens its step).
 */
@Component({
  selector: 'dc-panel-readiness',
  imports: [LocalizePipe, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section
      class="dc-ready"
      data-testid="panel-readiness"
      [attr.aria-label]="'panel.readiness.title' | dcTranslate"
    >
      <div class="dc-ready__top">
        <div class="dc-ready__ring" role="img" [attr.aria-label]="readiness().percent + '%'">
          <svg viewBox="0 0 36 36" aria-hidden="true">
            <circle class="dc-ready__track" cx="18" cy="18" r="15.9155" />
            <circle
              class="dc-ready__value"
              cx="18"
              cy="18"
              r="15.9155"
              [attr.stroke-dasharray]="readiness().percent + ' 100'"
              [style.stroke]="color()"
            />
          </svg>
          <span class="dc-ready__percent"
            ><b dir="ltr" data-testid="readiness-percent">{{ readiness().percent }}%</b
            ><small>{{ 'panel.readiness.caption' | dcTranslate }}</small></span
          >
        </div>
        <div class="dc-ready__text">
          <h3>{{ 'panel.readiness.title' | dcTranslate }}</h3>
          <p class="dc-muted">{{ 'panel.readiness.hint' | dcTranslate }}</p>
          <div class="dc-ready__counts">
            <span class="dc-ready__count dc-ready__count--ok"
              ><mat-icon fontSet="material-symbols-outlined" aria-hidden="true"
                >check_circle</mat-icon
              ><b>{{ readiness().complete }}</b>
              {{ 'panel.readiness.complete' | dcTranslate }}</span
            >
            <span class="dc-ready__count dc-ready__count--bad"
              ><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">error</mat-icon
              ><b data-testid="readiness-missing">{{ readiness().missing }}</b>
              {{ 'panel.readiness.missing' | dcTranslate }}</span
            >
            @if (readiness().conditional) {
              <span class="dc-ready__count dc-ready__count--cond"
                ><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">help</mat-icon
                ><b>{{ readiness().conditional }}</b>
                {{ 'panel.readiness.conditional' | dcTranslate }}</span
              >
            }
          </div>
        </div>
        @if (groups().length) {
          <button
            type="button"
            class="dc-ready__fold"
            (click)="unfolded.set(!expanded())"
            [attr.aria-expanded]="expanded()"
            [attr.aria-label]="'panel.readiness.toggle' | dcTranslate"
          >
            <mat-icon fontSet="material-symbols-outlined">{{
              expanded() ? 'expand_less' : 'expand_more'
            }}</mat-icon>
          </button>
        }
      </div>
      @if (!groups().length) {
        <p class="dc-ready__done">
          <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">task_alt</mat-icon>
          {{ 'panel.readiness.allDone' | dcTranslate }}
        </p>
      } @else if (expanded()) {
        <div class="dc-ready__groups">
          @for (g of groups(); track g.step) {
            <div class="dc-ready__group">
              <button
                type="button"
                class="dc-ready__group-head"
                (click)="openStep.emit(g.step)"
                [disabled]="!canOpen()"
                [attr.data-open-step]="g.step"
                [attr.title]="'panel.readiness.openStep' | dcTranslate"
              >
                <b>{{ g.title | dcLocalize }}</b>
                <span class="dc-ready__n">{{ g.items.length }}</span>
                @if (canOpen()) {
                  <mat-icon fontSet="material-symbols-outlined" class="dc-mirror" aria-hidden="true"
                    >arrow_forward</mat-icon
                  >
                }
              </button>
              <ul>
                @for (i of g.items; track i.key) {
                  <li
                    [class]="'dc-ready__item dc-ready__item--' + i.status"
                    [attr.data-item]="i.key"
                  >
                    <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{
                      icon(i)
                    }}</mat-icon>
                    <span>{{ i.label | dcLocalize }}</span>
                  </li>
                }
              </ul>
            </div>
          }
        </div>
      }
    </section>
  `,
  styles: `
    :host {
      display: block;
    }
    .dc-ready {
      padding: 16px 18px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 14px;
      background: #fff;
    }
    .dc-ready__top {
      display: flex;
      align-items: center;
      gap: 16px;
    }
    .dc-ready__ring {
      position: relative;
      flex: none;
      width: 88px;
      height: 88px;
    }
    .dc-ready__ring svg {
      width: 100%;
      height: 100%;
      transform: rotate(-90deg);
    }
    .dc-ready__track,
    .dc-ready__value {
      fill: none;
      stroke-width: 3.2;
    }
    .dc-ready__track {
      stroke: var(--dga-gray-100, #f3f4f6);
    }
    .dc-ready__value {
      stroke-linecap: round;
      transition:
        stroke-dasharray 0.5s ease,
        stroke 0.3s ease;
    }
    .dc-ready__percent {
      position: absolute;
      inset: 0;
      display: grid;
      place-content: center;
      text-align: center;
      line-height: 1.1;
    }
    .dc-ready__percent b {
      font-size: 20px;
    }
    .dc-ready__percent small {
      font-size: 11px;
      color: var(--dga-gray-500, #6c737f);
    }
    .dc-ready__text {
      flex: 1;
      min-width: 0;
    }
    .dc-ready__text h3 {
      margin: 0;
      font-size: 17px;
    }
    .dc-ready__text p {
      margin: 2px 0 8px;
      font-size: 13px;
    }
    .dc-ready__counts {
      display: flex;
      flex-wrap: wrap;
      gap: 6px 14px;
      font-size: 13px;
    }
    .dc-ready__count {
      display: inline-flex;
      align-items: center;
      gap: 4px;
    }
    .dc-ready__count mat-icon,
    .dc-ready__item mat-icon,
    .dc-ready__done mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .dc-ready__count--ok mat-icon {
      color: var(--dga-primary, #1b8354);
    }
    .dc-ready__count--bad mat-icon {
      color: var(--dga-error-600, #d92d20);
    }
    .dc-ready__count--cond mat-icon {
      color: #b54708;
    }
    .dc-ready__fold {
      all: unset;
      cursor: pointer;
      display: grid;
      place-items: center;
      width: 32px;
      height: 32px;
      border-radius: 50%;
    }
    .dc-ready__fold:hover {
      background: var(--dga-gray-100, #f3f4f6);
    }
    .dc-ready__fold:focus-visible {
      outline: 2px solid var(--dga-primary, #1b8354);
    }
    .dc-ready__done {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 12px 0 0;
      color: var(--dga-primary-dark, #074d31);
      font-weight: 600;
    }
    .dc-ready__groups {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr));
      gap: 10px;
      margin-top: 14px;
    }
    .dc-ready__group {
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 10px;
      padding: 8px 10px;
    }
    .dc-ready__group-head {
      all: unset;
      box-sizing: border-box;
      display: flex;
      align-items: center;
      gap: 6px;
      width: 100%;
      font-size: 14px;
    }
    .dc-ready__group-head:not(:disabled) {
      cursor: pointer;
    }
    .dc-ready__group-head:not(:disabled):hover b {
      color: var(--dga-primary, #1b8354);
    }
    .dc-ready__group-head:focus-visible {
      outline: 2px solid var(--dga-primary, #1b8354);
      border-radius: 4px;
    }
    .dc-ready__group-head b {
      flex: 1;
    }
    .dc-ready__group-head mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
      color: var(--dga-primary, #1b8354);
    }
    .dc-ready__n {
      min-width: 20px;
      padding: 0 6px;
      border-radius: 999px;
      background: var(--dga-gray-100, #f3f4f6);
      font-size: 12px;
      text-align: center;
    }
    .dc-ready__group ul {
      margin: 6px 0 0;
      padding: 0;
      list-style: none;
      display: grid;
      gap: 4px;
      font-size: 13px;
    }
    .dc-ready__item {
      display: flex;
      align-items: flex-start;
      gap: 6px;
    }
    .dc-ready__item--missing mat-icon {
      color: var(--dga-error-600, #d92d20);
    }
    .dc-ready__item--conditional mat-icon {
      color: #b54708;
    }
    :host-context([dir='rtl']) .dc-mirror {
      transform: scaleX(-1);
    }
  `,
})
export class PanelReadiness {
  readonly version = input.required<ResolvedVersion>();
  readonly readiness = input.required<Readiness>();
  /** Whether a group may open its step (off in a read-only view). */
  readonly canOpen = input(true);
  /** A group was clicked: the step that completes its items. */
  readonly openStep = output<string>();

  /** Chosen by the applicant; until then the details show only when few items are open. */
  private readonly unfolded = signal<boolean | null>(null);
  protected readonly groups = computed(() => openGroups(this.version(), this.readiness()));
  protected readonly expanded = computed(
    () => this.unfolded() ?? this.groups().reduce((n, g) => n + g.items.length, 0) <= FOLD_ABOVE,
  );
  protected readonly color = computed(() => {
    const percent = this.readiness().percent;
    return percent >= 100
      ? 'var(--dga-primary, #1b8354)'
      : percent >= 50
        ? '#ca8504'
        : 'var(--dga-error-600, #d92d20)';
  });

  protected icon(item: ReadinessItem): string {
    if (item.status === 'conditional') return 'help';
    if (item.kind === 'file') return 'attach_file';
    if (item.kind === 'integration') return 'sync_alt';
    if (item.kind === 'level') return 'stacks';
    return 'radio_button_unchecked';
  }
}
