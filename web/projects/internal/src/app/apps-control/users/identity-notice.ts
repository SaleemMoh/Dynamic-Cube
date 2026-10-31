import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { AccountCapabilities, AppsControlApi, TranslatePipe } from '@dynamic-cube/shared';
import { Observable, catchError, of } from 'rxjs';

/**
 * Where sign-in accounts come from on this installation and what can be done with them here: created and maintained in
 * Apps Control (managed), taken from a directory or identity provider such as AD, LDAP, Entra ID or Nafath (federated),
 * or maintained entirely elsewhere. Shown above the employees' users and the applicants' portal accounts.
 */
@Component({
  selector: 'dc-identity-notice',
  imports: [MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (kind(); as k) {
      <aside
        class="dc-idn"
        [class.dc-idn--managed]="k === 'managed'"
        [attr.data-testid]="'identity-notice-' + k"
      >
        <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{
          k === 'managed' ? 'manage_accounts' : k === 'federated' ? 'hub' : 'info'
        }}</mat-icon>
        <div>
          <strong>{{ 'identity.' + audience() + '.' + k + '.title' | dcTranslate }}</strong>
          @if (k === 'federated' && caps()?.federations?.length) {
            <span class="dc-idn__from" dir="ltr">{{ caps()!.federations.join(' · ') }}</span>
          }
          <p>{{ 'identity.' + audience() + '.' + k + '.text' | dcTranslate }}</p>
        </div>
      </aside>
    }
  `,
  styles: `
    .dc-idn {
      display: flex;
      align-items: flex-start;
      gap: 10px;
      margin-bottom: 12px;
      padding: 10px 14px;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius);
      background: var(--dga-gray-50);
      font-size: 13px;
    }
    .dc-idn--managed {
      border-color: var(--dynamic-cube-gold-200, #f0dca8);
      background: var(--dynamic-cube-gold-50, #fdf8ec);
    }
    .dc-idn > mat-icon {
      color: var(--dynamic-cube-gold-700);
    }
    .dc-idn p {
      margin: 2px 0 0;
      color: var(--dga-gray-700);
    }
    .dc-idn__from {
      margin-inline-start: 8px;
      color: var(--dga-gray-500);
    }
  `,
})
export class IdentityNotice {
  /** Employees' accounts (users) or applicants' accounts (portal). */
  readonly audience = input<'users' | 'portal'>('users');
  private readonly api = inject(AppsControlApi);

  private readonly capabilities = rxResource({
    params: () => this.audience(),
    stream: ({ params }): Observable<AccountCapabilities | null> =>
      (params === 'portal'
        ? this.api.portalAccountCapabilities()
        : this.api.accountCapabilities()
      ).pipe(catchError(() => of(null))),
  });
  protected readonly caps = computed(() =>
    this.capabilities.hasValue() ? this.capabilities.value() : null,
  );
  protected readonly kind = computed<'managed' | 'federated' | 'none' | null>(() => {
    const caps = this.caps();
    if (!caps) return null;
    if (caps.provider === 'None') return 'none';
    return caps.mode === 'Federated' ? 'federated' : 'managed';
  });
}
