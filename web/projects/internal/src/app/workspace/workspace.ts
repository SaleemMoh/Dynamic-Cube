import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenavModule } from '@angular/material/sidenav';
import { RouterLink, RouterOutlet } from '@angular/router';
import { AppsControlApi, MenuGroupNode, NavMenu, RUNTIME_CONFIG, TranslatePipe } from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';

/** Signed-in layout: side navigation from the user's Apps Control menu, pages on the right. */
@Component({
  selector: 'dc-workspace',
  imports: [MatButtonModule, MatIconModule, MatSidenavModule, RouterLink, RouterOutlet, NavMenu, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-sidenav-container class="dc-workspace">
      <mat-sidenav mode="side" opened class="dc-workspace__nav">
        <a matButton routerLink="/" class="dc-workspace__home" data-menu="home">
          <mat-icon fontSet="material-symbols-outlined">home</mat-icon>
          {{ 'nav.home' | dcTranslate }}
        </a>
        <dc-nav-menu [groups]="menu()" />
      </mat-sidenav>
      <mat-sidenav-content class="dc-workspace__content">
        <router-outlet />
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    :host {
      display: flex;
      flex: 1;
    }
    .dc-workspace {
      flex: 1;
    }
    .dc-workspace__nav {
      width: 17rem;
      padding-block: 0.5rem;
    }
    .dc-workspace__home {
      margin-inline: 0.5rem;
    }
    .dc-workspace__content {
      padding: 1.5rem;
    }
  `,
})
export class Workspace {
  private readonly config = inject(RUNTIME_CONFIG);
  protected readonly menu = toSignal(inject(AppsControlApi).menu(this.config.app).pipe(catchError(() => of<MenuGroupNode[]>([]))), {
    initialValue: [],
  });
}
