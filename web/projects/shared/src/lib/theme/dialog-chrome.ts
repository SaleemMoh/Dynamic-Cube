import { DOCUMENT, Injectable, OnDestroy, inject } from '@angular/core';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { Subscription, take } from 'rxjs';
import { I18nService } from '../i18n/i18n.service';

/** A dialog opened at least this wide (in CSS pixels) is a big one and can fill the screen. */
export const BIG_DIALOG_WIDTH = 720;

/** Below this window width dialogs are already about as wide as the screen: they are neither moved nor maximised. */
const SMALL_SCREEN = 700;

/** A dialog opened with this panel class keeps its place and size (no moving, no full screen). */
export const FIXED_DIALOG_CLASS = 'dc-dialog-fixed';

/** Panel class of a dialog filling the screen (also set by designers that open full screen). */
export const MAX_DIALOG_CLASS = 'dc-dialog-max';

const INTERACTIVE =
  'button, a, input, select, textarea, [role="button"], [role="tab"], .mat-mdc-tab, mat-button-toggle';

interface Chrome {
  ref: MatDialogRef<unknown>;
  pane: HTMLElement;
  handle: HTMLElement;
  x: number;
  y: number;
  max: boolean;
  /** The size the dialog had before filling the screen. */
  size: { width: string; height: string };
  button: HTMLButtonElement | null;
  cleanup: (() => void)[];
}

/**
 * Every dialog of the app can be moved by dragging its title bar, and a big dialog (a designer, anything opened at least
 * {@link BIG_DIALOG_WIDTH} wide) can fill the screen: a button at the end of its title bar, or a double click on the
 * title bar, switches it, and again gives it back its size and place. Set up once for the app; dialogs need nothing.
 */
@Injectable({ providedIn: 'root' })
export class DialogChrome implements OnDestroy {
  private readonly dialog = inject(MatDialog);
  private readonly document = inject(DOCUMENT);
  private readonly i18n = inject(I18nService);
  private subscription: Subscription | null = null;

  start(): void {
    // Once the dialog has opened (its content drawn and sized), not when it is created.
    this.subscription ??= this.dialog.afterOpened.subscribe((ref) =>
      ref
        .afterOpened()
        .pipe(take(1))
        .subscribe(() => this.attach(ref)),
    );
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
  }

  private attach(ref: MatDialogRef<unknown>): void {
    const view = this.document.defaultView;
    const container = this.document.getElementById(ref.id);
    const pane = container?.closest<HTMLElement>('.cdk-overlay-pane');
    if (
      !view ||
      !container ||
      !pane ||
      pane.classList.contains(FIXED_DIALOG_CLASS) ||
      view.innerWidth < SMALL_SCREEN
    )
      return;
    const handle =
      container.querySelector<HTMLElement>(
        '.mat-mdc-dialog-title, [mat-dialog-title], [matDialogTitle]',
      ) ??
      container.querySelector<HTMLElement>('header') ??
      container.querySelector<HTMLElement>('.mat-mdc-dialog-surface > * > :first-child');
    if (!handle) return;

    const chrome: Chrome = {
      ref,
      pane,
      handle,
      x: 0,
      y: 0,
      max: pane.classList.contains(MAX_DIALOG_CLASS),
      size: { width: pane.style.width, height: pane.style.height },
      button: null,
      cleanup: [],
    };
    handle.classList.add('dc-dialog-handle');
    this.draggable(chrome, view);
    if (chrome.max || pane.getBoundingClientRect().width >= BIG_DIALOG_WIDTH)
      this.maximizable(chrome);
    ref.afterClosed().subscribe(() => chrome.cleanup.forEach((undo) => undo()));
  }

