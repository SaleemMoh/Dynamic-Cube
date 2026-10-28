import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { Router } from '@angular/router';
import { firstValueFrom, interval } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { NotificationView, NotificationsApi } from './communication.api';

/** Polling interval of the unread count. */
const REFRESH_MS = 60_000;

/** Toolbar bell: the unread count, and the latest notifications in a menu (opening one marks it read and follows its link). */
@Component({
  selector: 'dc-notification-bell',
  imports: [DatePipe, MatBadgeModule, MatButtonModule, MatIconModule, MatMenuModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (auth.isAuthenticated()) {
      <button
        matIconButton
        class="dc-bell"
        [matMenuTriggerFor]="menu"
        (menuOpened)="load()"
        [attr.aria-label]="'notifications.title' | dcTranslate"
        data-testid="notification-bell"
      >
        <mat-icon fontSet="material-symbols-outlined" [matBadge]="unread() || null" matBadgeColor="warn" matBadgeSize="small" [matBadgeHidden]="!unread()"
          >notifications</mat-icon
        >
      </button>
      <mat-menu #menu="matMenu" class="dc-bell__menu" xPosition="before">
        <div class="dc-bell__head" (click)="$event.stopPropagation()" (keydown)="$event.stopPropagation()" role="presentation">
          <strong>{{ 'notifications.title' | dcTranslate }}</strong>
          @if (unread()) {
            <button matButton type="button" (click)="readAll()" data-testid="notifications-read-all">{{ 'notifications.readAll' | dcTranslate }}</button>
          }
        </div>
        @for (n of items(); track n.id) {
          <button mat-menu-item class="dc-bell__item" [class.dc-bell__item--unread]="!n.readAt" (click)="open(n)" [attr.data-testid]="'notification-' + n.id">
            <span class="dc-bell__title">{{ { ar: n.titleAr, en: n.titleEn } | dcLocalize }}</span>
            <span class="dc-bell__body">{{ { ar: n.bodyAr, en: n.bodyEn } | dcLocalize }}</span>
            <span class="dc-bell__time" dir="ltr">{{ n.createdAt | date: 'yyyy-MM-dd HH:mm' }}</span>
          </button>
        } @empty {
          <p class="dc-bell__empty">{{ 'notifications.none' | dcTranslate }}</p>
        }
      </mat-menu>
    }
  `,
  styles: `
    .dc-bell {
      align-self: center;
      color: inherit;
    }
    ::ng-deep .dc-bell__menu.mat-mdc-menu-panel {
      max-width: min(28rem, 92vw);
    }
    .dc-bell__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      padding: 0.5rem 1rem;
      min-width: 20rem;
    }
    .dc-bell__item {
      height: auto !important;
      min-height: 4rem;
      padding-block: 0.5rem !important;
      white-space: normal;
      max-width: 26rem;
    }
    .dc-bell__item ::ng-deep .mat-mdc-menu-item-text {
      display: grid;
      gap: 2px;
    }
    .dc-bell__item--unread {
      background: var(--dga-green-50, #f3fcf6);
    }
    .dc-bell__title {
      font-weight: 600;
    }
    .dc-bell__body {
      font-size: 13px;
      color: var(--dga-gray-700, #384250);
    }
    .dc-bell__time {
      font-size: 12px;
      color: var(--dga-gray-500, #6c737f);
      text-align: end;
    }
    .dc-bell__empty {
      padding: 0.5rem 1rem 1rem;
      margin: 0;
      color: var(--dga-gray-500, #6c737f);
    }
  `,
})
export class NotificationBell {
  /** Changing it reloads the count (e.g. the portal's acting party). */
  readonly scope = input<string | null | undefined>(null);

  protected readonly auth = inject(AuthService);
  private readonly api = inject(NotificationsApi);
  private readonly router = inject(Router);
  protected readonly unread = signal(0);
  protected readonly items = signal<NotificationView[]>([]);

  constructor() {
    effect(() => {
      this.scope();
      if (this.auth.isAuthenticated()) {
        void this.count();
      }
    });
    interval(REFRESH_MS)
      .pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => {
        if (this.auth.isAuthenticated() && document.visibilityState === 'visible') void this.count();
      });
  }

  protected async load(): Promise<void> {
    try {
      const page = await firstValueFrom(this.api.list(false, 10));
      this.items.set(page.items);
      this.unread.set(page.unread);
    } catch {
      // The bell is not worth an error on every page; the count stays as it was.
    }
  }

  protected async open(notification: NotificationView): Promise<void> {
    if (!notification.readAt) {
      await firstValueFrom(this.api.read(notification.id)).catch(() => undefined);
      this.unread.update((n) => Math.max(0, n - 1));
    }
    if (notification.link?.startsWith('/')) {
      await this.router.navigateByUrl(notification.link);
    }
  }

  protected async readAll(): Promise<void> {
    await firstValueFrom(this.api.readAll()).catch(() => undefined);
    await this.load();
  }

  private async count(): Promise<void> {
    try {
      this.unread.set((await firstValueFrom(this.api.unreadCount())).count);
    } catch {
      // See load().
    }
  }
}
