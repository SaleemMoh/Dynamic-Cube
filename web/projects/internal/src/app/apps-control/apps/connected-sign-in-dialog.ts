import { Clipboard } from '@angular/cdk/clipboard';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
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
import { Observable, firstValueFrom } from 'rxjs';
import { ConfigEditorData } from '../config/config-editor';
import { ConfirmData, ConfirmDialog } from '../confirm-dialog';
import { PrivilegesService } from '../privileges.service';
import { CONNECTED_APP_TRANSLATIONS } from './connected-app-translations';

/** A connected app's sign-in as the server reports it. */
export interface SignInStatus {
  appCode: string;
  clientId: string;
  managed: boolean;
  exists: boolean;
  enabled: boolean;
  inSync: boolean;
  redirectUris: string[];
  registeredRedirectUris: string[];
  issuer: string | null;
  audience: string;
  apiPath: string;
}

interface Credentials {
  signIn: SignInStatus;
  secret: string | null;
}

/** The state shown on the pill: not set up, in line, needing an update, disabled, or maintained elsewhere. */
export function signInState(
  status: SignInStatus,
): 'unmanaged' | 'notSet' | 'disabled' | 'inSync' | 'outOfSync' {
  if (!status.managed) return 'unmanaged';
  if (!status.exists) return 'notSet';
  if (!status.enabled) return 'disabled';
  return status.inSync ? 'inSync' : 'outOfSync';
}

/**
 * A connected app's sign-in (ADR-0036 step 7): the client it signs in with, where users return, whether the sign-in
 * server holds it as registered here, and the actions to set it up or replace its secret. A secret is shown once and
 * never kept by the platform.
 */
