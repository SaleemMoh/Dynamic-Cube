import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import {
  ApiFunctionInfo,
  I18nService,
  LocalizePipe,
  PublicApiAdmin,
  PublicApiInfo,
  TranslatePipe,
} from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';
import { PUBLIC_API_TRANSLATIONS } from './public-api-translations';

/** Above the endpoints and clients: where partners call the API, where they get tokens, and the system functions. */
@Component({
  selector: 'dc-api-endpoints-intro',
  imports: [LocalizePipe, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="ai" data-testid="pa-intro">
      <mat-icon fontSet="material-symbols-outlined" class="ai-icon" aria-hidden="true"
        >webhook</mat-icon
      >
      <div class="ai-text">
        <h2>{{ 'pa.intro.title' | dcTranslate }}</h2>
        <p>{{ 'pa.intro.text' | dcTranslate }}</p>
        @if (info(); as i) {
          <dl>
            <div>
              <dt>{{ 'pa.intro.api' | dcTranslate }}</dt>
              <dd dir="ltr">{{ i.apiUrl }}</dd>
            </div>
            <div>
              <dt>{{ 'pa.intro.token' | dcTranslate }}</dt>
              <dd dir="ltr">{{ i.tokenUrl ?? '—' }}</dd>
            </div>
            <div>
              <dt>{{ 'pa.intro.audience' | dcTranslate }}</dt>
              <dd dir="ltr">{{ i.audience }}</dd>
            </div>
          </dl>
        }
        @if (functions().length) {
          <p class="ai-functions">
            <b>{{ 'pa.intro.functions' | dcTranslate }}:</b>
            @for (f of functions(); track f.code) {
              <span class="dc-pill">{{ { ar: f.nameAr, en: f.nameEn } | dcLocalize }}</span>
            }
          </p>
        }
      </div>
    </section>
  `,
  styles: `
    .ai {
      display: flex;
      gap: 12px;
      margin-bottom: 12px;
      padding: 14px 16px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 12px;
      background: var(--dga-gray-50, #f9fafb);
    }
    .ai-icon {
      color: var(--dynamic-cube-gold, #ca9f45);
    }
    .ai-text {
      display: grid;
      gap: 6px;
    }
    h2 {
      margin: 0;
      font-size: 16px;
    }
    p {
      margin: 0;
      color: var(--dga-gray-700, #384250);
      font-size: 13px;
    }
    dl {
      display: flex;
      flex-wrap: wrap;
      gap: 6px 24px;
      margin: 0;
    }
    dt {
      color: var(--dga-gray-500, #6c737f);
      font-size: 12px;
    }
    dd {
      margin: 0;
      font-family: ui-monospace, monospace;
      font-size: 12px;
    }
    .ai-functions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
    }
  `,
})
export class ApiEndpointsIntro {
  private readonly api = inject(PublicApiAdmin);
  protected readonly info = toSignal(
    this.api.info().pipe(catchError(() => of(null as PublicApiInfo | null))),
    {
      initialValue: null as PublicApiInfo | null,
    },
  );
  protected readonly functions = toSignal(
    this.api.functions().pipe(catchError(() => of([] as ApiFunctionInfo[]))),
    {
      initialValue: [] as ApiFunctionInfo[],
    },
  );

  constructor() {
    inject(I18nService).register(PUBLIC_API_TRANSLATIONS);
  }
}
