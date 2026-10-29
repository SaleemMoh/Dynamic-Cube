import { JsonPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { AuthImageSrc, TranslatePipe } from '@dynamic-cube/shared';
import { DiffEntry, diffPayload } from './payload-diff';

/** Branding images are stored as asset keys; show the pictures rather than the keys. */
const IMAGE_FIELDS = new Set(['logoFileKey', 'footerImageKey', 'loginImageKey']);
const MAX_ROWS = 200;

/** A row before and after a change, field by field: removed values on one side, new values on the other. */
@Component({
  selector: 'dc-payload-diff',
  imports: [JsonPipe, AuthImageSrc, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-diff" dir="ltr">
      @for (entry of shown(); track entry.path) {
        <div class="dc-diff__row" [attr.data-kind]="entry.kind" [attr.data-path]="entry.path">
          <span class="dc-diff__field">{{ entry.path }}</span>
          @if (isImage(entry.path)) {
            <span class="dc-diff__before">
              @if (entry.before) {
                <img class="dc-diff__img" [dcAuthSrc]="assetUrl(entry.before)" alt="" />
              }
            </span>
            <span class="dc-diff__after">
              @if (entry.after) {
                <img class="dc-diff__img" [dcAuthSrc]="assetUrl(entry.after)" alt="" />
              }
            </span>
          } @else {
            <code class="dc-diff__before">{{ entry.kind === 'added' ? '' : (entry.before | json) }}</code>
            <code class="dc-diff__after">{{ entry.kind === 'removed' ? '' : (entry.after | json) }}</code>
          }
        </div>
      } @empty {
        <span class="dc-muted">{{ 'changeSets.noFieldChanges' | dcTranslate }}</span>
      }
      @if (hidden()) {
        <span class="dc-muted">+{{ hidden() }}</span>
      }
    </div>
  `,
  styles: `
    .dc-diff__row {
      display: grid;
      grid-template-columns: minmax(8rem, 14rem) 1fr 1fr;
      gap: 0.75rem;
      padding-block: 0.25rem;
      border-block-end: 1px solid var(--mat-sys-outline-variant);
      font-size: 13px;
    }
    .dc-diff__field {
      font-weight: 600;
      word-break: break-all;
    }
    .dc-diff__before,
    .dc-diff__after {
      max-height: 12rem;
      overflow: auto;
      white-space: pre-wrap;
      word-break: break-word;
    }
    .dc-diff__before {
      color: var(--mat-sys-error);
    }
    .dc-diff__after {
      color: var(--dc-diff-after, #067647);
    }
    [data-kind='removed'] .dc-diff__before {
      text-decoration: line-through;
    }
    .dc-diff__img {
      max-height: 3rem;
      max-width: 12rem;
    }
  `,
})
export class PayloadDiffView {
  readonly before = input<Record<string, unknown> | null | undefined>(null);
  readonly after = input<Record<string, unknown> | null | undefined>(null);
  /** How deep to compare field by field; a new row reads best by its top-level fields. */
  readonly depth = input(6);

  protected readonly entries = computed<DiffEntry[]>(() => diffPayload(this.before(), this.after(), { depth: this.depth() }));
  protected readonly shown = computed(() => this.entries().slice(0, MAX_ROWS));
  protected readonly hidden = computed(() => Math.max(0, this.entries().length - MAX_ROWS));

  protected isImage(path: string): boolean {
    return IMAGE_FIELDS.has(path);
  }

  protected assetUrl(key: unknown): string {
    return `/api/apps-control/v1/branding/assets/${String(key)}`;
  }
}
