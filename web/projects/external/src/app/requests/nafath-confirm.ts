import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import {
  I18nService,
  NafathChallenge,
  ServicesApi,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { Observable, firstValueFrom } from 'rxjs';

/** How often the dialog asks where the confirmation stands. */
const POLL_MS = 3000;

type Stage = 'starting' | 'waiting' | 'verified' | 'rejected' | 'expired' | 'failed';

/**
 * Confirming a draft with Nafath before it is submitted (ADR-0043): a request goes to the applicant's Nafath app, the
 * dialog shows the number to choose there and asks every few seconds where it stands. It closes with the confirmation's
 * id once Nafath has the person's signed answer; a rejected or expired request can be sent again.
 */
@Component({
  selector: 'dc-nafath-confirm',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressSpinnerModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'nafath.title' | dcTranslate }}</h2>
    <mat-dialog-content class="dc-nafath" aria-live="polite">
      @switch (stage()) {
        @case ('starting') {
          <div class="dc-nafath__line">
            <mat-spinner diameter="24" />
            <span>{{ 'nafath.starting' | dcTranslate }}</span>
          </div>
        }
        @case ('waiting') {
          <p>{{ 'nafath.intro' | dcTranslate }}</p>
          <div
            class="dc-nafath__number"
            [attr.aria-label]="'nafath.number' | dcTranslate"
            data-testid="nafath-number"
            dir="ltr"
          >
            {{ challenge()?.random }}
          </div>
          <div class="dc-nafath__line">
            <mat-spinner diameter="20" />
            <span>{{ 'nafath.waiting' | dcTranslate }}</span>
          </div>
          <p class="dc-muted dc-nafath__remaining">{{ remainingText() }}</p>
          <p class="dc-muted dc-nafath__help">{{ 'nafath.help' | dcTranslate }}</p>
        }
        @case ('verified') {
          <div class="dc-nafath__line dc-nafath__line--ok" data-testid="nafath-verified">
            <mat-icon fontSet="material-symbols-outlined">verified</mat-icon>
            <span>{{ 'nafath.verified' | dcTranslate }}</span>
          </div>
        }
        @default {
          <div
            class="dc-nafath__line dc-nafath__line--bad"
            role="alert"
            data-testid="nafath-problem"
          >
            <mat-icon fontSet="material-symbols-outlined">error</mat-icon>
            <span>{{ problem() }}</span>
          </div>
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="close()" data-testid="nafath-cancel">
        {{ 'nafath.cancel' | dcTranslate }}
      </button>
      @if (stage() === 'rejected' || stage() === 'expired' || stage() === 'failed') {
        <button matButton="filled" type="button" (click)="start()" data-testid="nafath-retry">
          {{ 'nafath.retry' | dcTranslate }}
        </button>
      }
    </mat-dialog-actions>
  `,
  styles: `
    .dc-nafath {
      min-width: min(420px, 80vw);
    }
    .dc-nafath__number {
      display: grid;
      place-items: center;
      width: 96px;
      height: 96px;
      margin: 16px auto;
      border-radius: 50%;
      background: var(--dga-green-50);
      border: 2px solid var(--dga-green-600);
      color: var(--dga-green-700);
      font-size: 40px;
      font-weight: 700;
    }
    .dc-nafath__line {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .dc-nafath__line--ok {
      color: var(--dga-green-700);
    }
    .dc-nafath__line--bad {
      color: var(--dga-error);
    }
    .dc-nafath__remaining {
      margin: 8px 0 0;
    }
    .dc-nafath__help {
      margin: 12px 0 0;
      font-size: 13px;
    }
  `,
})
export class NafathConfirm {
  private readonly api = inject(ServicesApi);
  private readonly i18n = inject(I18nService);
  private readonly ref = inject(MatDialogRef<NafathConfirm, string>);
  private readonly requestId = inject<{ requestId: string }>(MAT_DIALOG_DATA).requestId;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private clock: ReturnType<typeof setInterval> | null = null;
  private attempt = 0;

  protected readonly stage = signal<Stage>('starting');
  protected readonly challenge = signal<NafathChallenge | null>(null);
  protected readonly message = signal<string | null>(null);
  private readonly now = signal(Date.now());

  protected readonly remainingText = computed(() => {
    const challenge = this.challenge();
    const seconds = challenge
      ? Math.max(0, Math.round((Date.parse(challenge.expiresAt) - this.now()) / 1000))
      : 0;
    return this.i18n.translate('nafath.remaining').replace('{seconds}', String(seconds));
  });

  protected readonly problem = computed(() => {
    switch (this.stage()) {
      case 'rejected':
        return this.i18n.translate('nafath.rejected');
      case 'expired':
        return this.i18n.translate('nafath.expired');
      default:
        return this.message() ?? '';
    }
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stop());
    void this.start();
  }

  protected async start(): Promise<void> {
    this.stop();
    const attempt = ++this.attempt;
    this.stage.set('starting');
    this.message.set(null);
    try {
      const challenge = await firstValueFrom(this.api.startNafath(this.requestId));
      if (attempt !== this.attempt) return;
      this.challenge.set(challenge);
      this.stage.set('waiting');
      this.clock = setInterval(() => this.now.set(Date.now()), 1000);
      this.schedule(attempt);
    } catch (error) {
      if (attempt === this.attempt) this.fail(error);
    }
  }

  protected close(): void {
    this.stop();
    this.ref.close();
  }

  private schedule(attempt: number): void {
    this.timer = setTimeout(() => void this.poll(attempt), POLL_MS);
  }

  private async poll(attempt: number): Promise<void> {
    const challenge = this.challenge();
    if (!challenge || attempt !== this.attempt) return;
    try {
      const check = await firstValueFrom(this.api.nafathStatus(this.requestId, challenge.checkId));
      if (attempt !== this.attempt) return;
      if (check.status === 'WAITING') {
        this.schedule(attempt);
        return;
      }
      this.stop();
      this.stage.set(
        check.status === 'VERIFIED'
          ? 'verified'
          : check.status === 'REJECTED'
            ? 'rejected'
            : 'expired',
      );
      if (check.status === 'VERIFIED') this.ref.close(check.checkId);
    } catch (error) {
      if (attempt === this.attempt) this.fail(error);
    }
  }

  private fail(error: unknown): void {
    this.stop();
    this.message.set(problemMessages(error).join(' '));
    this.stage.set('failed');
  }

  private stop(): void {
    if (this.timer) clearTimeout(this.timer);
    if (this.clock) clearInterval(this.clock);
    this.timer = null;
    this.clock = null;
  }
}

/** Opens the dialog; emits the confirmation's id, or undefined when the applicant closed it first. */
export function confirmWithNafath(
  dialog: MatDialog,
  requestId: string,
): Observable<string | undefined> {
  return dialog
    .open<NafathConfirm, { requestId: string }, string>(NafathConfirm, {
      data: { requestId },
      disableClose: true,
      autoFocus: false,
    })
    .afterClosed();
}
