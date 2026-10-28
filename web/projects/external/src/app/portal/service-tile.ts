import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { LocalizePipe, PortalServiceSummary, TranslatePipe } from '@dynamic-cube/shared';
import { PortalStore } from './portal-store';
import { PARTY_KIND_KEYS } from './portal-text';

/** A service as a card: icon, name, short description, audience and category tags, and an arrow to its card page. */
@Component({
  selector: 'dc-service-tile',
  imports: [MatIconModule, RouterLink, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let s = service();
    <a class="dc-tile" [routerLink]="['/services', s.code]" [attr.data-testid]="'tile-' + s.code">
      <span class="dc-tile__icon" aria-hidden="true">
        <mat-icon fontSet="material-symbols-outlined">{{ s.icon || 'description' }}</mat-icon>
      </span>
      <h3 class="dc-tile__name">{{ { ar: s.nameAr, en: s.nameEn } | dcLocalize }}</h3>
      @if (s.descriptionAr || s.descriptionEn) {
        <p class="dc-tile__description">{{ { ar: s.descriptionAr ?? '', en: s.descriptionEn ?? '' } | dcLocalize }}</p>
      }
      <span class="dc-tile__tags">
        @for (kind of s.partyKinds; track kind) {
          <span class="dc-tile__tag">{{ kindKeys[kind] | dcTranslate }}</span>
        }
        @if (category(); as c) {
          <span class="dc-tile__tag dc-tile__tag--category">{{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}</span>
        }
      </span>
      <span class="dc-tile__arrow" aria-hidden="true">
        <mat-icon fontSet="material-symbols-outlined">arrow_back</mat-icon>
      </span>
      @if (note()) {
        <span class="dc-tile__note">{{ note() }}</span>
      }
    </a>
  `,
  styles: `
    :host {
      display: block;
      height: 100%;
    }
    .dc-tile {
      position: relative;
      display: flex;
      flex-direction: column;
      gap: 12px;
      height: 100%;
      min-height: 15rem;
      padding: 24px;
      border: 1px solid var(--dga-border);
      border-radius: 16px;
      background: #ffffff;
      color: var(--dga-text);
      text-decoration: none;
      transition:
        box-shadow 0.15s ease,
        border-color 0.15s ease;
    }
    .dc-tile:hover,
    .dc-tile:focus-visible {
      border-color: var(--dga-green-400);
      box-shadow: 0 8px 24px rgb(16 24 40 / 8%);
    }
    .dc-tile:focus-visible {
      outline: 2px solid var(--dga-green-700);
      outline-offset: 2px;
    }
    .dc-tile__icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 48px;
      height: 48px;
      border-radius: 12px;
      background: var(--dga-green-50);
      color: var(--dga-green-700);
    }
    .dc-tile__icon mat-icon {
      font-size: 28px;
      width: 28px;
      height: 28px;
    }
    .dc-tile__name {
      margin: 0;
      font-size: 18px;
      font-weight: 700;
      line-height: 28px;
    }
    .dc-tile__description {
      display: -webkit-box;
      margin: 0;
      overflow: hidden;
      color: var(--dga-text-3);
      font-size: 14px;
      line-height: 22px;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
    }
    .dc-tile__tags {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
      margin-top: auto;
      padding-inline-end: 44px;
    }
    .dc-tile__tag {
      padding: 2px 10px;
      border-radius: 999px;
      background: var(--dga-gray-100);
      color: var(--dga-text-2);
      font-size: 12px;
      line-height: 20px;
    }
    .dc-tile__tag--category {
      background: var(--dga-green-50);
      color: var(--dga-green-800);
    }
    .dc-tile__arrow {
      position: absolute;
      inset-inline-end: 20px;
      bottom: 20px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 36px;
      height: 36px;
      border-radius: 50%;
      background: var(--dga-green-700);
      color: #ffffff;
    }
    :host-context([dir='ltr']) .dc-tile__arrow mat-icon {
      transform: scaleX(-1);
    }
    .dc-tile__note {
      color: #b54708;
      font-size: 13px;
    }
  `,
})
export class ServiceTile {
  readonly service = input.required<PortalServiceSummary>();
  /** A line under the tags, e.g. why the acting party cannot use the service. */
  readonly note = input<string | null>(null);

  private readonly store = inject(PortalStore);
  protected readonly kindKeys = PARTY_KIND_KEYS;
  protected readonly category = computed(() => this.store.category(this.service().categoryCode));
}
