import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Clipboard } from '@angular/cdk/clipboard';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppsControlApi, I18nService, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { Observable, catchError, firstValueFrom, of } from 'rxjs';
import { PrivilegesService } from '../privileges.service';
import { temporaryPassword } from '../users/password';

/** Who the account is for: a national ID to open, and the names and email to start a new account with. */
export interface PortalAccountData {
  nationalId?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  email?: string | null;
}

const NATIONAL_ID = /^[12][0-9]{9}$/;

/**
 * An applicant's portal account. The user name the applicant signs in with is the national ID (or iqama number), shown
 * first with a copy button. Creates the account with a temporary password or an email link, and afterwards shows the
 * sign-in details to hand over; activates and deactivates it, resets the password and signs the applicant out.
 */
@Component({
  selector: 'dc-portal-account-dialog',
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTooltipModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'portalAccount.title' | dcTranslate }}</h2>
    <mat-dialog-content class="dc-pacc">
      @if (busy() || account.isLoading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li dir="auto">{{ e }}</li>
          }
        </ul>
      }
      @if (caps()?.provider === 'None') {
        <p class="dc-pacc__note" data-testid="portal-account-unmanaged">
          <mat-icon fontSet="material-symbols-outlined">info</mat-icon>{{ 'portalAccount.unmanaged' | dcTranslate }}
        </p>
      }

      @if (handOver(); as h) {
        <section class="dc-pacc__handover" data-testid="portal-account-handover" role="status">
          <h3><mat-icon fontSet="material-symbols-outlined">task_alt</mat-icon>{{ 'portalAccount.handOver' | dcTranslate }}</h3>
          <dl>
            <div>
              <dt>{{ 'portalAccount.userName' | dcTranslate }}</dt>
              <dd class="mono" dir="ltr" data-testid="portal-account-handover-user">{{ h.userName }}</dd>
            </div>
            @if (h.password) {
              <div>
                <dt>{{ 'account.temporary' | dcTranslate }}</dt>
                <dd class="mono" dir="ltr">{{ h.password }}</dd>
              </div>
            }
          </dl>
          <p class="dc-muted">{{ (h.password ? 'portalAccount.handOverHint' : 'portalAccount.handOverEmail') | dcTranslate }}</p>
          <button matButton="outlined" type="button" (click)="copyHandOver(h)" data-testid="portal-account-copy-handover">
            <mat-icon fontSet="material-symbols-outlined">content_copy</mat-icon>{{ 'portalAccount.copyDetails' | dcTranslate }}
          </button>
        </section>
      }

      @if (view(); as a) {
        @if (a.exists) {
          <div class="dc-pacc__state" data-testid="portal-account-state">
            <span class="dc-pacc__avatar" aria-hidden="true"><mat-icon fontSet="material-symbols-outlined">badge</mat-icon></span>
            <div class="dc-pacc__who">
              <span class="dc-muted">{{ 'portalAccount.userName' | dcTranslate }}</span>
              <span class="dc-pacc__user">
                <strong class="mono" dir="ltr" data-testid="portal-account-user">{{ a.userName }}</strong>
                <button matIconButton type="button" (click)="copy(a.userName, 'portalAccount.userCopied')" [matTooltip]="'account.copy' | dcTranslate">
                  <mat-icon fontSet="material-symbols-outlined">content_copy</mat-icon>
                </button>
              </span>
              <span>{{ fullName(a.firstName, a.lastName) }}</span>
            </div>
            <span class="dc-pill" [class.dc-pill--green]="a.enabled" data-testid="portal-account-enabled">{{
              (a.enabled ? 'account.active' : 'account.inactive') | dcTranslate
            }}</span>
            <span class="dc-pill dc-pill--gold">{{ a.source === 'LOCAL' ? ('account.local' | dcTranslate) : a.source }}</span>
          </div>
          <dl class="dc-pacc__facts">
            <div>
              <dt>{{ 'portalAccount.email' | dcTranslate }}</dt>
              <dd dir="ltr">{{ a.email ?? '—' }}</dd>
            </div>
            <div>
              <dt>{{ 'account.created' | dcTranslate }}</dt>
              <dd dir="ltr">{{ a.createdAt ? (a.createdAt | date: 'yyyy-MM-dd') : '—' }}</dd>
            </div>
            <div>
              <dt>{{ 'account.pending' | dcTranslate }}</dt>
              <dd>
                @for (action of a.requiredActions; track action) {
                  <span class="dc-pill">{{ 'account.action.' + action | dcTranslate }}</span>
                } @empty {
                  —
                }
              </dd>
            </div>
          </dl>

          @if (canEdit()) {
            <section class="dc-pacc__section">
              <h3>{{ 'account.access' | dcTranslate }}</h3>
              <div class="dc-pacc__row">
                @if (a.enabled) {
                  <button
                    matButton="outlined"
                    class="dc-pacc__danger"
                    type="button"
                    (click)="setEnabled(false)"
                    [disabled]="busy()"
                    data-testid="portal-account-disable"
                  >
                    <mat-icon fontSet="material-symbols-outlined">block</mat-icon>{{ 'account.deactivate' | dcTranslate }}
                  </button>
                } @else {
                  <button matButton="filled" type="button" (click)="setEnabled(true)" [disabled]="busy()" data-testid="portal-account-enable">
                    <mat-icon fontSet="material-symbols-outlined">check_circle</mat-icon>{{ 'account.activate' | dcTranslate }}
                  </button>
                }
                <button matButton="outlined" type="button" (click)="signOut()" [disabled]="busy()">
                  <mat-icon fontSet="material-symbols-outlined">logout</mat-icon>{{ 'account.signOut' | dcTranslate }}
                </button>
              </div>
              <p class="dc-muted">{{ 'portalAccount.accessHint' | dcTranslate }}</p>
            </section>
          }
        }
      }

      @if (!handOver() && (showCreate() || (view()?.exists && view()?.canSetPassword && canEdit()))) {
        <section class="dc-pacc__section" data-testid="portal-account-password">
          <h3>{{ (view()?.exists ? 'account.password' : 'portalAccount.create') | dcTranslate }}</h3>
          @if (!view()?.exists) {
            <div class="dc-pacc__grid">
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'portalAccount.nationalId' | dcTranslate }}</mat-label>
                <input
                  matInput
                  dir="ltr"
                  inputmode="numeric"
                  maxlength="10"
                  [ngModel]="nationalId()"
                  (ngModelChange)="nationalId.set($event.trim())"
                  [readonly]="!!data.nationalId"
                  data-testid="portal-account-national-id"
                />
                <mat-hint>{{ 'portalAccount.nationalIdHint' | dcTranslate }}</mat-hint>
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'portalAccount.email' | dcTranslate }}</mat-label>
                <input matInput dir="ltr" type="email" [ngModel]="email()" (ngModelChange)="email.set($event)" />
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'portalAccount.firstName' | dcTranslate }}</mat-label>
                <input matInput [ngModel]="firstName()" (ngModelChange)="firstName.set($event)" data-testid="portal-account-first-name" />
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'portalAccount.lastName' | dcTranslate }}</mat-label>
                <input matInput [ngModel]="lastName()" (ngModelChange)="lastName.set($event)" />
              </mat-form-field>
            </div>
          }
          <mat-button-toggle-group [ngModel]="mode()" (ngModelChange)="mode.set($event)" [attr.aria-label]="'account.password' | dcTranslate">
            <mat-button-toggle value="temporary">{{ 'account.temporary' | dcTranslate }}</mat-button-toggle>
            <mat-button-toggle value="email">{{ 'account.byEmail' | dcTranslate }}</mat-button-toggle>
          </mat-button-toggle-group>
          @if (mode() === 'temporary') {
            <div class="dc-pacc__row">
              <mat-form-field subscriptSizing="dynamic" class="dc-pacc__password">
                <mat-label>{{ 'account.temporary' | dcTranslate }}</mat-label>
                <input matInput dir="ltr" [ngModel]="password()" (ngModelChange)="password.set($event)" data-testid="portal-account-temp-password" />
                <button matIconButton matSuffix type="button" (click)="password.set(generate())" [matTooltip]="'account.generate' | dcTranslate">
                  <mat-icon fontSet="material-symbols-outlined">autorenew</mat-icon>
                </button>
              </mat-form-field>
            </div>
            <p class="dc-muted">{{ 'portalAccount.temporaryHint' | dcTranslate }}</p>
          } @else {
            <p class="dc-muted">
              {{ 'portalAccount.byEmailHint' | dcTranslate }} <span dir="ltr">{{ (view()?.exists ? view()?.email : email()) || '—' }}</span>
            </p>
          }
          <button
            matButton="filled"
            type="button"
            (click)="view()?.exists ? setPassword() : create()"
            [disabled]="busy() || !canSubmit()"
            data-testid="portal-account-save"
          >
            {{ (view()?.exists ? 'account.setPassword' : 'account.createAccount') | dcTranslate }}
          </button>
        </section>
      } @else if (view()?.exists && !view()?.canSetPassword) {
        <p class="dc-pacc__note">
          <mat-icon fontSet="material-symbols-outlined">lock</mat-icon>{{ 'account.passwordElsewhere' | dcTranslate }} {{ view()?.source }}
        </p>
      } @else if (!view()?.exists && !account.isLoading() && !canCreate() && caps()?.provider !== 'None') {
        <p class="dc-pacc__note" data-testid="portal-account-missing">
          <mat-icon fontSet="material-symbols-outlined">person_off</mat-icon>{{ 'portalAccount.none' | dcTranslate }}
        </p>
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      <button matButton type="button" [mat-dialog-close]="changed()">{{ 'common.close' | dcTranslate }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-pacc {
      display: grid;
      gap: 12px;
    }
    .dc-pacc__state {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 10px;
      padding: 12px;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius);
    }
    .dc-pacc__avatar {
      display: grid;
      place-items: center;
      width: 40px;
      height: 40px;
      border-radius: 50%;
      background: var(--dynamic-cube-gold-50);
      color: var(--dynamic-cube-gold-700);
    }
    .dc-pacc__who {
      display: grid;
      flex: 1;
      min-width: 12rem;
      font-size: 13px;
    }
    .dc-pacc__user {
      display: flex;
      align-items: center;
      gap: 2px;
      font-size: 18px;
    }
    .dc-pacc__facts,
    .dc-pacc__handover dl {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(11rem, 1fr));
      gap: 8px 16px;
      margin: 0;
    }
    dt {
      color: var(--dga-gray-500);
      font-size: 12px;
    }
    dd {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
      margin: 2px 0 0;
      font-weight: 600;
    }
    .dc-pacc__handover {
      display: grid;
      justify-items: start;
      gap: 8px;
      padding: 12px 14px;
      border: 1px solid #a6f4c5;
      border-radius: var(--shell-radius);
      background: #ecfdf3;
    }
    .dc-pacc__handover h3 {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0;
      font-size: 14px;
      color: #067647;
    }
    .dc-pacc__handover dd {
      font-size: 18px;
    }
    .dc-pacc__handover p {
      margin: 0;
      font-size: 12px;
    }
    .dc-pacc__section {
      display: grid;
      justify-items: start;
      gap: 8px;
      padding-top: 12px;
      border-top: 1px solid var(--dga-gray-200);
    }
    .dc-pacc__section h3 {
      margin: 0;
      font-size: 14px;
    }
    .dc-pacc__section p {
      margin: 0;
      font-size: 12px;
    }
    .dc-pacc__grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr));
      gap: 12px;
      width: 100%;
    }
    .dc-pacc__row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .dc-pacc__password {
      width: 18rem;
    }
    .dc-pacc__danger {
      --mat-button-outlined-label-text-color: #b42318;
      --mat-button-outlined-outline-color: #fda29b;
    }
    .dc-pacc__note {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0;
      padding: 10px 12px;
      border-radius: var(--shell-radius);
      background: var(--dga-gray-50);
      color: var(--dga-gray-700);
      font-size: 13px;
    }
  `,
})
export class PortalAccountDialog {
  protected readonly data = inject<PortalAccountData>(MAT_DIALOG_DATA);
  private readonly api = inject(AppsControlApi);
  private readonly i18n = inject(I18nService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly clipboard = inject(Clipboard);
  private readonly privileges = inject(PrivilegesService);

  protected readonly nationalId = signal(this.data.nationalId ?? '');
  protected readonly firstName = signal(this.data.firstName ?? '');
  protected readonly lastName = signal(this.data.lastName ?? '');
  protected readonly email = signal(this.data.email ?? '');
  /** The account opened: the one given, or the one just created. */
  private readonly opened = signal(this.data.nationalId ?? null);

  protected readonly capabilities = rxResource({ stream: () => this.api.portalAccountCapabilities().pipe(catchError(() => of(null))) });
  protected readonly account = rxResource({
    params: () => this.opened() ?? undefined,
    stream: ({ params }) => this.api.portalAccount(params),
  });
  protected readonly caps = computed(() => (this.capabilities.hasValue() ? this.capabilities.value() : null));
  protected readonly view = computed(() => (this.opened() && this.account.hasValue() ? this.account.value() : null));
  protected readonly canEdit = computed(() => this.privileges.can('apps-control.portal-accounts', 'EDIT'));
  protected readonly canCreate = computed(() => this.privileges.can('apps-control.portal-accounts', 'CREATE') && !!this.caps()?.canCreate);
  protected readonly showCreate = computed(() => this.canCreate() && !this.account.isLoading() && !this.view()?.exists);
  protected readonly mode = signal<'temporary' | 'email'>('temporary');
  protected readonly password = signal(temporaryPassword());
  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly changed = signal(false);
  protected readonly handOver = signal<{ userName: string; password: string | null } | null>(null);
  protected readonly generate = temporaryPassword;

  protected readonly canSubmit = computed(() => {
    if (this.mode() === 'temporary' && this.password().length < 8) return false;
    if (this.view()?.exists) return true;
    if (this.mode() === 'email' && !this.email().trim()) return false;
    return NATIONAL_ID.test(this.nationalId()) && !!this.firstName().trim();
  });

  protected fullName(first?: string | null, last?: string | null): string {
    return [first, last].filter(Boolean).join(' ') || '—';
  }

  protected setEnabled(enabled: boolean): Promise<boolean> {
    return this.run(this.api.setPortalAccountEnabled(this.opened()!, enabled), enabled ? 'account.activated' : 'account.deactivated');
  }

  protected signOut(): Promise<boolean> {
    return this.run(this.api.signOutPortalAccount(this.opened()!), 'account.signedOut');
  }

  protected async setPassword(): Promise<void> {
    const temporary = this.mode() === 'temporary';
    const userName = this.opened()!;
    const done = await this.run(
      this.api.setPortalAccountPassword(userName, { temporaryPassword: temporary ? this.password() : null, sendEmail: !temporary }),
      temporary ? 'account.passwordSet' : 'account.emailSent',
    );
    if (done) this.handOver.set({ userName, password: temporary ? this.password() : null });
  }

  protected async create(): Promise<void> {
    const temporary = this.mode() === 'temporary';
    const nationalId = this.nationalId();
    const done = await this.run(
      this.api.createPortalAccount({
        nationalId,
        firstName: this.firstName().trim(),
        lastName: this.lastName().trim() || null,
        email: this.email().trim() || null,
        temporaryPassword: temporary ? this.password() : null,
        sendSetupEmail: !temporary,
      }),
      'account.createdDone',
    );
    if (done) {
      this.handOver.set({ userName: nationalId, password: temporary ? this.password() : null });
      this.opened.set(nationalId);
    }
  }

  protected copy(text: string, done = 'account.copied'): void {
    if (this.clipboard.copy(text)) this.snackBar.open(this.i18n.translate(done), undefined, { duration: 2500 });
  }

  protected copyHandOver(h: { userName: string; password: string | null }): void {
    const lines = [`${this.i18n.translate('portalAccount.userName')}: ${h.userName}`];
    if (h.password) lines.push(`${this.i18n.translate('account.temporary')}: ${h.password}`);
    this.copy(lines.join('\n'), 'portalAccount.detailsCopied');
  }

  private async run(change: Observable<unknown>, done: string): Promise<boolean> {
    this.busy.set(true);
    this.errors.set([]);
    try {
      await firstValueFrom(change);
      this.changed.set(true);
      this.snackBar.open(this.i18n.translate(done), undefined, { duration: 4000 });
      this.account.reload();
      return true;
    } catch (error) {
      this.errors.set(problemMessages(error));
      return false;
    } finally {
      this.busy.set(false);
    }
  }
}
