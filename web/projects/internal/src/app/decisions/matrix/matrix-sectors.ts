import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import {
  I18nService,
  LocalizePipe,
  LogicChip,
  SearchableSelectModule,
  TranslatePipe,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import {
  MatrixCellEditor,
  MatrixCellEditorData,
  MatrixCellEditorResult,
} from './matrix-cell-editor';
import {
  TrackView,
  addBand,
  addCriterion,
  addGroup,
  copyBandsToSizes,
  editTrack,
  moveCriterion,
  moveGroup,
  ownTrack,
  removeBand,
  removeCriterion,
  removeGroup,
  setAnswer,
  setBand,
  setCriterion,
  setGroup,
  startTrack,
  trackOf,
} from './matrix-drill';
import {
  FormLink,
  Placement,
  applicantInput,
  formForSector,
  moveQuestion,
  parseOptions,
  placeQuestion,
  placements,
  sectorQuestions,
  sectorValue,
  sectorsQuestion,
  setRequired,
  showForSector,
  shownFor,
  stepForTrack,
  visibility,
} from './matrix-form';
import { MatrixInputEditor, MatrixInputEditorData } from './matrix-input-editor';
import {
  CELL_ICONS,
  Matrix,
  MatrixBand,
  MatrixCell,
  MatrixCriterion,
  MatrixGroup,
  MatrixInput,
  MatrixRow,
  bankOptions,
  cellInputs,
  freeCode,
  hasWeights,
  inputFields,
  removeRow,
  withCell,
  withShared,
} from './matrix-model';
import { MatrixReferences } from './matrix-references';

/**
 * The classification set up one sector at a time, as the reviewed system does it: the sectors, then a sector's tracks,
 * and under the chosen track its groups, under a group its criteria and under a criterion its bands or answers — all on
 * the same page, edited in the tables. A track shared with other sectors says so and can be given its own copy.
 */
@Component({
  selector: 'dc-matrix-sectors',
  imports: [
    FormsModule,
    LocalizePipe,
    LogicChip,
    MatButtonModule,
    MatCheckboxModule,
    MatIconModule,
    MatTooltipModule,
    RouterLink,
    SearchableSelectModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!sector(); as _) {
      <section class="sx-panel" data-testid="sx-sectors">
        <header class="sx-panel__head">
          <h3>
            {{ 'mx.sec.sectors' | dcTranslate }} <span class="sx-count">{{ m().rows.length }}</span>
          </h3>
        </header>
        <table class="dc-table sx-table">
          <thead>
            <tr>
              <th>{{ 'mx.code' | dcTranslate }}</th>
              <th>{{ 'config.nameAr' | dcTranslate }}</th>
              <th>{{ 'config.nameEn' | dcTranslate }}</th>
              <th>{{ 'mx.sec.answer' | dcTranslate }}</th>
              <th>{{ 'mx.sec.tracks' | dcTranslate }}</th>
              <th>{{ 'config.active' | dcTranslate }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (r of m().rows; track r.code) {
              <tr [attr.data-testid]="'sx-sector-' + r.code">
                <td dir="ltr" class="sx-code">{{ r.code }}</td>
                <td>
                  <input
                    class="sx-cell"
                    [ngModel]="r.nameAr"
                    (ngModelChange)="setRow(r.code, { nameAr: $event })"
                    [disabled]="!editable()"
                    [attr.aria-label]="'config.nameAr' | dcTranslate"
                  />
                </td>
                <td>
                  <input
                    class="sx-cell"
                    dir="ltr"
                    [ngModel]="r.nameEn"
                    (ngModelChange)="setRow(r.code, { nameEn: $event })"
                    [disabled]="!editable()"
                    [attr.aria-label]="'config.nameEn' | dcTranslate"
                  />
                </td>
                <td>
                  <input
                    class="sx-cell sx-cell--short"
                    dir="ltr"
                    [ngModel]="r.answer ?? ''"
                    (ngModelChange)="setRow(r.code, { answer: $event || null })"
                    [disabled]="!editable()"
                    [placeholder]="r.code"
                    [attr.aria-label]="'mx.sec.answer' | dcTranslate"
                  />
                </td>
                <td>
                  <span class="sx-tracks">
                    @for (c of m().columns; track c.code) {
                      @let v = trackView(r.code, c.code);
                      <span
                        class="sx-chip"
                        [class.sx-chip--off]="v.mode === 'empty'"
                        [matTooltip]="trackHint(v)"
                        >{{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}
                        @if (v.mode === 'shared') {
                          <mat-icon fontSet="material-symbols-outlined">link</mat-icon>
                        }
                      </span>
                    }
                  </span>
                </td>
                <td>
                  <mat-checkbox
                    [ngModel]="r.active"
                    (ngModelChange)="setRow(r.code, { active: $event })"
                    [disabled]="!editable()"
                    [aria-label]="'config.active' | dcTranslate"
                  />
                </td>
                <td class="sx-actions">
                  <button
                    matButton="tonal"
                    type="button"
                    (click)="openSector(r.code)"
                    [attr.data-testid]="'sx-open-' + r.code"
                  >
                    {{ 'mx.sec.tracks' | dcTranslate }}
                    <mat-icon class="sx-flip" fontSet="material-symbols-outlined"
                      >chevron_left</mat-icon
                    >
                  </button>
                  @if (editable()) {
                    <button
                      matIconButton
                      type="button"
                      (click)="dropSector(r.code)"
                      [attr.aria-label]="'config.delete' | dcTranslate"
                    >
                      <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                    </button>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
        @if (editable()) {
          <div class="sx-add">
            <input
              class="sx-cell sx-cell--short"
              dir="ltr"
              [(ngModel)]="newCode"
              [placeholder]="'mx.code' | dcTranslate"
              data-testid="sx-new-code"
            />
            <input
              class="sx-cell"
              [(ngModel)]="newNameAr"
              [placeholder]="'config.nameAr' | dcTranslate"
            />
            <input
              class="sx-cell"
              dir="ltr"
              [(ngModel)]="newNameEn"
              [placeholder]="'config.nameEn' | dcTranslate"
            />
            <button matButton type="button" (click)="addSector()" data-testid="sx-add-sector">
              <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
              {{ 'mx.sec.addSector' | dcTranslate }}
            </button>
          </div>
        }
      </section>
    } @else {
      @let r = row()!;
      <div class="sx-crumbs">
        <button matButton type="button" (click)="closeSector()" data-testid="sx-back">
          <mat-icon class="sx-flip" fontSet="material-symbols-outlined">arrow_forward</mat-icon>
          {{ 'mx.sec.sectors' | dcTranslate }}
        </button>
        <h2>
          {{ { ar: r.nameAr, en: r.nameEn } | dcLocalize }}
          <span dir="ltr" class="sx-code">{{ r.code }}</span>
        </h2>
      </div>

      <nav class="sx-tabs" role="tablist">
        @for (t of views; track t) {
          <button
            type="button"
            role="tab"
            class="sx-tab"
            [class.sx-tab--on]="view() === t"
            [attr.aria-selected]="view() === t"
            (click)="view.set(t)"
            [attr.data-testid]="'sx-view-' + t"
          >
            {{ 'mx.sec.view.' + t | dcTranslate }}
            @if (t === 'questions') {
              <span class="sx-count">{{ questionRows().length }}</span>
            }
          </button>
        }
      </nav>

      @if (view() === 'criteria') {
        <!-- Tracks of the sector -->
        <section class="sx-panel" data-testid="sx-tracks">
          <header class="sx-panel__head">
            <h3>
              {{ 'mx.sec.tracks' | dcTranslate }}
              <span class="sx-count">{{ m().columns.length }}</span>
            </h3>
            <span class="dc-muted">{{ 'mx.sec.tracksHint' | dcTranslate }}</span>
          </header>
          <table class="dc-table sx-table">
            <thead>
              <tr>
                <th>{{ 'mx.code' | dcTranslate }}</th>
                <th>{{ 'config.name' | dcTranslate }}</th>
                <th>{{ 'mx.cell.kind' | dcTranslate }}</th>
                <th>{{ 'mx.cell.scale' | dcTranslate }}</th>
                <th>{{ 'mx.sec.inFinal' | dcTranslate }}</th>
                <th>{{ 'mx.sec.groups' | dcTranslate }}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (c of m().columns; track c.code) {
                @let v = trackView(r.code, c.code);
                <tr
                  [class.sx-row--on]="track() === c.code"
                  [attr.data-testid]="'sx-track-' + c.code"
                >
                  <td dir="ltr" class="sx-code">{{ c.code }}</td>
                  <td>{{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}</td>
                  <td>
                    <span class="sx-kind">
                      <mat-icon fontSet="material-symbols-outlined">{{
                        icons[v.mode === 'shared' ? 'shared' : (v.calculation?.kind ?? 'empty')]
                      }}</mat-icon>
                      {{ kindLabel(v) }}
                      @if (v.mode === 'shared') {
                        <span class="sx-badge" [matTooltip]="sharedNames(v)">{{
                          sharedLabel(v)
                        }}</span>
                      }
                    </span>
                  </td>
                  <td>
                    @if (v.calculation?.kind === 'points') {
                      <mat-select
                        class="dc-compact-select sx-cell"
                        [ngModel]="v.calculation!.scale ?? null"
                        (ngModelChange)="edit(c.code, (x) => ({ ...x, scale: $event }))"
                        [disabled]="!canEdit(v)"
                        [aria-label]="'mx.cell.scale' | dcTranslate"
                      >
                        @for (s of m().scales; track s.code) {
                          <mat-option [value]="s.code">{{
                            { ar: s.nameAr, en: s.nameEn } | dcLocalize
                          }}</mat-option>
                        }
                      </mat-select>
                      <button
                        matIconButton
                        type="button"
                        (click)="openShared.emit('grades')"
                        [matTooltip]="'mx.sec.editScales' | dcTranslate"
                        [attr.aria-label]="'mx.sec.editScales' | dcTranslate"
                        data-testid="sx-edit-scales"
                      >
                        <mat-icon fontSet="material-symbols-outlined">tune</mat-icon>
                      </button>
                    } @else {
                      —
                    }
                  </td>
                  <td>
                    <mat-checkbox
                      [ngModel]="c.includeInFinal"
                      (ngModelChange)="setColumn(c.code, $event)"
                      [disabled]="!editable()"
                      [matTooltip]="'mx.sec.allSectors' | dcTranslate"
                      [aria-label]="'mx.sec.inFinal' | dcTranslate"
                    />
                  </td>
                  <td>{{ v.calculation?.groups?.length ?? 0 }}</td>
                  <td class="sx-actions">
                    <button
                      matButton="tonal"
                      type="button"
                      (click)="openTrack(c.code)"
                      [attr.data-testid]="'sx-open-track-' + c.code"
                    >
                      {{ 'mx.sec.groups' | dcTranslate }}
                      <mat-icon fontSet="material-symbols-outlined">expand_more</mat-icon>
                    </button>
                    <button
                      matIconButton
                      type="button"
                      (click)="fullEditor(c.code)"
                      [matTooltip]="'mx.sec.fullEditor' | dcTranslate"
                      [attr.aria-label]="'mx.sec.fullEditor' | dcTranslate"
                    >
                      <mat-icon fontSet="material-symbols-outlined">open_in_new</mat-icon>
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </section>

        @if (track(); as col) {
          @let v = trackView(r.code, col);
          @let calc = v.calculation;
          <!-- What kind of calculation the chosen track has, and how it is shared -->
          @if (v.mode === 'shared') {
            <div class="sx-note sx-note--shared" data-testid="sx-shared">
              <mat-icon fontSet="material-symbols-outlined">link</mat-icon>
              <span>{{ 'mx.sec.sharedNote' | dcTranslate }} {{ sharedNames(v) }}</span>
              @if (editable()) {
                <button matButton type="button" (click)="own(col)" data-testid="sx-own">
                  <mat-icon fontSet="material-symbols-outlined">link_off</mat-icon>
                  {{ 'mx.shared.onlyThis' | dcTranslate }}
                </button>
              }
            </div>
          } @else if (v.mode === 'same') {
            <div class="sx-note">
              <mat-icon fontSet="material-symbols-outlined">content_copy</mat-icon>
              <span>{{ 'mx.cell.sameAs' | dcTranslate }} {{ rowName(v.sameAs) }}</span>
              @if (editable()) {
                <button matButton type="button" (click)="own(col)">
                  {{ 'mx.sec.ownCopy' | dcTranslate }}
                </button>
              }
            </div>
          } @else if (v.mode === 'empty') {
            <div class="sx-note">
              <mat-icon fontSet="material-symbols-outlined">block</mat-icon>
              <span>{{ 'mx.kind.emptyHint' | dcTranslate }}</span>
              @if (editable()) {
                <button
                  matButton
                  type="button"
                  (click)="start(col, 'points')"
                  data-testid="sx-start"
                >
                  {{ 'mx.kind.points' | dcTranslate }}
                </button>
                <button matButton type="button" (click)="start(col, 'worst')">
                  {{ 'mx.kind.worst' | dcTranslate }}
                </button>
              }
            </div>
          }
          @if (calc?.kind === 'formula') {
            <div class="sx-note">
              <mat-icon fontSet="material-symbols-outlined">function</mat-icon>
              <span>{{ 'mx.kind.formulaHint' | dcTranslate }}</span>
              <button matButton type="button" (click)="fullEditor(col)">
                {{ 'mx.sec.fullEditor' | dcTranslate }}
              </button>
            </div>
          }

          @if (calc && (calc.kind === 'points' || calc.kind === 'worst')) {
            <!-- Groups of the track -->
            <section class="sx-panel" data-testid="sx-groups">
              <header class="sx-panel__head">
                <span class="sx-path">{{ columnName(col) }} ›</span>
                <h3>
                  {{ 'mx.sec.groups' | dcTranslate }}
                  <span class="sx-count">{{ calc.groups.length }}</span>
                </h3>
                @if (editable() && canEdit(v)) {
                  <button
                    matButton
                    type="button"
                    (click)="edit(col, addGroup)"
                    data-testid="sx-add-group"
                  >
                    <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
                    {{ 'mx.cell.addGroup' | dcTranslate }}
                  </button>
                }
              </header>
              <table class="dc-table sx-table">
                <thead>
                  <tr>
                    <th>{{ 'mx.code' | dcTranslate }}</th>
                    <th>{{ 'config.nameAr' | dcTranslate }}</th>
                    <th>{{ 'config.nameEn' | dcTranslate }}</th>
                    @if (calc.kind === 'points') {
                      <th>{{ 'mx.cell.maxPoints' | dcTranslate }}</th>
                    }
                    <th>{{ 'mx.cell.mandatory' | dcTranslate }}</th>
                    <th>{{ 'mx.cell.minPoints' | dcTranslate }}</th>
                    <th>{{ 'mx.sec.criteria' | dcTranslate }}</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  @for (
                    g of calc.groups;
                    track $index;
                    let gi = $index;
                    let first = $first;
                    let last = $last
                  ) {
                    <tr
                      [class.sx-row--on]="group() === gi"
                      [attr.data-testid]="'sx-group-' + g.code"
                    >
                      <td>
                        <input
                          class="sx-cell sx-cell--short"
                          dir="ltr"
                          [ngModel]="g.code"
                          (ngModelChange)="setG(col, gi, { code: $event })"
                          [disabled]="!canEdit(v)"
                          [attr.aria-label]="'mx.code' | dcTranslate"
                        />
                      </td>
                      <td>
                        <input
                          class="sx-cell"
                          [ngModel]="g.nameAr"
                          (ngModelChange)="setG(col, gi, { nameAr: $event })"
                          [disabled]="!canEdit(v)"
                          [attr.aria-label]="'config.nameAr' | dcTranslate"
                        />
                      </td>
                      <td>
                        <input
                          class="sx-cell"
                          dir="ltr"
                          [ngModel]="g.nameEn"
                          (ngModelChange)="setG(col, gi, { nameEn: $event })"
                          [disabled]="!canEdit(v)"
                          [attr.aria-label]="'config.nameEn' | dcTranslate"
                        />
                      </td>
                      @if (calc.kind === 'points') {
                        <td>
                          <input
                            class="sx-cell sx-cell--num"
                            type="number"
                            [ngModel]="g.maxPoints ?? null"
                            (ngModelChange)="setG(col, gi, { maxPoints: num($event) })"
                            [disabled]="!canEdit(v)"
                            [attr.aria-label]="'mx.cell.maxPoints' | dcTranslate"
                          />
                        </td>
                      }
                      <td>
                        <mat-checkbox
                          [ngModel]="g.mandatory"
                          (ngModelChange)="setG(col, gi, { mandatory: $event })"
                          [disabled]="!canEdit(v)"
                          [aria-label]="'mx.cell.mandatory' | dcTranslate"
                        />
                      </td>
                      <td>
                        <input
                          class="sx-cell sx-cell--num"
                          type="number"
                          [ngModel]="g.minPoints ?? 0"
                          (ngModelChange)="setG(col, gi, { minPoints: num($event) ?? 0 })"
                          [disabled]="!canEdit(v) || !g.mandatory"
                          [attr.aria-label]="'mx.cell.minPoints' | dcTranslate"
                        />
                      </td>
                      <td>{{ g.criteria.length }}</td>
                      <td class="sx-actions">
                        <button
                          matButton="tonal"
                          type="button"
                          (click)="openGroup(gi)"
                          [attr.data-testid]="'sx-open-group-' + g.code"
                        >
                          {{ 'mx.sec.criteria' | dcTranslate }}
                          <mat-icon fontSet="material-symbols-outlined">expand_more</mat-icon>
                        </button>
                        @if (canEdit(v)) {
                          <button
                            matIconButton
                            type="button"
                            [disabled]="first"
                            (click)="edit(col, (x) => moveGroup(x, gi, -1))"
                            [attr.aria-label]="'mx.sec.up' | dcTranslate"
                          >
                            <mat-icon fontSet="material-symbols-outlined">arrow_upward</mat-icon>
                          </button>
                          <button
                            matIconButton
                            type="button"
                            [disabled]="last"
                            (click)="edit(col, (x) => moveGroup(x, gi, 1))"
                            [attr.aria-label]="'mx.sec.down' | dcTranslate"
                          >
                            <mat-icon fontSet="material-symbols-outlined">arrow_downward</mat-icon>
                          </button>
                          <button
                            matIconButton
                            type="button"
                            (click)="dropGroup(col, gi)"
                            [attr.aria-label]="'config.delete' | dcTranslate"
                          >
                            <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                          </button>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
              <p class="sx-form-link dc-muted" data-testid="sx-questions-count">
                <mat-icon fontSet="material-symbols-outlined">dynamic_form</mat-icon>
                {{ questionsLabel(col) }}
                <button
                  matButton
                  type="button"
                  (click)="goTo('form', col)"
                  data-testid="sx-view-form-of-track"
                >
                  {{ 'mx.sec.viewForm' | dcTranslate }}
                </button>
                <button
                  matButton
                  type="button"
                  (click)="goTo('questions', col)"
                  data-testid="sx-edit-questions-of-track"
                >
                  {{ 'mx.sec.editQuestions' | dcTranslate }}
                </button>
              </p>
            </section>

            @if (group() !== null && calc.groups[group()!]; as g) {
              @let gi = group()!;
              <!-- Criteria of the group -->
              <section class="sx-panel" data-testid="sx-criteria">
                <header class="sx-panel__head">
                  <span class="sx-path"
                    >{{ columnName(col) }} ›
                    {{ { ar: g.nameAr, en: g.nameEn } | dcLocalize }} ›</span
                  >
                  <h3>
                    {{ 'mx.sec.criteria' | dcTranslate }}
                    <span class="sx-count">{{ g.criteria.length }}</span>
                  </h3>
                  @if (canEdit(v)) {
                    <button
                      matButton
                      type="button"
                      (click)="newCriterionIn(col, gi)"
                      data-testid="sx-add-criterion"
                    >
                      <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
                      {{ 'mx.cell.addCriterion' | dcTranslate }}
                    </button>
                  }
                </header>
                <table class="dc-table sx-table">
                  <thead>
                    <tr>
                      <th>{{ 'mx.code' | dcTranslate }}</th>
                      <th>{{ 'mx.cell.input' | dcTranslate }}</th>
                      <th>{{ 'mx.cell.scoring' | dcTranslate }}</th>
                      <th>{{ 'mx.cell.output' | dcTranslate }}</th>
                      <th>{{ 'mx.cell.maxPoints' | dcTranslate }}</th>
                      <th>{{ 'mx.cell.appliesWhen' | dcTranslate }}</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (
                      c of g.criteria;
                      track $index;
                      let ci = $index;
                      let first = $first;
                      let last = $last
                    ) {
                      <tr
                        [class.sx-row--on]="criterion() === ci"
                        [attr.data-testid]="'sx-criterion-' + c.code"
                      >
                        <td>
                          <input
                            class="sx-cell sx-cell--short"
                            dir="ltr"
                            [ngModel]="c.code"
                            (ngModelChange)="setC(col, gi, ci, { code: $event })"
                            [disabled]="!canEdit(v)"
                            [attr.aria-label]="'mx.code' | dcTranslate"
                          />
                        </td>
                        <td>
                          <span class="sx-input">
                            <mat-select
                              class="dc-compact-select sx-cell sx-cell--wide"
                              [ngModel]="c.input"
                              (ngModelChange)="chooseInput(col, gi, ci, $event)"
                              [disabled]="!canEdit(v)"
                              [aria-label]="'mx.cell.input' | dcTranslate"
                            >
                              @for (i of m().inputs; track i.code) {
                                <mat-option [value]="i.code"
                                  >{{ { ar: i.nameAr, en: i.nameEn } | dcLocalize }} ({{
                                    i.code
                                  }})</mat-option
                                >
                              }
                            </mat-select>
                            <button
                              matIconButton
                              type="button"
                              (click)="editInput(c.input)"
                              [disabled]="!editable()"
                              [matTooltip]="'mx.sec.editInput' | dcTranslate"
                              [attr.aria-label]="'mx.sec.editInput' | dcTranslate"
                            >
                              <mat-icon fontSet="material-symbols-outlined">tune</mat-icon>
                            </button>
                          </span>
                        </td>
                        <td>
                          <mat-select
                            class="dc-compact-select sx-cell"
                            [ngModel]="c.scoring"
                            (ngModelChange)="setC(col, gi, ci, { scoring: $event })"
                            [disabled]="!canEdit(v)"
                            [aria-label]="'mx.cell.scoring' | dcTranslate"
                          >
                            @for (s of scorings; track s) {
                              <mat-option [value]="s">{{
                                'mx.scoring.' + s | dcTranslate
                              }}</mat-option>
                            }
                          </mat-select>
                        </td>
                        <td>
                          <mat-select
                            class="dc-compact-select sx-cell"
                            [ngModel]="c.output"
                            (ngModelChange)="setC(col, gi, ci, { output: $event })"
                            [disabled]="!canEdit(v)"
                            [aria-label]="'mx.cell.output' | dcTranslate"
                          >
                            <mat-option value="points">{{
                              'mx.output.points' | dcTranslate
                            }}</mat-option>
                            <mat-option value="grade">{{
                              'mx.output.grade' | dcTranslate
                            }}</mat-option>
                          </mat-select>
                        </td>
                        <td>
                          <input
                            class="sx-cell sx-cell--num"
                            type="number"
                            [ngModel]="c.maxPoints ?? null"
                            (ngModelChange)="setC(col, gi, ci, { maxPoints: num($event) })"
                            [disabled]="!canEdit(v) || c.output !== 'points'"
                            [attr.aria-label]="'mx.cell.maxPoints' | dcTranslate"
                          />
                        </td>
                        <td>
                          <dc-logic-chip
                            mode="condition"
                            [rule]="c.appliesWhen ?? null"
                            [fields]="fields()"
                            [title]="'mx.cell.appliesWhen' | dcTranslate"
                            [disabled]="!canEdit(v)"
                            (ruleChange)="setC(col, gi, ci, { appliesWhen: $event })"
                          />
                        </td>
                        <td class="sx-actions">
                          @if (c.scoring !== 'direct') {
                            <button
                              matButton="tonal"
                              type="button"
                              (click)="openCriterion(ci)"
                              [attr.data-testid]="'sx-open-criterion-' + c.code"
                            >
                              {{
                                (c.scoring === 'bands' ? 'mx.cell.bands' : 'mx.sec.answers')
                                  | dcTranslate
                              }}
                              <span class="sx-count">{{
                                c.scoring === 'bands' ? c.bands.length : c.options.length
                              }}</span>
                            </button>
                          }
                          @if (canEdit(v)) {
                            <button
                              matIconButton
                              type="button"
                              [disabled]="first"
                              (click)="edit(col, (x) => moveCriterion(x, gi, ci, -1))"
                              [attr.aria-label]="'mx.sec.up' | dcTranslate"
                            >
                              <mat-icon fontSet="material-symbols-outlined">arrow_upward</mat-icon>
                            </button>
                            <button
                              matIconButton
                              type="button"
                              [disabled]="last"
                              (click)="edit(col, (x) => moveCriterion(x, gi, ci, 1))"
                              [attr.aria-label]="'mx.sec.down' | dcTranslate"
                            >
                              <mat-icon fontSet="material-symbols-outlined"
                                >arrow_downward</mat-icon
                              >
                            </button>
                            <button
                              matIconButton
                              type="button"
                              (click)="dropCriterion(col, gi, ci)"
                              [attr.aria-label]="'mx.cell.removeCriterion' | dcTranslate"
                            >
                              <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                            </button>
                          }
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </section>

              @if (criterion() !== null && g.criteria[criterion()!]; as c) {
                @let ci = criterion()!;
                @if (c.scoring === 'bands') {
                  <!-- Bands of the criterion -->
                  <section class="sx-panel" data-testid="sx-bands">
                    <header class="sx-panel__head">
                      <span class="sx-path"
                        >{{ columnName(col) }} › {{ { ar: g.nameAr, en: g.nameEn } | dcLocalize }} ›
                        {{ inputName(c.input) }} ›</span
                      >
                      <h3>
                        {{ 'mx.cell.bands' | dcTranslate }}
                        <span class="sx-count">{{ c.bands.length }}</span>
                      </h3>
                      @if (canEdit(v)) {
                        <button
                          matButton
                          type="button"
                          (click)="edit(col, (x) => addBand(x, gi, ci, sizeFilter()))"
                          data-testid="sx-add-band"
                        >
                          <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
                          {{ 'mx.cell.addBand' | dcTranslate }}
                        </button>
                      }
                    </header>
                    @if (sizes().length) {
                      <div class="sx-sizes">
                        <span>{{ 'mx.cell.size' | dcTranslate }}:</span>
                        @for (s of sizes(); track s.code) {
                          <button
                            type="button"
                            class="sx-size"
                            [class.sx-size--on]="sizeFilter() === s.code"
                            (click)="sizeFilter.set(s.code)"
                          >
                            {{ { ar: s.nameAr, en: s.nameEn } | dcLocalize }}
                            <span class="sx-count">{{ bandsOf(c, s.code).length }}</span>
                          </button>
                        }
                        <button
                          type="button"
                          class="sx-size"
                          [class.sx-size--on]="sizeFilter() === null"
                          (click)="sizeFilter.set(null)"
                        >
                          {{ 'mx.cell.allSizes' | dcTranslate }}
                          <span class="sx-count">{{ bandsOf(c, null).length }}</span>
                        </button>
                        @if (canEdit(v) && sizeFilter()) {
                          <button matButton type="button" (click)="copyToSizes(col, gi, ci)">
                            <mat-icon fontSet="material-symbols-outlined">content_copy</mat-icon>
                            {{ 'mx.sec.copyToSizes' | dcTranslate }}
                          </button>
                        }
                      </div>
                    }
                    <table class="dc-table sx-table">
                      <thead>
                        <tr>
                          <th>{{ 'mx.cell.from' | dcTranslate }}</th>
                          <th>≥</th>
                          <th>{{ 'mx.cell.to' | dcTranslate }}</th>
                          <th>≤</th>
                          <th>
                            {{
                              (c.output === 'points' ? 'mx.output.points' : 'mx.output.grade')
                                | dcTranslate
                            }}
                          </th>
                          <th>{{ 'mx.cell.label' | dcTranslate }}</th>
                          <th></th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (b of c.bands; track $index; let bi = $index) {
                          @if (!sizes().length || (b.size ?? null) === sizeFilter()) {
                            <tr>
                              <td>
                                <input
                                  class="sx-cell sx-cell--num"
                                  type="number"
                                  [ngModel]="b.min ?? null"
                                  (ngModelChange)="setB(col, gi, ci, bi, { min: num($event) })"
                                  [disabled]="!canEdit(v)"
                                  [attr.aria-label]="'mx.cell.from' | dcTranslate"
                                />
                              </td>
                              <td>
                                <mat-checkbox
                                  [ngModel]="b.minInclusive"
                                  (ngModelChange)="setB(col, gi, ci, bi, { minInclusive: $event })"
                                  [disabled]="!canEdit(v)"
                                  aria-label="≥"
                                />
                              </td>
                              <td>
                                <input
                                  class="sx-cell sx-cell--num"
                                  type="number"
                                  [ngModel]="b.max ?? null"
                                  (ngModelChange)="setB(col, gi, ci, bi, { max: num($event) })"
                                  [disabled]="!canEdit(v)"
                                  [attr.aria-label]="'mx.cell.to' | dcTranslate"
                                />
                              </td>
                              <td>
                                <mat-checkbox
                                  [ngModel]="b.maxInclusive"
                                  (ngModelChange)="setB(col, gi, ci, bi, { maxInclusive: $event })"
                                  [disabled]="!canEdit(v)"
                                  aria-label="≤"
                                />
                              </td>
                              <td>
                                @if (c.output === 'points') {
                                  <input
                                    class="sx-cell sx-cell--num"
                                    type="number"
                                    [ngModel]="b.points ?? null"
                                    (ngModelChange)="setB(col, gi, ci, bi, { points: num($event) })"
                                    [disabled]="!canEdit(v)"
                                    [attr.aria-label]="'mx.output.points' | dcTranslate"
                                  />
                                } @else {
                                  <mat-select
                                    class="dc-compact-select sx-cell"
                                    [ngModel]="b.grade ?? null"
                                    (ngModelChange)="setB(col, gi, ci, bi, { grade: $event })"
                                    [disabled]="!canEdit(v)"
                                    [aria-label]="'mx.output.grade' | dcTranslate"
                                  >
                                    <mat-option [value]="null">—</mat-option>
                                    @for (gr of m().grades; track gr.rank) {
                                      <mat-option [value]="gr.rank">{{
                                        { ar: gr.nameAr, en: gr.nameEn } | dcLocalize
                                      }}</mat-option>
                                    }
                                  </mat-select>
                                }
                              </td>
                              <td>
                                <input
                                  class="sx-cell"
                                  [ngModel]="b.label ?? ''"
                                  (ngModelChange)="setB(col, gi, ci, bi, { label: $event || null })"
                                  [disabled]="!canEdit(v)"
                                  [attr.aria-label]="'mx.cell.label' | dcTranslate"
                                />
                              </td>
                              <td class="sx-actions">
                                @if (canEdit(v)) {
                                  <button
                                    matIconButton
                                    type="button"
                                    (click)="edit(col, (x) => removeBand(x, gi, ci, bi))"
                                    [attr.aria-label]="'config.delete' | dcTranslate"
                                  >
                                    <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                                  </button>
                                }
                              </td>
                            </tr>
                          }
                        }
                      </tbody>
                    </table>
                  </section>
                } @else if (c.scoring === 'options') {
                  <!-- Answers of the criterion -->
                  <section class="sx-panel" data-testid="sx-answers">
                    <header class="sx-panel__head">
                      <span class="sx-path"
                        >{{ columnName(col) }} › {{ { ar: g.nameAr, en: g.nameEn } | dcLocalize }} ›
                        {{ inputName(c.input) }} ›</span
                      >
                      <h3>{{ 'mx.sec.answers' | dcTranslate }}</h3>
                      @if (bankWeighted(c)) {
                        <mat-checkbox
                          [ngModel]="c.fromBank === true"
                          (ngModelChange)="followBank(col, gi, ci, c, $event)"
                          [disabled]="!canEdit(v)"
                          data-testid="sx-from-bank"
                          >{{ 'mx.cell.fromBank' | dcTranslate }}</mat-checkbox
                        >
                      }
                    </header>
                    @if (!answers(c).length) {
                      <p class="dc-muted">{{ 'mx.cell.noOptions' | dcTranslate }}</p>
                    }
                    <table class="dc-table sx-table">
                      <thead>
                        <tr>
                          <th>{{ 'mx.cell.answer' | dcTranslate }}</th>
                          <th>
                            {{
                              (c.output === 'points' ? 'mx.output.points' : 'mx.output.grade')
                                | dcTranslate
                            }}
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (a of answers(c); track a.value) {
                          <tr>
                            <td>
                              {{ a.label | dcLocalize }}
                              <span dir="ltr" class="sx-code">{{ a.value }}</span>
                            </td>
                            <td>
                              @if (c.output === 'points') {
                                <input
                                  class="sx-cell sx-cell--num"
                                  type="number"
                                  [ngModel]="a.points"
                                  (ngModelChange)="
                                    setA(col, gi, ci, a.value, { points: num($event) })
                                  "
                                  [disabled]="!canEdit(v) || c.fromBank === true"
                                  [attr.aria-label]="'mx.output.points' | dcTranslate"
                                />
                              } @else {
                                <mat-select
                                  class="dc-compact-select sx-cell"
                                  [ngModel]="a.grade"
                                  (ngModelChange)="setA(col, gi, ci, a.value, { grade: $event })"
                                  [disabled]="!canEdit(v) || c.fromBank === true"
                                  [aria-label]="'mx.output.grade' | dcTranslate"
                                >
                                  <mat-option [value]="null">—</mat-option>
                                  @for (gr of m().grades; track gr.rank) {
                                    <mat-option [value]="gr.rank">{{
                                      { ar: gr.nameAr, en: gr.nameEn } | dcLocalize
                                    }}</mat-option>
                                  }
                                </mat-select>
                              }
                            </td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </section>
                }
              }
            }
          }
        }
      } @else {
        <!-- The linked service's application form -->
        <div class="sx-note" [class.sx-note--shared]="!!form()" data-testid="sx-form-link">
          <mat-icon fontSet="material-symbols-outlined">dynamic_form</mat-icon>
          @if (form(); as f) {
            <span>
              {{ 'mx.sec.formOf' | dcTranslate }}
              <strong>{{ f.serviceName | dcLocalize }}</strong>
              <span dir="ltr" class="sx-code">{{ f.service }}/{{ f.version }}</span>
              — {{ 'mx.sec.formSaved' | dcTranslate }}
            </span>
            <a
              matButton
              [routerLink]="['/services/builder']"
              [queryParams]="{ service: f.service }"
              target="_blank"
            >
              <mat-icon fontSet="material-symbols-outlined">open_in_new</mat-icon>
              {{ 'mx.sec.openBuilder' | dcTranslate }}
            </a>
          } @else {
            <span>{{ 'mx.sec.noForm' | dcTranslate }}</span>
          }
        </div>
        <div class="sx-sizes">
          <span>{{ 'mx.sec.tracks' | dcTranslate }}:</span>
          <button
            type="button"
            class="sx-size"
            [class.sx-size--on]="trackFilter() === null"
            (click)="trackFilter.set(null)"
          >
            {{ 'mx.sec.allTracks' | dcTranslate }}
          </button>
          @for (c of m().columns; track c.code) {
            <button
              type="button"
              class="sx-size"
              [class.sx-size--on]="trackFilter() === c.code"
              (click)="trackFilter.set(c.code)"
              [attr.data-testid]="'sx-filter-' + c.code"
            >
              {{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}
            </button>
          }
        </div>

        @if (view() === 'questions') {
          <section class="sx-panel" data-testid="sx-questions">
            <header class="sx-panel__head">
              <h3>
                {{ 'mx.sec.view.questions' | dcTranslate }}
                <span class="sx-count">{{ questionRows().length }}</span>
              </h3>
              <span class="dc-muted">{{ 'mx.sec.questionsHint' | dcTranslate }}</span>
              @if (canEditForm() && !draft()) {
                <button
                  matButton
                  type="button"
                  (click)="startQuestion()"
                  data-testid="sx-new-question"
                >
                  <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
                  {{ 'mx.sec.newQuestion' | dcTranslate }}
                </button>
              }
            </header>
            @if (draft(); as d) {
              <div class="sx-new" data-testid="sx-question-form">
                <input
                  class="sx-cell sx-cell--short"
                  dir="ltr"
                  [(ngModel)]="d.code"
                  [placeholder]="'mx.code' | dcTranslate"
                  [attr.aria-label]="'mx.code' | dcTranslate"
                  data-testid="sx-q-code"
                />
                <input
                  class="sx-cell"
                  [(ngModel)]="d.nameAr"
                  [placeholder]="'config.nameAr' | dcTranslate"
                  [attr.aria-label]="'config.nameAr' | dcTranslate"
                  data-testid="sx-q-name-ar"
                />
                <input
                  class="sx-cell"
                  dir="ltr"
                  [(ngModel)]="d.nameEn"
                  [placeholder]="'config.nameEn' | dcTranslate"
                  [attr.aria-label]="'config.nameEn' | dcTranslate"
                  data-testid="sx-q-name-en"
                />
                <mat-select
                  class="dc-compact-select sx-cell"
                  [(ngModel)]="d.type"
                  [aria-label]="'mx.sec.answerType' | dcTranslate"
                  data-testid="sx-q-type"
                >
                  @for (t of questionTypes; track t) {
                    <mat-option [value]="t">{{ 'mx.qtype.' + t | dcTranslate }}</mat-option>
                  }
                </mat-select>
                <label class="sx-field">
                  <span>{{ 'mx.sec.inStep' | dcTranslate }}</span>
                  <mat-select
                    class="dc-compact-select sx-cell"
                    [(ngModel)]="d.step"
                    data-testid="sx-q-step"
                  >
                    @for (st of formSteps(); track st.code) {
                      <mat-option [value]="st.code">{{
                        { ar: st.titleAr, en: st.titleEn } | dcLocalize
                      }}</mat-option>
                    }
                  </mat-select>
                </label>
                <label class="sx-field">
                  <span>{{ 'mx.sec.scoreIn' | dcTranslate }}</span>
                  <mat-select
                    class="dc-compact-select sx-cell sx-cell--wide"
                    [(ngModel)]="d.scoreIn"
                    data-testid="sx-q-score-in"
                  >
                    <mat-option [value]="null">{{ 'mx.sec.notScored' | dcTranslate }}</mat-option>
                    @for (g of scoringGroups(); track g.key) {
                      <mat-option [value]="g.key">{{ g.label }}</mat-option>
                    }
                  </mat-select>
                </label>
                @if (d.type === 'SELECT') {
                  <label class="sx-field sx-field--wide">
                    <span>{{ 'mx.sec.optionsHint' | dcTranslate }}</span>
                    <textarea
                      class="sx-cell"
                      rows="3"
                      [(ngModel)]="d.options"
                      dir="auto"
                      data-testid="sx-q-options"
                    ></textarea>
                  </label>
                }
                @if (draftError(); as e) {
                  <span class="sx-error" role="alert">{{ e | dcTranslate }}</span>
                }
                <span class="sx-new__actions">
                  <button
                    matButton="filled"
                    type="button"
                    (click)="addQuestion()"
                    data-testid="sx-q-add"
                  >
                    {{ 'mx.sec.addQuestion' | dcTranslate }}
                  </button>
                  <button matButton type="button" (click)="draft.set(null)">
                    {{ 'mx.sec.cancel' | dcTranslate }}
                  </button>
                </span>
              </div>
            }
            <table class="dc-table sx-table">
              <thead>
                <tr>
                  <th>{{ 'mx.sec.question' | dcTranslate }}</th>
                  <th>{{ 'mx.sec.answerType' | dcTranslate }}</th>
                  <th>{{ 'mx.sec.readBy' | dcTranslate }}</th>
                  <th>{{ 'mx.sec.inStep' | dcTranslate }}</th>
                  <th>{{ 'mx.sec.shownForSector' | dcTranslate }}</th>
                  <th>{{ 'mx.sec.required' | dcTranslate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (q of questionRows(); track q.code) {
                  <tr [attr.data-testid]="'sx-q-' + q.code">
                    <td>
                      {{ q.name }}
                      @if (q.isNew) {
                        <span class="sx-badge">{{ 'mx.sec.new' | dcTranslate }}</span>
                      }
                      <div dir="ltr" class="sx-code">{{ q.code }}</div>
                    </td>
                    <td>{{ q.type ? ('mx.qtype.' + q.type | dcTranslate) : '—' }}</td>
                    <td>
                      <span class="sx-tracks">
                        @for (t of q.tracks; track t) {
                          <span class="sx-chip">{{ columnName(t) }}</span>
                        } @empty {
                          <span class="dc-muted">{{ 'mx.sec.notScored' | dcTranslate }}</span>
                        }
                      </span>
                    </td>
                    <td>
                      @if (q.step) {
                        {{
                          q.step.titleAr || q.step.titleEn
                            ? ({ ar: q.step.titleAr, en: q.step.titleEn } | dcLocalize)
                            : q.step.code
                        }}
                      } @else if (canEditForm()) {
                        <mat-select
                          class="dc-compact-select sx-cell"
                          [placeholder]="'mx.sec.addToStep' | dcTranslate"
                          [ngModel]="null"
                          (ngModelChange)="place(q.code, $event)"
                          [aria-label]="'mx.sec.addToStep' | dcTranslate"
                          [attr.data-testid]="'sx-place-' + q.code"
                        >
                          @for (st of formSteps(); track st.code) {
                            <mat-option [value]="st.code">{{
                              { ar: st.titleAr, en: st.titleEn } | dcLocalize
                            }}</mat-option>
                          }
                        </mat-select>
                      } @else {
                        <span class="sx-warn">{{ 'mx.sec.notInForm' | dcTranslate }}</span>
                      }
                    </td>
                    <td>
                      <mat-checkbox
                        [ngModel]="q.shown"
                        (ngModelChange)="show(q.code, $event)"
                        [disabled]="
                          !canEditForm() ||
                          !q.step ||
                          q.custom ||
                          !hasSectorsQuestion() ||
                          isSectorsQuestion(q.code)
                        "
                        [matTooltip]="q.custom ? ('mx.sec.customRule' | dcTranslate) : ''"
                        [aria-label]="'mx.sec.shownForSector' | dcTranslate"
                        [attr.data-testid]="'sx-shown-' + q.code"
                      />
                    </td>
                    <td>
                      <mat-checkbox
                        [ngModel]="q.required"
                        (ngModelChange)="require(q.code, $event)"
                        [disabled]="!canEditForm() || !q.step"
                        [aria-label]="'mx.sec.required' | dcTranslate"
                      />
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </section>
        } @else {
          <section class="sx-panel" data-testid="sx-form">
            <header class="sx-panel__head">
              <h3>{{ 'mx.sec.view.form' | dcTranslate }}</h3>
              <span class="dc-muted">{{ 'mx.sec.formHint' | dcTranslate }}</span>
            </header>
            @if (!form()) {
              <p class="dc-muted">{{ 'mx.sec.noForm' | dcTranslate }}</p>
            }
            @for (st of sectorForm(); track st.index; let n = $index) {
              @if (st.step.type !== 'FORM') {
                <div class="sx-step sx-step--other">
                  <mat-icon fontSet="material-symbols-outlined">{{
                    stepIcons[st.step.type] ?? 'circle'
                  }}</mat-icon>
                  {{ { ar: st.step.titleAr, en: st.step.titleEn } | dcLocalize }}
                  <span class="dc-muted">· {{ 'mx.stepType.' + st.step.type | dcTranslate }}</span>
                </div>
              } @else if (st.questions.length) {
                <div class="sx-step" [attr.data-testid]="'sx-step-' + st.step.code">
                  <h4>
                    {{ { ar: st.step.titleAr, en: st.step.titleEn } | dcLocalize }}
                    <span class="sx-count">{{ st.questions.length }}</span>
                  </h4>
                  <ul class="sx-qlist">
                    @for (
                      q of st.questions;
                      track q.section + '/' + q.index;
                      let first = $first;
                      let last = $last
                    ) {
                      <li
                        [class.sx-q--hit]="isHit(q.use.code)"
                        [class.sx-q--dim]="trackFilter() !== null && !isHit(q.use.code)"
                        [attr.data-testid]="'sx-fq-' + q.use.code"
                      >
                        <span class="sx-q__name">
                          {{ questionName(q.use.code) }}
                          @if (q.use.required) {
                            <b class="sx-req">*</b>
                          }
                          <span dir="ltr" class="sx-code">{{ q.use.code }}</span>
                        </span>
                        <span class="sx-tracks">
                          @for (t of tracksOf(q.use.code); track t) {
                            <span class="sx-chip">{{ columnName(t) }}</span>
                          }
                        </span>
                        <span class="sx-badge" [class.sx-badge--muted]="!q.sectorOnly">{{
                          (q.custom
                            ? 'mx.sec.conditional'
                            : q.sectorOnly
                              ? 'mx.sec.thisSector'
                              : 'mx.sec.everySector'
                          ) | dcTranslate
                        }}</span>
                        @if (canEditForm()) {
                          <span class="sx-actions">
                            <button
                              matIconButton
                              type="button"
                              [disabled]="first"
                              (click)="move(q, -1)"
                              [attr.aria-label]="'mx.sec.up' | dcTranslate"
                            >
                              <mat-icon fontSet="material-symbols-outlined">arrow_upward</mat-icon>
                            </button>
                            <button
                              matIconButton
                              type="button"
                              [disabled]="last"
                              (click)="move(q, 1)"
                              [attr.aria-label]="'mx.sec.down' | dcTranslate"
                            >
                              <mat-icon fontSet="material-symbols-outlined"
                                >arrow_downward</mat-icon
                              >
                            </button>
                            @if (
                              !q.custom && hasSectorsQuestion() && !isSectorsQuestion(q.use.code)
                            ) {
                              <button
                                matIconButton
                                type="button"
                                (click)="show(q.use.code, false)"
                                [matTooltip]="'mx.sec.hideForSector' | dcTranslate"
                                [attr.aria-label]="'mx.sec.hideForSector' | dcTranslate"
                                [attr.data-testid]="'sx-hide-' + q.use.code"
                              >
                                <mat-icon fontSet="material-symbols-outlined"
                                  >visibility_off</mat-icon
                                >
                              </button>
                            }
                          </span>
                        }
                      </li>
                    }
                  </ul>
                </div>
              }
            }
          </section>
        }
      }
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .sx-panel {
      margin-bottom: 1rem;
      padding: 0.75rem 1rem 1rem;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 12px;
      background: var(--dga-bg, #fff);
    }
    .sx-panel__head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem 0.75rem;
      margin-bottom: 0.5rem;
    }
    .sx-panel__head h3 {
      margin: 0;
      font-size: 16px;
    }
    .sx-panel__head > button,
    .sx-panel__head > mat-checkbox {
      margin-inline-start: auto;
    }
    .sx-path {
      flex-basis: 100%;
      color: var(--dga-gray-500, #6c737f);
      font-size: 13px;
    }
    .sx-count {
      display: inline-block;
      min-width: 1.5rem;
      padding: 0 6px;
      border-radius: 999px;
      background: var(--dga-gray-100, #f3f4f6);
      font-size: 12px;
      text-align: center;
    }
    .sx-panel {
      overflow-x: auto;
    }
    .sx-table {
      width: 100%;
    }
    /* Icons drawn for right-to-left pages point the other way in English. */
    :host-context([dir='ltr']) .sx-flip {
      transform: scaleX(-1);
    }
    .sx-table td {
      vertical-align: middle;
    }
    .sx-row--on {
      background: var(--dynamic-cube-gold-25, #fdfaf3);
    }
    .sx-cell {
      box-sizing: border-box;
      width: 100%;
      min-width: 6rem;
      padding: 5px 8px;
      border: 1px solid var(--dga-gray-300, #d2d6db);
      border-radius: 6px;
      background: #fff;
      font: inherit;
    }
    .sx-cell:disabled {
      background: transparent;
      border-color: transparent;
    }
    .sx-cell--short {
      width: 8rem;
    }
    .sx-cell--num {
      width: 6rem;
      min-width: 5rem;
    }
    .sx-cell--wide {
      min-width: 16rem;
    }
    .sx-code {
      font-family: ui-monospace, monospace;
      font-size: 12px;
      color: var(--dga-gray-500, #6c737f);
    }
    .sx-actions {
      white-space: nowrap;
      text-align: end;
    }
    .sx-tracks {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
    }
    .sx-chip {
      display: inline-flex;
      align-items: center;
      gap: 2px;
      padding: 1px 8px;
      border-radius: 999px;
      background: var(--dga-success-50, #ecfdf3);
      font-size: 12px;
    }
    .sx-chip--off {
      background: var(--dga-gray-100, #f3f4f6);
      color: var(--dga-gray-500, #6c737f);
    }
    .sx-chip mat-icon,
    .sx-kind mat-icon,
    .sx-note mat-icon,
    .sx-form-link mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
    .sx-kind {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      white-space: nowrap;
    }
    .sx-badge {
      padding: 0 6px;
      border-radius: 999px;
      background: var(--dga-info-50, #eff8ff);
      color: var(--dga-info-700, #175cd3);
      font-size: 12px;
    }
    .sx-crumbs {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 0.75rem;
    }
    .sx-crumbs h2 {
      margin: 0;
      font-size: 20px;
    }
    .sx-note {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
      margin: 0 0 1rem;
      padding: 0.5rem 0.75rem;
      border-radius: 8px;
      background: var(--dga-gray-50, #f9fafb);
      border: 1px solid var(--dga-gray-200, #e5e7eb);
    }
    .sx-note--shared {
      background: var(--dga-info-25, #f5faff);
      border-color: var(--dga-info-200, #b2ddff);
    }
    .sx-form-link {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0.5rem 0 0;
    }
    .sx-input {
      display: flex;
      align-items: center;
      gap: 2px;
    }
    .sx-sizes {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
      margin-bottom: 0.5rem;
    }
    .sx-size {
      padding: 3px 10px;
      border: 1px solid var(--dga-gray-300, #d2d6db);
      border-radius: 999px;
      background: #fff;
      font: inherit;
      cursor: pointer;
    }
    .sx-size--on {
      border-color: var(--dynamic-cube-gold-600, #b8862f);
      background: var(--dynamic-cube-gold-25, #fdfaf3);
    }
    .sx-tabs {
      display: flex;
      gap: 4px;
      margin-bottom: 1rem;
      border-bottom: 1px solid var(--dga-gray-200, #e5e7eb);
    }
    .sx-tab {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 8px 14px;
      border: 0;
      border-bottom: 2px solid transparent;
      background: none;
      font: inherit;
      color: var(--dga-gray-600, #4d5761);
      cursor: pointer;
    }
    .sx-tab--on {
      border-bottom-color: var(--dynamic-cube-gold-600, #b8862f);
      color: inherit;
      font-weight: 600;
    }
    .sx-new {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      gap: 0.5rem 0.75rem;
      margin-bottom: 1rem;
      padding: 0.75rem;
      border: 1px dashed var(--dga-gray-300, #d2d6db);
      border-radius: 8px;
    }
    .sx-new .sx-cell {
      width: auto;
    }
    .sx-field {
      display: flex;
      flex-direction: column;
      gap: 2px;
      font-size: 12px;
      color: var(--dga-gray-600, #4d5761);
    }
    .sx-field--wide {
      flex-basis: 100%;
    }
    .sx-field--wide .sx-cell {
      width: 100%;
    }
    .sx-new__actions {
      display: flex;
      gap: 0.5rem;
      margin-inline-start: auto;
    }
    .sx-error {
      flex-basis: 100%;
      color: var(--dga-error-600, #d92d20);
    }
    .sx-warn {
      color: var(--dga-warning-700, #b54708);
    }
    .sx-step {
      margin-bottom: 0.75rem;
      padding: 0.5rem 0.75rem;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 8px;
    }
    .sx-step h4 {
      margin: 0 0 0.25rem;
      font-size: 15px;
    }
    .sx-step--other {
      display: flex;
      align-items: center;
      gap: 6px;
      background: var(--dga-gray-50, #f9fafb);
      color: var(--dga-gray-600, #4d5761);
    }
    .sx-step--other mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .sx-qlist {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .sx-qlist li {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 4px 10px;
      padding: 4px 6px;
      border-top: 1px solid var(--dga-gray-100, #f3f4f6);
    }
    .sx-q__name {
      flex: 1 1 18rem;
    }
    .sx-q--hit {
      background: var(--dynamic-cube-gold-25, #fdfaf3);
    }
    .sx-q--dim {
      opacity: 0.55;
    }
    .sx-q__name .sx-code,
    .sx-note .sx-code {
      margin-inline-start: 6px;
    }
    .sx-req {
      color: var(--dga-error-600, #d92d20);
    }
    .sx-badge--muted {
      background: var(--dga-gray-100, #f3f4f6);
      color: var(--dga-gray-600, #4d5761);
    }
    .sx-add {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      align-items: center;
      margin-top: 0.75rem;
    }
    .sx-add .sx-cell {
      width: auto;
    }
  `,
})
export class MatrixSectors {
  readonly matrix = input.required<Matrix>();
  readonly references = input.required<MatrixReferences>();
  readonly editable = input(false);
  readonly matrixChange = output<Matrix>();
  /** The application form of the service deciding with the matrix (null when no service does). */
  readonly form = input<FormLink | null>(null);
  readonly formChange = output<FormLink>();
  /** Asks the page to open a part of its Shared setup tab (scales, the inputs…). */
  readonly openShared = output<'data' | 'grades' | 'rules'>();

  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(I18nService);

  protected readonly icons: Record<string, string> = CELL_ICONS;
  protected readonly scorings = ['bands', 'options', 'direct'] as const;
  protected readonly addGroup = addGroup;
  protected readonly moveGroup = moveGroup;
  protected readonly moveCriterion = moveCriterion;
  protected readonly addBand = addBand;
  protected readonly removeBand = removeBand;

  /** The open sector, track, group and criterion (each opens the next panel under it). */
  readonly sector = signal<string | null>(null);
  protected readonly track = signal<string | null>(null);
  protected readonly group = signal<number | null>(null);
  protected readonly criterion = signal<number | null>(null);
  protected readonly sizeFilter = signal<string | null>(null);

  /** What the open sector shows: its criteria, the questions its tracks read, or its application form. */
  protected readonly views = ['criteria', 'questions', 'form'] as const;
  protected readonly view = signal<'criteria' | 'questions' | 'form'>('criteria');
  /** The track whose questions are listed (or marked in the form); null for all. */
  protected readonly trackFilter = signal<string | null>(null);
  protected readonly questionTypes = ['NUMBER', 'SELECT', 'CHECKBOX', 'TEXT', 'DATE'] as const;
  protected readonly stepIcons: Record<string, string> = {
    LEVEL: 'stairs',
    INTEGRATION: 'sync_alt',
    REVIEW: 'fact_check',
    DECLARATION: 'gavel',
    FEES: 'payments',
  };
  /** A question being added: to the bank, the matrix inputs and the form (and, when chosen, a group's criteria). */
  protected readonly draft = signal<QuestionDraft | null>(null);
  protected readonly draftError = signal<string | null>(null);

  protected newCode = '';
  protected newNameAr = '';
  protected newNameEn = '';

  protected readonly m = computed(() => this.matrix());
  protected readonly row = computed(
    () => this.m().rows.find((r) => r.code === this.sector()) ?? null,
  );
  protected readonly sizes = computed(() => this.m().sizes?.categories ?? []);
  protected readonly fields = computed(() =>
    inputFields(this.m(), undefined, this.references().answersOf),
  );

  protected trackView(row: string, column: string): TrackView {
    return trackOf(this.m(), row, column);
  }

  /** A shared track can be edited (the change applies to every linked sector); a copied or empty one cannot. */
  protected canEdit(v: TrackView): boolean {
    return this.editable() && (v.mode === 'own' || v.mode === 'shared');
  }

  protected kindLabel(v: TrackView): string {
    if (v.mode === 'same') return this.i18n.translate('mx.kind.same');
    return this.i18n.translate('mx.kind.' + (v.calculation?.kind ?? 'empty'));
  }

  protected trackHint(v: TrackView): string {
    return v.mode === 'shared'
      ? `${this.kindLabel(v)} · ${this.i18n.translate('mx.kind.shared')}`
      : this.kindLabel(v);
  }

  protected rowName(code: string | null | undefined): string {
    const r = this.m().rows.find((x) => x.code === code);
    return r ? this.i18n.localize({ ar: r.nameAr, en: r.nameEn }) : (code ?? '');
  }

  protected sharedNames(v: TrackView): string {
    return v.sharedWith
      .map((r) => this.rowName(r))
      .join(this.i18n.language() === 'ar' ? '، ' : ', ');
  }

  protected columnName(code: string): string {
    const c = this.m().columns.find((x) => x.code === code);
    return c ? this.i18n.localize({ ar: c.nameAr, en: c.nameEn }) : code;
  }

  protected inputName(code: string): string {
    const i = this.m().inputs.find((x) => x.code === code);
    return i ? this.i18n.localize({ ar: i.nameAr, en: i.nameEn }) || code : code;
  }

  /** How many applicant questions this sector's track reads (they belong in the form step of the track). */
  protected questionsOf(column: string): number {
    const sector = this.sector();
    if (!sector) return 0;
    const m = this.m();
    return cellInputs(
      m,
      m.cells.find((c) => c.row === sector && c.column === column) ?? null,
    ).filter((i) => i.source.kind === 'applicant').length;
  }

  protected sharedLabel(v: TrackView): string {
    return this.i18n.translate('mx.sec.sharedWith').replace('{n}', String(v.sharedWith.length));
  }

  protected questionsLabel(column: string): string {
    return this.i18n
      .translate('mx.sec.questionsOfTrack')
      .replace('{n}', String(this.questionsOf(column)));
  }

  protected bandsOf(c: MatrixCriterion, size: string | null): MatrixBand[] {
    return c.bands.filter((b) => (b.size ?? null) === size);
  }

  protected num(value: unknown): number | null {
    return value === null || value === undefined || value === '' ? null : Number(value);
  }

  // Questions and the application form ---------------------------------------------------------------------------------

  protected readonly canEditForm = computed(() => this.editable() && !!this.form());
  private readonly sectorsQuestionCode = computed(() => sectorsQuestion(this.m()));
  /** Questions can be shown per sector only when the matrix knows the question listing the sectors applied for. */
  protected readonly hasSectorsQuestion = computed(() => !!this.sectorsQuestionCode());
  private readonly value = computed(() => {
    const r = this.row();
    return r ? sectorValue(r) : '';
  });
  private readonly allSectors = computed(() => this.m().rows.map(sectorValue));
  protected readonly formSteps = computed(() =>
    (this.form()?.definition.steps ?? []).filter((s) => s.type === 'FORM'),
  );
  /** The bank questions the open sector's tracks read, by code. */
  private readonly scored = computed(() => {
    const sector = this.sector();
    return sector ? sectorQuestions(this.m(), sector) : [];
  });
  protected readonly sectorForm = computed(() => {
    const f = this.form();
    return f ? formForSector(f.definition, this.sectorsQuestionCode(), this.value()) : [];
  });

  /** The questions of the open sector: those its tracks read, then the others its form shows. */
  protected readonly questionRows = computed(() => {
    const f = this.form();
    const filter = this.trackFilter();
    const codes = [
      ...this.scored().map((q) => q.code),
      ...this.sectorForm()
        .filter((s) => s.step.type === 'FORM')
        .flatMap((s) => s.questions.map((q) => q.use.code)),
    ];
    const seen = new Set<string>();
    return codes
      .filter((c) => !seen.has(c.toLowerCase()) && !!seen.add(c.toLowerCase()))
      .map((code) => {
        const at = f ? placements(f.definition, code) : [];
        const first = at[0];
        const step = first ? f!.definition.steps[first.step] : null;
        return {
          code,
          name: this.questionName(code),
          type: (this.references().question(code)?.['type'] as string | undefined) ?? null,
          tracks: this.tracksOf(code),
          step,
          shown: first
            ? shownFor(first.use.visibleWhen, this.sectorsQuestionCode(), this.value())
            : false,
          custom: first
            ? visibility(first.use.visibleWhen, this.sectorsQuestionCode()).kind === 'custom'
            : false,
          required: first?.use.required ?? false,
          isNew: !!f?.newQuestions.some((q) => q.code === code),
        };
      })
      .filter((q) => !filter || q.tracks.includes(filter));
  });

  /** Groups of the open sector's tracks a new question can be scored in. */
  protected readonly scoringGroups = computed(() => {
    const sector = this.sector();
    if (!sector) return [];
    return this.m().columns.flatMap((c) => {
      const v = trackOf(this.m(), sector, c.code);
      if (!this.canEdit(v) || !v.calculation || !['points', 'worst'].includes(v.calculation.kind))
        return [];
      return v.calculation.groups.map((g, gi) => ({
        key: `${c.code}|${gi}`,
        label: `${this.columnName(c.code)} › ${this.i18n.localize({ ar: g.nameAr, en: g.nameEn }) || g.code}`,
      }));
    });
  });

  protected tracksOf(code: string): string[] {
    return this.scored().find((q) => q.code.toLowerCase() === code.toLowerCase())?.tracks ?? [];
  }

  /** The question listing the sectors applied for is asked of everyone: it is never hidden for one sector. */
  protected isSectorsQuestion(code: string): boolean {
    return code.toLowerCase() === (this.sectorsQuestionCode() ?? '').toLowerCase();
  }

  protected isHit(code: string): boolean {
    const filter = this.trackFilter();
    return !!filter && this.tracksOf(code).includes(filter);
  }

  protected questionName(code: string): string {
    const q = this.references().question(code);
    if (q)
      return this.i18n.localize({ ar: String(q.nameAr ?? ''), en: String(q.nameEn ?? '') }) || code;
    const input = this.m().inputs.find((i) => i.code === code);
    return input ? this.i18n.localize({ ar: input.nameAr, en: input.nameEn }) || code : code;
  }

  /** Opens the questions or the form of a track (from its groups panel). */
  protected goTo(view: 'questions' | 'form', track: string): void {
    this.trackFilter.set(track);
    this.view.set(view);
  }

  private emitForm(change: (def: FormLink['definition']) => FormLink['definition']): void {
    const f = this.form();
    if (f) this.formChange.emit({ ...f, definition: change(f.definition) });
  }

  protected place(code: string, step: string): void {
    if (step)
      this.emitForm((d) => placeQuestion(d, step, code, this.sectorsQuestionCode(), this.value()));
  }

  protected show(code: string, on: boolean): void {
    const question = this.sectorsQuestionCode();
    if (question)
      this.emitForm((d) => showForSector(d, code, question, this.value(), on, this.allSectors()));
  }

  protected require(code: string, required: boolean): void {
    this.emitForm((d) => setRequired(d, code, required));
  }

  protected move(at: Placement, by: -1 | 1): void {
    this.emitForm((d) => moveQuestion(d, at, by));
  }

  protected startQuestion(): void {
    const f = this.form();
    if (!f) return;
    const track = this.trackFilter();
    const step = stepForTrack(
      f.definition,
      track ?? this.m().columns[0]?.code ?? '',
      track
        ? this.scored()
            .filter((q) => q.tracks.includes(track))
            .map((q) => q.code)
        : [],
    );
    const group = this.scoringGroups().find((g) => track && g.key.startsWith(track + '|'));
    this.draftError.set(null);
    this.draft.set({
      code: '',
      nameAr: '',
      nameEn: '',
      type: 'NUMBER',
      options: '',
      step: step ?? '',
      scoreIn: group?.key ?? null,
    });
  }

  /** Adds the new question to the bank (saved with the form), the matrix inputs and the form for the open sector. */
  protected addQuestion(): void {
    const d = this.draft();
    const f = this.form();
    const sector = this.sector();
    if (!d || !f || !sector) return;
    const code = d.code.trim();
    const error = !/^[A-Za-z][A-Za-z0-9_-]*$/.test(code)
      ? 'mx.sec.badCode'
      : this.references().question(code) ||
          this.m().inputs.some((i) => i.code.toLowerCase() === code.toLowerCase())
        ? 'mx.sec.codeUsed'
        : !d.nameAr.trim() && !d.nameEn.trim()
          ? 'mx.sec.nameNeeded'
          : !d.step
            ? 'mx.sec.stepNeeded'
            : d.type === 'SELECT' && !parseOptions(d.options).length
              ? 'mx.sec.optionsNeeded'
              : null;
    this.draftError.set(error);
    if (error) return;
    const question = {
      code,
      nameAr: d.nameAr.trim() || d.nameEn.trim(),
      nameEn: d.nameEn.trim() || d.nameAr.trim(),
      type: d.type,
      options: d.type === 'SELECT' ? parseOptions(d.options) : [],
    };
    let m: Matrix = { ...this.m(), inputs: [...this.m().inputs, applicantInput(question)] };
    if (d.scoreIn) {
      const [column, group] = d.scoreIn.split('|');
      const gi = Number(group);
      const byAnswer = d.type === 'SELECT' || d.type === 'CHECKBOX';
      m = editTrack(m, sector, column, (c) => {
        const added = addCriterion(c, gi, code);
        const ci = added.groups[gi].criteria.length - 1;
        return setCriterion(added, gi, ci, {
          nameAr: question.nameAr,
          nameEn: question.nameEn,
          ...(byAnswer ? { scoring: 'options' as const } : {}),
        });
      });
    }
    this.emit(m);
    this.formChange.emit({
      ...f,
      newQuestions: [...f.newQuestions, question],
      definition: placeQuestion(
        f.definition,
        d.step,
        code,
        this.sectorsQuestionCode(),
        this.value(),
      ),
    });
    this.draft.set(null);
  }

  // Navigation -----------------------------------------------------------------------------------------------------------

  protected openSector(code: string): void {
    this.sector.set(code);
    this.trackFilter.set(null);
    this.draft.set(null);
    this.track.set(null);
    this.group.set(null);
    this.criterion.set(null);
  }

  protected closeSector(): void {
    this.sector.set(null);
  }

  protected openTrack(code: string): void {
    this.track.set(this.track() === code ? null : code);
    this.group.set(null);
    this.criterion.set(null);
  }

  protected openGroup(gi: number): void {
    this.group.set(this.group() === gi ? null : gi);
    this.criterion.set(null);
  }

  protected openCriterion(ci: number): void {
    this.criterion.set(this.criterion() === ci ? null : ci);
    this.sizeFilter.set(this.sizes()[0]?.code ?? null);
  }

  // Changes --------------------------------------------------------------------------------------------------------------

  private emit(m: Matrix): void {
    this.matrixChange.emit(m);
  }

  protected setRow(code: string, change: Partial<MatrixRow>): void {
    const m = this.m();
    this.emit({ ...m, rows: m.rows.map((r) => (r.code === code ? { ...r, ...change } : r)) });
  }

  protected setColumn(code: string, includeInFinal: boolean): void {
    const m = this.m();
    this.emit({
      ...m,
      columns: m.columns.map((c) => (c.code === code ? { ...c, includeInFinal } : c)),
    });
  }

  protected addSector(): void {
    const m = this.m();
    const code =
      this.newCode.trim() ||
      freeCode(
        m.rows.map((r) => r.code),
        'sector',
      );
    if (m.rows.some((r) => r.code.toLowerCase() === code.toLowerCase())) return;
    this.emit({
      ...m,
      rows: [
        ...m.rows,
        {
          code,
          nameAr: this.newNameAr.trim() || code,
          nameEn: this.newNameEn.trim() || code,
          active: true,
          final: 'worst',
        },
      ],
    });
    this.newCode = this.newNameAr = this.newNameEn = '';
  }

  protected dropSector(code: string): void {
    if (confirm(this.i18n.translate('mx.row.confirmRemove'))) this.emit(removeRow(this.m(), code));
  }

  protected edit(column: string, change: (c: MatrixCell) => MatrixCell): void {
    const sector = this.sector();
    if (sector) this.emit(editTrack(this.m(), sector, column, change));
  }

  protected own(column: string): void {
    const sector = this.sector();
    if (sector) this.emit(ownTrack(this.m(), sector, column));
  }

  protected start(column: string, kind: 'points' | 'worst'): void {
    const sector = this.sector();
    if (sector) this.emit(startTrack(this.m(), sector, column, kind));
  }

  protected setG(column: string, gi: number, change: Partial<MatrixGroup>): void {
    this.edit(column, (c) => setGroup(c, gi, change));
  }

  protected dropGroup(column: string, gi: number): void {
    if (!confirm(this.i18n.translate('mx.sec.confirmRemove'))) return;
    this.edit(column, (c) => removeGroup(c, gi));
    this.group.set(null);
  }

  protected setC(column: string, gi: number, ci: number, change: Partial<MatrixCriterion>): void {
    this.edit(column, (c) => setCriterion(c, gi, ci, change));
  }

  protected newCriterionIn(column: string, gi: number): void {
    const first = this.m().inputs[0]?.code ?? '';
    this.edit(column, (c) => addCriterion(c, gi, first));
  }

  protected dropCriterion(column: string, gi: number, ci: number): void {
    if (!confirm(this.i18n.translate('mx.sec.confirmRemove'))) return;
    this.edit(column, (c) => removeCriterion(c, gi, ci));
    this.criterion.set(null);
  }

  protected setB(
    column: string,
    gi: number,
    ci: number,
    bi: number,
    change: Partial<MatrixBand>,
  ): void {
    this.edit(column, (c) => setBand(c, gi, ci, bi, change));
  }

  protected copyToSizes(column: string, gi: number, ci: number): void {
    const from = this.sizeFilter();
    if (!from || !confirm(this.i18n.translate('mx.sec.confirmCopySizes'))) return;
    this.edit(column, (c) =>
      copyBandsToSizes(
        c,
        gi,
        ci,
        from,
        this.sizes().map((s) => s.code),
      ),
    );
  }

  protected setA(
    column: string,
    gi: number,
    ci: number,
    value: string,
    change: { points?: number | null; grade?: number | null },
  ): void {
    this.edit(column, (c) => setAnswer(c, gi, ci, value, change));
  }

  // Answers of a criterion scored by answer -------------------------------------------------------------------------------

  private inputOf(code: string): MatrixInput | undefined {
    return this.m().inputs.find((i) => i.code === code);
  }

  /** The answers to score: the bank question's (or the matrix option set's), then answers scored but no longer offered. */
  protected answers(c: MatrixCriterion) {
    const input = this.inputOf(c.input);
    const bank = input ? this.references().answersOf(input) : null;
    const set = this.m().optionSets.find((s) => s.code === input?.optionSet);
    const offered = bank
      ? bank.map((o) => ({ value: o.value, label: { ar: o.labelAr, en: o.labelEn } }))
      : (set?.items ?? []).map((o) => ({ value: o.code, label: { ar: o.labelAr, en: o.labelEn } }));
    const known = new Set(offered.map((o) => o.value.toLowerCase()));
    const rest = c.options
      .filter((o) => !known.has(o.value.toLowerCase()))
      .map((o) => ({ value: o.value, label: { ar: o.value, en: o.value } }));
    return [...offered, ...rest].map((a) => {
      const score = c.options.find((o) => o.value.toLowerCase() === a.value.toLowerCase());
      return { ...a, points: score?.points ?? null, grade: score?.grade ?? null };
    });
  }

  protected bankWeighted(c: MatrixCriterion) {
    const input = this.inputOf(c.input);
    const answers = input ? this.references().answersOf(input) : null;
    return hasWeights(answers) ? answers : null;
  }

  protected followBank(
    column: string,
    gi: number,
    ci: number,
    c: MatrixCriterion,
    on: boolean,
  ): void {
    const answers = this.bankWeighted(c);
    const output = answers?.some((a) => a.points !== null && a.points !== undefined)
      ? 'points'
      : 'grade';
    this.setC(
      column,
      gi,
      ci,
      on && answers
        ? { fromBank: true, output, options: bankOptions(c, answers) }
        : { fromBank: false },
    );
  }

  /** Choosing an input whose bank question weighs its answers scores the criterion by answer with those weights. */
  protected chooseInput(column: string, gi: number, ci: number, code: string): void {
    const input = this.inputOf(code);
    const answers = input ? this.references().answersOf(input) : null;
    if (hasWeights(answers)) {
      const output = answers!.some((a) => a.points !== null && a.points !== undefined)
        ? 'points'
        : 'grade';
      this.edit(column, (c) => {
        const criterion = { ...c.groups[gi].criteria[ci], input: code };
        return setCriterion(c, gi, ci, {
          input: code,
          scoring: 'options',
          fromBank: true,
          output,
          options: bankOptions(criterion, answers!),
        });
      });
    } else {
      this.setC(column, gi, ci, { input: code });
    }
  }

  // Dialogs --------------------------------------------------------------------------------------------------------------

  /** Where an input's value comes from (question, task field, operation…), set up in place. */
  protected async editInput(code: string): Promise<void> {
    const m = this.m();
    const current = this.inputOf(code);
    if (!current) return;
    const result = await firstValueFrom(
      this.dialog
        .open<
          MatrixInputEditor,
          MatrixInputEditorData,
          { input: MatrixInput | null; removed?: boolean }
        >(MatrixInputEditor, {
          data: { matrix: m, input: current, isNew: false, references: this.references() },
          width: '60rem',
          maxWidth: '96vw',
        })
        .afterClosed(),
    );
    if (!result || result.removed || !result.input) return;
    this.emit({ ...m, inputs: m.inputs.map((i) => (i.code === code ? result.input! : i)) });
  }

  /** The whole calculation of the sector's track in the full cell editor (formulas, "same as", every option). */
  protected async fullEditor(column: string): Promise<void> {
    const sector = this.sector();
    if (!sector) return;
    const m = this.m();
    const cell = m.cells.find((c) => c.row === sector && c.column === column) ?? null;
    const result = await firstValueFrom(
      this.dialog
        .open<MatrixCellEditor, MatrixCellEditorData, MatrixCellEditorResult>(MatrixCellEditor, {
          data: { matrix: m, row: sector, column, cell, references: this.references() },
          width: '72rem',
          maxWidth: '96vw',
        })
        .afterClosed(),
    );
    if (!result) return;
    const withInputs = { ...m, inputs: result.inputs };
    this.emit(
      cell?.kind === 'shared' && cell.shared && !result.detach
        ? withShared(withInputs, cell.shared, result.cell)
        : withCell(withInputs, result.cell),
    );
  }
}

/** The fields of a question being added on the sector page. */
interface QuestionDraft {
  code: string;
  nameAr: string;
  nameEn: string;
  type: 'NUMBER' | 'SELECT' | 'CHECKBOX' | 'TEXT' | 'DATE';
  /** SELECT answers, one per line: value | Arabic | English. */
  options: string;
  step: string;
  /** "track|group index" of the group the question is scored in, or null. */
  scoreIn: string | null;
}
