import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { TranslatePipe } from '../i18n/translate.pipe';
import { AuthService } from './auth.service';

/** Toolbar control: sign-in button, or the signed-in user's name with a sign-out menu. */
@Component({
  selector: 'dc-user-menu',
  imports: [MatButtonModule, MatIconModule, MatMenuModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (auth.user(); as user) {
      <button matButton class="dc-user-menu" [matMenuTriggerFor]="menu" data-testid="user-menu">
        <mat-icon fontSet="material-symbols-outlined">account_circle</mat-icon>
        {{ user.name }}
      </button>
      <mat-menu #menu="matMenu">
        <button mat-menu-item (click)="auth.logout()" data-testid="logout">
          <mat-icon fontSet="material-symbols-outlined">logout</mat-icon>
          {{ 'auth.logout' | dcTranslate }}
        </button>
      </mat-menu>
    } @else {
      <button matButton class="dc-user-menu" (click)="auth.login()" data-testid="login">
        <mat-icon fontSet="material-symbols-outlined">login</mat-icon>
        {{ 'auth.login' | dcTranslate }}
      </button>
    }
  `,
  styles: `
    .dc-user-menu {
      color: inherit;
    }
  `,
})
export class UserMenu {
  protected readonly auth = inject(AuthService);
}
