import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe } from '@dynamic-cube/shared';
import { responseTree } from './operation-designer-model';

/** A provider's response as a tree: each field shows its value, and clicking it makes it an output (its path is read). */
@Component({
  selector: 'dc-response-tree',
  imports: [MatIconModule, MatTooltipModule, NgTemplateOutlet, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (tree(); as root) {
      <ul class="rt-list rt-list--root">
        @for (node of root.children; track node.path) {
          <ng-container *ngTemplateOutlet="nodeTpl; context: { $implicit: node }" />
        }
      </ul>
      @if (root.kind !== 'object') {
        <p class="rt-muted">{{ 'od.rootNotObject' | dcTranslate }}</p>
      }
    } @else {
      <p class="rt-muted">{{ 'od.noResponse' | dcTranslate }}</p>
    }

    <ng-template #nodeTpl let-node>
      <li>
        <div class="rt-node" [class.rt-node--picked]="picked().has(node.path)">
          <span class="rt-key" dir="ltr"
            >{{ node.key }}
            @if (node.kind === 'array') {
              <small>[*]</small>
            }
          </span>
          <span class="rt-preview" dir="ltr">{{ node.preview }}</span>
          <button
            type="button"
            class="rt-pick"
            (click)="pick.emit(node.path)"
            [disabled]="picked().has(node.path)"
            [attr.data-pick]="node.path"
            [attr.aria-label]="('od.pick' | dcTranslate) + ' ' + node.path"
            [matTooltip]="node.path"
          >
            <mat-icon fontSet="material-symbols-outlined">{{
              picked().has(node.path) ? 'check' : 'add'
            }}</mat-icon>
          </button>
        </div>
        @if (node.children.length) {
          <ul class="rt-list">
            @for (child of node.children; track child.path) {
              <ng-container *ngTemplateOutlet="nodeTpl; context: { $implicit: child }" />
            }
          </ul>
        }
      </li>
    </ng-template>
  `,
  styles: `
    :host {
      display: block;
    }
    .rt-list {
      margin: 0;
      padding-inline-start: 14px;
      list-style: none;
      border-inline-start: 1px dashed var(--dga-gray-200, #e5e7eb);
    }
    .rt-list--root {
      padding-inline-start: 0;
      border: 0;
    }
    .rt-node {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 2px 4px;
      border-radius: 6px;
      font-size: 13px;
    }
    .rt-node:hover {
      background: var(--dga-gray-50, #f9fafb);
    }
    .rt-node--picked {
      background: #ecfdf3;
    }
    .rt-key,
    .rt-preview {
      font-family: ui-monospace, monospace;
    }
    .rt-key {
      font-weight: 600;
    }
    .rt-key small,
    .rt-preview,
    .rt-muted {
      color: var(--dga-text-3, #6c737f);
      font-weight: 400;
    }
    .rt-preview {
      flex: 1;
      min-width: 0;
      overflow: hidden;
      font-size: 12px;
      white-space: nowrap;
      text-overflow: ellipsis;
    }
    .rt-muted {
      margin: 0;
      font-size: 13px;
    }
    .rt-pick {
      all: unset;
      display: grid;
      place-items: center;
      width: 24px;
      height: 24px;
      border-radius: 6px;
      cursor: pointer;
      color: var(--dynamic-cube-gold-dark, #8a6d2f);
    }
    .rt-pick:hover:not(:disabled) {
      background: #f8f1e1;
    }
    .rt-pick:disabled {
      color: #067647;
      cursor: default;
    }
    .rt-pick:focus-visible {
      outline: 2px solid var(--dynamic-cube-gold, #ca9f45);
    }
    .rt-pick mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
  `,
})
export class ResponseTree {
  /** The response (or the part the envelope reads as the result). */
  readonly value = input<unknown>(undefined);
  /** Paths already read by an output. */
  readonly picked = input<ReadonlySet<string>>(new Set());
  readonly pick = output<string>();

  protected readonly tree = computed(() => {
    const value = this.value();
    return value === undefined || value === null ? null : responseTree(value);
  });
}
