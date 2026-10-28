import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router } from '@angular/router';
import { TranslatePipe } from '../i18n/translate.pipe';
import { AuthService } from './auth.service';

/** Landing page of the identity-provider redirect. */
@Component({
  selector: 'dc-auth-callback',
  imports: [MatProgressSpinnerModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-callback">
      @if (failed()) {
        <p role="alert">{{ 'auth.failed' | dcTranslate }}</p>
      } @else {
        <mat-spinner diameter="40" />
        <p>{{ 'auth.signingIn' | dcTranslate }}</p>
      }
    </div>
  `,
  styles: `
    .dc-callback {
      display: grid;
      justify-items: center;
      gap: 1rem;
      padding-block: 4rem;
    }
  `,
})
export class AuthCallback implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly failed = signal(false);

  async ngOnInit(): Promise<void> {
    try {
      await this.router.navigateByUrl(await this.auth.completeLogin(), { replaceUrl: true });
    } catch {
      this.failed.set(true);
    }
  }
}
