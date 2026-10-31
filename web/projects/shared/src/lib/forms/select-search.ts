import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  output,
  viewChild,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '../i18n/translate.pipe';

/**
 * Whether a choice matches a search: every word typed appears in one of its texts (code, Arabic or English name…),
 * ignoring case. An empty search matches everything.
 */
export function matchesSearch(search: string, ...texts: (string | null | undefined)[]): boolean {
  const words = search
    .trim()
    .toLowerCase()
    .split(/\s+/)
    .filter((w) => w.length > 0);
  if (words.length === 0) return true;
  const haystack = texts
    .filter((t) => !!t)
    .join(' ')
    .toLowerCase();
  return words.every((w) => haystack.includes(w));
}

/**
 * A search box for the top of a long `mat-select` panel: place it as the select's first child, filter the options by
 * its value (keep the selected one), call {@link focus} when the panel opens and clear the search when it closes. Keys
 * typed in it stay in it (the select would otherwise take them to pick options), except Escape and Tab.
 */
@Component({
  selector: 'dc-select-search',
  imports: [MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-ss">
      <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">search</mat-icon>
      <input
        #box
        type="search"
        autocomplete="off"
        [value]="value()"
        [placeholder]="placeholder() ?? ('selectSearch.placeholder' | dcTranslate)"
        [attr.aria-label]="placeholder() ?? ('selectSearch.placeholder' | dcTranslate)"
        [attr.data-testid]="testId()"
        (input)="valueChange.emit(box.value)"
        (keydown)="keydown($event)"
        (click)="$event.stopPropagation()"
      />
    </div>
  `,
  styles: `
    :host {
      display: block;
      position: sticky;
      top: 0;
      z-index: 2;
      background: var(--mat-select-panel-background-color, #fff);
    }
    .dc-ss {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 4px 8px 6px;
      padding: 0 10px;
      border: 1px solid var(--dga-gray-300, #d2d6db);
      border-radius: 8px;
      color: var(--dga-gray-500, #6c737f);
    }
    .dc-ss:focus-within {
      border-color: var(--dynamic-cube-gold-600, #b8862f);
    }
    .dc-ss input {
      flex: 1;
      min-width: 0;
      height: 36px;
      border: 0;
      outline: 0;
      background: transparent;
      color: var(--dga-gray-900, #161616);
      font: inherit;
    }
    .dc-ss input:focus-visible {
      outline: none;
      box-shadow: none;
    }
  `,
})
export class SelectSearch {
  readonly value = input('');
  readonly placeholder = input<string | null>(null);
  readonly testId = input<string | null>(null);
  readonly valueChange = output<string>();
  private readonly box = viewChild.required<ElementRef<HTMLInputElement>>('box');

  /** Puts the cursor in the box (after the select has focused its own panel). */
  focus(): void {
    setTimeout(() => this.box().nativeElement.focus());
  }

  protected keydown(event: KeyboardEvent): void {
    if (event.key !== 'Escape' && event.key !== 'Tab') event.stopPropagation();
  }
}
