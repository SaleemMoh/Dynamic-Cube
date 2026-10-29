import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActingPartyService, LocalizePipe, TranslatePipe } from '@dynamic-cube/shared';

/** For a signed-in applicant: who requests are made for now, the other parties they may act for, and any warnings. */
@Component({
  selector: 'dc-acting-panel',
  imports: [LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'data-testid': 'acting-card' },
  template: `
    <h2 class="dc-acting__title">{{ 'home.acting' | dcTranslate }}</h2>
    @if (acting.current(); as current) {
      <p class="dc-acting__now">
        {{ 'home.actingNow' | dcTranslate }}
        <strong data-testid="acting-current">{{ { ar: current.nameAr, en: current.nameEn } | dcLocalize }}</strong>
      </p>
    }
    <ul class="dc-acting__contexts">
      @if (acting.individual(); as me) {
        <li>
          <button class="dc-acting__context" [class.dc-acting__context--current]="acting.current()?.partyId === me.partyId" (click)="acting.select(null)">
            <span>{{ { ar: me.nameAr, en: me.nameEn } | dcLocalize }}</span>
            <span class="dc-muted">{{ 'acting.self' | dcTranslate }} · <span dir="ltr">{{ me.identifier }}</span></span>
          </button>
        </li>
      }
      @for (c of acting.contexts(); track c.partyId) {
        <li>
          <button
            class="dc-acting__context"
            [class.dc-acting__context--current]="acting.current()?.partyId === c.partyId"
            (click)="acting.select(c.partyId)"
            [attr.data-testid]="'context-' + c.identifier"
          >
            <span>{{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}</span>
            <span class="dc-muted">{{ { ar: c.relationshipNameAr ?? '', en: c.relationshipNameEn ?? '' } | dcLocalize }} · <span dir="ltr">{{ c.identifier }}</span></span>
          </button>
        </li>
      }
    </ul>
    @for (w of acting.warnings(); track w.source) {
      <p class="dc-acting__warning" role="status">{{ { ar: w.messageAr ?? '', en: w.messageEn ?? '' } | dcLocalize }}</p>
    }
  `,
  styles: `
    :host {
      display: block;
      padding: 16px 16px;
      border: 1px solid var(--dga-border);
      border-radius: 16px;
      background: #ffffff;
    }
    .dc-acting__title {
      margin: 0 0 4px;
      font-size: 18px;
    }
    .dc-acting__now {
      margin: 0 0 12px;
    }
    .dc-acting__contexts {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr));
      gap: 12px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .dc-acting__context {
      display: flex;
      flex-direction: column;
      gap: 4px;
      width: 100%;
      padding: 12px 16px;
      border: 1px solid var(--dga-border);
      border-radius: 8px;
      background: #ffffff;
      font: inherit;
      text-align: start;
      cursor: pointer;
    }
    .dc-acting__context--current {
      border-color: var(--dga-green-700);
      box-shadow: inset 0 0 0 1px var(--dga-green-700);
    }
    .dc-acting__warning {
      margin: 12px 0 0;
      color: #b54708;
    }
  `,
})
export class ActingPanel {
  protected readonly acting = inject(ActingPartyService);
}
