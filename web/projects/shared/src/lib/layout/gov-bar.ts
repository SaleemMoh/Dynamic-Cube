import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '../i18n/translate.pipe';

/**
 * DGA government identification bar shown above the header of every page: the Saudi flag, the official-website
 * statement and how to verify it.
 */
@Component({
  selector: 'dc-gov-bar',
  imports: [MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-gov-bar">
      <div class="dc-gov-bar__inner">
        <img class="dc-gov-bar__flag" src="dc/gov/sa-flag.svg" width="24" height="16" alt="" />
        <span>{{ 'gov.official' | dcTranslate }}</span>
        <button
          type="button"
          class="dc-gov-bar__toggle"
          [attr.aria-expanded]="open()"
          (click)="open.set(!open())"
        >
          {{ 'gov.verify' | dcTranslate }}
          <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{
            open() ? 'expand_less' : 'expand_more'
          }}</mat-icon>
        </button>
      </div>
      @if (open()) {
        <div class="dc-gov-bar__details">
          <div class="dc-gov-bar__item">
            <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">link</mat-icon>
            <div>
              <strong>{{ 'gov.domainTitle' | dcTranslate }}</strong>
              <p>{{ 'gov.domainText' | dcTranslate }}</p>
            </div>
          </div>
          <div class="dc-gov-bar__item">
            <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">lock</mat-icon>
            <div>
              <strong>{{ 'gov.secureTitle' | dcTranslate }}</strong>
              <p>{{ 'gov.secureText' | dcTranslate }}</p>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .dc-gov-bar {
      background: var(--dga-gray-100);
      border-bottom: 1px solid var(--dga-gray-200);
      color: var(--dga-text-2);
      font-size: 13px;
    }
    .dc-gov-bar__inner,
    .dc-gov-bar__details {
      display: flex;
      align-items: center;
      gap: 8px;
      max-width: 80rem;
      margin-inline: auto;
      padding-inline: 1.5rem;
    }
    .dc-gov-bar__inner {
      min-height: 34px;
    }
    .dc-gov-bar__flag {
      flex: none;
      border-radius: 2px;
    }
    .dc-gov-bar__toggle {
      display: inline-flex;
      align-items: center;
      gap: 2px;
      padding: 0;
      border: 0;
      background: none;
      color: var(--dga-green-700);
      font: inherit;
      text-decoration: underline;
      cursor: pointer;
    }
    .dc-gov-bar__toggle mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .dc-gov-bar__details {
      align-items: flex-start;
      gap: 2rem;
      padding-block: 1rem;
      flex-wrap: wrap;
    }
    .dc-gov-bar__item {
      display: flex;
      gap: 8px;
      max-width: 28rem;
    }
    .dc-gov-bar__item p {
      margin: 4px 0 0;
    }
  `,
})
export class GovBar {
  protected readonly open = signal(false);
}
