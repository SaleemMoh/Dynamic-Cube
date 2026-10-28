import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActingPartyService, AuthService, HostInfo, HostInfoService, LocalizePipe, TranslatePipe } from '@dynamic-cube/shared';
import { MatCardModule } from '@angular/material/card';
import { catchError, map, of } from 'rxjs';


type ApiStatus = { state: 'loading' } | { state: 'connected'; info: HostInfo } | { state: 'unavailable' };

@Component({
  selector: 'dc-home',
  imports: [MatCardModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="dc-home__description">{{ 'app.description' | dcTranslate }}</p>

    @if (auth.isAuthenticated()) {
      <mat-card appearance="outlined" data-testid="acting-card">
        <mat-card-header><mat-card-title>{{ 'home.acting' | dcTranslate }}</mat-card-title></mat-card-header>
        <mat-card-content>
          @if (acting.current(); as current) {
            <p>
              {{ 'home.actingNow' | dcTranslate }}
              <strong data-testid="acting-current">{{ { ar: current.nameAr, en: current.nameEn } | dcLocalize }}</strong>
            </p>
          }
          <ul class="dc-home__contexts">
            @if (acting.individual(); as me) {
              <li>
                <button class="dc-home__context" [class.dc-home__context--current]="acting.current()?.partyId === me.partyId" (click)="acting.select(null)">
                  <span>{{ { ar: me.nameAr, en: me.nameEn } | dcLocalize }}</span>
                  <span class="dc-muted">{{ 'acting.self' | dcTranslate }} · <span dir="ltr">{{ me.identifier }}</span></span>
                </button>
              </li>
            }
            @for (c of acting.contexts(); track c.partyId) {
              <li>
                <button class="dc-home__context" [class.dc-home__context--current]="acting.current()?.partyId === c.partyId" (click)="acting.select(c.partyId)" [attr.data-testid]="'context-' + c.identifier">
                  <span>{{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}</span>
                  <span class="dc-muted">{{ { ar: c.relationshipNameAr ?? '', en: c.relationshipNameEn ?? '' } | dcLocalize }} · <span dir="ltr">{{ c.identifier }}</span></span>
                </button>
              </li>
            }
          </ul>
          @for (w of acting.warnings(); track w.source) {
            <p class="dc-home__warning" role="status">{{ { ar: w.messageAr ?? '', en: w.messageEn ?? '' } | dcLocalize }}</p>
          }
        </mat-card-content>
      </mat-card>
    }

    <mat-card appearance="outlined">
      <mat-card-header><mat-card-title>{{ 'home.api' | dcTranslate }}</mat-card-title></mat-card-header>
      <mat-card-content>
      @switch (status().state) {
        @case ('connected') {
          @if (status(); as s) {
            @if (s.state === 'connected') {
              <span class="dc-tag dc-tag--success">{{ 'home.api.connected' | dcTranslate }}</span>
              <span class="dc-home__version" dir="ltr">{{ s.info.app }} · {{ s.info.version }}</span>
            }
          }
        }
        @case ('unavailable') {
          <span class="dc-tag dc-tag--danger">{{ 'home.api.unavailable' | dcTranslate }}</span>
        }
        @default {
          <span class="dc-tag">…</span>
        }
      }
      </mat-card-content>
    </mat-card>

  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .dc-home__description {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
    mat-card-content {
      padding-top: 1rem;
    }
    .dc-tag {
      display: inline-block;
      padding: 0.125rem 0.625rem;
      border-radius: 999px;
      font-size: 0.8125rem;
      font-weight: 600;
      background: var(--mat-sys-surface-container-high);
    }
    .dc-tag--success {
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }
    .dc-tag--danger {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .dc-home__contexts {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr));
      gap: 0.75rem;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .dc-home__context {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      width: 100%;
      padding: 0.75rem 1rem;
      border: 1px solid var(--dga-border, #d2d6db);
      border-radius: 8px;
      background: #fff;
      font: inherit;
      text-align: start;
      cursor: pointer;
    }
    .dc-home__context--current {
      border-color: var(--dga-primary, #1b8354);
      box-shadow: inset 0 0 0 1px var(--dga-primary, #1b8354);
    }
    .dc-home__warning {
      color: #b54708;
    }
    .dc-home__version {
      margin-inline-start: 0.75rem;
      font-family: monospace;
    }
    .dc-home__result {
      background: var(--mat-sys-surface-container);
      padding: 1rem;
      border-radius: var(--dc-radius);
      overflow: auto;
    }
  `,
})
export class Home {
  protected readonly auth = inject(AuthService);
  protected readonly acting = inject(ActingPartyService);
  protected readonly status = toSignal(
    inject(HostInfoService)
      .get()
      .pipe(
        map((info): ApiStatus => ({ state: 'connected', info })),
        catchError(() => of<ApiStatus>({ state: 'unavailable' })),
      ),
    { initialValue: { state: 'loading' } as ApiStatus },
  );

}
