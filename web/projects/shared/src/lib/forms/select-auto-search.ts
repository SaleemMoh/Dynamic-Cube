import { DestroyRef, Directive, NgModule, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatOption } from '@angular/material/core';
import { MatSelect, MatSelectModule } from '@angular/material/select';
import { I18nService } from '../i18n/i18n.service';
import { matchesSearch } from './select-search';

/** Lists with at least this many choices get a search box; shorter ones (yes/no, a few states) stay as they are. */
export const SELECT_SEARCH_MIN_OPTIONS = 6;

/**
 * Adds a search box to the top of every `mat-select` panel with {@link SELECT_SEARCH_MIN_OPTIONS} choices or more:
 * typing hides the choices that do not match (by their text), the arrow keys and Enter work from the box, and the search
 * is cleared when the panel closes. Selects that place their own `dc-select-search` keep it; `dcNoSearch` turns it off.
 * It applies to every select of a component that imports {@link SearchableSelectModule} instead of `MatSelectModule`.
 */
// It must reach every mat-select without changing their templates, so it selects the element itself.
// eslint-disable-next-line @angular-eslint/directive-selector
@Directive({ selector: 'mat-select:not([dcNoSearch])' })
export class SelectAutoSearch {
  private readonly select = inject(MatSelect);
  private readonly i18n = inject(I18nService);
  private box: HTMLElement | null = null;
  private input: HTMLInputElement | null = null;
  private empty: HTMLElement | null = null;
  private readonly hidden = new Set<MatOption>();

  constructor() {
    this.select.openedChange
      .pipe(takeUntilDestroyed())
      .subscribe((open) => (open ? setTimeout(() => this.attach()) : this.detach()));
    this.select.options?.changes?.pipe(takeUntilDestroyed()).subscribe(() => this.filter());
    inject(DestroyRef).onDestroy(() => this.detach());
  }

  private attach(): void {
    // The panel is rendered in an overlay: found by its id once the select has opened it.
    const panel = (this.select.panel?.nativeElement ??
      document.getElementById(`${this.select.id}-panel`)) as HTMLElement | null;
    if (
      !this.select.panelOpen ||
      !panel ||
      this.box ||
      (this.select.options?.length ?? 0) < SELECT_SEARCH_MIN_OPTIONS
    )
      return;
    if (panel.querySelector('dc-select-search')) return;

    const doc = panel.ownerDocument;
    const box = doc.createElement('div');
    box.className = 'dc-select-auto-search';
    box.setAttribute(
      'style',
      'position:sticky;top:0;z-index:2;padding:6px 8px;background:var(--mat-select-panel-background-color,#fff);',
    );
    const input = doc.createElement('input');
    input.type = 'search';
    input.autocomplete = 'off';
    input.placeholder = this.i18n.translate('selectSearch.placeholder');
    input.setAttribute('aria-label', input.placeholder);
    input.setAttribute('data-testid', 'select-search');
    input.setAttribute(
      'style',
      'box-sizing:border-box;width:100%;height:36px;padding:0 10px;border:1px solid var(--dga-gray-300,#d2d6db);border-radius:8px;outline:none;font:inherit;color:inherit;background:transparent;',
    );
    input.addEventListener('input', () => this.filter());
    input.addEventListener('keydown', (e) => this.keydown(e));
    input.addEventListener('click', (e) => e.stopPropagation());
    input.addEventListener(
      'focus',
      () => (input.style.borderColor = 'var(--dynamic-cube-gold-600,#b8862f)'),
    );
    input.addEventListener('blur', () => (input.style.borderColor = ''));
    box.appendChild(input);

    const empty = doc.createElement('div');
    empty.textContent = this.i18n.translate('selectSearch.none');
    empty.setAttribute('style', 'display:none;padding:8px 16px;color:var(--dga-gray-500,#6c737f);');
    box.appendChild(empty);

    panel.insertBefore(box, panel.firstChild);
    this.box = box;
    this.input = input;
    this.empty = empty;

    // Hidden choices are skipped by the arrow keys (the select itself skips none while open).
    const manager = this.select._keyManager;
    manager?.skipPredicate((option: MatOption) => this.hidden.has(option));

    setTimeout(() => input.focus());
  }

  private detach(): void {
    if (!this.box) return;
    const hadFocus = this.input && this.input.ownerDocument.activeElement === this.input;
    this.input!.value = '';
    this.filter();
    // Back to the select's own rule: every choice while open, the enabled ones while closed.
    this.select._keyManager?.skipPredicate((option: MatOption) =>
      this.select.panelOpen ? false : option.disabled,
    );
    this.box.remove();
    this.box = this.input = this.empty = null;
    if (hadFocus) this.select.focus();
  }

  /** Shows the choices matching the search (and groups that still hold one). */
  private filter(): void {
    const search = this.input?.value ?? '';
    const options = this.select.options?.toArray() ?? [];
    this.hidden.clear();
    for (const option of options) {
      const element = option._getHostElement();
      const show = matchesSearch(search, option.viewValue, String(option.value ?? ''));
      element.style.display = show ? '' : 'none';
      if (!show) this.hidden.add(option);
    }
    const panel = this.box?.parentElement;
    panel?.querySelectorAll<HTMLElement>('mat-optgroup').forEach((group) => {
      const visible = [...group.querySelectorAll<HTMLElement>('mat-option')].some(
        (o) => o.style.display !== 'none',
      );
      group.style.display = visible ? '' : 'none';
    });
    if (this.empty)
      this.empty.style.display = search && this.hidden.size === options.length ? '' : 'none';

    const manager = this.select._keyManager;
    if (search && manager && (!manager.activeItem || this.hidden.has(manager.activeItem))) {
      manager.setFirstItemActive();
    }
  }

  /** The arrow keys, Home/End and Enter act on the choices; other keys stay in the box; Escape and Tab close the panel. */
  private keydown(event: KeyboardEvent): void {
    const manager = this.select._keyManager;
    if (event.key === 'Escape' || event.key === 'Tab') return;
    event.stopPropagation();
    if (!manager) return;
    if (['ArrowDown', 'ArrowUp', 'PageDown', 'PageUp'].includes(event.key)) {
      event.preventDefault();
      manager.onKeydown(event);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const active = manager.activeItem;
      if (active && !this.hidden.has(active) && !active.disabled) active._selectViaInteraction();
    }
  }
}

/** `MatSelectModule` with a search box in every long list (see {@link SelectAutoSearch}). */
@NgModule({
  imports: [MatSelectModule, SelectAutoSearch],
  exports: [MatSelectModule, SelectAutoSearch],
})
export class SearchableSelectModule {}
