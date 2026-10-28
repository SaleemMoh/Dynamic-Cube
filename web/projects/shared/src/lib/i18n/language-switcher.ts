import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { I18nService } from './i18n.service';
import { TranslatePipe } from './translate.pipe';

@Component({
  selector: 'dc-language-switcher',
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button matButton type="button" [attr.aria-label]="'language.switch.aria' | dcTranslate" (click)="i18n.toggle()">
      <mat-icon fontSet="material-symbols-outlined">language</mat-icon>
      {{ 'language.switch' | dcTranslate }}
    </button>
  `,
})
export class LanguageSwitcher {
  protected readonly i18n = inject(I18nService);
}
