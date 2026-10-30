import { DOCUMENT, Directive, OnDestroy, inject, signal } from '@angular/core';

/**
 * Lets a designer take the whole browser window (over the side menu and header) and give it back: a host directive of
 * the page, toggled by a toolbar button; Escape leaves it when no menu or dialog is open.
 */
@Directive({
  selector: '[dcFullWindow]',
  exportAs: 'dcFullWindow',
  host: {
    '[class.dc-full-window]': 'on()',
    '(document:keydown.escape)': 'leaveOnEscape()',
  },
})
export class FullWindow implements OnDestroy {
  private readonly document = inject(DOCUMENT);
  readonly on = signal(false);

  toggle(): void {
    this.set(!this.on());
  }

  ngOnDestroy(): void {
    this.set(false);
  }

  protected leaveOnEscape(): void {
    // Escape first closes an open select panel, menu or dialog (they live in the overlay container).
    if (
      this.on() &&
      !this.document.querySelector('.cdk-overlay-container .cdk-overlay-pane:not(:empty)')
    ) {
      this.set(false);
    }
  }

  private set(on: boolean): void {
    this.on.set(on);
    this.document.body.classList.toggle('dc-full-window-open', on);
  }
}
