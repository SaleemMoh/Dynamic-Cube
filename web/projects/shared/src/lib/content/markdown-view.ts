import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { renderMarkdown } from './markdown';

/** Shows Markdown (see {@link renderMarkdown}); Angular's sanitizer checks the result once more. */
@Component({
  selector: 'dc-markdown',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="dc-markdown" [innerHTML]="html()"></div>`,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .dc-markdown {
      line-height: 1.75;
      overflow-wrap: anywhere;
    }
    .dc-markdown :is(h2, h3, h4, h5) {
      margin: 1.25em 0 0.5em;
      line-height: 1.4;
    }
    .dc-markdown h2 {
      font-size: 20px;
    }
    .dc-markdown h3 {
      font-size: 18px;
    }
    .dc-markdown :is(h4, h5) {
      font-size: 16px;
    }
    .dc-markdown > :first-child {
      margin-top: 0;
    }
    .dc-markdown p,
    .dc-markdown ul,
    .dc-markdown ol {
      margin: 0 0 0.75em;
    }
    .dc-markdown :is(ul, ol) {
      padding-inline-start: 1.5em;
    }
    .dc-markdown blockquote {
      margin: 0 0 0.75em;
      padding: 0.5em 1em;
      border-inline-start: 4px solid var(--dga-green-600, #1b8354);
      background: var(--dga-gray-50, #f9fafb);
    }
    .dc-markdown hr {
      border: 0;
      border-top: 1px solid var(--dga-border, #d2d6db);
      margin: 1.25em 0;
    }
    .dc-markdown code {
      padding: 0 0.25em;
      border-radius: 4px;
      background: var(--dga-gray-100, #f3f4f6);
      font-size: 0.9em;
    }
    .dc-markdown a {
      color: var(--dga-green-700, #166a45);
    }
  `,
})
export class MarkdownView {
  readonly source = input<string | null | undefined>('');
  protected readonly html = computed(() => renderMarkdown(this.source()));
}
