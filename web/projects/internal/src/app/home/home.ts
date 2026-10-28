import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { HostInfo, HostInfoService, TranslatePipe } from '@dynamic-cube/shared';
import { MatCardModule } from '@angular/material/card';
import { catchError, map, of } from 'rxjs';
import { JsonPipe } from '@angular/common';
import { DynamicForm, FormValue } from '@dynamic-cube/shared';
import { SAMPLE_SCHEMA } from './sample-schema';

type ApiStatus = { state: 'loading' } | { state: 'connected'; info: HostInfo } | { state: 'unavailable' };

@Component({
  selector: 'dc-home',
  imports: [MatCardModule, TranslatePipe, DynamicForm, JsonPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="dc-home__description">{{ 'app.description' | dcTranslate }}</p>

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

    <mat-card appearance="outlined">
      <mat-card-header><mat-card-title>{{ 'home.preview' | dcTranslate }}</mat-card-title></mat-card-header>
      <mat-card-content>
      <dc-dynamic-form [schema]="schema" (submitted)="submittedValue.set($event)" />
      @if (submittedValue(); as value) {
        <h3>{{ 'home.preview.result' | dcTranslate }}</h3>
        <pre class="dc-home__result" dir="ltr">{{ value | json }}</pre>
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
  protected readonly status = toSignal(
    inject(HostInfoService)
      .get()
      .pipe(
        map((info): ApiStatus => ({ state: 'connected', info })),
        catchError(() => of<ApiStatus>({ state: 'unavailable' })),
      ),
    { initialValue: { state: 'loading' } as ApiStatus },
  );

  protected readonly schema = SAMPLE_SCHEMA;
  protected readonly submittedValue = signal<FormValue | null>(null);
}
