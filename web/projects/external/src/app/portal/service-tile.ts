import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { LocalizePipe, PortalServiceSummary, TranslatePipe } from '@dynamic-cube/shared';
import { PortalStore } from './portal-store';
import { PARTY_KIND_KEYS } from './portal-text';

/** A service as a DGA card: icon in a green circle, name, two lines of description, tags (audience, category, free) and a square arrow. */
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
          <span class="dc-tag">{{ kindKeys[kind] | dcTranslate }}</span>
        }
        @if (category(); as c) {
          <span class="dc-tag dc-tag--info">{{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}</span>
        }
        @if (free()) {
          <span class="dc-tag dc-tag--success">{{ 'portal.free' | dcTranslate }}</span>
        }
      </span>
      @if (note()) {
        <span class="dc-tile__note">{{ note() }}</span>
      }
      <span class="dc-square-button dc-tile__arrow" aria-hidden="true">
        <mat-icon fontSet="material-symbols-outlined" class="dc-flip-ltr">arrow_back</mat-icon>
      </span>
    </a>
  `,
  styles: `
    :host {
      display: block;
      height: 100%;
    }
    .dc-tile {
      display: flex;
      flex-direction: column;
      gap: 16px;
      height: 100%;
      min-height: 21rem;
      padding: 16px;
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
      border-color: var(--dga-green-700);
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
      width: 56px;
      height: 40px;
      margin-bottom: 8px;
      border-radius: 50%;
      background: var(--dga-green-700);
      color: #ffffff;
    }
    .dc-tile__icon mat-icon {
      font-size: 22px;
      width: 28px;
      height: 20px;
    }
    .dc-tile__name {
      margin: 0;
      font-size: 20px;
      font-weight: 700;
      line-height: 30px;
    }
    .dc-tile__description {
      display: -webkit-box;
      margin: 0;
      overflow: hidden;
      color: var(--dga-text-3);
      font-size: 16px;
      line-height: 26px;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
    }
    .dc-tile__tags {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-top: auto;
      padding-top: 8px;
    }
    .dc-tile__arrow {
      align-self: flex-start;
      margin-top: 8px;
    }
    .dc-tile:hover .dc-tile__arrow {
      border-color: var(--dga-green-700);
      color: var(--dga-green-700);
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
  /** No fee at any level. */
  protected readonly free = computed(() => {
    const cost = this.service().cost;
    return !!cost && !cost.dependsOnRequest && cost.min === 0 && cost.max === 0;
  });
}
