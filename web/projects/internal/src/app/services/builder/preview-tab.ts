import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import {
  EMPTY_READINESS,
  LiveResultFeed,
  PanelReadiness,
  ResolvedVersion,
  ServicePanel,
  ServiceWizard,
  ServicesApi,
  TranslatePipe,
  VersionDefinition,
  WizardDraft,
  WizardMove,
  browserFileUploader,
  computeReadiness,
  hasPanel,
  problemMessages,
} from '@dynamic-cube/shared';
import { Subscription, timer } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { BuilderStore } from './builder-store';

/**
 * The applicant's wizard for the version being built, as it is now: questions resolved on the server from the bank,
 * rules evaluated as in the portal, nothing saved. On a desktop or a phone width; the language follows the app's. With a
 * panel, the readiness check shows above the form and the assistant, summary and live result beside it (the live result
 * runs the published workflow's decision step on the answers typed here).
 */
@Component({
  selector: 'dc-builder-preview-tab',
  imports: [FormsModule, MatButtonToggleModule, MatIconModule, PanelReadiness, ServicePanel, ServiceWizard, TranslatePipe],
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
      <div class="dc-pt__frame" [class.dc-pt__frame--mobile]="device() === 'mobile'" [class.dc-pt__frame--panel]="!!panel()" data-testid="builder-preview">
        @if (version(); as v) {
          <div class="dc-pt__main">
            @if (panel()?.readiness?.enabled) {
              <dc-panel-readiness [version]="v" [readiness]="readiness()" (openStep)="step.set($event)" />
            }
            <dc-service-wizard
              [version]="v"
              [data]="data()"
              [levelCode]="level()"
              [currentStep]="step()"
              [preview]="true"
              [uploader]="uploader"
              (moved)="move($event)"
              (edited)="edited($event)"
            />
          </div>
          @if (panel(); as p) {
            <dc-service-panel
              class="dc-pt__side"
              [panel]="p"
              [version]="v"
              [service]="{ ar: service(), en: service() }"
              [data]="onScreen().data"
              [levelCode]="onScreen().levelCode"
              [step]="step()"
              [readiness]="readiness()"
              [files]="uploader.files()"
              [live]="live.result()"
              [liveLoading]="live.loading()"
              [liveError]="live.error()"
            />
          }
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
    .dc-pt__frame--panel {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 22rem;
      gap: 1rem 1.5rem;
      align-items: start;
    }
    .dc-pt__main {
      display: grid;
      gap: 1rem;
      min-width: 0;
    }
    .dc-pt__frame--mobile {
      display: block;
      width: 390px;
      max-width: 100%;
      margin-inline: auto;
      padding: 0.75rem;
      border-width: 10px;
      border-color: var(--dga-gray-800, #1f2a37);
      border-radius: 28px;
    }
    .dc-pt__frame--mobile .dc-pt__side {
      margin-top: 1rem;
    }
    @media (max-width: 64rem) {
      .dc-pt__frame--panel {
        grid-template-columns: minmax(0, 1fr);
      }
    }
  `,
})
export class PreviewTab {
  /** Files chosen in the preview stay in the browser. */
  protected readonly uploader = browserFileUploader();
  private readonly store = inject(BuilderStore);
  private readonly api = inject(ServicesApi);
  readonly service = input.required<string>();
  /** An archetype's template is open (no workflow of its own: no live result). */
  readonly archetype = input(false);
  protected readonly device = signal<'desktop' | 'mobile'>('desktop');
  protected readonly version = signal<ResolvedVersion | null>(null);
  protected readonly errors = signal<string[]>([]);
  protected readonly data = signal<Record<string, unknown>>({});
  protected readonly level = signal<string | null>(null);
  protected readonly step = signal<string | null>(null);
  /** The answers and level as typed (not yet moved on). */
  private readonly typed = signal<WizardDraft | null>(null);
  protected readonly onScreen = computed<WizardDraft>(() => this.typed() ?? { data: this.data(), levelCode: this.level() });
  protected readonly panel = computed(() => {
    const panel = this.version()?.definition.panel;
    return hasPanel(panel) ? panel : null;
  });
  protected readonly readiness = computed(() => {
    const v = this.version();
    return v ? computeReadiness({ version: v, data: this.onScreen().data, levelCode: this.onScreen().levelCode }) : EMPTY_READINESS;
  });
  /** The live result on the answers typed here, from the decision step of the service's published workflow. */
  protected readonly live = new LiveResultFeed((data, levelCode) => this.api.previewLiveResult(this.service(), this.panel()!.result.step!, data, levelCode));
  private subscription: Subscription | null = null;

  constructor() {
    // Resolved again a moment after each change.
    effect(() => {
      const definition = this.store.definition();
      const service = this.service();
      untracked(() => this.resolve(service, definition as unknown as VersionDefinition));
    });
    inject(DestroyRef).onDestroy(() => {
      this.subscription?.unsubscribe();
      this.live.stop();
    });
  }

  /** Whether the live result can run: the part is on, its step is chosen, and a service (not a template) is open. */
  private liveOn(): boolean {
    const result = this.panel()?.result;
    return !this.archetype() && !!result?.enabled && !!result.step;
  }

  private refreshLive(now: boolean): void {
    if (!this.liveOn()) return this.live.stop();
    const { data, levelCode } = this.onScreen();
    if (now) void this.live.now(data, levelCode);
    else this.live.change(data, levelCode);
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
          this.refreshLive(true);
        },
        error: (error) => this.errors.set(problemMessages(error)),
      });
  }

  protected move(move: WizardMove): void {
    this.data.set(move.data);
    this.level.set(move.levelCode);
    this.step.set(move.to);
    this.typed.set(null);
    this.refreshLive(true);
  }

  protected edited(draft: WizardDraft): void {
    this.typed.set(draft);
    this.refreshLive(false);
  }
}