  /** Dragging the title bar moves the dialog, keeping its title bar on the screen. */
  private draggable(chrome: Chrome, view: Window): void {
    const down = (event: PointerEvent) => {
      if (event.button !== 0 || chrome.max || (event.target as Element).closest(INTERACTIVE))
        return;
      event.preventDefault();
      const start = { px: event.clientX, py: event.clientY, x: chrome.x, y: chrome.y };
      const rect = chrome.pane.getBoundingClientRect();
      const base = { left: rect.left - chrome.x, top: rect.top - chrome.y };
      const move = (e: PointerEvent) => {
        const x = start.x + e.clientX - start.px;
        const y = start.y + e.clientY - start.py;
        // Keep the dialog across the screen (its title bar buttons stay reachable) and its title bar down on it.
        const room = view.innerWidth - rect.width;
        const left = Math.min(Math.max(base.left + x, Math.min(0, room)), Math.max(0, room));
        const top = Math.min(Math.max(base.top + y, 0), view.innerHeight - 48);
        chrome.x = left - base.left;
        chrome.y = top - base.top;
        chrome.pane.style.transform = `translate(${chrome.x}px, ${chrome.y}px)`;
      };
      const up = () => {
        this.document.removeEventListener('pointermove', move);
        this.document.removeEventListener('pointerup', up);
        this.document.body.classList.remove('dc-dialog-dragging');
      };
      this.document.body.classList.add('dc-dialog-dragging');
      this.document.addEventListener('pointermove', move);
      this.document.addEventListener('pointerup', up);
    };
    chrome.handle.addEventListener('pointerdown', down);
    chrome.cleanup.push(() => chrome.handle.removeEventListener('pointerdown', down));
  }

  /** A button at the end of the title bar (and a double click on it) fills the screen and gives the dialog back. */
  private maximizable(chrome: Chrome): void {
    const button = this.document.createElement('button');
    button.type = 'button';
    button.className = 'dc-dialog-max-toggle';
    button.setAttribute('data-testid', 'dialog-full-screen');
    const icon = this.document.createElement('span');
    icon.className = 'material-symbols-outlined';
    icon.setAttribute('aria-hidden', 'true');
    button.appendChild(icon);
    chrome.button = button;
    // Before a close button at the end of the title bar, else at its end.
    const last = chrome.handle.lastElementChild;
    if (last?.matches('button, [mat-dialog-close]')) chrome.handle.insertBefore(button, last);
    else chrome.handle.appendChild(button);
    chrome.handle.classList.add('dc-dialog-handle--max');
    this.label(chrome);

    const toggle = () => this.toggle(chrome);
    const double = (event: MouseEvent) => {
      if (!(event.target as Element).closest(INTERACTIVE)) toggle();
    };
    button.addEventListener('click', toggle);
    chrome.handle.addEventListener('dblclick', double);
    chrome.cleanup.push(() => {
      button.removeEventListener('click', toggle);
      chrome.handle.removeEventListener('dblclick', double);
    });
  }

  private toggle(chrome: Chrome): void {
    chrome.max = !chrome.max;
    if (chrome.max) {
      chrome.size = { width: chrome.pane.style.width, height: chrome.pane.style.height };
      chrome.pane.style.transform = '';
      chrome.ref.updateSize('100vw', '100vh');
      chrome.ref.addPanelClass(MAX_DIALOG_CLASS);
    } else {
      chrome.ref.removePanelClass(MAX_DIALOG_CLASS);
      chrome.ref.updateSize(chrome.size.width || '', chrome.size.height || '');
      chrome.pane.style.transform =
        chrome.x || chrome.y ? `translate(${chrome.x}px, ${chrome.y}px)` : '';
    }
    this.label(chrome);
  }

  private label(chrome: Chrome): void {
    if (!chrome.button) return;
    const ar = this.i18n.language() === 'ar';
    const text = chrome.max
      ? ar
        ? 'الخروج من ملء الشاشة'
        : 'Exit full screen'
      : ar
        ? 'ملء الشاشة'
        : 'Full screen';
    chrome.button.setAttribute('aria-label', text);
    chrome.button.title = text;
    chrome.button.firstElementChild!.textContent = chrome.max ? 'close_fullscreen' : 'open_in_full';
  }
}
