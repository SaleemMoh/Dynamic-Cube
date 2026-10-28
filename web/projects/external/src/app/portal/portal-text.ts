import { Pipe, PipeTransform, inject } from '@angular/core';
import { CostRange, I18nService, PartyKind, ServiceChannel } from '@dynamic-cube/shared';

const amount = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 });

/** "Free", "500 SAR", "200 – 1,500 SAR" or "Depends on the request" (VAT included). */
@Pipe({ name: 'dcCost', pure: false })
export class CostPipe implements PipeTransform {
  private readonly i18n = inject(I18nService);

  transform(cost: CostRange | null | undefined): string {
    if (!cost) return '';
    const t = (key: string) => this.i18n.translate(key);
    if (cost.max === null) return cost.min > 0 ? `${t('portal.from')} ${amount.format(cost.min)} ${t('fees.currency')}` : t('portal.dependsOnRequest');
    if (cost.max === 0 && !cost.dependsOnRequest) return t('portal.free');
    if (cost.min === cost.max) return `${amount.format(cost.min)} ${t('fees.currency')}`;
    return `${amount.format(cost.min)} – ${amount.format(cost.max)} ${t('fees.currency')}`;
  }
}

/** "5 working days" or "10 – 60 working days". */
@Pipe({ name: 'dcDuration', pure: false })
export class DurationPipe implements PipeTransform {
  private readonly i18n = inject(I18nService);

  transform(duration: { min: number; max: number } | number | null | undefined): string {
    if (duration === null || duration === undefined) return this.i18n.translate('portal.durationUnknown');
    const days = this.i18n.translate('portal.workingDays');
    if (typeof duration === 'number') return `${duration} ${days}`;
    return duration.min === duration.max ? `${duration.min} ${days}` : `${duration.min} – ${duration.max} ${days}`;
  }
}

export const PARTY_KIND_KEYS: Record<PartyKind, string> = { INDIVIDUAL: 'portal.audience.INDIVIDUAL', ESTABLISHMENT: 'portal.audience.ESTABLISHMENT' };

export const CHANNEL_ICONS: Record<ServiceChannel, string> = { PORTAL: 'language', MOBILE_APP: 'smartphone', BRANCH: 'apartment', CALL_CENTER: 'support_agent' };
