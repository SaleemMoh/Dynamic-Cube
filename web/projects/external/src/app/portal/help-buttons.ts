import { DOCUMENT, ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { TranslatePipe } from '@dynamic-cube/shared';
import { PortalStore } from './portal-store';

const STORAGE_KEY = 'dc.accessibility';

interface Accessibility {
  /** Zoom step: 0 is normal, each step enlarges the page by 12.5%. */
  size: number;
  contrast: boolean;
}

/**
 * Floating help on every portal page: contact (phone, email, contact page from the portal settings) and accessibility
 * tools (larger text, high contrast), remembered on this device.
 */
@Component({
  selector: 'dc-help-buttons',
  imports: [MatButtonModule, MatIconModule, MatMenuModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-help">
      @if (help().phone || help().email || help().contactUrl) {
        <button class="dc-help__button" [matMenuTriggerFor]="contact" [attr.aria-label]="'portal.contact' | dcTranslate" data-testid="help-contact">
          <mat-icon fontSet="material-symbols-outlined">support_agent</mat-icon>
        </button>
      }
      <button class="dc-help__button" [matMenuTriggerFor]="tools" [attr.aria-label]="'portal.accessibility' | dcTranslate" data-testid="help-accessibility">
        <mat-icon fontSet="material-symbols-outlined">accessibility_new</mat-icon>
      </button>
    </div>

    <mat-menu #contact="matMenu" xPosition="before">
      @if (help().phone; as phone) {
        <a mat-menu-item [href]="'tel:' + phone"><mat-icon fontSet="material-symbols-outlined">call</mat-icon><span dir="ltr">{{ phone }}</span></a>
      }
      @if (help().email; as email) {
        <a mat-menu-item [href]="'mailto:' + email"><mat-icon fontSet="material-symbols-outlined">mail</mat-icon><span dir="ltr">{{ email }}</span></a>
      }
      @if (help().contactUrl; as url) {
        <a mat-menu-item [href]="url" target="_blank" rel="noopener"><mat-icon fontSet="material-symbols-outlined">forum</mat-icon>{{ 'portal.contactUs' | dcTranslate }}</a>
      }
    </mat-menu>

    <mat-menu #tools="matMenu" xPosition="before">
      <button mat-menu-item (click)="$event.stopPropagation(); resize(1)" [disabled]="settings().size >= 3" data-testid="text-larger">
        <mat-icon fontSet="material-symbols-outlined">text_increase</mat-icon>{{ 'portal.textLarger' | dcTranslate }}
      </button>
      <button mat-menu-item (click)="$event.stopPropagation(); resize(-1)" [disabled]="settings().size <= 0">
        <mat-icon fontSet="material-symbols-outlined">text_decrease</mat-icon>{{ 'portal.textSmaller' | dcTranslate }}
      </button>
      <button mat-menu-item (click)="toggleContrast()" data-testid="contrast">
        <mat-icon fontSet="material-symbols-outlined">contrast</mat-icon>{{ (settings().contrast ? 'portal.contrastOff' : 'portal.contrastOn') | dcTranslate }}
      </button>
      @if (help().accessibilityUrl; as url) {
        <a mat-menu-item [href]="url" target="_blank" rel="noopener"><mat-icon fontSet="material-symbols-outlined">info</mat-icon>{{ 'portal.accessibilityStatement' | dcTranslate }}</a>
      }
    </mat-menu>
  `,
  styles: `
    .dc-help {
      position: fixed;
      inset-inline-start: 24px;
      bottom: 24px;
      z-index: 100;
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    .dc-help__button {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 64px;
      height: 64px;
      border: none;
      border-radius: 50%;
      background: var(--dga-green-700);
      color: #ffffff;
      box-shadow: 0 6px 16px rgb(7 77 49 / 30%);
      cursor: pointer;
    }
    .dc-help__button:hover {
      background: var(--dga-green-800);
    }
    .dc-help__button:focus-visible {
      outline: 3px solid var(--dga-green-400);
      outline-offset: 2px;
    }
  `,
})
export class HelpButtons {
  private readonly document = inject(DOCUMENT);
  protected readonly help = inject(PortalStore).help;
  protected readonly settings = signal<Accessibility>(this.read());

  constructor() {
    effect(() => {
      const { size, contrast } = this.settings();
      const root = this.document.documentElement;
      // Sizes are in pixels (DGA), so the whole page is zoomed rather than the root font size changed.
      this.document.body.style.zoom = size ? String(1 + size * 0.125) : '';
      root.classList.toggle('dc-high-contrast', contrast);
      try {
        this.document.defaultView?.localStorage.setItem(STORAGE_KEY, JSON.stringify({ size, contrast }));
      } catch {
        // Storage can be unavailable (private mode); the choice still applies on this page.
      }
    });
  }

  protected resize(step: number): void {
    this.settings.update((s) => ({ ...s, size: Math.min(3, Math.max(0, s.size + step)) }));
  }

  protected toggleContrast(): void {
    this.settings.update((s) => ({ ...s, contrast: !s.contrast }));
  }

  private read(): Accessibility {
    try {
      const stored = JSON.parse(this.document.defaultView?.localStorage.getItem(STORAGE_KEY) ?? 'null') as Partial<Accessibility> | null;
      return { size: Number(stored?.size) || 0, contrast: stored?.contrast === true };
    } catch {
      return { size: 0, contrast: false };
    }
  }
}
