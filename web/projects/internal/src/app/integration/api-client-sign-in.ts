import { Clipboard } from '@angular/cdk/clipboard';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  ApiClientCredentials,
  ApiClientSignIn,
  I18nService,
  LocalizePipe,
  PublicApiAdmin,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { Observable, firstValueFrom } from 'rxjs';
import { ConfigEditorData } from '../apps-control/config/config-editor';
import { ConfirmData, ConfirmDialog } from '../apps-control/confirm-dialog';
import { PrivilegesService } from '../apps-control/privileges.service';
import { PUBLIC_API_TRANSLATIONS } from './public-api-translations';

/** The state shown on the pill: maintained elsewhere, not set up, disabled, in line, or needing an update. */
export function clientState(
  s: ApiClientSignIn,
): 'unmanaged' | 'notSet' | 'disabled' | 'inSync' | 'outOfSync' {
  if (!s.managed) return 'unmanaged';
  if (!s.exists) return 'notSet';
  if (!s.enabled) return 'disabled';
  return s.inSync ? 'inSync' : 'outOfSync';
}

/**
 * An API client's sign-in (ADR-0042): its service-account client in the realm the public API trusts, the actions to set
 * it up or replace its secret (shown once, never kept), how the partner calls the API, and its OpenAPI description.
 */
