import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, linkedSignal, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { I18nService, JsonValue, LocalizePipe, TranslatePipe, localize } from '@dynamic-cube/shared';
import { DesignerCanvas } from './designer-canvas';
import { NodeDef } from './designer-model';
import { DesignerReferences } from './designer-references';
import { DesignerStore } from './designer-store';
import { TestLogEntry, TestState, choose, startRun, waiting } from './test-run';

/**
 * Tries the workflow being designed without saving it: sample answers and a service level, then the run goes on by
 * itself through gateways (conditions evaluated as on the server), decisions, documents and splits, and stops where
 * a person acts: choose the task's action or whether the bill is paid. Nothing is evaluated by rule sets or issued.
 */
@Component({
  selector: 'dc-designer-test-run',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule, LocalizePipe, TranslatePipe, DesignerCanvas],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-test">
      <section class="dc-test__side">
        <h2>{{ 'designer.test.title' | dcTranslate }}</h2>
        <p class="dc-test__hint">{{ 'designer.test.hint' | dcTranslate }}</p>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'designer.test.level' | dcTranslate }}</mat-label>
          <mat-select [ngModel]="level()" (ngModelChange)="level.set($event)" data-testid="test-level">
            <mat-option [value]="null">—</mat-option>
            @for (l of refs.levels(); track l.value) {
              <mat-option [value]="l.value">{{ l.label | dcLocalize }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'designer.test.fees' | dcTranslate }}</mat-label>
          <input matInput type="number" min="0" [ngModel]="fees()" (ngModelChange)="fees.set(+$event || 0)" data-testid="test-fees" />
        </mat-form-field>
        <label class="dc-test__label" for="dc-test-data">{{ 'designer.test.data' | dcTranslate }}</label>
        <textarea
          id="dc-test-data"
          dir="ltr"
          rows="8"
          spellcheck="false"
          [ngModel]="dataText()"
          (ngModelChange)="dataText.set($event)"
          data-testid="test-data"
        ></textarea>
        @if (dataError()) {
          <p class="dc-test__error" role="alert">{{ 'wfVersions.invalidJson' | dcTranslate }}</p>
        }
        <button matButton="filled" type="button" (click)="start()" data-testid="test-start">
          <mat-icon fontSet="material-symbols-outlined">play_arrow</mat-icon>
          {{ (state() ? 'designer.test.restart' : 'designer.test.start') | dcTranslate }}
        </button>

        @if (state(); as s) {
          @for (w of waitingNow(); track w.token.id) {
            <div class="dc-test__wait" [attr.data-testid]="'test-wait-' + w.node.code">
              <strong>{{ name(w.node) }}</strong>
              <div class="dc-test__choices">
                @if (w.node.type === 'PAYMENT') {
                  <button matButton="outlined" type="button" (click)="pick(w.token.id, 'PAID')" data-testid="test-paid">
                    {{ 'designer.test.markPaid' | dcTranslate }}
                  </button>
                  <button matButton="outlined" type="button" (click)="pick(w.token.id, 'UNPAID')" data-testid="test-unpaid">
                    {{ 'designer.test.markUnpaid' | dcTranslate }}
                  </button>
                } @else if (w.node.type === 'VISIT') {
                  @for (o of visitOutcomes(w.node); track o.value) {
                    <button matButton="outlined" type="button" (click)="pick(w.token.id, o.value)" [attr.data-testid]="'test-outcome-' + o.value">
                      {{ o.label | dcLocalize }}
                    </button>
                  }
                  <button matButton="outlined" type="button" (click)="pick(w.token.id, 'CANCELLED')" data-testid="test-visit-cancelled">
                    {{ 'designer.test.cancelVisit' | dcTranslate }}
                  </button>
                } @else {
                  @for (a of w.node.actions ?? []; track a.code) {
                    <button matButton="outlined" type="button" (click)="pick(w.token.id, a.code)" [attr.data-testid]="'test-act-' + a.code">
                      {{ { ar: a.nameAr, en: a.nameEn } | dcLocalize }}
                    </button>
                  }
                }
              </div>
            </div>
          }
          @if (s.outcome) {
            <p class="dc-test__outcome" role="status" data-testid="test-outcome">
              {{ 'designer.test.outcome' | dcTranslate }}:
              {{ 'designer.outcome.' + s.outcome | dcTranslate }}
            </p>
          }
          <ol class="dc-test__log" data-testid="test-log">
            @for (e of s.log; track $index) {
              <li [class.dc-test__error]="e.error">
                <span dir="ltr" class="dc-test__node">{{ e.node }}</span> {{ text(e) }}
              </li>
            }
          </ol>
        }
      </section>
      <dc-designer-canvas class="dc-test__canvas" [zoom]="0.8" [readOnly]="true" />
    </div>
  `,
  styles: `
    :host {
      display: block;
      min-height: 0;
    }
    .dc-test {
      display: grid;
      grid-template-columns: 20rem minmax(0, 1fr);
      height: 100%;
      min-height: 0;
    }
    .dc-test__side {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      padding: 0.75rem;
      overflow: auto;
      border-inline-end: 1px solid var(--dga-gray-200, #e5e7eb);
    }
    .dc-test__side h2 {
      margin: 0;
      font-size: 16px;
    }
    .dc-test__hint {
      margin: 0;
      color: var(--dga-text-3, #6c737f);
      font-size: 12px;
    }
    .dc-test__label {
      font-size: 13px;
      font-weight: 600;
    }
    textarea {
      font-family: ui-monospace, monospace;
      font-size: 12px;
    }
    .dc-test__wait {
      display: grid;
      gap: 0.375rem;
      padding: 0.5rem;
      border: 1px solid #84adff;
      border-radius: 8px;
      background: #f0f6ff;
    }
    .dc-test__choices {
      display: flex;
      flex-wrap: wrap;
      gap: 0.25rem;
    }
    .dc-test__outcome {
      margin: 0;
      padding: 0.5rem;
      border-radius: 8px;
      background: var(--dynamic-cube-green-50, #e8f3ed);
      font-weight: 700;
    }
    .dc-test__log {
      margin: 0;
      padding-inline-start: 1.25rem;
      font-size: 13px;
    }
    .dc-test__node {
      font-family: ui-monospace, monospace;
      font-size: 12px;
      color: var(--dga-text-3, #6c737f);
    }
    .dc-test__error {
      color: #b42318;
    }
    .dc-test__canvas {
      min-height: 0;
    }
  `,
})
export class DesignerTestRun {
  private readonly store = inject(DesignerStore);
  protected readonly refs = inject(DesignerReferences);
  private readonly i18n = inject(I18nService);

  protected readonly level = linkedSignal<string | null>(() => this.refs.levels()[0]?.value ?? null);
  protected readonly fees = signal(0);
  /** Sample answers: every question of the service, empty. */
  protected readonly dataText = linkedSignal(() =>
    JSON.stringify(Object.fromEntries(this.refs.questions().map((q) => [q.code, q.type === 'NUMBER' ? 0 : q.type === 'CHECKBOX' ? false : null])), null, 2),
  );
  protected readonly dataError = signal(false);
  protected readonly state = signal<TestState | null>(null);
  protected readonly waitingNow = computed(() => {
    const s = this.state();
    return s ? waiting(this.store.definition(), s) : [];
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => this.store.active.set([]));
  }

  /** The outcomes to try at a visit step: its type's, or the ones it routes when the type is not known. */
  protected visitOutcomes(node: NodeDef): { value: string; label: { ar: string; en: string } }[] {
    const outcomes = this.refs.visitType(node.visitType).outcomes;
    return outcomes.length ? outcomes : [...Object.keys(node.outcomes ?? {}), 'COMPLETED'].map((o) => ({ value: o, label: { ar: o, en: o } }));
  }

  protected name(node: NodeDef): string {
    return localize({ ar: node.nameAr || node.code, en: node.nameEn || node.code }, this.i18n.language());
  }

  protected text(entry: TestLogEntry): string {
    let text = this.i18n.translate(entry.key);
    for (const [k, v] of Object.entries(entry.params ?? {})) text = text.replace(`{${k}}`, String(v));
    return text;
  }

  protected start(): void {
    let data: Record<string, JsonValue>;
    try {
      data = JSON.parse(this.dataText() || '{}') as Record<string, JsonValue>;
      this.dataError.set(false);
    } catch {
      this.dataError.set(true);
      return;
    }
    this.show(startRun(this.store.definition(), { ...data, __feeTotal: this.fees() }, this.level()));
  }

  protected pick(token: number, choice: string): void {
    const s = this.state();
    if (s) this.show(choose(this.store.definition(), s, token, choice));
  }

  private show(state: TestState): void {
    this.state.set(state);
    this.store.active.set(state.tokens.map((t) => t.node));
  }
}
