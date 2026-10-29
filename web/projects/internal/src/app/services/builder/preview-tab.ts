import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import {
  ResolvedVersion,
  ServiceWizard,
  ServicesApi,
  TranslatePipe,
  VersionDefinition,
  WizardMove,
  browserFileUploader,
  problemMessages,
} from '@dynamic-cube/shared';
import { Subscription, timer } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { BuilderStore } from './builder-store';

/**
 * The applicant's wizard for the version being built, as it is now: questions resolved on the server from the bank,
 * rules evaluated as in the portal, nothing saved. On a desktop or a phone width; the language follows the app's.
 */
@Component({
  selector: 'dc-builder-preview-tab',
  imports: [FormsModule, MatButtonToggleModule, MatIconModule, ServiceWizard, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-pt">
      <div class="dc-pt__bar">
        <mat-button-toggle-group [ngModel]="device()" (ngModelChange)="device.set($event)" [attr.aria-label]="'builder.preview.device' | dcTranslate">
          <mat-button-toggle value="desktop" data-testid="preview-desktop">
            <mat-icon fontSet="material-symbols-outlined">desktop_windows</mat-icon>
            {{ 'builder.preview.desktop' | dcTranslate }}
          </mat-button-toggle>
          <mat-button-toggle value="mobile" data-testid="preview-mobile">
            <mat-icon fontSet="material-symbols-outlined">smartphone</mat-icon>
            {{ 'builder.preview.mobile' | dcTranslate }}
          </mat-button-toggle>
        </mat-button-toggle-group>
        <span class="dc-pt__muted">{{ 'builder.preview.hint' | dcTranslate }}</span>
      </div>
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li dir="ltr">{{ e }}</li>
          }
        </ul>
      }
      <div class="dc-pt__frame" [class.dc-pt__frame--mobile]="device() === 'mobile'" data-testid="builder-preview">
        @if (version(); as v) {
          <dc-service-wizard
            [version]="v"
            [data]="data()"
            [levelCode]="level()"
            [currentStep]="step()"
            [preview]="true"
            [uploader]="uploader"
            (moved)="move($event)"
          />
        }
      </div>
    </div>
  `,
  styles: `
    :host {
      display: block;
      height: 100%;
      overflow: auto;
    }
    .dc-pt {
      display: grid;
      gap: 0.75rem;
      padding: 0.75rem;
    }
    .dc-pt__bar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 1rem;
    }
    .dc-pt__muted {
      color: var(--dga-text-3, #6c737f);
      font-size: 12px;
    }
    .dc-pt__frame {
      padding: 1rem;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 12px;
      background: #fff;
    }
    .dc-pt__frame--mobile {
      width: 390px;
      max-width: 100%;
      margin-inline: auto;
      padding: 0.75rem;
      border-width: 10px;
      border-color: var(--dga-gray-800, #1f2a37);
      border-radius: 28px;
    }
  `,
})
export class PreviewTab {
  /** Files chosen in the preview stay in the browser. */
  protected readonly uploader = browserFileUploader();
  private readonly store = inject(BuilderStore);
  private readonly api = inject(ServicesApi);
  readonly service = input.required<string>();
  protected readonly device = signal<'desktop' | 'mobile'>('desktop');
  protected readonly version = signal<ResolvedVersion | null>(null);
  protected readonly errors = signal<string[]>([]);
  protected readonly data = signal<Record<string, unknown>>({});
  protected readonly level = signal<string | null>(null);
  protected readonly step = signal<string | null>(null);
  private subscription: Subscription | null = null;

  constructor() {
    // Resolved again a moment after each change.
    effect(() => {
      const definition = this.store.definition();
      const service = this.service();
      untracked(() => this.resolve(service, definition as unknown as VersionDefinition));
    });
    inject(DestroyRef).onDestroy(() => this.subscription?.unsubscribe());
  }

  private resolve(service: string, definition: VersionDefinition): void {
    this.subscription?.unsubscribe();
    this.subscription = timer(400)
      .pipe(switchMap(() => this.api.preview(service, definition)))
      .subscribe({
        next: (resolved) => {
          this.errors.set([]);
          this.version.set(resolved);
          const steps = resolved.definition.steps.map((s) => s.code);
          if (!this.step() || !steps.includes(this.step()!)) this.step.set(steps[0] ?? null);
        },
        error: (error) => this.errors.set(problemMessages(error)),
      });
  }

  protected move(move: WizardMove): void {
    this.data.set(move.data);
    this.level.set(move.levelCode);
    this.step.set(move.to);
  }
}