@Component({
  selector: 'dc-api-client-sign-in',
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
      {{ 'pa.signIn.title' | dcTranslate }} — {{ { ar: row.nameAr, en: row.nameEn } | dcLocalize }}
    </h2>
    <mat-dialog-content class="cs">
      @if (busy()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <p class="cs-note">{{ 'pa.signIn.intro' | dcTranslate }}</p>
      @if (errors().length) {
        <ul class="dc-errors" role="alert" data-testid="pa-sign-in-errors">
          @for (e of errors(); track $index) {
            <li dir="auto">{{ e }}</li>
          }
        </ul>
      }
      @if (status(); as s) {
        <div class="cs-state">
          <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">key</mat-icon>
          <strong class="mono" dir="ltr">{{ s.clientId }}</strong>
          @if (state() !== 'unmanaged') {
            <span
              class="dc-pill"
              [class.dc-pill--green]="state() === 'inSync'"
              [class.dc-pill--gold]="state() === 'outOfSync' || state() === 'disabled'"
              data-testid="pa-sign-in-state"
              >{{ 'pa.signIn.' + state() | dcTranslate }}</span
            >
          }
        </div>
        @if (state() === 'unmanaged') {
          <p class="cs-warn">{{ 'pa.signIn.unmanaged' | dcTranslate }}</p>
        }
        @if (secret(); as value) {
          <section class="cs-secret" role="status" data-testid="pa-sign-in-secret">
            <h3>{{ 'pa.signIn.secret' | dcTranslate }}</h3>
            <div class="cs-value">
              <code dir="ltr">{{ value }}</code>
              <button
                matIconButton
                type="button"
                (click)="copy(value)"
                [matTooltip]="'pa.signIn.copy' | dcTranslate"
                [attr.aria-label]="'pa.signIn.copy' | dcTranslate"
              >
                <mat-icon fontSet="material-symbols-outlined">content_copy</mat-icon>
              </button>
            </div>
            <p>{{ 'pa.signIn.secretOnce' | dcTranslate }}</p>
          </section>
        }
        <dl class="cs-facts">
          <div>
            <dt>{{ 'pa.intro.token' | dcTranslate }}</dt>
            <dd class="mono" dir="ltr">{{ s.tokenUrl ?? '—' }}</dd>
          </div>
          <div>
            <dt>{{ 'pa.intro.api' | dcTranslate }}</dt>
            <dd class="mono" dir="ltr">{{ s.apiUrl }}</dd>
          </div>
          <div>
            <dt>{{ 'pa.intro.audience' | dcTranslate }}</dt>
            <dd class="mono" dir="ltr">{{ s.audience }}</dd>
          </div>
        </dl>
        <section>
          <h3>{{ 'pa.signIn.how' | dcTranslate }}</h3>
          <ol class="cs-how">
            <li>
              {{ 'pa.signIn.how1' | dcTranslate }}
              <code dir="ltr">POST {{ s.tokenUrl ?? '…/token' }}</code>
            </li>
            <li>
              {{ 'pa.signIn.how2' | dcTranslate }}
              <code dir="ltr">GET {{ s.apiUrl }}/…</code>
            </li>
            <li>
              {{ 'pa.signIn.how3' | dcTranslate }}
              <code dir="ltr">GET {{ s.apiUrl }}/openapi.json</code>
            </li>
          </ol>
        </section>
        @if (s.managed && !canEdit()) {
          <p class="dc-muted">{{ 'pa.signIn.noEdit' | dcTranslate }}</p>
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button
        matButton
        type="button"
        (click)="download()"
        [disabled]="busy()"
        data-testid="pa-openapi"
      >
        <mat-icon fontSet="material-symbols-outlined">download</mat-icon
        >{{ 'pa.signIn.download' | dcTranslate }}
      </button>
      <span class="cs-grow"></span>
      <button matButton type="button" mat-dialog-close>{{ 'pa.close' | dcTranslate }}</button>
      @if (status(); as s) {
        @if (s.managed && canEdit()) {
          @if (s.exists) {
            <button
              matButton="outlined"
              type="button"
              (click)="newSecret()"
              [disabled]="busy()"
              data-testid="pa-new-secret"
            >
              <mat-icon fontSet="material-symbols-outlined">autorenew</mat-icon
              >{{ 'pa.signIn.newSecret' | dcTranslate }}
            </button>
          }
          @if (!s.inSync) {
            <button
              matButton="filled"
              type="button"
              (click)="setUp()"
              [disabled]="busy()"
              data-testid="pa-set-up"
            >
              <mat-icon fontSet="material-symbols-outlined">key</mat-icon
              >{{ (s.exists ? 'pa.signIn.update' : 'pa.signIn.setUp') | dcTranslate }}
            </button>
          }
        }
      }
    </mat-dialog-actions>
  `,
  styles: `
    .cs {
      display: grid;
      gap: 12px;
    }
    .cs-note {
      margin: 0;
      color: var(--dga-gray-600);
    }
    .cs-state {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 10px;
      padding: 12px;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius);
    }
    .cs-state mat-icon {
      color: var(--dynamic-cube-gold-700);
    }
    .cs-warn,
    .cs-secret {
      margin: 0;
      padding: 10px 12px;
      border-radius: var(--shell-radius);
      background: var(--dynamic-cube-gold-50);
    }
    .cs-secret {
      border: 1px solid var(--dynamic-cube-gold-600, #b8862f);
    }
    h3 {
      margin: 0 0 4px;
      font-size: 15px;
    }
    .cs-secret p {
      margin: 4px 0 0;
      font-size: 13px;
    }
    .cs-value {
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .cs-value code {
      flex: 1;
      padding: 6px 8px;
      border-radius: 6px;
      background: #fff;
      overflow-wrap: anywhere;
    }
    .cs-facts {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(18rem, 1fr));
      gap: 8px 16px;
      margin: 0;
    }
    .cs-facts dt {
      color: var(--dga-gray-500);
      font-size: 12px;
    }
    .cs-facts dd {
      margin: 0;
      overflow-wrap: anywhere;
    }
    .cs-how {
      display: grid;
      gap: 4px;
      margin: 0;
      padding-inline-start: 20px;
      font-size: 13px;
    }
    .cs-how code {
      display: block;
      width: fit-content;
      font-size: 12px;
    }
    .cs-grow {
      flex: 1;
    }
  `,
})
export class ApiClientSignInDialog {
  private readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  private readonly api = inject(PublicApiAdmin);
  private readonly i18n = inject(I18nService);
  private readonly dialog = inject(MatDialog);
  private readonly clipboard = inject(Clipboard);
  private readonly snackBar = inject(MatSnackBar);
  private readonly privileges = inject(PrivilegesService);

  protected readonly row = this.data.row!;
  protected readonly status = signal<ApiClientSignIn | null>(null);
  protected readonly secret = signal<string | null>(null);
  protected readonly busy = signal(true);
  protected readonly errors = signal<string[]>([]);
  protected readonly state = computed(() =>
    this.status() ? clientState(this.status()!) : 'notSet',
  );
  protected readonly canEdit = computed(() => this.privileges.can('integration.config', 'EDIT'));

  constructor() {
    this.i18n.register(PUBLIC_API_TRANSLATIONS);
    void this.run(this.api.signIn(this.row.code), (s) => this.status.set(s));
  }

  protected setUp(): void {
    void this.run(this.api.setUpSignIn(this.row.code), (c) => this.show(c));
  }

  protected async newSecret(): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog
        .open<ConfirmDialog, ConfirmData, string>(ConfirmDialog, {
          data: {
            title: this.i18n.translate('pa.signIn.newSecret'),
            message: this.i18n.translate('pa.signIn.confirmSecret'),
            confirmLabel: this.i18n.translate('pa.signIn.newSecret'),
            danger: true,
          },
        })
        .afterClosed(),
    );
    if (confirmed !== undefined)
      void this.run(this.api.newSecret(this.row.code), (c) => this.show(c));
  }

  protected copy(value: string): void {
    this.clipboard.copy(value);
    this.snackBar.open(this.i18n.translate('pa.signIn.copied'), undefined, { duration: 2500 });
  }

  /** Saves the client's OpenAPI description, to hand to the partner. */
  protected download(): void {
    void this.run(this.api.description(this.row.code), (document) => {
      const blob = new Blob([JSON.stringify(document, null, 2)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const link = Object.assign(window.document.createElement('a'), {
        href: url,
        download: `${this.row.code.toLowerCase()}-openapi.json`,
      });
      link.click();
      URL.revokeObjectURL(url);
    });
  }

  private show(credentials: ApiClientCredentials): void {
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
