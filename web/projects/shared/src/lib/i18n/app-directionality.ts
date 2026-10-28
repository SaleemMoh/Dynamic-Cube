import { Directionality } from '@angular/cdk/bidi';
import { Injectable, effect, inject, untracked } from '@angular/core';
import { I18nService } from './i18n.service';

/**
 * Root {@link Directionality} that follows the current language. Content projected into the shell resolves the root
 * provider rather than the shell's `dir` directive, so Material components there (side navigation, tables, overlays)
 * need this to flip when the language changes.
 */
@Injectable()
export class AppDirectionality extends Directionality {
  constructor() {
    super();
    const i18n = inject(I18nService);
    effect(() => {
      const direction = i18n.direction();
      untracked(() => {
        if (this.valueSignal() !== direction) {
          this.valueSignal.set(direction);
          this.change.emit(direction);
        }
      });
    });
  }
}
