import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { I18nService } from './i18n.service';
import { TranslatePipe } from './translate.pipe';

@Component({
  selector: 'dc-language-switcher',
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .dc-language-short {
      font-size: 16px;
      font-weight: 600;
    }
  `,
  template: `
    <button matButton type="button" [attr.aria-label]="'language.switch.aria' | dcTranslate" (click)="i18n.toggle()" data-testid="language-switch">
      @if (!short()) {
        <mat-icon fontSet="material-symbols-outlined">language</mat-icon>
      }
      @if (short()) {
        <span class="dc-language-short" [attr.lang]="i18n.language() === 'ar' ? 'en' : 'ar'">{{ i18n.language() === 'ar' ? 'En' : 'ع' }}</span>
      } @else {
        {{ 'language.switch' | dcTranslate }}
      }
    </button>
  `,
})
export class LanguageSwitcher {
  /** Only the other language's short name ("En" / "ع"), as in the DGA portal header. */
  readonly short = input(false);
  protected readonly i18n = inject(I18nService);
}
