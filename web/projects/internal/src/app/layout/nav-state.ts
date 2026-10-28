import { Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'dc.internal.nav.open';

/**
 * Whether the side navigation is open; shared by the top bar's toggle and the workspace. On wide screens the choice
 * is remembered in the browser; on narrow ones the menu slides over the page and starts closed.
 */
@Injectable({ providedIn: 'root' })
export class NavState {
  readonly open = signal(read());
  /** True while the menu slides over the page (narrow screens). */
  readonly overlay = signal(false);

  toggle(): void {
    this.set(!this.open());
  }

  set(open: boolean): void {
    this.open.set(open);
    if (this.overlay()) {
      return;
    }
    try {
      localStorage.setItem(STORAGE_KEY, String(open));
    } catch {
      // Storage may be unavailable (private mode); the menu then opens on every visit.
    }
  }

  /** Switches between beside-the-page and over-the-page; over the page the menu starts closed. */
  setOverlay(overlay: boolean): void {
    this.overlay.set(overlay);
    this.open.set(overlay ? false : read());
  }
}

function read(): boolean {
  try {
    return localStorage.getItem(STORAGE_KEY) !== 'false';
  } catch {
    return true;
  }
}
