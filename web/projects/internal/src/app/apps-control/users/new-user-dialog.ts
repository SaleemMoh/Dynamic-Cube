import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppsControlApi, ConfigRow, I18nService, LocalizePipe, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { catchError, firstValueFrom, of } from 'rxjs';
import { DraftsService } from '../drafts.service';
import { temporaryPassword } from './password';

const USER_NAME = /^[A-Za-z0-9][A-Za-z0-9_.@-]{0,255}$/;
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * Adds an employee: the sign-in account (when Apps Control maintains accounts) with a temporary password or an email
 * link, and the Apps Control user with names, contact details and roles, which goes through the users change set. When
 * accounts come from a directory or identity provider, only the user is added and the account is linked on first sign-in.
 */
@Component({
  selector: 'dc-new-user-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTooltipModule,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'newUser.title' | dcTranslate }}</h2>
    <mat-dialog-content class="dc-nu">
      @if (busy()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (caps(); as c) {
        @if (!c.canCreate) {
          <p class="dc-nu__note" data-testid="new-user-federated">
            <mat-icon fontSet="material-symbols-outlined">info</mat-icon>
            {{ (c.provider === 'None' ? 'newUser.unmanaged' : 'newUser.federated') | dcTranslate }}
            @if (c.federations.length) {
              ({{ c.federations.join('، ') }})
            }
          </p>
        }
      }
      <div class="dc-nu__grid">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'newUser.userName' | dcTranslate }}</mat-label>
          <input matInput dir="ltr" [ngModel]="userName()" (ngModelChange)="userName.set($event)" data-testid="new-user-name" />
          <mat-hint>{{ 'newUser.userNameHint' | dcTranslate }}</mat-hint>
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'newUser.email' | dcTranslate }}</mat-label>
          <input matInput dir="ltr" type="email" [ngModel]="email()" (ngModelChange)="email.set($event)" data-testid="new-user-email" />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'config.nameAr' | dcTranslate }}</mat-label>
          <input matInput dir="rtl" [ngModel]="nameAr()" (ngModelChange)="nameAr.set($event)" data-testid="new-user-name-ar" />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'config.nameEn' | dcTranslate }}</mat-label>
          <input matInput dir="ltr" [ngModel]="nameEn()" (ngModelChange)="nameEn.set($event)" data-testid="new-user-name-en" />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'newUser.mobile' | dcTranslate }}</mat-label>
          <input matInput dir="ltr" [ngModel]="mobile()" (ngModelChange)="mobile.set($event)" />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'newUser.roles' | dcTranslate }}</mat-label>
          <mat-select multiple [ngModel]="roles()" (ngModelChange)="roles.set($event)" data-testid="new-user-roles">
            @for (r of roleOptions(); track r.code) {
              <mat-option [value]="r.code">{{ { ar: r.nameAr, en: r.nameEn } | dcLocalize }} ({{ r.code }})</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>

      @if (caps()?.canCreate) {
        <section class="dc-nu__section" data-testid="new-user-account">
          <h3>{{ 'newUser.account' | dcTranslate }}</h3>
          <mat-button-toggle-group [ngModel]="mode()" (ngModelChange)="mode.set($event)" [attr.aria-label]="'newUser.account' | dcTranslate">
            <mat-button-toggle value="temporary">{{ 'account.temporary' | dcTranslate }}</mat-button-toggle>
            <mat-button-toggle value="email">{{ 'account.byEmail' | dcTranslate }}</mat-button-toggle>
          </mat-button-toggle-group>
          @if (mode() === 'temporary') {
            <mat-form-field subscriptSizing="dynamic" class="dc-nu__password">
              <mat-label>{{ 'account.temporary' | dcTranslate }}</mat-label>
              <input matInput dir="ltr" [ngModel]="password()" (ngModelChange)="password.set($event)" data-testid="new-user-password" />
              <button matIconButton matSuffix type="button" (click)="password.set(generate())" [matTooltip]="'account.generate' | dcTranslate">
                <mat-icon fontSet="material-symbols-outlined">autorenew</mat-icon>
              </button>
            </mat-form-field>
            <p class="dc-muted">{{ 'account.temporaryHint' | dcTranslate }}</p>
          } @else {
            <p class="dc-muted">
              {{ 'account.byEmailHint' | dcTranslate }} <span dir="ltr">{{ email() || '—' }}</span>
            </p>
          }
        </section>
      }

      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li dir="auto">{{ e }}</li>
          }
        </ul>
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      @if (problem(); as p) {
        <span class="dc-nu__problem" data-testid="new-user-problem">{{ p | dcTranslate }}</span>
      }
      <button matButton mat-dialog-close type="button">{{ 'form.cancel' | dcTranslate }}</button>
      <button matButton="filled" type="button" [disabled]="!!problem() || busy()" (click)="save()" data-testid="new-user-save">
        {{ (caps()?.canCreate ? 'newUser.saveWithAccount' : 'newUser.save') | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-nu {
      display: grid;
      gap: 12px;
    }
    .dc-nu__grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 12px;
    }
    .dc-nu__section {
      display: grid;
      justify-items: start;
      gap: 8px;
      padding-top: 12px;
      border-top: 1px solid var(--dga-gray-200);
    }
    .dc-nu__section h3 {
      margin: 0;
      font-size: 14px;
    }
    .dc-nu__section p {
      margin: 0;
      font-size: 12px;
    }
    .dc-nu__password {
      width: 18rem;
    }
    .dc-nu__note {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0;
      padding: 10px 12px;
      border-radius: var(--shell-radius);
      background: var(--dga-gray-50);
      font-size: 13px;
    }
    .dc-nu__problem {
      margin-inline-end: auto;
      color: #b42318;
      font-size: 13px;
    }
    @media (max-width: 640px) {
      .dc-nu__grid {
        grid-template-columns: minmax(0, 1fr);
      }
    }
  `,
})
export class NewUserDialog {
  private readonly ref = inject(MatDialogRef<NewUserDialog, boolean>);
  private readonly api = inject(AppsControlApi);
  private readonly drafts = inject(DraftsService);
  private readonly i18n = inject(I18nService);

  protected readonly capabilities = rxResource({ stream: () => this.api.accountCapabilities().pipe(catchError(() => of(null))) });
  private readonly roleRows = rxResource({ stream: () => this.api.list('apps.role').pipe(catchError(() => of([] as ConfigRow[]))) });
  protected readonly caps = computed(() => (this.capabilities.hasValue() ? this.capabilities.value() : null));
  protected readonly roleOptions = computed(() => (this.roleRows.hasValue() ? this.roleRows.value() : []).filter((r) => r.isActive !== false));

  protected readonly userName = signal('');
  protected readonly email = signal('');
  protected readonly nameAr = signal('');
  protected readonly nameEn = signal('');
  protected readonly mobile = signal('');
  protected readonly roles = signal<string[]>([]);
  protected readonly mode = signal<'temporary' | 'email'>('temporary');
  protected readonly password = signal(temporaryPassword());
  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly generate = temporaryPassword;

  protected readonly problem = computed(() => {
    if (!USER_NAME.test(this.userName().trim())) return 'newUser.problem.userName';
    if (!this.nameAr().trim() || !this.nameEn().trim()) return 'newUser.problem.names';
    if (this.email().trim() && !EMAIL.test(this.email().trim())) return 'newUser.problem.email';
    if (this.caps()?.canCreate) {
      if (this.mode() === 'temporary' && this.password().length < 8) return 'newUser.problem.password';
      if (this.mode() === 'email' && !this.email().trim()) return 'newUser.problem.emailNeeded';
    }
    return null;
  });

  protected async save(): Promise<void> {
    this.busy.set(true);
    this.errors.set([]);
    const userName = this.userName().trim().toLowerCase();
    try {
      if (this.caps()?.canCreate) {
        const [firstName, ...rest] = this.nameEn().trim().split(/\s+/);
        await firstValueFrom(
          this.api.createAccount({
            userName,
            email: this.email().trim() || null,
            firstName: firstName || null,
            lastName: rest.join(' ') || null,
            temporaryPassword: this.mode() === 'temporary' ? this.password() : null,
            sendSetupEmail: this.mode() === 'email',
          }),
        );
      }
      await this.drafts.addChange('apps.user', userName, 'CREATE', {
        nameAr: this.nameAr().trim(),
        nameEn: this.nameEn().trim(),
        isActive: true,
        email: this.email().trim() || null,
        mobile: this.mobile().trim() || null,
        status: 'ACTIVE',
        roles: this.roles().map((roleCode) => ({ roleCode })),
        assignments: [],
      });
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }
}
