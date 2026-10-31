import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { I18nService } from '../../i18n/i18n.service';
import { LocalizedText } from '../../i18n/language';
import { LocalizePipe, TranslatePipe } from '../../i18n/translate.pipe';
import {
  AssistantContext,
  AssistantQuestion,
  AutoNoteTexts,
  NoteTone,
  assistantView,
  matchQuestion,
} from './assistant';
import { AssistantAsk } from './assistant-ask';
import { AssistantPanelDefinition } from './panel.models';

interface ThreadEntry {
  id: number;
  question: LocalizedText;
  answer: LocalizedText | null;
}

const TONE_ICONS: Record<NoteTone, string> = {
  MANDATORY: 'priority_high',
  CONDITIONAL: 'call_split',
  IMPACT: 'trending_up',
  ADVICE: 'lightbulb',
};

/**
 * The assistant beside the form: the step's greeting (typed out), its notes (each with the rule behind it under «why?»),
 * the questions it can answer and a box to ask in your own words (matched to the closest configured question). Every text
 * comes from the service version; nothing is sent anywhere.
 */
@Component({
  selector: 'dc-panel-assistant',
  imports: [AssistantAsk, LocalizePipe, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="dc-agent" [class.dc-agent--folded]="collapsed()" data-testid="panel-assistant">
      <header class="dc-agent__head">
        <span class="dc-agent__avatar" aria-hidden="true">
          <mat-icon fontSet="material-symbols-outlined">smart_toy</mat-icon>
          <i class="dc-agent__ring"></i>
        </span>
        <div class="dc-agent__title">
          <strong>{{ title() }}</strong>
          <small><i class="dc-agent__live"></i>{{ subtitle() }}</small>
        </div>
        <button
          type="button"
          class="dc-agent__fold"
          (click)="collapsed.set(!collapsed())"
          [attr.aria-expanded]="!collapsed()"
          [attr.aria-label]="
            (collapsed() ? 'panel.assistant.open' : 'panel.assistant.close') | dcTranslate
          "
        >
          <mat-icon fontSet="material-symbols-outlined">{{
            collapsed() ? 'expand_more' : 'expand_less'
          }}</mat-icon>
        </button>
      </header>

      @if (!collapsed()) {
        <div #chat class="dc-agent__chat" aria-live="polite">
          @if (message()) {
            <div class="dc-agent__bubble dc-agent__bubble--agent" data-testid="assistant-message">
              <p>{{ typed() }}</p>
            </div>
          }
          @for (n of view().notes; track n.key) {
            <article [class]="'dc-agent__note dc-agent__note--' + n.tone" [attr.data-note]="n.tone">
              <header>
                <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{
                  icons[n.tone]
                }}</mat-icon>
                <b>{{ 'panel.assistant.kind.' + n.tone | dcTranslate }}</b>
              </header>
              <p>{{ n.text | dcLocalize }}</p>
              @if (n.why) {
                <button
                  type="button"
                  class="dc-agent__why"
                  (click)="toggleWhy(n.key)"
                  [attr.aria-expanded]="open().has(n.key)"
                >
                  <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">help</mat-icon>
                  {{ 'panel.assistant.why' | dcTranslate }}
                </button>
                @if (open().has(n.key)) {
                  <div class="dc-agent__rule">
                    <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">gavel</mat-icon
                    ><span>{{ n.why | dcLocalize }}</span>
                  </div>
                }
              }
            </article>
          }
          @for (t of thread(); track t.id) {
            <div class="dc-agent__bubble dc-agent__bubble--user">{{ t.question | dcLocalize }}</div>
            <div class="dc-agent__bubble dc-agent__bubble--agent" data-testid="assistant-answer">
              @if (t.answer) {
                <p>{{ t.answer | dcLocalize }}</p>
              } @else {
                <span class="dc-agent__dots" aria-label="…"><i></i><i></i><i></i></span>
              }
            </div>
          }
        </div>

        <dc-assistant-ask
          [questions]="view().questions"
          [asked]="asked()"
          [disabled]="thinking()"
          (chosen)="ask($event)"
          (typed)="askTyped($event)"
        />
      }
    </section>
  `,
  styles: `
    :host {
      display: block;
    }
    .dc-agent {
      overflow: hidden;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 14px;
      background: #fff;
      box-shadow: 0 10px 30px -18px rgba(7, 77, 49, 0.45);
    }
    .dc-agent__head {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 12px 14px;
      color: #fff;
      background: linear-gradient(
        120deg,
        var(--dga-primary-dark, #074d31) 0%,
        var(--dga-primary, #1b8354) 100%
      );
    }
    .dc-agent__avatar {
      position: relative;
      display: grid;
      place-items: center;
      flex: none;
      width: 40px;
      height: 40px;
      border-radius: 12px;
      background: rgba(255, 255, 255, 0.18);
    }
    .dc-agent__ring {
      position: absolute;
      inset: -4px;
      border: 2px solid rgba(255, 255, 255, 0.5);
      border-radius: 16px;
      animation: dc-agent-ping 2.8s ease-out infinite;
    }
    .dc-agent__title {
      display: grid;
      flex: 1;
      min-width: 0;
      line-height: 1.35;
    }
    .dc-agent__title small {
      display: flex;
      align-items: center;
      gap: 6px;
      overflow: hidden;
      font-size: 12px;
      opacity: 0.9;
      white-space: nowrap;
      text-overflow: ellipsis;
    }
    .dc-agent__live {
      flex: none;
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: #7bf0b1;
    }
    .dc-agent__fold {
      all: unset;
      cursor: pointer;
      display: grid;
      place-items: center;
      width: 32px;
      height: 32px;
      border-radius: 50%;
    }
    .dc-agent__fold:hover {
      background: rgba(255, 255, 255, 0.18);
    }
    .dc-agent__fold:focus-visible {
      outline: 2px solid #fff;
    }
    .dc-agent__chat {
      display: grid;
      gap: 10px;
      max-height: 30rem;
      overflow-y: auto;
      padding: 14px 14px 6px;
      scrollbar-width: thin;
    }
    .dc-agent__bubble {
      max-width: 94%;
      padding: 8px 12px;
      border-radius: 14px;
      font-size: 14px;
      line-height: 1.8;
      white-space: pre-line;
    }
    .dc-agent__bubble p {
      margin: 0;
    }
    .dc-agent__bubble--agent {
      justify-self: start;
      border-end-start-radius: 4px;
      background: var(--dga-primary-50, #f1f8f4);
    }
    .dc-agent__bubble--user {
      justify-self: end;
      border-end-end-radius: 4px;
      background: var(--dga-primary, #1b8354);
      color: #fff;
      font-size: 13px;
    }
    .dc-agent__dots {
      display: inline-flex;
      gap: 4px;
      align-items: center;
      height: 20px;
    }
    .dc-agent__dots i {
      width: 7px;
      height: 7px;
      border-radius: 50%;
      background: var(--dga-primary, #1b8354);
      animation: dc-agent-dot 1s infinite ease-in-out;
    }
    .dc-agent__dots i:nth-child(2) {
      animation-delay: 0.15s;
    }
    .dc-agent__dots i:nth-child(3) {
      animation-delay: 0.3s;
    }
    .dc-agent__note {
      padding: 8px 12px 10px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-inline-start-width: 4px;
      border-radius: 12px;
      background: #fff;
    }
    .dc-agent__note header {
      display: flex;
      align-items: center;
      gap: 6px;
      margin-bottom: 2px;
      font-size: 12px;
    }
    .dc-agent__note header mat-icon {
      font-size: 17px;
      width: 17px;
      height: 17px;
    }
    .dc-agent__note p {
      margin: 0;
      font-size: 13.5px;
      line-height: 1.75;
    }
    .dc-agent__note--MANDATORY {
      border-inline-start-color: var(--dga-error-600, #d92d20);
      background: #fdf6f6;
    }
    .dc-agent__note--MANDATORY header {
      color: var(--dga-error-600, #d92d20);
    }
    .dc-agent__note--CONDITIONAL {
      border-inline-start-color: #dc6803;
      background: #fdf9ef;
    }
    .dc-agent__note--CONDITIONAL header {
      color: #b54708;
    }
    .dc-agent__note--IMPACT {
      border-inline-start-color: #ca8504;
      background: #fffaea;
    }
    .dc-agent__note--IMPACT header {
      color: #8a5a06;
    }
    .dc-agent__note--ADVICE {
      border-inline-start-color: var(--dga-primary, #1b8354);
      background: var(--dga-primary-50, #f1f8f4);
    }
    .dc-agent__note--ADVICE header {
      color: var(--dga-primary-dark, #074d31);
    }
    .dc-agent__why {
      all: unset;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      gap: 4px;
      margin-top: 4px;
      color: var(--dga-primary, #1b8354);
      font-size: 12.5px;
      font-weight: 700;
    }
    .dc-agent__why mat-icon,
    .dc-agent__rule mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
    .dc-agent__why:hover {
      text-decoration: underline;
    }
    .dc-agent__why:focus-visible {
      outline: 2px solid var(--dga-primary, #1b8354);
      border-radius: 4px;
    }
    .dc-agent__rule {
      display: flex;
      gap: 6px;
      margin-top: 6px;
      padding: 8px 10px;
      border-radius: 10px;
      background: rgba(27, 131, 84, 0.07);
      font-size: 12.5px;
      line-height: 1.7;
    }
    .dc-agent__rule mat-icon {
      flex: none;
      margin-top: 2px;
      color: var(--dga-primary, #1b8354);
    }
    @keyframes dc-agent-ping {
      0% {
        opacity: 0.8;
        transform: scale(0.92);
      }
      100% {
        opacity: 0;
        transform: scale(1.18);
      }
    }
    @keyframes dc-agent-dot {
      0%,
      80%,
      100% {
        opacity: 0.3;
        transform: translateY(0);
      }
      40% {
        opacity: 1;
        transform: translateY(-3px);
      }
    }
    @media (prefers-reduced-motion: reduce) {
      .dc-agent__ring,
      .dc-agent__dots i {
        animation: none;
      }
    }
  `,
})
export class PanelAssistant {
  readonly definition = input.required<AssistantPanelDefinition>();
  readonly context = input.required<AssistantContext>();
  /** How long the assistant "thinks" before answering, in milliseconds. */
  readonly thinkMs = input(600);

  private readonly i18n = inject(I18nService);
  private readonly injector = inject(Injector);
  private readonly chat = viewChild<ElementRef<HTMLElement>>('chat');
  protected readonly icons = TONE_ICONS;
  protected readonly collapsed = signal(false);
  protected readonly open = signal(new Set<string>());
  protected readonly asked = signal(new Set<string>());
  protected readonly thread = signal<ThreadEntry[]>([]);
  protected readonly thinking = computed(() => this.thread().some((t) => !t.answer));
  protected readonly typed = signal('');

  private readonly texts = computed<AutoNoteTexts>(() => {
    const both = (key: string): LocalizedText => ({
      ar: this.i18n.translate(key, 'ar'),
      en: this.i18n.translate(key, 'en'),
    });
    return {
      missing: both('panel.assistant.auto.missing'),
      conditional: both('panel.assistant.auto.conditional'),
      uploads: both('panel.assistant.auto.uploads'),
      problems: both('panel.assistant.auto.problems'),
      ready: both('panel.assistant.auto.ready'),
    };
  });
  protected readonly view = computed(() =>
    assistantView(this.definition(), this.context(), this.texts()),
  );
  protected readonly message = computed(() => this.i18n.localize(this.view().message));
  protected readonly title = computed(
    () =>
      this.i18n.localize({
        ar: this.definition().titleAr ?? '',
        en: this.definition().titleEn ?? '',
      }) || this.i18n.translate('panel.assistant.title'),
  );
  protected readonly subtitle = computed(() => {
    const configured = this.i18n.localize({
      ar: this.definition().subtitleAr ?? '',
      en: this.definition().subtitleEn ?? '',
    });
    if (configured) return configured;
    const step = this.context().version.definition.steps.find(
      (s) => s.code === this.context().step,
    );
    return step
      ? this.i18n
          .translate('panel.assistant.on')
          .replace('{step}', this.i18n.localize({ ar: step.titleAr, en: step.titleEn }))
      : this.i18n.translate('panel.assistant.subtitle');
  });

  private timers: ReturnType<typeof setTimeout>[] = [];
  private typing: ReturnType<typeof setInterval> | undefined;
  private nextId = 0;

  constructor() {
    // The greeting is typed out each time it changes (at once when motion is reduced).
    effect(() => this.typeOut(this.message()));
    inject(DestroyRef).onDestroy(() => {
      this.timers.forEach(clearTimeout);
      clearInterval(this.typing);
    });
  }

  protected toggleWhy(key: string): void {
    this.open.update((set) => {
      const next = new Set(set);
      if (!next.delete(key)) next.add(key);
      return next;
    });
  }

  protected ask(question: AssistantQuestion): void {
    this.asked.update((set) => new Set(set).add(question.key));
    this.answer(question.question, question.answer);
  }

  protected askTyped(text: string): void {
    const match = matchQuestion(text, this.view().questions, this.i18n.language());
    if (match) this.asked.update((set) => new Set(set).add(match.key));
    this.answer(
      { ar: text, en: text },
      match?.answer ?? {
        ar: this.i18n.translate('panel.assistant.noAnswer', 'ar'),
        en: this.i18n.translate('panel.assistant.noAnswer', 'en'),
      },
    );
  }

  private answer(question: LocalizedText, answer: LocalizedText): void {
    const id = this.nextId++;
    this.thread.update((list) => [...list, { id, question, answer: null }]);
    this.scrollDown();
    this.timers.push(
      setTimeout(() => {
        this.thread.update((list) => list.map((t) => (t.id === id ? { ...t, answer } : t)));
        this.scrollDown();
      }, this.thinkMs()),
    );
  }

  /** Shows the newest question and answer once they are drawn. */
  private scrollDown(): void {
    afterNextRender(
      {
        read: () =>
          this.chat()?.nativeElement.scrollTo({ top: this.chat()!.nativeElement.scrollHeight }),
      },
      { injector: this.injector },
    );
  }

  private typeOut(text: string): void {
    clearInterval(this.typing);
    const reduced =
      typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (!text || reduced || this.thinkMs() === 0) {
      this.typed.set(text);
      return;
    }
    let shown = 0;
    this.typed.set('');
    this.typing = setInterval(() => {
      shown = Math.min(text.length, shown + 2);
      this.typed.set(text.slice(0, shown));
      if (shown >= text.length) clearInterval(this.typing);
    }, 18);
  }
}
