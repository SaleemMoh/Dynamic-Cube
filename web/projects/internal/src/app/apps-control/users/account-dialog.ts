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
import {
  AppsControlApi,
  I18nService,
  LocalizePipe,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { Observable, catchError, firstValueFrom, of } from 'rxjs';
import { ConfigEditorData } from '../config/config-editor';
import { PrivilegesService } from '../privileges.service';
import { temporaryPassword } from './password';

/**
 * A user's sign-in account: where it comes from, whether it is active, and what can be done from here — activate or
 * deactivate it (at once, for the account and the user), give a temporary password or email a link to choose one, and
 * sign the user out everywhere. Accounts owned by a directory or identity provider keep their passwords there.
 */
@Component({
  selector: 'dc-account-dialog',
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
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ 'account.title' | dcTranslate }} — {{ { ar: row.nameAr, en: row.nameEn } | dcLocalize }}
    </h2>
    <mat-dialog-content class="dc-acc">
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
      @if (caps(); as c) {
        @if (c.provider === 'None') {
          <p class="dc-acc__note" data-testid="account-unmanaged">
            <mat-icon fontSet="material-symbols-outlined">info</mat-icon
            >{{ 'account.unmanaged' | dcTranslate }}
          </p>
        }
      }
      @if (view(); as a) {
        <div class="dc-acc__state" data-testid="account-state">
          <span class="dc-acc__avatar" aria-hidden="true"
            ><mat-icon fontSet="material-symbols-outlined">person</mat-icon></span
          >
          <div class="dc-acc__who">
            <strong class="mono">{{ row.code }}</strong>
            <span class="dc-muted">{{ a.email ?? row['email'] ?? '—' }}</span>
          </div>
          @if (a.exists) {
            <span
              class="dc-pill"
              [class.dc-pill--green]="a.enabled"
              data-testid="account-enabled"
              >{{ (a.enabled ? 'account.active' : 'account.inactive') | dcTranslate }}</span
            >
            <span class="dc-pill dc-pill--gold" data-testid="account-source">{{
              a.source === 'LOCAL' ? ('account.local' | dcTranslate) : a.source
            }}</span>
          } @else if (caps()?.provider !== 'None') {
            <span class="dc-pill" data-testid="account-missing">{{
              'account.none' | dcTranslate
            }}</span>
          }
        </div>
        @if (a.exists) {
          <dl class="dc-acc__facts">
            <div>
              <dt>{{ 'account.userStatus' | dcTranslate }}</dt>
              <dd>{{ 'account.status.' + (row['status'] ?? 'ACTIVE') | dcTranslate }}</dd>
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
        }

        @if (canEdit()) {
          @if (a.exists) {
            <section class="dc-acc__section">
              <h3>{{ 'account.access' | dcTranslate }}</h3>
              <div class="dc-acc__row">
                @if (a.enabled) {
                  <button
                    matButton="outlined"
                    class="dc-acc__danger"
                    type="button"
                    (click)="setEnabled(false)"
                    [disabled]="busy()"
                    data-testid="account-disable"
                  >
                    <mat-icon fontSet="material-symbols-outlined">block</mat-icon>
                    {{ 'account.deactivate' | dcTranslate }}
                  </button>
                } @else {
                  <button
                    matButton="filled"
                    type="button"
                    (click)="setEnabled(true)"
                    [disabled]="busy()"
                    data-testid="account-enable"
                  >
                    <mat-icon fontSet="material-symbols-outlined">check_circle</mat-icon>
                    {{ 'account.activate' | dcTranslate }}
                  </button>
                }
                <button
                  matButton="outlined"
                  type="button"
                  (click)="signOut()"
                  [disabled]="busy()"
                  data-testid="account-sign-out"
                >
                  <mat-icon fontSet="material-symbols-outlined">logout</mat-icon>
                  {{ 'account.signOut' | dcTranslate }}
                </button>
              </div>
              <p class="dc-muted">{{ 'account.accessHint' | dcTranslate }}</p>
            </section>
          }

          @if (a.exists ? a.canSetPassword : caps()?.canCreate) {
            <section class="dc-acc__section" data-testid="account-password">
              <h3>{{ (a.exists ? 'account.password' : 'account.create') | dcTranslate }}</h3>
              <mat-button-toggle-group
                [ngModel]="mode()"
                (ngModelChange)="mode.set($event)"
                [attr.aria-label]="'account.password' | dcTranslate"
              >
                <mat-button-toggle value="temporary">{{
                  'account.temporary' | dcTranslate
                }}</mat-button-toggle>
                <mat-button-toggle value="email">{{
                  'account.byEmail' | dcTranslate
                }}</mat-button-toggle>
              </mat-button-toggle-group>
              @if (mode() === 'temporary') {
                <div class="dc-acc__row">
                  <mat-form-field subscriptSizing="dynamic" class="dc-acc__password">
                    <mat-label>{{ 'account.temporary' | dcTranslate }}</mat-label>
                    <input
                      matInput
                      dir="ltr"
                      [ngModel]="password()"
                      (ngModelChange)="password.set($event)"
                      data-testid="account-temp-password"
                    />
                    <button
                      matIconButton
                      matSuffix
                      type="button"
                      (click)="password.set(generate())"
                      [matTooltip]="'account.generate' | dcTranslate"
                    >
                      <mat-icon fontSet="material-symbols-outlined">autorenew</mat-icon>
                    </button>
                  </mat-form-field>
                  <button
                    matIconButton
                    type="button"
                    (click)="copy(password())"
                    [disabled]="!password()"
                    [matTooltip]="'account.copy' | dcTranslate"
                  >
                    <mat-icon fontSet="material-symbols-outlined">content_copy</mat-icon>
                  </button>
                </div>
                <p class="dc-muted">{{ 'account.temporaryHint' | dcTranslate }}</p>
              } @else {
                <p class="dc-muted">
                  {{ 'account.byEmailHint' | dcTranslate }}
                  <span dir="ltr">{{ a.email ?? row['email'] ?? '—' }}</span>
                </p>
              }
              <button
                matButton="filled"
                type="button"
                (click)="a.exists ? setPassword() : create()"
                [disabled]="busy() || (mode() === 'temporary' && password().length < 8)"
                data-testid="account-password-save"
              >
                {{ (a.exists ? 'account.setPassword' : 'account.createAccount') | dcTranslate }}
              </button>
            </section>
          } @else if (a.exists && !a.canSetPassword) {
            <p class="dc-acc__note">
              <mat-icon fontSet="material-symbols-outlined">lock</mat-icon
              >{{ 'account.passwordElsewhere' | dcTranslate }} {{ a.source }}
            </p>
          }
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      <button matButton type="button" [mat-dialog-close]="changed()">
        {{ 'common.close' | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-acc {
      display: grid;
      gap: 12px;
    }
    .dc-acc__state {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 10px;
      padding: 12px;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius);
    }
    .dc-acc__avatar {
      display: grid;
      place-items: center;
      width: 40px;
      height: 40px;
      border-radius: 50%;
      background: var(--dynamic-cube-gold-50);
      color: var(--dynamic-cube-gold-700);
    }
    .dc-acc__who {
      display: grid;
      flex: 1;
      min-width: 10rem;
    }
    .dc-acc__facts {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(11rem, 1fr));
      gap: 8px 16px;
      margin: 0;
    }
    .dc-acc__facts dt {
      color: var(--dga-gray-500);
      font-size: 12px;
    }
    .dc-acc__facts dd {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
      margin: 2px 0 0;
      font-weight: 600;
    }
    .dc-acc__section {
      display: grid;
      justify-items: start;
      gap: 8px;
      padding-top: 12px;
      border-top: 1px solid var(--dga-gray-200);
    }
    .dc-acc__section h3 {
      margin: 0;
      font-size: 14px;
    }
    .dc-acc__section p {
      margin: 0;
      font-size: 12px;
    }
    .dc-acc__row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .dc-acc__password {
      width: 18rem;
    }
    .dc-acc__danger {
      --mat-button-outlined-label-text-color: #b42318;
      --mat-button-outlined-outline-color: #fda29b;
    }
    .dc-acc__note {
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
export class AccountDialog {
  private readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  protected readonly row = this.data.row!;
  private readonly api = inject(AppsControlApi);
  private readonly i18n = inject(I18nService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly clipboard = inject(Clipboard);
  private readonly privileges = inject(PrivilegesService);

  protected readonly capabilities = rxResource({
    stream: () => this.api.accountCapabilities().pipe(catchError(() => of(null))),
  });
  protected readonly account = rxResource({ stream: () => this.api.account(this.row.code) });
  protected readonly caps = computed(() =>
    this.capabilities.hasValue() ? this.capabilities.value() : null,
  );
  protected readonly view = computed(() => (this.account.hasValue() ? this.account.value() : null));
  protected readonly canEdit = computed(() => this.privileges.can('apps-control.users', 'EDIT'));
  protected readonly mode = signal<'temporary' | 'email'>('temporary');
  protected readonly password = signal(temporaryPassword());
  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly changed = signal(false);
  protected readonly generate = temporaryPassword;

  protected setEnabled(enabled: boolean): Promise<void> {
    return this.run(
      this.api.setAccountEnabled(this.row.code, enabled),
      enabled ? 'account.activated' : 'account.deactivated',
    );
  }

  protected signOut(): Promise<void> {
    return this.run(this.api.signOutAccount(this.row.code), 'account.signedOut');
  }

  protected setPassword(): Promise<void> {
    const temporary = this.mode() === 'temporary';
    return this.run(
      this.api.setAccountPassword(this.row.code, {
        temporaryPassword: temporary ? this.password() : null,
        sendEmail: !temporary,
      }),
      temporary ? 'account.passwordSet' : 'account.emailSent',
    );
  }

  protected create(): Promise<void> {
    const temporary = this.mode() === 'temporary';
    const [firstName, ...rest] = String(this.row.nameEn || this.row.nameAr || this.row.code).split(
      ' ',
    );
    return this.run(
      this.api.createAccount({
        userName: this.row.code,
        email: (this.row['email'] as string | null) ?? null,
        firstName,
        lastName: rest.join(' ') || null,
        temporaryPassword: temporary ? this.password() : null,
        sendSetupEmail: !temporary,
      }),
      'account.createdDone',
    );
  }

  protected copy(text: string): void {
    if (this.clipboard.copy(text))
      this.snackBar.open(this.i18n.translate('account.copied'), undefined, { duration: 2500 });
  }

  private async run(change: Observable<unknown>, done: string): Promise<void> {
    this.busy.set(true);
    this.errors.set([]);
    try {
      await firstValueFrom(change);
      this.changed.set(true);
      this.snackBar.open(this.i18n.translate(done), undefined, { duration: 4000 });
      this.account.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }
}
