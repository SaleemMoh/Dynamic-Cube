import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { I18nService, LocalizePipe, LogicChip, TranslatePipe } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { MatrixInputEditor, MatrixInputEditorData } from './matrix-input-editor';
import {
  CELL_ICONS,
  CELL_KINDS,
  CellKind,
  Matrix,
  MatrixBand,
  MatrixCell,
  MatrixCriterion,
  MatrixGroup,
  MatrixInput,
  SOURCE_ICONS,
  bankQuestionCode,
  freeCode,
  inputFields,
  newCell,
  newCriterion,
  newGroup,
} from './matrix-model';
import { MatrixReferences } from './matrix-references';

export interface MatrixCellEditorData {
  matrix: Matrix;
  row: string;
  column: string;
  cell: MatrixCell | null;
  references: MatrixReferences;
}

export interface MatrixCellEditorResult {
  cell: MatrixCell;
  inputs: MatrixInput[];
}

/**
 * The calculation in one cell (a sector's track): criteria in groups, each scored by bands (per size when the matrix
 * has sizes), by the answer chosen or as it is; or the worst grade of its criteria; or formulas; or the same as another
 * cell. Every input a criterion reads shows where its value comes from, and is set up from here.
 */
@Component({
  selector: 'dc-matrix-cell-editor',
  imports: [
    FormsModule,
    LocalizePipe,
    LogicChip,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatDialogModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ rowName() }} · {{ columnName() }}</h2>
    <mat-dialog-content class="mx-ce">
      <mat-button-toggle-group
        [ngModel]="cell().kind"
        (ngModelChange)="kind($event)"
        [attr.aria-label]="'mx.cell.kind' | dcTranslate"
        data-testid="mx-cell-kind"
      >
        @for (k of kinds; track k) {
          <mat-button-toggle [value]="k" [attr.data-testid]="'mx-kind-' + k">
            <mat-icon fontSet="material-symbols-outlined">{{ cellIcons[k] }}</mat-icon>
            {{ 'mx.kind.' + k | dcTranslate }}
          </mat-button-toggle>
        }
      </mat-button-toggle-group>
      <p class="dc-muted">{{ 'mx.kind.' + cell().kind + 'Hint' | dcTranslate }}</p>

      @switch (cell().kind) {
        @case ('points') {
          <mat-form-field subscriptSizing="dynamic" class="mx-ce__scale">
            <mat-label>{{ 'mx.cell.scale' | dcTranslate }}</mat-label>
            <mat-select
              [ngModel]="cell().scale ?? null"
              (ngModelChange)="set({ scale: $event })"
              data-testid="mx-cell-scale"
            >
              @for (s of data.matrix.scales; track s.code) {
                <mat-option [value]="s.code"
                  >{{ { ar: s.nameAr, en: s.nameEn } | dcLocalize }} ({{ s.code }}) ·
                  {{ scaleText(s.code) }}</mat-option
                >
              }
            </mat-select>
          </mat-form-field>
        }
        @case ('same') {
          <div class="mx-ce__row">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'mx.cell.sameRow' | dcTranslate }}</mat-label>
              <mat-select
                [ngModel]="cell().sameRow ?? null"
                (ngModelChange)="set({ sameRow: $event })"
                data-testid="mx-cell-same-row"
              >
                @for (r of otherRows(); track r.code) {
                  <mat-option [value]="r.code">{{
                    { ar: r.nameAr, en: r.nameEn } | dcLocalize
                  }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'mx.cell.sameColumn' | dcTranslate }}</mat-label>
              <mat-select
                [ngModel]="cell().sameColumn ?? null"
                (ngModelChange)="set({ sameColumn: $event })"
              >
                <mat-option [value]="null">{{ 'mx.cell.thisColumn' | dcTranslate }}</mat-option>
                @for (c of data.matrix.columns; track c.code) {
                  <mat-option [value]="c.code">{{
                    { ar: c.nameAr, en: c.nameEn } | dcLocalize
                  }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>
        }
        @case ('formula') {
          <div class="mx-ce__formulas">
            <span class="dc-field-label">{{ 'mx.cell.gradeRule' | dcTranslate }}</span>
            <dc-logic-chip
              mode="value"
              [rule]="cell().gradeRule ?? null"
              [fields]="fields()"
              [title]="'mx.cell.gradeRule' | dcTranslate"
              (ruleChange)="set({ gradeRule: $event })"
            />
            <span class="dc-field-label">{{ 'mx.cell.scoreRule' | dcTranslate }}</span>
            <dc-logic-chip
              mode="value"
              [rule]="cell().scoreRule ?? null"
              [fields]="fields()"
              [title]="'mx.cell.scoreRule' | dcTranslate"
              (ruleChange)="set({ scoreRule: $event })"
            />
            <span class="dc-field-label">{{ 'mx.cell.detailsRule' | dcTranslate }}</span>
            <dc-logic-chip
              mode="value"
              [rule]="cell().detailsRule ?? null"
              [fields]="fields()"
              [title]="'mx.cell.detailsRule' | dcTranslate"
              (ruleChange)="set({ detailsRule: $event })"
            />
          </div>
        }
      }

      @if (cell().kind === 'points' || cell().kind === 'worst') {
        @for (g of cell().groups; track $index; let gi = $index) {
          <section class="mx-ce__group" [attr.data-testid]="'mx-group-' + g.code">
            <div class="mx-ce__row">
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'mx.cell.groupName' | dcTranslate }}</mat-label>
                <input
                  matInput
                  [ngModel]="lang() === 'ar' ? g.nameAr : g.nameEn"
                  (ngModelChange)="
                    setGroup(gi, lang() === 'ar' ? { nameAr: $event } : { nameEn: $event })
                  "
                />
              </mat-form-field>
              @if (cell().kind === 'points') {
                <mat-form-field subscriptSizing="dynamic" class="mx-narrow">
                  <mat-label>{{ 'mx.cell.maxPoints' | dcTranslate }}</mat-label>
                  <input
                    matInput
                    type="number"
                    dir="ltr"
                    [ngModel]="g.maxPoints ?? null"
                    (ngModelChange)="setGroup(gi, { maxPoints: num($event) })"
                  />
                </mat-form-field>
                <mat-checkbox
                  [ngModel]="g.mandatory"
                  (ngModelChange)="setGroup(gi, { mandatory: $event })"
                  >{{ 'mx.cell.mandatory' | dcTranslate }}</mat-checkbox
                >
                @if (g.mandatory) {
                  <mat-form-field
                    subscriptSizing="dynamic"
                    class="mx-narrow"
                    [matTooltip]="'mx.cell.minPointsHint' | dcTranslate"
                  >
                    <mat-label>{{ 'mx.cell.minPoints' | dcTranslate }}</mat-label>
                    <input
                      matInput
                      type="number"
                      dir="ltr"
                      [ngModel]="g.minPoints ?? 0"
                      (ngModelChange)="setGroup(gi, { minPoints: num($event) ?? 0 })"
                    />
                  </mat-form-field>
                }
              }
              <span class="mx-ce__total dc-muted">{{ groupTotal(g) }}</span>
              <button
                matIconButton
                type="button"
                (click)="removeGroup(gi)"
                [matTooltip]="'mx.remove' | dcTranslate"
              >
                <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
              </button>
            </div>

            <mat-accordion multi displayMode="flat">
              @for (c of g.criteria; track $index; let ci = $index) {
                <mat-expansion-panel [attr.data-testid]="'mx-criterion-' + c.code">
                  <mat-expansion-panel-header>
                    <mat-panel-title>
                      <mat-icon
                        fontSet="material-symbols-outlined"
                        class="mx-ce__src"
                        [matTooltip]="'mx.source.' + sourceOf(c.input) | dcTranslate"
                        >{{ sourceIcon(c.input) }}</mat-icon
                      >
                      {{ (lang() === 'ar' ? c.nameAr : c.nameEn) || inputName(c.input) || c.code }}
                    </mat-panel-title>
                    <mat-panel-description>{{ criterionSummary(c) }}</mat-panel-description>
                  </mat-expansion-panel-header>
                  <div class="mx-ce__row">
                    <mat-form-field subscriptSizing="dynamic">
                      <mat-label>{{ 'mx.nameAr' | dcTranslate }}</mat-label>
                      <input
                        matInput
                        dir="rtl"
                        [ngModel]="c.nameAr"
                        (ngModelChange)="setCriterion(gi, ci, { nameAr: $event })"
                      />
                    </mat-form-field>
                    <mat-form-field subscriptSizing="dynamic">
                      <mat-label>{{ 'mx.nameEn' | dcTranslate }}</mat-label>
                      <input
                        matInput
                        dir="ltr"
                        [ngModel]="c.nameEn"
                        (ngModelChange)="setCriterion(gi, ci, { nameEn: $event })"
                      />
                    </mat-form-field>
                  </div>
                  <div class="mx-ce__row">
                    <mat-form-field subscriptSizing="dynamic" class="mx-wide">
                      <mat-label>{{ 'mx.cell.input' | dcTranslate }}</mat-label>
                      <mat-select
                        [ngModel]="c.input"
                        (ngModelChange)="setCriterion(gi, ci, { input: $event })"
                        [attr.data-testid]="'mx-criterion-input-' + c.code"
                      >
                        @for (i of inputs(); track i.code) {
                          <mat-option [value]="i.code">
                            <mat-icon fontSet="material-symbols-outlined">{{
                              sourceIcons[i.source.kind]
                            }}</mat-icon>
                            {{ { ar: i.nameAr, en: i.nameEn } | dcLocalize }} ({{ i.code }})
                          </mat-option>
                        }
                      </mat-select>
                    </mat-form-field>
                    <button
                      matButton="tonal"
                      type="button"
                      (click)="editInput(c.input)"
                      [disabled]="!inputOf(c.input)"
                      [attr.data-testid]="'mx-edit-source-' + c.code"
                    >
                      <mat-icon fontSet="material-symbols-outlined">{{
                        sourceIcon(c.input)
                      }}</mat-icon>
                      {{ 'mx.cell.editSource' | dcTranslate }}
                    </button>
                    <button matButton type="button" (click)="newInput(gi, ci)">
                      <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
                      {{ 'mx.input.new' | dcTranslate }}
                    </button>
                  </div>
                  <div class="mx-ce__row">
                    <mat-form-field subscriptSizing="dynamic">
                      <mat-label>{{ 'mx.cell.scoring' | dcTranslate }}</mat-label>
                      <mat-select [ngModel]="c.scoring" (ngModelChange)="scoring(gi, ci, $event)">
                        <mat-option value="bands">{{
                          'mx.scoring.bands' | dcTranslate
                        }}</mat-option>
                        <mat-option value="options">{{
                          'mx.scoring.options' | dcTranslate
                        }}</mat-option>
                        <mat-option value="direct">{{
                          'mx.scoring.direct' | dcTranslate
                        }}</mat-option>
                      </mat-select>
                    </mat-form-field>
                    <mat-form-field subscriptSizing="dynamic">
                      <mat-label>{{ 'mx.cell.output' | dcTranslate }}</mat-label>
                      <mat-select
                        [ngModel]="c.output"
                        (ngModelChange)="setCriterion(gi, ci, { output: $event })"
                      >
                        <mat-option value="points">{{
                          'mx.output.points' | dcTranslate
                        }}</mat-option>
                        <mat-option value="grade">{{ 'mx.output.grade' | dcTranslate }}</mat-option>
                      </mat-select>
                    </mat-form-field>
                    @if (c.output === 'points') {
                      <mat-form-field subscriptSizing="dynamic" class="mx-narrow">
                        <mat-label>{{ 'mx.cell.maxPoints' | dcTranslate }}</mat-label>
                        <input
                          matInput
                          type="number"
                          dir="ltr"
                          [ngModel]="c.maxPoints ?? null"
                          (ngModelChange)="setCriterion(gi, ci, { maxPoints: num($event) })"
                        />
                      </mat-form-field>
                    }
                    @if (c.scoring === 'bands' && isOption(c.input)) {
                      <mat-checkbox
                        [ngModel]="c.useOrdinal === true"
                        (ngModelChange)="setCriterion(gi, ci, { useOrdinal: $event })"
                        >{{ 'mx.cell.useOrdinal' | dcTranslate }}</mat-checkbox
                      >
                    }
                  </div>
                  <span class="dc-field-label">{{ 'mx.cell.appliesWhen' | dcTranslate }}</span>
                  <dc-logic-chip
                    mode="condition"
                    [rule]="c.appliesWhen ?? null"
                    [fields]="fields()"
                    [title]="'mx.cell.appliesWhen' | dcTranslate"
                    (ruleChange)="setCriterion(gi, ci, { appliesWhen: $event })"
                  />

                  @if (c.scoring === 'bands' && limitsOf(c.input); as limits) {
                    <p class="mx-ce__from">
                      <mat-icon fontSet="material-symbols-outlined">quiz</mat-icon>
                      {{ 'mx.cell.bankLimits' | dcTranslate }} <span dir="ltr">{{ limits }}</span>
                    </p>
                  }
                  @if (c.scoring === 'bands') {
                    <div class="mx-ce__bands-head">
                      <h4>{{ 'mx.cell.bands' | dcTranslate }}</h4>
                      @if (sizes().length) {
                        <mat-button-toggle-group
                          [ngModel]="sizeFilter()"
                          (ngModelChange)="sizeFilter.set($event)"
                          [attr.aria-label]="'mx.cell.size' | dcTranslate"
                        >
                          <mat-button-toggle [value]="null">{{
                            'mx.cell.allSizes' | dcTranslate
                          }}</mat-button-toggle>
                          @for (s of sizes(); track s.code) {
                            <mat-button-toggle [value]="s.code">{{
                              { ar: s.nameAr, en: s.nameEn } | dcLocalize
                            }}</mat-button-toggle>
                          }
                        </mat-button-toggle-group>
                      }
                    </div>
                    <table class="mx-ce__bands">
                      <thead>
                        <tr>
                          @if (sizes().length) {
                            <th>{{ 'mx.cell.size' | dcTranslate }}</th>
                          }
                          <th>{{ 'mx.cell.from' | dcTranslate }}</th>
                          <th>{{ 'mx.cell.to' | dcTranslate }}</th>
                          <th>
                            {{
                              (c.output === 'grade' ? 'mx.output.grade' : 'mx.output.points')
                                | dcTranslate
                            }}
                          </th>
                          <th>{{ 'mx.cell.label' | dcTranslate }}</th>
                          <th></th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (b of c.bands; track $index; let bi = $index) {
                          @if (sizeFilter() === null || b.size === sizeFilter() || !b.size) {
                            <tr>
                              @if (sizes().length) {
                                <td>
                                  <select
                                    [ngModel]="b.size ?? ''"
                                    (ngModelChange)="setBand(gi, ci, bi, { size: $event || null })"
                                    class="mx-ce__in"
                                  >
                                    <option value="">{{ 'mx.cell.allSizes' | dcTranslate }}</option>
                                    @for (s of sizes(); track s.code) {
                                      <option [value]="s.code">{{ s.code }}</option>
                                    }
                                  </select>
                                </td>
                              }
                              <td class="mx-ce__range">
                                <select
                                  [ngModel]="b.minInclusive"
                                  (ngModelChange)="setBand(gi, ci, bi, { minInclusive: $event })"
                                  class="mx-ce__op"
                                  [attr.aria-label]="'mx.cell.from' | dcTranslate"
                                >
                                  <option [ngValue]="true">≥</option>
                                  <option [ngValue]="false">&gt;</option>
                                </select>
                                <input
                                  type="number"
                                  dir="ltr"
                                  class="mx-ce__in"
                                  [ngModel]="b.min ?? null"
                                  (ngModelChange)="setBand(gi, ci, bi, { min: num($event) })"
                                />
                              </td>
                              <td class="mx-ce__range">
                                <select
                                  [ngModel]="b.maxInclusive"
                                  (ngModelChange)="setBand(gi, ci, bi, { maxInclusive: $event })"
                                  class="mx-ce__op"
                                  [attr.aria-label]="'mx.cell.to' | dcTranslate"
                                >
                                  <option [ngValue]="true">≤</option>
                                  <option [ngValue]="false">&lt;</option>
                                </select>
                                <input
                                  type="number"
                                  dir="ltr"
                                  class="mx-ce__in"
                                  [ngModel]="b.max ?? null"
                                  (ngModelChange)="setBand(gi, ci, bi, { max: num($event) })"
                                />
                              </td>
                              <td>
                                <input
                                  type="number"
                                  dir="ltr"
                                  class="mx-ce__in"
                                  [ngModel]="
                                    c.output === 'grade' ? (b.grade ?? null) : (b.points ?? null)
                                  "
                                  (ngModelChange)="
                                    setBand(
                                      gi,
                                      ci,
                                      bi,
                                      c.output === 'grade'
                                        ? { grade: num($event) }
                                        : { points: num($event) }
                                    )
                                  "
                                />
                              </td>
                              <td>
                                <input
                                  class="mx-ce__in"
                                  [ngModel]="b.label ?? ''"
                                  (ngModelChange)="setBand(gi, ci, bi, { label: $event || null })"
                                />
                              </td>
                              <td>
                                <button
                                  matIconButton
                                  type="button"
                                  (click)="removeBand(gi, ci, bi)"
                                  [attr.aria-label]="'mx.remove' | dcTranslate"
                                >
                                  <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
                                </button>
                              </td>
                            </tr>
                          }
                        }
                      </tbody>
                    </table>
                    <button
                      matButton
                      type="button"
                      (click)="addBand(gi, ci)"
                      [attr.data-testid]="'mx-add-band-' + c.code"
                    >
                      <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
                      {{ 'mx.cell.addBand' | dcTranslate }}
                    </button>
                  }
                  @if (c.scoring === 'options') {
                    <h4>{{ 'mx.cell.options' | dcTranslate }}</h4>
                    @switch (answerSource(c).from) {
                      @case ('bank') {
                        <p class="mx-ce__from" data-testid="mx-answers-from-bank">
                          <mat-icon fontSet="material-symbols-outlined">quiz</mat-icon>
                          {{ 'mx.cell.answersFromBank' | dcTranslate }}
                          <span dir="ltr">{{ answerSource(c).code }}</span>
                        </p>
                      }
                      @case ('set') {
                        <p class="mx-ce__from mx-ce__from--warn" data-testid="mx-answers-from-set">
                          <mat-icon fontSet="material-symbols-outlined">warning</mat-icon>
                          {{ 'mx.cell.answersFromSet' | dcTranslate }}
                          <span dir="ltr">{{ answerSource(c).code }}</span>
                        </p>
                      }
                    }
                    <table class="mx-ce__bands">
                      <thead>
                        <tr>
                          <th>{{ 'mx.cell.answer' | dcTranslate }}</th>
                          <th>
                            {{
                              (c.output === 'grade' ? 'mx.output.grade' : 'mx.output.points')
                                | dcTranslate
                            }}
                          </th>
                          <th>{{ 'mx.cell.attachment' | dcTranslate }}</th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (o of optionRows(c); track o.value) {
                          <tr>
                            <td [class.mx-ce__stale]="o.stale">
                              {{ o.label | dcLocalize }}
                              <span dir="ltr" class="dc-muted">{{ o.value }}</span>
                              @if (o.stale) {
                                <span class="mx-ce__stale-note">{{
                                  'mx.cell.staleAnswer' | dcTranslate
                                }}</span>
                                <button
                                  matIconButton
                                  type="button"
                                  (click)="dropOption(gi, ci, o.value)"
                                  [attr.aria-label]="'mx.remove' | dcTranslate"
                                >
                                  <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
                                </button>
                              }
                            </td>
                            <td>
                              <input
                                type="number"
                                dir="ltr"
                                class="mx-ce__in"
                                [ngModel]="
                                  c.output === 'grade'
                                    ? (o.score?.grade ?? null)
                                    : (o.score?.points ?? null)
                                "
                                (ngModelChange)="
                                  setOption(
                                    gi,
                                    ci,
                                    o.value,
                                    c.output === 'grade'
                                      ? { grade: num($event) }
                                      : { points: num($event) }
                                  )
                                "
                              />
                            </td>
                            <td>
                              <input
                                class="mx-ce__in"
                                dir="ltr"
                                [ngModel]="o.score?.attachment ?? ''"
                                (ngModelChange)="
                                  setOption(gi, ci, o.value, { attachment: $event || null })
                                "
                              />
                            </td>
                          </tr>
                        }
                      </tbody>
                    </table>
                    @if (!optionRows(c).length) {
                      <p class="dc-muted">{{ 'mx.cell.noOptions' | dcTranslate }}</p>
                    }
                  }
                  <div class="mx-ce__row mx-ce__end">
                    <button
                      matButton
                      type="button"
                      class="mx-danger"
                      (click)="removeCriterion(gi, ci)"
                    >
                      <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                      {{ 'mx.cell.removeCriterion' | dcTranslate }}
                    </button>
                  </div>
                </mat-expansion-panel>
              }
            </mat-accordion>
            <button
              matButton
              type="button"
              (click)="addCriterion(gi)"
              [attr.data-testid]="'mx-add-criterion-' + g.code"
            >
              <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
              {{ 'mx.cell.addCriterion' | dcTranslate }}
            </button>
          </section>
        }
        <button matButton="tonal" type="button" (click)="addGroup()" data-testid="mx-add-group">
          <mat-icon fontSet="material-symbols-outlined">library_add</mat-icon>
          {{ 'mx.cell.addGroup' | dcTranslate }}
        </button>
      }
      <div class="mx-ce__row">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'mx.cell.noteAr' | dcTranslate }}</mat-label>
          <input
            matInput
            dir="rtl"
            [ngModel]="cell().noteAr ?? ''"
            (ngModelChange)="set({ noteAr: $event || null })"
          />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'mx.cell.noteEn' | dcTranslate }}</mat-label>
          <input
            matInput
            dir="ltr"
            [ngModel]="cell().noteEn ?? ''"
            (ngModelChange)="set({ noteEn: $event || null })"
          />
        </mat-form-field>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close type="button">{{ 'form.cancel' | dcTranslate }}</button>
      <button matButton="filled" type="button" (click)="done()" data-testid="mx-cell-done">
        {{ 'logic.done' | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .mx-ce {
      display: grid;
      gap: 0.75rem;
    }
    .mx-ce mat-button-toggle-group {
      flex-wrap: wrap;
    }
    .mx-ce mat-button-toggle mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
      vertical-align: middle;
    }
    .mx-ce__row {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      align-items: center;
      margin-block: 0.25rem;
    }
    .mx-ce__row mat-form-field {
      flex: 1 1 12rem;
    }
    .mx-ce__row .mx-narrow {
      flex: 0 1 8rem;
    }
    .mx-ce__row .mx-wide {
      flex: 2 1 20rem;
    }
    .mx-ce__end {
      justify-content: flex-end;
    }
    .mx-ce__scale {
      width: min(32rem, 100%);
    }
    .mx-ce__formulas {
      display: grid;
      gap: 0.25rem;
    }
    .mx-ce__group {
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 10px;
      padding: 0.5rem 0.75rem 0.75rem;
    }
    .mx-ce__total {
      font-size: 13px;
    }
    .mx-ce__src {
      margin-inline-end: 6px;
      font-size: 18px;
      width: 18px;
      height: 18px;
      color: var(--mat-sys-primary, #1b8354);
    }
    .mx-ce__bands-head {
      display: flex;
      align-items: center;
      gap: 1rem;
      flex-wrap: wrap;
      margin-top: 0.5rem;
    }
    h4 {
      margin: 0.5rem 0 0.25rem;
      font-size: 14px;
    }
    .mx-ce__bands {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
    }
    .mx-ce__bands th {
      text-align: start;
      font-weight: 600;
      padding: 4px;
    }
    .mx-ce__bands td {
      padding: 2px 4px;
      border-top: 1px solid var(--dga-gray-200, #e5e7eb);
    }
    .mx-ce__range {
      white-space: nowrap;
    }
    .mx-ce__in {
      width: 100%;
      min-width: 4rem;
      padding: 5px 6px;
      border: 1px solid var(--dga-gray-300, #d2d6db);
      border-radius: 6px;
      font: inherit;
      background: #fff;
    }
    .mx-ce__range .mx-ce__in {
      width: 6rem;
    }
    .mx-ce__op {
      padding: 5px 2px;
      border: 1px solid var(--dga-gray-300, #d2d6db);
      border-radius: 6px;
      margin-inline-end: 4px;
      background: #fff;
    }
    .mx-danger {
      color: var(--dga-error-600, #d92d20);
    }
    .mx-ce__from {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0.25rem 0;
      font-size: 12px;
      color: var(--dga-text-3, #6c737f);
    }
    .mx-ce__from mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
    .mx-ce__from--warn {
      color: var(--dga-warning-700, #b54708);
    }
    .mx-ce__stale {
      color: var(--dga-error-600, #d92d20);
    }
    .mx-ce__stale-note {
      font-size: 11px;
      margin-inline-start: 6px;
    }
  `,
})
export class MatrixCellEditor {
  protected readonly data = inject<MatrixCellEditorData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<MatrixCellEditor, MatrixCellEditorResult>);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(I18nService);
  protected readonly kinds = CELL_KINDS;
  protected readonly cellIcons = CELL_ICONS;
  protected readonly sourceIcons = SOURCE_ICONS;
  protected readonly lang = this.i18n.language;

  protected readonly cell = signal<MatrixCell>(
    structuredClone(this.data.cell ?? newCell(this.data.row, this.data.column, 'points')),
  );
  protected readonly inputs = signal<MatrixInput[]>(structuredClone(this.data.matrix.inputs));
  protected readonly sizeFilter = signal<string | null>(null);
  protected readonly sizes = computed(() => this.data.matrix.sizes?.categories ?? []);
  protected readonly fields = computed(() =>
    inputFields(
      { ...this.data.matrix, inputs: this.inputs() },
      undefined,
      this.data.references.answersOf,
    ),
  );
  protected readonly otherRows = computed(() =>
    this.data.matrix.rows.filter((r) => r.code !== this.data.row),
  );

  protected rowName(): string {
    const r = this.data.matrix.rows.find((x) => x.code === this.data.row);
    return (this.lang() === 'ar' ? r?.nameAr : r?.nameEn) || this.data.row;
  }

  protected columnName(): string {
    const c = this.data.matrix.columns.find((x) => x.code === this.data.column);
    return (this.lang() === 'ar' ? c?.nameAr : c?.nameEn) || this.data.column;
  }

  protected num(value: unknown): number | null {
    return value === '' || value === null || value === undefined ? null : Number(value);
  }

  protected scaleText(code: string): string {
    const s = this.data.matrix.scales.find((x) => x.code === code);
    return (s?.steps ?? []).map((st) => `${st.min ?? ''}–${st.max ?? ''}→${st.grade}`).join(' · ');
  }

  protected inputOf(code: string): MatrixInput | undefined {
    return this.inputs().find((i) => i.code === code);
  }

  protected inputName(code: string): string {
    const i = this.inputOf(code);
    return i ? (this.lang() === 'ar' ? i.nameAr : i.nameEn) || i.code : code;
  }

  protected sourceOf(code: string): string {
    return this.inputOf(code)?.source.kind ?? 'given';
  }

  protected sourceIcon(code: string): string {
    return SOURCE_ICONS[(this.inputOf(code)?.source.kind ?? 'given') as keyof typeof SOURCE_ICONS];
  }

  protected isOption(code: string): boolean {
    return this.inputOf(code)?.type === 'option';
  }

  protected groupTotal(g: MatrixGroup): string {
    const points = g.criteria
      .filter((c) => c.output === 'points')
      .reduce((sum, c) => sum + (c.maxPoints ?? 0), 0);
    return this.cell().kind === 'points'
      ? `${g.criteria.length} · ${points}${g.maxPoints ? ` / ${g.maxPoints}` : ''}`
      : `${g.criteria.length}`;
  }

  protected criterionSummary(c: MatrixCriterion): string {
    const t = (k: string) => this.i18n.translate(k);
    const scoring = t(`mx.scoring.${c.scoring}`);
    const count =
      c.scoring === 'bands'
        ? ` (${c.bands.length})`
        : c.scoring === 'options'
          ? ` (${c.options.length})`
          : '';
    const max =
      c.output === 'points' && c.maxPoints
        ? ` · ${c.maxPoints} ${t('mx.output.points')}`
        : c.output === 'grade'
          ? ` · ${t('mx.output.grade')}`
          : '';
    return `${scoring}${count}${max}${c.appliesWhen ? ` · ${t('mx.cell.conditional')}` : ''}`;
  }

  /** The answers of an option input, with the score each one gives. */
  /** Where a criterion's answers come from: the input's bank question, else its answer list, else only what is scored. */
  protected answerSource(c: MatrixCriterion): {
    from: 'bank' | 'set' | 'none';
    code: string | null;
  } {
    const input = this.inputOf(c.input);
    if (!input) return { from: 'none', code: null };
    const bank = this.data.references.bankQuestion(bankQuestionCode(input));
    if (bank?.answers) return { from: 'bank', code: bank.code };
    return input.optionSet ? { from: 'set', code: input.optionSet } : { from: 'none', code: null };
  }

  /**
   * The answers of an option input with the score each one gives: the bank question's answers (else the answer list's);
   * scored answers the question no longer has are kept at the end, marked, so they can be removed.
   */
  protected optionRows(c: MatrixCriterion) {
    const input = this.inputOf(c.input);
    const source = this.answerSource(c);
    const bank =
      source.from === 'bank' && input ? (this.data.references.answersOf(input) ?? []) : null;
    const set =
      source.from === 'set'
        ? this.data.matrix.optionSets.find((s) => s.code === input?.optionSet)
        : undefined;
    const values = bank
      ? bank.map((o) => ({ value: o.value, label: { ar: o.labelAr, en: o.labelEn } }))
      : set
        ? set.items.map((o) => ({ value: o.code, label: { ar: o.labelAr, en: o.labelEn } }))
        : [];
    const known = new Set(values.map((v) => v.value.toLowerCase()));
    const stale = c.options
      .filter((o) => !known.has(o.value.toLowerCase()))
      .map((o) => ({
        value: o.value,
        label: { ar: o.value, en: o.value },
        stale: source.from !== 'none',
      }));
    return [...values.map((v) => ({ ...v, stale: false })), ...stale].map((v) => ({
      ...v,
      score: c.options.find((o) => o.value.toLowerCase() === v.value.toLowerCase()),
    }));
  }

  /** The answer limits the bank question sets, as text (e.g. "0 – 500"), for a number criterion. */
  protected limitsOf(code: string): string | null {
    const input = this.inputOf(code);
    const bank = input ? this.data.references.bankQuestion(bankQuestionCode(input)) : null;
    if (!bank || (bank.min === null && bank.max === null)) return null;
    return `${bank.min ?? '…'} – ${bank.max ?? '…'}`;
  }

  protected dropOption(gi: number, ci: number, value: string): void {
    this.criteria(gi, (cs) =>
      cs.map((c, i) =>
        i === ci ? { ...c, options: c.options.filter((o) => o.value !== value) } : c,
      ),
    );
  }

  protected set(change: Partial<MatrixCell>): void {
    this.cell.update((c) => ({ ...c, ...change }));
  }

  protected kind(kind: CellKind): void {
    this.cell.update((c) => newCell(c.row, c.column, kind, c));
    if (kind === 'worst')
      this.groups((gs) =>
        gs.map((g) => ({
          ...g,
          criteria: g.criteria.map((c) => ({ ...c, output: 'grade' as const })),
        })),
      );
  }

  private groups(fn: (groups: MatrixGroup[]) => MatrixGroup[]): void {
    this.cell.update((c) => ({ ...c, groups: fn(c.groups) }));
  }

  protected setGroup(gi: number, change: Partial<MatrixGroup>): void {
    this.groups((gs) => gs.map((g, i) => (i === gi ? { ...g, ...change } : g)));
  }

  protected addGroup(): void {
    this.groups((gs) => [
      ...gs,
      newGroup(
        freeCode(
          gs.map((g) => g.code),
          'group',
        ),
      ),
    ]);
  }

  protected removeGroup(gi: number): void {
    this.groups((gs) => gs.filter((_, i) => i !== gi));
  }

  private criteria(gi: number, fn: (criteria: MatrixCriterion[]) => MatrixCriterion[]): void {
    this.groups((gs) => gs.map((g, i) => (i === gi ? { ...g, criteria: fn(g.criteria) } : g)));
  }

  protected setCriterion(gi: number, ci: number, change: Partial<MatrixCriterion>): void {
    this.criteria(gi, (cs) => cs.map((c, i) => (i === ci ? { ...c, ...change } : c)));
  }

  protected addCriterion(gi: number): void {
    const used = this.cell().groups.flatMap((g) => g.criteria.map((c) => c.code));
    const output = this.cell().kind === 'worst' ? 'grade' : 'points';
    this.criteria(gi, (cs) => [
      ...cs,
      newCriterion(freeCode(used, 'c'), this.inputs()[0]?.code ?? '', output),
    ]);
  }

  protected removeCriterion(gi: number, ci: number): void {
    this.criteria(gi, (cs) => cs.filter((_, i) => i !== ci));
  }

  protected scoring(gi: number, ci: number, scoring: MatrixCriterion['scoring']): void {
    this.setCriterion(gi, ci, { scoring });
  }

  private bands(gi: number, ci: number, fn: (bands: MatrixBand[]) => MatrixBand[]): void {
    this.criteria(gi, (cs) => cs.map((c, i) => (i === ci ? { ...c, bands: fn(c.bands) } : c)));
  }

  protected setBand(gi: number, ci: number, bi: number, change: Partial<MatrixBand>): void {
    this.bands(gi, ci, (bs) => bs.map((b, i) => (i === bi ? { ...b, ...change } : b)));
  }

  /** A new band starts where the last band of the same size ended. */
  protected addBand(gi: number, ci: number): void {
    const size = this.sizeFilter();
    this.bands(gi, ci, (bs) => {
      const last = [...bs].reverse().find((b) => (b.size ?? null) === size);
      return [
        ...bs,
        {
          size,
          min: last?.max ?? 0,
          minInclusive: last ? !last.maxInclusive : true,
          max: null,
          maxInclusive: false,
          points: null,
          grade: null,
          label: null,
        },
      ];
    });
  }

  protected removeBand(gi: number, ci: number, bi: number): void {
    this.bands(gi, ci, (bs) => bs.filter((_, i) => i !== bi));
  }

  protected setOption(
    gi: number,
    ci: number,
    value: string,
    change: { points?: number | null; grade?: number | null; attachment?: string | null },
  ): void {
    this.criteria(gi, (cs) =>
      cs.map((c, i) => {
        if (i !== ci) return c;
        const existing = c.options.find((o) => o.value === value);
        const options = existing
          ? c.options.map((o) => (o.value === value ? { ...o, ...change } : o))
          : [...c.options, { value, ...change }];
        return { ...c, options };
      }),
    );
  }

  /** Where the input's value comes from, set up in place (question, task field, operation…). */
  protected async editInput(code: string): Promise<void> {
    const input = this.inputOf(code);
    if (input) await this.openInput(input, false);
  }

  protected async newInput(gi: number, ci: number): Promise<void> {
    const code = freeCode(
      this.inputs().map((i) => i.code),
      'input',
    );
    const created = await this.openInput(
      { code, nameAr: '', nameEn: '', type: 'number', source: { kind: 'applicant' } },
      true,
    );
    if (created) this.setCriterion(gi, ci, { input: created.code });
  }

  private async openInput(input: MatrixInput, isNew: boolean): Promise<MatrixInput | null> {
    const result = await firstValueFrom(
      this.dialog
        .open<
          MatrixInputEditor,
          MatrixInputEditorData,
          { input: MatrixInput | null; removed?: boolean }
        >(MatrixInputEditor, {
          data: {
            matrix: { ...this.data.matrix, inputs: this.inputs() },
            input,
            isNew,
            references: this.data.references,
          },
          width: '60rem',
          maxWidth: '96vw',
        })
        .afterClosed(),
    );
    if (!result) return null;
    if (result.removed) {
      this.inputs.update((list) => list.filter((i) => i.code !== input.code));
      return null;
    }
    const saved = result.input!;
    this.inputs.update((list) =>
      isNew ? [...list, saved] : list.map((i) => (i.code === input.code ? saved : i)),
    );
    return saved;
  }

  protected done(): void {
    this.ref.close({ cell: this.cell(), inputs: this.inputs() });
  }
}
