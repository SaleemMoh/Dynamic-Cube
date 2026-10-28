import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { AuthService } from '../auth/auth.service';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { ActingPartyService } from './acting-party';

/** Portal toolbar control: who the applicant is acting for (themselves or an establishment / principal they represent). */
@Component({
  selector: 'dc-acting-context-selector',
  imports: [MatButtonModule, MatDividerModule, MatIconModule, MatMenuModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (acting.current(); as current) {
      <button matButton class="dc-acting" [matMenuTriggerFor]="menu" data-testid="acting-context">
        <mat-icon fontSet="material-symbols-outlined">{{ current.kind === 'ESTABLISHMENT' ? 'storefront' : 'person' }}</mat-icon>
        {{ { ar: current.nameAr, en: current.nameEn } | dcLocalize }}
      </button>
      <mat-menu #menu="matMenu">
        <div class="dc-acting__heading" mat-menu-item disabled>{{ 'acting.title' | dcTranslate }}</div>
        @if (acting.individual(); as me) {
          <button mat-menu-item (click)="acting.select(null)" data-testid="acting-self">
            <mat-icon fontSet="material-symbols-outlined">person</mat-icon>
            {{ { ar: me.nameAr, en: me.nameEn } | dcLocalize }} — {{ 'acting.self' | dcTranslate }}
          </button>
        }
        @for (c of acting.contexts(); track c.partyId) {
          <button mat-menu-item (click)="acting.select(c.partyId)" [attr.data-testid]="'acting-' + c.identifier">
            <mat-icon fontSet="material-symbols-outlined">{{ c.kind === 'ESTABLISHMENT' ? 'storefront' : 'person' }}</mat-icon>
            {{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}
            <span class="dc-acting__role">{{ { ar: c.relationshipNameAr ?? '', en: c.relationshipNameEn ?? '' } | dcLocalize }}</span>
          </button>
        }
        <mat-divider />
        <button mat-menu-item (click)="acting.load(true)" data-testid="acting-refresh">
          <mat-icon fontSet="material-symbols-outlined">sync</mat-icon>
          {{ 'acting.refresh' | dcTranslate }}
        </button>
      </mat-menu>
    }
  `,
  styles: `
    .dc-acting {
      color: inherit;
    }
    .dc-acting__heading {
      font-weight: 600;
    }
    .dc-acting__role {
      margin-inline-start: 0.5rem;
      color: var(--dga-text-3, #6c737f);
      font-size: 0.875rem;
    }
  `,
})
export class ActingContextSelector {
  protected readonly acting = inject(ActingPartyService);

  constructor() {
    const auth = inject(AuthService);
    let requested = false;
    effect(() => {
      // Once per sign-in; a failed load is retried from the menu's refresh item.
      if (auth.isAuthenticated() && !requested) {
        requested = true;
        void this.acting.load().catch(() => undefined);
      }
    });
  }
}
