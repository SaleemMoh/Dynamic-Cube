import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { LocalizePipe, TranslatePipe } from '../../i18n/translate.pipe';
import { AssistantQuestion } from './assistant';

/** The foot of the assistant: the questions it answers, as chips, and a box to ask in your own words. */
@Component({
  selector: 'dc-assistant-ask',
  imports: [LocalizePipe, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (questions().length) {
      <div class="dc-ask__title">
        <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">forum</mat-icon>
        {{ 'panel.assistant.ask' | dcTranslate }}
      </div>
      <div class="dc-ask__chips">
        @for (q of questions(); track q.key) {
          <button
            type="button"
            class="dc-ask__chip"
            [class.dc-ask__chip--done]="asked().has(q.key)"
            (click)="chosen.emit(q)"
            [disabled]="disabled()"
          >
            {{ q.question | dcLocalize }}
          </button>
        }
      </div>
    }
    <form class="dc-ask__free" (submit)="$event.preventDefault(); send()">
      <input
        type="text"
        [value]="draft()"
        (input)="draft.set($any($event.target).value)"
        [placeholder]="'panel.assistant.type' | dcTranslate"
        [attr.aria-label]="'panel.assistant.type' | dcTranslate"
        data-testid="assistant-input"
      />
      <button
        type="submit"
        [disabled]="!draft().trim() || disabled()"
        [attr.aria-label]="'panel.assistant.send' | dcTranslate"
      >
        <mat-icon fontSet="material-symbols-outlined" class="dc-mirror">send</mat-icon>
      </button>
    </form>
    <p class="dc-ask__fine">{{ 'panel.assistant.disclaimer' | dcTranslate }}</p>
  `,
  styles: `
    :host {
      display: grid;
      gap: 8px;
      margin-top: 6px;
      padding: 8px 14px 14px;
      border-top: 1px dashed var(--dga-gray-200, #e5e7eb);
    }
    .dc-ask__title {
      display: flex;
      align-items: center;
      gap: 6px;
      color: var(--dga-gray-600, #4d5761);
      font-size: 12.5px;
      font-weight: 600;
    }
    .dc-ask__title mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
    .dc-ask__chips {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }
    .dc-ask__chip {
      all: unset;
      box-sizing: border-box;
      cursor: pointer;
      padding: 5px 12px;
      border: 1px solid #cfe0d8;
      border-radius: 999px;
      background: #fff;
      color: var(--dga-primary-dark, #074d31);
      font-size: 12.5px;
      font-weight: 600;
      line-height: 1.5;
    }
    .dc-ask__chip:hover:not(:disabled) {
      background: var(--dga-primary-50, #f1f8f4);
    }
    .dc-ask__chip:focus-visible {
      outline: 2px solid var(--dga-primary, #1b8354);
    }
    .dc-ask__chip:disabled {
      opacity: 0.6;
      cursor: default;
    }
    .dc-ask__chip--done {
      border-style: dashed;
      color: var(--dga-gray-600, #4d5761);
    }
    .dc-ask__free {
      display: flex;
      gap: 6px;
    }
    .dc-ask__free input {
      flex: 1;
      min-width: 0;
      padding: 8px 12px;
      border: 1px solid var(--dga-gray-300, #d2d6db);
      border-radius: 999px;
      font: inherit;
      font-size: 13px;
      box-shadow: none;
    }
    .dc-ask__free input:focus {
      outline: 2px solid var(--dga-primary, #1b8354);
      outline-offset: 0;
      border-color: transparent;
    }
    .dc-ask__free button {
      all: unset;
      cursor: pointer;
      display: grid;
      place-items: center;
      width: 36px;
      height: 36px;
      border-radius: 50%;
      background: var(--dga-primary, #1b8354);
      color: #fff;
    }
    .dc-ask__free button:disabled {
      opacity: 0.45;
      cursor: default;
    }
    .dc-ask__free button mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .dc-ask__fine {
      margin: 0;
      color: var(--dga-gray-500, #6c737f);
      font-size: 11.5px;
      line-height: 1.6;
    }
    :host-context([dir='rtl']) .dc-mirror {
      transform: scaleX(-1);
    }
  `,
})
export class AssistantAsk {
  readonly questions = input<AssistantQuestion[]>([]);
  /** Keys of the questions asked already (their chips are marked). */
  readonly asked = input<ReadonlySet<string>>(new Set());
  /** While the assistant is answering. */
  readonly disabled = input(false);
  readonly chosen = output<AssistantQuestion>();
  /** A question typed by the applicant. */
  readonly typed = output<string>();

  protected readonly draft = signal('');

  protected send(): void {
    const text = this.draft().trim();
    if (!text) return;
    this.draft.set('');
    this.typed.emit(text);
  }
}
