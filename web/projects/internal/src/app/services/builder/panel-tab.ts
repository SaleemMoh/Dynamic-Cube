import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  ASSISTANT_LINE_KINDS,
  AssistantLineDefinition,
  AssistantPanelDefinition,
  JsonLogicRule,
  LocalizePipe,
  PanelDefinition,
  ResultItemsDefinition,
  ResultScaleValue,
  ResultValueDefinition,
  RuleEditor,
  RuleVariable,
  ServicesApi,
  TranslatePipe,
  SearchableSelectModule,
} from '@dynamic-cube/shared';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { BuilderReferences } from './builder-references';
import { BuilderStore } from './builder-store';
import { move, normalizePanel, usedQuestions } from './builder-model';

type Part = 'assistant' | 'readiness' | 'summary' | 'result';

/**
 * The panel beside the applicant's form (ADR-0040), in four parts any service can turn on: the assistant (greetings, notes
 * with their rule, questions and answers, each on a step and under a condition), the readiness check, the request summary
 * and the live result (the workflow's decision step on the answers as they are, shown on a scale).
 */
@Component({
  selector: 'dc-builder-panel-tab',
  imports: [
    FormsModule,
    LocalizePipe,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    SearchableSelectModule,
    MatTooltipModule,
    NgTemplateOutlet,
    RuleEditor,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-pn">
      <p class="dc-pn__muted">{{ 'builder.panel.intro' | dcTranslate }}</p>

      <section class="dc-card">
        <h2>{{ 'builder.panel.parts' | dcTranslate }}</h2>
        <div class="dc-pn__parts">
          @for (part of parts; track part) {
            <div class="dc-pn__part" [class.dc-pn__part--on]="panel()[part].enabled">
              <mat-checkbox
                [checked]="panel()[part].enabled"
                (change)="toggle(part, $event.checked)"
                [attr.data-testid]="'panel-part-' + part"
              >
                <strong>{{ 'builder.panel.part.' + part | dcTranslate }}</strong>
              </mat-checkbox>
              <span class="dc-pn__muted">{{
                'builder.panel.part.' + part + '.hint' | dcTranslate
              }}</span>
            </div>
          }
        </div>
      </section>

      @if (panel().assistant.enabled) {
        <section class="dc-card" data-testid="panel-assistant-config">
          <h2>{{ 'builder.panel.part.assistant' | dcTranslate }}</h2>
          <div class="dc-pn__pair">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'builder.panel.titleAr' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="rtl"
                [ngModel]="panel().assistant.titleAr ?? ''"
                (ngModelChange)="assistant({ titleAr: $event || null })"
                [ngModelOptions]="blur"
              />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'builder.panel.titleEn' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="panel().assistant.titleEn ?? ''"
                (ngModelChange)="assistant({ titleEn: $event || null })"
                [ngModelOptions]="blur"
              />
            </mat-form-field>
          </div>
          <div class="dc-pn__pair">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'builder.panel.subtitleAr' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="rtl"
                [ngModel]="panel().assistant.subtitleAr ?? ''"
                (ngModelChange)="assistant({ subtitleAr: $event || null })"
                [ngModelOptions]="blur"
              />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'builder.panel.subtitleEn' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="panel().assistant.subtitleEn ?? ''"
                (ngModelChange)="assistant({ subtitleEn: $event || null })"
                [ngModelOptions]="blur"
              />
            </mat-form-field>
          </div>
          <mat-checkbox
            [checked]="panel().assistant.autoNotes"
            (change)="assistant({ autoNotes: $event.checked })"
            >{{ 'builder.panel.autoNotes' | dcTranslate }}</mat-checkbox
          >
          <p class="dc-pn__muted">{{ 'builder.panel.words' | dcTranslate }}</p>

          <div class="dc-pn__head">
            <h3>{{ 'builder.panel.lines' | dcTranslate }}</h3>
            <button
              matButton="tonal"
              type="button"
              (click)="addLine()"
              data-testid="panel-add-line"
            >
              <mat-icon fontSet="material-symbols-outlined">add</mat-icon
              >{{ 'builder.panel.addLine' | dcTranslate }}
            </button>
          </div>
          @for (line of panel().assistant.lines; track $index; let i = $index; let last = $last) {
            <div class="dc-pn__line" [attr.data-line]="i">
              <div class="dc-pn__row">
                <span class="dc-pn__n">{{ i + 1 }}</span>
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ 'builder.panel.kind' | dcTranslate }}</mat-label>
                  <mat-select [ngModel]="line.kind" (ngModelChange)="line$(i, { kind: $event })">
                    @for (k of kinds; track k) {
                      <mat-option [value]="k">{{
                        'builder.panel.kind.' + k | dcTranslate
                      }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ 'builder.panel.step' | dcTranslate }}</mat-label>
                  <mat-select
                    [ngModel]="line.step ?? ''"
                    (ngModelChange)="line$(i, { step: $event || null })"
                  >
                    <mat-option value="">{{ 'builder.panel.everyStep' | dcTranslate }}</mat-option>
                    @for (s of steps(); track s.code) {
                      <mat-option [value]="s.code"
                        >{{ { ar: s.titleAr, en: s.titleEn } | dcLocalize }} ({{
                          s.code
                        }})</mat-option
                      >
                    }
                  </mat-select>
                </mat-form-field>
                <span class="dc-pn__grow"></span>
                <button
                  matIconButton
                  type="button"
                  (click)="moveLine(i, -1)"
                  [disabled]="i === 0"
                  [matTooltip]="'designer.up' | dcTranslate"
                >
                  <mat-icon fontSet="material-symbols-outlined">arrow_upward</mat-icon>
                </button>
                <button
                  matIconButton
                  type="button"
                  (click)="moveLine(i, 1)"
                  [disabled]="last"
                  [matTooltip]="'designer.down' | dcTranslate"
                >
                  <mat-icon fontSet="material-symbols-outlined">arrow_downward</mat-icon>
                </button>
                <button
                  matIconButton
                  type="button"
                  (click)="removeLine(i)"
                  [matTooltip]="'builder.remove' | dcTranslate"
                >
                  <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                </button>
              </div>
              @if (line.kind === 'QUESTION') {
                <div class="dc-pn__pair">
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'builder.panel.questionAr' | dcTranslate }}</mat-label>
                    <input
                      matInput
                      dir="rtl"
                      [ngModel]="line.questionAr ?? ''"
                      (ngModelChange)="line$(i, { questionAr: $event })"
                      [ngModelOptions]="blur"
                    />
                  </mat-form-field>
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'builder.panel.questionEn' | dcTranslate }}</mat-label>
                    <input
                      matInput
                      dir="ltr"
                      [ngModel]="line.questionEn ?? ''"
                      (ngModelChange)="line$(i, { questionEn: $event })"
                      [ngModelOptions]="blur"
                    />
                  </mat-form-field>
                </div>
              }
              <div class="dc-pn__pair">
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ textLabel(line) + 'Ar' | dcTranslate }}</mat-label>
                  <textarea
                    matInput
                    dir="rtl"
                    rows="2"
                    [ngModel]="line.textAr"
                    (ngModelChange)="line$(i, { textAr: $event })"
                    [ngModelOptions]="blur"
                  ></textarea>
                </mat-form-field>
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ textLabel(line) + 'En' | dcTranslate }}</mat-label>
                  <textarea
                    matInput
                    dir="ltr"
                    rows="2"
                    [ngModel]="line.textEn"
                    (ngModelChange)="line$(i, { textEn: $event })"
                    [ngModelOptions]="blur"
                  ></textarea>
                </mat-form-field>
              </div>
              @if (line.kind !== 'MESSAGE' && line.kind !== 'QUESTION') {
                <div class="dc-pn__pair">
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'builder.panel.whyAr' | dcTranslate }}</mat-label>
                    <input
                      matInput
                      dir="rtl"
                      [ngModel]="line.whyAr ?? ''"
                      (ngModelChange)="line$(i, { whyAr: $event || null })"
                      [ngModelOptions]="blur"
                    />
                  </mat-form-field>
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'builder.panel.whyEn' | dcTranslate }}</mat-label>
                    <input
                      matInput
                      dir="ltr"
                      [ngModel]="line.whyEn ?? ''"
                      (ngModelChange)="line$(i, { whyEn: $event || null })"
                      [ngModelOptions]="blur"
                    />
                  </mat-form-field>
                </div>
              }
              <dc-rule-editor
                [label]="'builder.panel.when' | dcTranslate"
                [rule]="line.when ?? null"
                [variables]="variables()"
                (ruleChange)="line$(i, { when: $event })"
              />
            </div>
          } @empty {
            <p class="dc-pn__muted">{{ 'builder.panel.noLines' | dcTranslate }}</p>
          }
        </section>
      }

      @if (panel().summary.enabled) {
        <section class="dc-card">
          <h2>{{ 'builder.panel.part.summary' | dcTranslate }}</h2>
          <p class="dc-pn__muted">{{ 'builder.panel.summaryHint' | dcTranslate }}</p>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'builder.panel.summaryQuestions' | dcTranslate }}</mat-label>
            <mat-select
              multiple
              [ngModel]="panel().summary.questions"
              (ngModelChange)="part('summary', { questions: $event })"
              data-testid="panel-summary-questions"
            >
              @for (q of questions(); track q.path) {
                <mat-option [value]="q.code">{{ q.label | dcLocalize }} ({{ q.code }})</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </section>
      }

      @if (panel().result.enabled) {
        <section class="dc-card" data-testid="panel-result-config">
          <h2>{{ 'builder.panel.part.result' | dcTranslate }}</h2>
          <p class="dc-pn__muted">{{ 'builder.panel.resultHint' | dcTranslate }}</p>
          <div class="dc-pn__pair">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'builder.panel.titleAr' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="rtl"
                [ngModel]="result().titleAr ?? ''"
                (ngModelChange)="part('result', { titleAr: $event || null })"
                [ngModelOptions]="blur"
              />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'builder.panel.titleEn' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="result().titleEn ?? ''"
                (ngModelChange)="part('result', { titleEn: $event || null })"
                [ngModelOptions]="blur"
              />
            </mat-form-field>
          </div>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'builder.panel.decisionStep' | dcTranslate }}</mat-label>
            <mat-select
              [ngModel]="result().step ?? null"
              (ngModelChange)="part('result', { step: $event })"
              data-testid="panel-result-step"
            >
              @for (d of decisionSteps(); track d.code) {
                <mat-option [value]="d.code"
                  >{{ { ar: d.nameAr ?? d.code, en: d.nameEn ?? d.code } | dcLocalize }} ({{
                    d.code
                  }}
                  · {{ d.ruleSet }})</mat-option
                >
              }
              @if (result().step && !hasStep(result().step!)) {
                <mat-option [value]="result().step">{{ result().step }}</mat-option>
              }
            </mat-select>
            @if (!decisionSteps().length) {
              <mat-hint>{{
                (store.service() ? 'builder.panel.noDecisionSteps' : 'builder.panel.templateResult')
                  | dcTranslate
              }}</mat-hint>
            }
          </mat-form-field>

          <h3>{{ 'builder.panel.headline' | dcTranslate }}</h3>
          @if (result().headline; as h) {
            <ng-container
              *ngTemplateOutlet="
                value;
                context: { $implicit: h, set: setHeadline, remove: removeHeadline }
              "
            />
          } @else {
            <button matButton type="button" (click)="addHeadline()">
              <mat-icon fontSet="material-symbols-outlined">add</mat-icon
              >{{ 'builder.panel.addHeadline' | dcTranslate }}
            </button>
          }

          <div class="dc-pn__head">
            <h3>{{ 'builder.panel.scale' | dcTranslate }}</h3>
            <button matButton type="button" (click)="addScale()">
              <mat-icon fontSet="material-symbols-outlined">add</mat-icon
              >{{ 'builder.panel.addScale' | dcTranslate }}
            </button>
          </div>
          <p class="dc-pn__muted">{{ 'builder.panel.scaleHint' | dcTranslate }}</p>
          @for (v of result().scale; track $index; let i = $index) {
            <div class="dc-pn__row">
              <mat-form-field subscriptSizing="dynamic" class="dc-pn__code">
                <mat-label>{{ 'builder.panel.value' | dcTranslate }}</mat-label>
                <input
                  matInput
                  dir="ltr"
                  [ngModel]="v.value"
                  (ngModelChange)="scale(i, { value: $event })"
                  [ngModelOptions]="blur"
                />
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'builder.panel.labelAr' | dcTranslate }}</mat-label>
                <input
                  matInput
                  dir="rtl"
                  [ngModel]="v.labelAr"
                  (ngModelChange)="scale(i, { labelAr: $event })"
                  [ngModelOptions]="blur"
                />
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'builder.panel.labelEn' | dcTranslate }}</mat-label>
                <input
                  matInput
                  dir="ltr"
                  [ngModel]="v.labelEn"
                  (ngModelChange)="scale(i, { labelEn: $event })"
                  [ngModelOptions]="blur"
                />
              </mat-form-field>
              <button
                matIconButton
                type="button"
                (click)="removeScale(i)"
                [matTooltip]="'builder.remove' | dcTranslate"
              >
                <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
              </button>
            </div>
          }
          <div class="dc-pn__pair">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'builder.panel.unknownAr' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="rtl"
                [ngModel]="result().unknownAr ?? ''"
                (ngModelChange)="part('result', { unknownAr: $event || null })"
                [ngModelOptions]="blur"
              />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'builder.panel.unknownEn' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="result().unknownEn ?? ''"
                (ngModelChange)="part('result', { unknownEn: $event || null })"
                [ngModelOptions]="blur"
              />
            </mat-form-field>
          </div>

          <div class="dc-pn__head">
            <h3>{{ 'builder.panel.values' | dcTranslate }}</h3>
            <button matButton type="button" (click)="addValue()">
              <mat-icon fontSet="material-symbols-outlined">add</mat-icon
              >{{ 'builder.panel.addValue' | dcTranslate }}
            </button>
          </div>
          @for (v of result().values; track $index; let i = $index) {
            <ng-container
              *ngTemplateOutlet="
                value;
                context: { $implicit: v, set: valueSetter(i), remove: valueRemover(i) }
              "
            />
          }

          <div class="dc-pn__head">
            <h3>{{ 'builder.panel.items' | dcTranslate }}</h3>
            @if (!result().items) {
              <button matButton type="button" (click)="addItems()">
                <mat-icon fontSet="material-symbols-outlined">add</mat-icon
                >{{ 'builder.panel.addItems' | dcTranslate }}
              </button>
            } @else {
              <button matButton type="button" (click)="part('result', { items: null })">
                <mat-icon fontSet="material-symbols-outlined">delete</mat-icon
                >{{ 'builder.remove' | dcTranslate }}
              </button>
            }
          </div>
          <p class="dc-pn__muted">{{ 'builder.panel.itemsHint' | dcTranslate }}</p>
          @if (result().items; as items) {
            <div class="dc-pn__row">
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'builder.panel.itemsOutput' | dcTranslate }}</mat-label>
                <input
                  matInput
                  dir="ltr"
                  [ngModel]="items.output"
                  (ngModelChange)="itemsChange({ output: $event })"
                  [ngModelOptions]="blur"
                />
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'builder.panel.titleOutput' | dcTranslate }}</mat-label>
                <input
                  matInput
                  dir="ltr"
                  [ngModel]="items.titleOutput"
                  (ngModelChange)="itemsChange({ titleOutput: $event })"
                  [ngModelOptions]="blur"
                />
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'builder.panel.titleOutputEn' | dcTranslate }}</mat-label>
                <input
                  matInput
                  dir="ltr"
                  [ngModel]="items.titleOutputEn ?? ''"
                  (ngModelChange)="itemsChange({ titleOutputEn: $event || null })"
                  [ngModelOptions]="blur"
                />
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'builder.panel.resultOutput' | dcTranslate }}</mat-label>
                <input
                  matInput
                  dir="ltr"
                  [ngModel]="items.resultOutput ?? ''"
                  (ngModelChange)="itemsChange({ resultOutput: $event || null })"
                  [ngModelOptions]="blur"
                />
              </mat-form-field>
            </div>
            <div class="dc-pn__head">
              <h4>{{ 'builder.panel.columns' | dcTranslate }}</h4>
              <button matButton type="button" (click)="addColumn()">
                <mat-icon fontSet="material-symbols-outlined">add</mat-icon
                >{{ 'builder.panel.addColumn' | dcTranslate }}
              </button>
            </div>
            @for (c of items.columns; track $index; let i = $index) {
              <ng-container
                *ngTemplateOutlet="
                  value;
                  context: { $implicit: c, set: columnSetter(i), remove: columnRemover(i) }
                "
              />
            }
          }
        </section>
      }
    </div>

    <ng-template #value let-v let-set="set" let-remove="remove">
      <div class="dc-pn__row">
        <mat-form-field subscriptSizing="dynamic" class="dc-pn__code">
          <mat-label>{{ 'builder.panel.output' | dcTranslate }}</mat-label>
          <input
            matInput
            dir="ltr"
            [ngModel]="v.output"
            (ngModelChange)="set({ output: $event })"
            [ngModelOptions]="blur"
          />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic" class="dc-pn__code">
          <mat-label>{{ 'builder.panel.outputEn' | dcTranslate }}</mat-label>
          <input
            matInput
            dir="ltr"
            [ngModel]="v.outputEn ?? ''"
            (ngModelChange)="set({ outputEn: $event || null })"
            [ngModelOptions]="blur"
          />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'builder.panel.labelAr' | dcTranslate }}</mat-label>
          <input
            matInput
            dir="rtl"
            [ngModel]="v.labelAr"
            (ngModelChange)="set({ labelAr: $event })"
            [ngModelOptions]="blur"
          />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'builder.panel.labelEn' | dcTranslate }}</mat-label>
          <input
            matInput
            dir="ltr"
            [ngModel]="v.labelEn"
            (ngModelChange)="set({ labelEn: $event })"
            [ngModelOptions]="blur"
          />
        </mat-form-field>
        <mat-checkbox [checked]="v.onScale" (change)="set({ onScale: $event.checked })">{{
          'builder.panel.onScale' | dcTranslate
        }}</mat-checkbox>
        <button
          matIconButton
          type="button"
          (click)="remove()"
          [matTooltip]="'builder.remove' | dcTranslate"
        >
          <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
        </button>
      </div>
    </ng-template>
  `,
  styles: `
    :host {
      display: block;
      min-height: 0;
      height: 100%;
      overflow: auto;
    }
    .dc-pn {
      display: grid;
      gap: 1rem;
      max-width: 72rem;
      padding: 0.75rem;
    }
    .dc-pn h2 {
      margin: 0 0 0.5rem;
      font-size: 16px;
    }
    .dc-pn h3 {
      margin: 0.75rem 0 0.25rem;
      font-size: 14px;
    }
    .dc-pn h4 {
      margin: 0.5rem 0 0.25rem;
      font-size: 13px;
    }
    .dc-pn__muted {
      margin: 0.25rem 0 0.5rem;
      color: var(--dga-text-3, #6c737f);
      font-size: 12px;
    }
    .dc-pn__parts {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(15rem, 1fr));
      gap: 0.75rem;
    }
    .dc-pn__part {
      display: grid;
      gap: 0.125rem;
      padding: 0.5rem 0.75rem;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 10px;
    }
    .dc-pn__part--on {
      border-color: var(--dga-primary, #1b8354);
      background: var(--dga-primary-50, #f1f8f4);
    }
    .dc-pn__pair {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.5rem;
      margin-bottom: 0.5rem;
    }
    .dc-pn__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
    }
    .dc-pn__row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 0.5rem;
    }
    .dc-pn__row mat-form-field {
      flex: 1 1 12rem;
    }
    .dc-pn__row .dc-pn__code {
      flex: 0 1 10rem;
    }
    .dc-pn__grow {
      flex: 1;
    }
    .dc-pn__n {
      display: inline-grid;
      place-items: center;
      width: 1.5rem;
      height: 1.5rem;
      border-radius: 50%;
      background: var(--dga-gray-100, #f3f4f6);
      font-size: 12px;
    }
    .dc-pn__line {
      margin-bottom: 0.75rem;
      padding: 0.75rem;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 10px;
    }
    @media (max-width: 48rem) {
      .dc-pn__pair {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class PanelTab {
  protected readonly store = inject(BuilderStore);
  private readonly refs = inject(BuilderReferences);
  private readonly api = inject(ServicesApi);
  protected readonly parts: Part[] = ['assistant', 'readiness', 'summary', 'result'];
  protected readonly kinds = ASSISTANT_LINE_KINDS;
  protected readonly blur = { updateOn: 'blur' as const };

  protected readonly panel = computed(() => normalizePanel(this.store.definition().panel));
  protected readonly result = computed(() => this.panel().result);
  protected readonly steps = computed(() => this.store.definition().steps);
  private readonly decisions = rxResource({
    params: () => this.store.service(),
    stream: ({ params }) =>
      params ? this.api.decisionSteps(params).pipe(catchError(() => of([]))) : of([]),
  });
  protected readonly decisionSteps = computed(() => this.decisions.value() ?? []);
  protected readonly questions = computed(() =>
    this.refs
      .questionVariables(usedQuestions(this.store.definition()), '')
      .map((v) => ({ ...v, code: v.path })),
  );
  /** What a line's condition reads: the answers under data., the level, the step, the readiness, what is missing, the live result. */
  protected readonly variables = computed<RuleVariable[]>(() => {
    const def = this.store.definition();
    const result = this.result();
    const outputs = [...(result.headline ? [result.headline] : []), ...result.values].filter(
      (v) => v.output,
    );
    return [
      ...this.refs.questionVariables(usedQuestions(def), 'data.'),
      {
        path: 'level',
        label: { ar: 'مستوى الخدمة', en: 'Service level' },
        options: def.levels.map((l) => ({ value: l.code, label: { ar: l.nameAr, en: l.nameEn } })),
      },
      {
        path: 'step',
        label: { ar: 'الخطوة الحالية', en: 'Current step' },
        options: def.steps.map((s) => ({ value: s.code, label: { ar: s.titleAr, en: s.titleEn } })),
      },
      { path: 'readiness', label: { ar: 'نسبة الجاهزية', en: 'Readiness (%)' }, type: 'number' },
      {
        path: 'missing',
        label: { ar: 'عدد المتطلبات الناقصة', en: 'Missing requirements' },
        type: 'number',
      },
      ...outputs.map((v) => ({
        path: `result.${v.output}`,
        label: { ar: `النتيجة: ${v.labelAr || v.output}`, en: `Result: ${v.labelEn || v.output}` },
        options: v.onScale
          ? result.scale.map((s) => ({ value: s.value, label: { ar: s.labelAr, en: s.labelEn } }))
          : undefined,
      })),
    ];
  });

  private change(update: (panel: PanelDefinition) => PanelDefinition): void {
    this.store.apply((def) => ({ ...def, panel: update(normalizePanel(def.panel)) }));
  }

  protected toggle(part: Part, enabled: boolean): void {
    this.change((p) => ({ ...p, [part]: { ...p[part], enabled } }));
  }

  protected part<K extends 'summary' | 'result'>(
    part: K,
    change: Partial<PanelDefinition[K]>,
  ): void {
    this.change((p) => ({ ...p, [part]: { ...p[part], ...change } }));
  }

  protected assistant(change: Partial<AssistantPanelDefinition>): void {
    this.change((p) => ({ ...p, assistant: { ...p.assistant, ...change } }));
  }

  protected hasStep(code: string): boolean {
    return this.decisionSteps().some((d) => d.code === code);
  }

  /** The label of a line's text: the greeting, the note or the answer. */
  protected textLabel(line: AssistantLineDefinition): string {
    return line.kind === 'MESSAGE'
      ? 'builder.panel.message'
      : line.kind === 'QUESTION'
        ? 'builder.panel.answer'
        : 'builder.panel.note';
  }

  // Assistant lines
  protected addLine(): void {
    const first = this.panel().assistant.lines.length === 0;
    this.assistant({
      lines: [
        ...this.panel().assistant.lines,
        {
          step: null,
          kind: first ? 'MESSAGE' : 'ADVICE',
          textAr: '',
          textEn: '',
          whyAr: null,
          whyEn: null,
          questionAr: null,
          questionEn: null,
          when: null,
        },
      ],
    });
  }

  protected line$(
    index: number,
    change: Partial<AssistantLineDefinition> & { when?: JsonLogicRule | null },
  ): void {
    this.assistant({
      lines: this.panel().assistant.lines.map((l, i) => (i === index ? { ...l, ...change } : l)),
    });
  }

  protected moveLine(index: number, by: number): void {
    this.assistant({ lines: move(this.panel().assistant.lines, index, index + by) });
  }

  protected removeLine(index: number): void {
    this.assistant({ lines: this.panel().assistant.lines.filter((_, i) => i !== index) });
  }

  // Result
  private readonly blank = (): ResultValueDefinition => ({
    output: '',
    outputEn: null,
    labelAr: '',
    labelEn: '',
    onScale: false,
  });

  protected addHeadline(): void {
    this.part('result', { headline: { ...this.blank(), onScale: true } });
  }

  protected readonly setHeadline = (change: Partial<ResultValueDefinition>) =>
    this.part('result', { headline: { ...this.result().headline!, ...change } });
  protected readonly removeHeadline = () => this.part('result', { headline: null });

  protected addScale(): void {
    const next = String(this.result().scale.length + 1);
    this.part('result', {
      scale: [...this.result().scale, { value: next, labelAr: '', labelEn: '' }],
    });
  }

  protected scale(index: number, change: Partial<ResultScaleValue>): void {
    this.part('result', {
      scale: this.result().scale.map((v, i) => (i === index ? { ...v, ...change } : v)),
    });
  }

  protected removeScale(index: number): void {
    this.part('result', { scale: this.result().scale.filter((_, i) => i !== index) });
  }

  protected addValue(): void {
    this.part('result', { values: [...this.result().values, this.blank()] });
  }

  protected valueSetter(index: number) {
    return (change: Partial<ResultValueDefinition>) =>
      this.part('result', {
        values: this.result().values.map((v, i) => (i === index ? { ...v, ...change } : v)),
      });
  }

  protected valueRemover(index: number) {
    return () =>
      this.part('result', { values: this.result().values.filter((_, i) => i !== index) });
  }

  protected addItems(): void {
    this.part('result', {
      items: {
        output: 'items',
        titleOutput: '',
        titleOutputEn: null,
        resultOutput: null,
        columns: [],
      },
    });
  }

  protected itemsChange(change: Partial<ResultItemsDefinition>): void {
    this.part('result', { items: { ...this.result().items!, ...change } });
  }

  protected addColumn(): void {
    this.itemsChange({ columns: [...this.result().items!.columns, this.blank()] });
  }

  protected columnSetter(index: number) {
    return (change: Partial<ResultValueDefinition>) =>
      this.itemsChange({
        columns: this.result().items!.columns.map((c, i) =>
          i === index ? { ...c, ...change } : c,
        ),
      });
  }

  protected columnRemover(index: number) {
    return () =>
      this.itemsChange({ columns: this.result().items!.columns.filter((_, i) => i !== index) });
  }
}
