import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { I18nService, TranslatePipe } from '@dynamic-cube/shared';

/** A moment as the DGA "last modified" line writes it: dd/MM/yyyy, h:mm and ص/م (AM/PM), in Riyadh time. */
export function saudiTime(at: string, language: string): { date: string; time: string; period: string } {
  const when = new Date(at);
  const date = new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Riyadh', day: '2-digit', month: '2-digit', year: 'numeric' }).format(when);
  const parts = new Intl.DateTimeFormat('en-US', { timeZone: 'Asia/Riyadh', hour: 'numeric', minute: '2-digit', hour12: true }).formatToParts(when);
  const part = (type: string) => parts.find((p) => p.type === type)?.value ?? '';
  const pm = part('dayPeriod').toUpperCase() === 'PM';
  const period = language === 'ar' ? (pm ? 'م' : 'ص') : pm ? 'PM' : 'AM';
  return { date, time: `${part('hour')}:${part('minute')}`, period };
}

/** "Last modified: dd/MM/yyyy - h:mm, Saudi time", in a white band above the footer (DGA). */
@Component({
  selector: 'dc-last-modified',
  imports: [TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (text(); as text) {
      <div class="dc-last-modified" data-testid="last-modified">
        <p class="dc-portal-block">
          {{ 'portal.lastModified' | dcTranslate }} <bdi dir="ltr">{{ text.date }} - {{ text.time }}</bdi> {{ text.period }}
          {{ 'portal.saudiTime' | dcTranslate }}
        </p>
      </div>
    }
  `,
  styles: `
    .dc-last-modified {
      background: #ffffff;
    }
    .dc-last-modified p {
      margin: 0 auto;
      padding-block: 16px;
      color: var(--dga-text-2);
      font-size: 14px;
      line-height: 20px;
    }
  `,
})
export class LastModified {
  readonly at = input<string | null>(null);
  private readonly i18n = inject(I18nService);

  protected readonly text = computed(() => {
    const at = this.at();
    if (!at) return null;
    return saudiTime(at, this.i18n.language());
  });
}
