import { DOCUMENT, ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { AuthService, ContentApi, FeedbackSummary, I18nService, TranslatePipe } from '@dynamic-cube/shared';
import { catchError, firstValueFrom, of } from 'rxjs';

const VISITOR_KEY = 'dc.visitor';

/**
 * "Was this page helpful? Yes / No" with how many said yes (DGA). Signed-in users answer as themselves; other visitors
 * by a random id this browser keeps, so each answers once and may change the answer.
 */
@Component({
  selector: 'dc-page-feedback',
  imports: [MatButtonModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="dc-helpful" data-testid="page-feedback">
      <div class="dc-helpful__ask">
        <span>{{ (answered() ? 'feedback.thanks' : 'feedback.question') | dcTranslate }}</span>
        <button
          matButton="filled"
          type="button"
          [class.dc-helpful__chosen]="summary()?.mine === true"
          (click)="answer(true)"
          [disabled]="busy()"
          data-testid="helpful-yes"
        >
          {{ 'feedback.yes' | dcTranslate }}
        </button>
        <button
          matButton="filled"
          type="button"
          [class.dc-helpful__chosen]="summary()?.mine === false"
          (click)="answer(false)"
          [disabled]="busy()"
          data-testid="helpful-no"
        >
          {{ 'feedback.no' | dcTranslate }}
        </button>
      </div>
      @if (summary(); as s) {
        @if (s.total) {
          <span class="dc-helpful__share" data-testid="helpful-share">{{ share(s) }}</span>
        }
      }
    </section>
  `,
  styles: `
    .dc-helpful {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 12px 16px;
      padding-block: 16px;
      border-top: 2px solid var(--dga-green-700);
    }
    .dc-helpful__ask {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 16px;
      font-size: 16px;
    }
    .dc-helpful__ask button {
      min-width: 56px;
      height: 32px;
    }
    .dc-helpful__chosen {
      box-shadow: 0 0 0 3px var(--dga-green-400, #54c08a);
    }
    .dc-helpful__share {
      color: var(--dga-text-2);
      font-size: 14px;
    }
  `,
})
export class PageFeedback {
  /** Which page: `service:CODE`, `faq`, `page:slug`… */
  readonly page = input.required<string>();

  private readonly api = inject(ContentApi);
  private readonly auth = inject(AuthService);
  private readonly document = inject(DOCUMENT);
  private readonly i18n = inject(I18nService);
  private readonly visitor = this.auth.isAuthenticated() ? null : this.visitorId();

  protected readonly busy = signal(false);
  protected readonly answered = signal(false);
  private readonly loaded = rxResource({
    params: () => this.page(),
    stream: ({ params }) => this.api.feedback(params, this.visitor).pipe(catchError(() => of(null))),
  });
  private readonly latest = signal<FeedbackSummary | null>(null);
  protected readonly summary = computed(() => this.latest() ?? this.loaded.value() ?? null);

  protected share(s: FeedbackSummary): string {
    const percent = Math.round((s.helpful * 100) / s.total);
    return this.i18n.translate('feedback.share').replace('{percent}', String(percent)).replace('{count}', String(s.total));
  }

  protected async answer(helpful: boolean): Promise<void> {
    this.busy.set(true);
    try {
      this.latest.set(await firstValueFrom(this.api.answerFeedback(this.page(), helpful, this.visitor)));
      this.answered.set(true);
    } catch {
      // Feedback is optional; a failed answer leaves the question as it was.
    } finally {
      this.busy.set(false);
    }
  }

  /** A random id this browser keeps for anonymous answers. */
  private visitorId(): string | null {
    try {
      const storage = this.document.defaultView?.localStorage;
      let id = storage?.getItem(VISITOR_KEY) ?? null;
      if (!id) {
        id = crypto.randomUUID();
        storage?.setItem(VISITOR_KEY, id);
      }
      return id;
    } catch {
      return crypto.randomUUID();
    }
  }
}
