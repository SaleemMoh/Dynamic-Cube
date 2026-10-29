import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@dynamic-cube/shared';
import { PortalStore } from './portal-store';

/** "Comments and suggestions": a card that sends visitors to the entity's contact page or email. */
@Component({
  selector: 'dc-suggestions-card',
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (link(); as href) {
      <section class="dc-suggest" data-testid="suggestions">
        <span class="dc-suggest__icon" aria-hidden="true"><mat-icon fontSet="material-symbols-outlined">check_circle</mat-icon></span>
        <h2>{{ 'feedback.suggestions' | dcTranslate }}</h2>
        <p>{{ 'feedback.suggestionsText' | dcTranslate }}</p>
        <a matButton="filled" [href]="href" [attr.target]="href.startsWith('http') ? '_blank' : null" rel="noopener">{{
          'feedback.contactUs' | dcTranslate
        }}</a>
      </section>
    }
  `,
  styles: `
    .dc-suggest {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 12px;
      padding: 16px;
      border: 1px solid var(--dga-border);
      border-radius: 16px;
      background: #ffffff;
    }
    .dc-suggest__icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 48px;
      height: 36px;
      border-radius: 50%;
      background: #ecfdf3;
      color: var(--dga-green-700);
    }
    .dc-suggest h2 {
      margin: 8px 0 0;
      font-size: 20px;
      font-weight: 700;
    }
    .dc-suggest p {
      margin: 0;
      color: var(--dga-text-2);
    }
    .dc-suggest a {
      height: 32px;
      margin-top: 8px;
    }
  `,
})
export class SuggestionsCard {
  private readonly store = inject(PortalStore);
  protected readonly link = computed(() => {
    const help = this.store.help();
    return help.contactUrl || (help.email ? `mailto:${help.email}` : null);
  });
}