@Component({
  selector: 'dc-connected-sign-in-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ 'ca.signIn.title' | dcTranslate }} — {{ { ar: app.nameAr, en: app.nameEn } | dcLocalize }}
    </h2>
    <mat-dialog-content class="dc-si">
      @if (busy()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <p class="dc-si__note">{{ 'ca.signIn.intro' | dcTranslate }}</p>
      @if (errors().length) {
        <ul class="dc-errors" role="alert" data-testid="sign-in-errors">
          @for (e of errors(); track $index) {
            <li dir="auto">{{ e }}</li>
          }
        </ul>
      }
      @if (status(); as s) {
        <div class="dc-si__state">
          <mat-icon fontSet="material-symbols-outlined" class="dc-si__icon" aria-hidden="true"
            >key</mat-icon
          >
          <strong class="mono" dir="ltr">{{ s.clientId }}</strong>
          <span
            class="dc-pill"
            [class.dc-pill--green]="state() === 'inSync'"
            [class.dc-pill--gold]="state() === 'outOfSync' || state() === 'disabled'"
            data-testid="sign-in-state"
            >{{ 'ca.signIn.' + state() | dcTranslate }}</span
          >
        </div>
        @if (state() === 'unmanaged') {
          <p class="dc-si__warn" data-testid="sign-in-unmanaged">
            {{ 'ca.signIn.unmanaged' | dcTranslate }}
          </p>
        }

        @if (secret(); as value) {
          <section class="dc-si__secret" data-testid="sign-in-secret" role="status">
            <h3>{{ 'ca.signIn.secret' | dcTranslate }}</h3>
            <div class="dc-si__value">
              <code dir="ltr" data-testid="sign-in-secret-value">{{ value }}</code>
              <button
                matIconButton
                type="button"
                (click)="copy(value)"
                [matTooltip]="'ca.signIn.copy' | dcTranslate"
                [attr.aria-label]="'ca.signIn.copy' | dcTranslate"
              >
                <mat-icon fontSet="material-symbols-outlined">content_copy</mat-icon>
              </button>
            </div>
            <p>{{ 'ca.signIn.secretOnce' | dcTranslate }}</p>
          </section>
        }

        <dl class="dc-si__facts">
          <div>
            <dt>{{ 'ca.signIn.client' | dcTranslate }}</dt>
            <dd class="mono" dir="ltr">{{ s.clientId }}</dd>
          </div>
          <div>
            <dt>{{ 'ca.signIn.issuer' | dcTranslate }}</dt>
            <dd class="mono" dir="ltr">{{ s.issuer ?? '—' }}</dd>
          </div>
          <div>
            <dt>{{ 'ca.signIn.redirects' | dcTranslate }}</dt>
            <dd class="mono" dir="ltr">
              @for (u of s.redirectUris; track u) {
                <span>{{ u }}</span>
              } @empty {
                —
              }
            </dd>
          </div>
          @if (s.exists) {
            <div>
              <dt>{{ 'ca.signIn.registered' | dcTranslate }}</dt>
              <dd class="mono" dir="ltr" data-testid="sign-in-registered">
                @for (u of s.registeredRedirectUris; track u) {
                  <span>{{ u }}</span>
                } @empty {
                  —
                }
              </dd>
            </div>
          }
          <div>
            <dt>{{ 'ca.signIn.api' | dcTranslate }}</dt>
            <dd class="mono" dir="ltr">{{ s.apiPath }}</dd>
          </div>
          <div>
            <dt>{{ 'ca.signIn.audience' | dcTranslate }}</dt>
            <dd class="mono" dir="ltr">{{ s.audience }}</dd>
          </div>
        </dl>

        <section class="dc-si__how">
          <h3>{{ 'ca.signIn.how' | dcTranslate }}</h3>
          <ol>
            <li>{{ 'ca.signIn.how1' | dcTranslate }}</li>
            <li>
              {{ 'ca.signIn.how2' | dcTranslate }}
              @for (path of userCalls; track path) {
                <code class="dc-si__call" dir="ltr">GET {{ s.apiPath }}{{ path }}</code>
              }
            </li>
            <li>
              {{ 'ca.signIn.how3' | dcTranslate }}
              <code class="dc-si__call" dir="ltr">GET {{ s.apiPath }}/app</code>
            </li>
          </ol>
        </section>
        @if (s.managed && !canEdit()) {
          <p class="dc-muted">{{ 'ca.signIn.noEdit' | dcTranslate }}</p>
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" mat-dialog-close>{{ 'ca.close' | dcTranslate }}</button>
      @if (status(); as s) {
        @if (s.managed && canEdit()) {
          @if (s.exists) {
            <button
              matButton="outlined"
              type="button"
              (click)="newSecret()"
              [disabled]="busy()"
              data-testid="sign-in-new-secret"
            >
              <mat-icon fontSet="material-symbols-outlined">autorenew</mat-icon
              >{{ 'ca.signIn.newSecret' | dcTranslate }}
            </button>
          }
          @if (!s.inSync) {
            <button
              matButton="filled"
              type="button"
              (click)="setUp()"
              [disabled]="busy()"
              data-testid="sign-in-set-up"
            >
              <mat-icon fontSet="material-symbols-outlined">key</mat-icon
              >{{ (s.exists ? 'ca.signIn.update' : 'ca.signIn.setUp') | dcTranslate }}
            </button>
          }
        }
      }
    </mat-dialog-actions>
  `,
  styles: `
    .dc-si {
      display: grid;
      gap: 12px;
    }
    .dc-si__note {
      margin: 0;
      color: var(--dga-gray-600);
    }
    .dc-si__state {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 10px;
      padding: 12px;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius);
    }
    .dc-si__icon {
      color: var(--dynamic-cube-gold-700);
    }
    .dc-si__warn {
      margin: 0;
      padding: 8px 12px;
      border-radius: var(--shell-radius);
      background: var(--dynamic-cube-gold-50);
    }
    .dc-si__secret {
      padding: 12px;
      border: 1px solid var(--dynamic-cube-gold-600, #b8862f);
      border-radius: var(--shell-radius);
      background: var(--dynamic-cube-gold-50);
    }
    .dc-si__secret h3,
    .dc-si__how h3 {
      margin: 0 0 4px;
      font-size: 15px;
    }
    .dc-si__secret p {
      margin: 4px 0 0;
      font-size: 13px;
    }
    .dc-si__value {
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .dc-si__value code {
      flex: 1;
      padding: 6px 8px;
      border-radius: 6px;
      background: #fff;
      overflow-wrap: anywhere;
    }
    .dc-si__facts {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(18rem, 1fr));
      gap: 8px 16px;
      margin: 0;
    }
    .dc-si__facts dt {
      color: var(--dga-gray-500);
      font-size: 12px;
    }
    .dc-si__facts dd {
      display: grid;
      margin: 0;
      overflow-wrap: anywhere;
    }
    .dc-si__call {
      display: block;
      width: fit-content;
      margin: 2px 0;
      font-size: 12px;
    }
    .dc-si__how ol {
      margin: 0;
      padding-inline-start: 20px;
      color: var(--dga-gray-700);
      font-size: 13px;
    }
  `,
})
export class ConnectedSignInDialog {
  private readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  private readonly api = inject(AppsControlApi);
  private readonly i18n = inject(I18nService);
  private readonly dialog = inject(MatDialog);
  private readonly clipboard = inject(Clipboard);
  private readonly snackBar = inject(MatSnackBar);
  private readonly privileges = inject(PrivilegesService);

  protected readonly app = this.data.row!;
  protected readonly status = signal<SignInStatus | null>(null);
  protected readonly secret = signal<string | null>(null);
  protected readonly busy = signal(true);
  protected readonly errors = signal<string[]>([]);
  protected readonly state = computed(() =>
    this.status() ? signInState(this.status()!) : 'notSet',
  );
  protected readonly canEdit = computed(() =>
    this.privileges.can('apps-control.installation', 'EDIT'),
  );

  constructor() {
    this.i18n.register(CONNECTED_APP_TRANSLATIONS);
    void this.run(this.api.connectedAppSignIn<SignInStatus>(this.app.code), (s) =>
      this.status.set(s),
    );
  }

  /** What the app asks with a user's token. */
  protected readonly userCalls = ['/me', '/menu', '/privileges', '/lovs/{code}/values'];

  protected setUp(): void {
    void this.run(this.api.setUpConnectedAppSignIn<Credentials>(this.app.code), (c) =>
      this.show(c),
    );
  }

  protected async newSecret(): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog
        .open<ConfirmDialog, ConfirmData, string>(ConfirmDialog, {
          data: {
            title: this.i18n.translate('ca.signIn.newSecret'),
            message: this.i18n.translate('ca.signIn.confirmSecret'),
            confirmLabel: this.i18n.translate('ca.signIn.newSecret'),
            danger: true,
          },
        })
        .afterClosed(),
    );
    if (confirmed !== undefined) {
      void this.run(this.api.replaceConnectedAppSecret<Credentials>(this.app.code), (c) =>
        this.show(c),
      );
    }
  }

  protected copy(value: string): void {
    this.clipboard.copy(value);
    this.snackBar.open(this.i18n.translate('ca.signIn.copied'), undefined, { duration: 2500 });
  }

  private show(credentials: Credentials): void {
    this.status.set(credentials.signIn);
    if (credentials.secret) this.secret.set(credentials.secret);
  }

  private async run<T>(call: Observable<T>, done: (value: T) => void): Promise<void> {
    this.busy.set(true);
    this.errors.set([]);
    try {
      done(await firstValueFrom(call));
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }
}
