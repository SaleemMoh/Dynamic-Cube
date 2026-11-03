import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { IntegrationEvidence, LocalizePipe, TranslatePipe } from '@dynamic-cube/shared';

/**
 * The request's integration evidence: every call made for it (when, which operation, the outcome and code), with the
 * masked request and response a reviewer can open.
 */
@Component({
  selector: 'dc-request-evidence',
  imports: [DatePipe, MatButtonModule, MatIconModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (calls().length) {
      <section class="dc-card dc-evidence" data-testid="request-evidence">
        <h2>
          <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">fact_check</mat-icon>
          {{ 'evidence.title' | dcTranslate }}
          <span class="dc-muted">({{ calls().length }})</span>
        </h2>
        <p class="dc-muted">{{ 'evidence.lead' | dcTranslate }}</p>
        @if (cached() > 0) {
          <button matButton type="button" (click)="all.set(!all())" data-testid="evidence-cached">
            {{ (all() ? 'evidence.hideCached' : 'evidence.showCached') | dcTranslate }} ({{
              cached()
            }})
          </button>
        }
        <ul>
          @for (c of shown(); track c.id) {
            <li [attr.data-testid]="'evidence-' + c.operationCode">
              <button
                type="button"
                class="dc-evidence__row"
                (click)="toggle(c.id)"
                [attr.aria-expanded]="open() === c.id"
              >
                <span
                  class="dc-badge"
                  [class.dc-badge--success]="c.outcome === 'SUCCESS'"
                  [class.dc-badge--danger]="c.outcome === 'TECHNICAL_ERROR'"
                >
                  {{ 'evidence.outcome.' + c.outcome | dcTranslate }}
                </span>
                <strong>{{ { ar: c.operationNameAr, en: c.operationNameEn } | dcLocalize }}</strong>
                <span class="dc-muted mono">{{ c.code ?? c.httpStatus ?? '' }}</span>
                <span class="dc-evidence__at" dir="ltr">{{
                  c.at | date: 'yyyy-MM-dd HH:mm:ss'
                }}</span>
                <mat-icon aria-hidden="true">{{
                  open() === c.id ? 'expand_less' : 'expand_more'
                }}</mat-icon>
              </button>
              @if (open() === c.id) {
                <div class="dc-evidence__detail">
                  <p class="dc-muted" dir="ltr">
                    <span class="mono">{{ c.method }} {{ c.url }}</span> · {{ c.durationMs }} ms
                    @if (c.fromCache) {
                      · {{ 'evidence.cache' | dcTranslate }}
                    }
                    · {{ c.userId }}
                  </p>
                  @if (c.message) {
                    <p dir="auto">{{ c.message }}</p>
                  }
                  <div class="dc-evidence__bodies">
                    <div>
                      <h3>{{ 'evidence.request' | dcTranslate }}</h3>
                      <pre dir="ltr">{{ pretty(c.requestBody) }}</pre>
                    </div>
                    <div>
                      <h3>{{ 'evidence.response' | dcTranslate }}</h3>
                      <pre dir="ltr">{{ pretty(c.responseBody) }}</pre>
                    </div>
                  </div>
                </div>
              }
            </li>
          }
        </ul>
      </section>
    }
  `,
  styles: `
    .dc-evidence h2 {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0 0 4px;
      font-size: 16px;
    }
    .dc-evidence ul {
      display: grid;
      gap: 6px;
      margin: 8px 0 0;
      padding: 0;
      list-style: none;
    }
    .dc-evidence li {
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius);
    }
    .dc-evidence__row {
      display: grid;
      grid-template-columns: auto 1fr auto auto auto;
      gap: 10px;
      align-items: center;
      width: 100%;
      padding: 8px 10px;
      border: 0;
      background: none;
      font: inherit;
      text-align: start;
      cursor: pointer;
    }
    .dc-evidence__at {
      font-size: 12px;
      color: var(--dga-gray-600);
    }
    .dc-evidence__detail {
      padding: 0 10px 10px;
    }
    .dc-evidence__bodies {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr));
      gap: 10px;
    }
    .dc-evidence__bodies h3 {
      margin: 0 0 4px;
      font-size: 13px;
    }
    .dc-evidence pre {
      max-height: 18rem;
      margin: 0;
      padding: 8px;
      overflow: auto;
      border-radius: 6px;
      background: var(--dga-gray-50);
      font-size: 12px;
      white-space: pre-wrap;
      word-break: break-word;
    }
  `,
})
export class RequestEvidence {
  readonly calls = input<IntegrationEvidence[]>([]);
  protected readonly open = signal<string | null>(null);
  /** Repeats answered from the cache are hidden unless asked for (they add nothing new). */
  protected readonly all = signal(false);
  protected readonly cached = computed(() => this.calls().filter((c) => c.fromCache).length);
  protected readonly shown = computed(() => {
    const fresh = this.calls().filter((c) => !c.fromCache);
    return this.all() || fresh.length === 0 ? this.calls() : fresh;
  });

  protected toggle(id: string): void {
    this.open.update((current) => (current === id ? null : id));
  }

  /** The body indented when it is JSON, else as it was logged. */
  protected pretty(body: string | null): string {
    if (!body) return '—';
    try {
      return JSON.stringify(JSON.parse(body), null, 2);
    } catch {
      return body;
    }
  }
}
