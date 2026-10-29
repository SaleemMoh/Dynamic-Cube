import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService, ContentApi, RatingSummary, TranslatePipe } from '@dynamic-cube/shared';
import { catchError, firstValueFrom, of } from 'rxjs';

/**
 * "This service is rated 3.9 on average" with stars and the number of ratings, and "Rate this service": signed-in
 * users pick one to five stars (rating again replaces their stars); others are asked to sign in first.
 */
@Component({
  selector: 'dc-service-rating',
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="dc-rating" data-testid="service-rating">
      <div class="dc-rating__summary">
        @if (summary(); as s) {
          @if (s.count) {
            <span
              >{{ 'rating.average' | dcTranslate }} <strong dir="ltr">{{ s.average.toFixed(1) }}</strong></span
            >
            <span class="dc-rating__stars" [attr.aria-label]="s.average.toFixed(1) + ' / 5'" role="img">
              @for (star of stars; track star) {
                <mat-icon fontSet="material-symbols-outlined" [class.dc-rating__star--on]="star <= s.average + 0.25" aria-hidden="true">star</mat-icon>
              }
            </span>
            <span class="dc-muted" data-testid="rating-count">{{ ('rating.count' | dcTranslate).replace('{count}', '' + s.count) }}</span>
          } @else {
            <span class="dc-muted">{{ 'rating.none' | dcTranslate }}</span>
          }
        }
      </div>
      @if (choosing()) {
        <div class="dc-rating__choose" role="radiogroup" [attr.aria-label]="'rating.rate' | dcTranslate">
          @for (star of stars; track star) {
            <button
              type="button"
              class="dc-rating__pick"
              role="radio"
              [attr.aria-checked]="star === summary()?.mine"
              [class.dc-rating__pick--on]="star <= (hover() || summary()?.mine || 0)"
              (mouseenter)="hover.set(star)"
              (mouseleave)="hover.set(0)"
              (click)="rate(star)"
              [disabled]="busy()"
              [attr.aria-label]="star + ' / 5'"
              [attr.data-testid]="'rate-' + star"
            >
              <mat-icon fontSet="material-symbols-outlined">star</mat-icon>
            </button>
          }
        </div>
      } @else {
        <button matButton="filled" type="button" (click)="start()" data-testid="rate-service">
          {{ (summary()?.mine ? 'rating.change' : 'rating.rate') | dcTranslate }}
        </button>
      }
    </section>
  `,
  styles: `
    .dc-rating {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 12px 24px;
      padding-block: 24px;
      border-top: 2px solid var(--dga-green-700);
    }
    .dc-rating__summary {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 12px;
      font-size: 16px;
    }
    .dc-rating__stars {
      display: inline-flex;
      gap: 4px;
    }
    .dc-rating__stars mat-icon,
    .dc-rating__pick mat-icon {
      font-size: 32px;
      width: 32px;
      height: 32px;
      color: var(--dga-gray-200);
      font-variation-settings: 'FILL' 1;
    }
    .dc-rating__stars .dc-rating__star--on,
    .dc-rating__pick--on mat-icon {
      color: var(--dga-green-700);
    }
    .dc-rating__choose {
      display: flex;
      gap: 4px;
    }
    .dc-rating__pick {
      display: inline-flex;
      padding: 4px;
      border: none;
      border-radius: 4px;
      background: none;
      cursor: pointer;
    }
    .dc-rating__pick:hover {
      background: var(--dga-gray-100);
    }
  `,
})
export class ServiceRating {
  readonly service = input.required<string>();

  private readonly api = inject(ContentApi);
  private readonly auth = inject(AuthService);
  protected readonly stars = [1, 2, 3, 4, 5];
  protected readonly choosing = signal(false);
  protected readonly hover = signal(0);
  protected readonly busy = signal(false);
  private readonly loaded = rxResource({
    params: () => ({ service: this.service(), signedIn: this.auth.isAuthenticated() }),
    stream: ({ params }) => this.api.rating(params.service).pipe(catchError(() => of(null))),
  });
  private readonly latest = signal<RatingSummary | null>(null);
  protected readonly summary = computed(() => this.latest() ?? this.loaded.value() ?? null);

  protected start(): void {
    if (!this.auth.isAuthenticated()) {
      void this.auth.login(`/services/${encodeURIComponent(this.service())}`);
      return;
    }
    this.choosing.set(true);
  }

  protected async rate(stars: number): Promise<void> {
    this.busy.set(true);
    try {
      this.latest.set(await firstValueFrom(this.api.rate(this.service(), stars)));
      this.choosing.set(false);
    } catch {
      // Rating is optional; on failure the stars stay open to try again.
    } finally {
      this.busy.set(false);
      this.hover.set(0);
    }
  }
}
