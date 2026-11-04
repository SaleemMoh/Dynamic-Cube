import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  AppsControlApi,
  ConfigRow,
  DecisionsApi,
  I18nService,
  LocalizePipe,
  TranslatePipe,
  problemMessages,
  SearchableSelectModule,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { DraftsService } from '../../apps-control/drafts.service';
import { PrivilegesService } from '../../apps-control/privileges.service';
import { SubmitButton } from '../../apps-control/submit-button';
import { MatrixConnections } from './matrix-connections';
import { MatrixGrid } from './matrix-grid';
import { MatrixInputEditor, MatrixInputEditorData } from './matrix-input-editor';
import {
  Matrix,
  MatrixInput,
  SOURCE_ICONS,
  cellInputs,
  emptyMatrix,
  freeCode,
  readMatrix,
  shareIdentical,
  syncFromBank,
} from './matrix-model';
import { FormLink, questionPayload } from './matrix-form';
import { MatrixReferences } from './matrix-references';
import { MatrixSectors } from './matrix-sectors';
import { MatrixSettings } from './matrix-settings';
import { MatrixSimulator } from './matrix-simulator';
import { MATRIX_TRANSLATIONS } from './matrix-translations';

/**
 * The scoring matrix designer (engine MATRIX): sectors are rows, tracks are columns and each cell holds that sector's
 * calculation for the track. A matrix is a rule set version: it starts from the latest version (or a guide file
 * imported here), is tried in the simulator and saved as the next version through a change set.
 */
@Component({
  selector: 'dc-matrix-page',
  imports: [
    FormsModule,
    LocalizePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    SearchableSelectModule,
    MatTabsModule,
    MatTooltipModule,
    MatrixConnections,
    MatrixGrid,
    MatrixSectors,
    MatrixSettings,
    MatrixSimulator,
    RouterLink,
    SubmitButton,
    TranslatePipe,
  ],
  providers: [MatrixReferences],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'mx.title' | dcTranslate }}</h1>
    </header>
    <p class="dc-muted">{{ 'mx.hint' | dcTranslate }}</p>

    <div class="dc-toolbar mx-page__bar">
      <mat-form-field subscriptSizing="dynamic" class="mx-page__pick">
        <mat-label>{{ 'decisions.ruleSet' | dcTranslate }}</mat-label>
        <mat-select
          [ngModel]="ruleSet()"
          (ngModelChange)="choose($event)"
          data-testid="mx-rule-set"
        >
          @for (r of matrixRuleSets(); track r.code) {
            <mat-option [value]="r.code"
              >{{ { ar: r.nameAr, en: r.nameEn } | dcLocalize }}</mat-option
            >
          }
        </mat-select>
        @if (!matrixRuleSets().length) {
          <mat-hint>{{ 'mx.noRuleSets' | dcTranslate }}</mat-hint>
        }
      </mat-form-field>
      @if (ruleSet()) {
        <mat-form-field subscriptSizing="dynamic" class="mx-page__base">
          <mat-label>{{ 'mx.startFrom' | dcTranslate }}</mat-label>
          <mat-select [ngModel]="base()" (ngModelChange)="startFrom($event)" data-testid="mx-base">
            @for (v of versions(); track v.code) {
              <mat-option [value]="v.code"
                >{{ v.code
                }}{{ v['isRetired'] ? ' · ' + ('versions.retired' | dcTranslate) : '' }}</mat-option
              >
            }
          </mat-select>
        </mat-form-field>
      }
      <span class="mx-page__spacer"></span>
      <input
        #file
        type="file"
        accept=".json,application/json"
        hidden
        (change)="importFile($event)"
        data-testid="mx-import-file"
      />
      <button
        matButton
        type="button"
        (click)="file.click()"
        data-testid="mx-import"
        [matTooltip]="'mx.importHint' | dcTranslate"
      >
        <mat-icon fontSet="material-symbols-outlined">upload_file</mat-icon>
        {{ 'mx.import' | dcTranslate }}
      </button>
      <button matButton type="button" (click)="startEmpty()">
        <mat-icon fontSet="material-symbols-outlined">grid_on</mat-icon>
        {{ 'mx.startEmpty' | dcTranslate }}
      </button>
      <a matButton routerLink="/decisions/versions">
        <mat-icon fontSet="material-symbols-outlined">history_edu</mat-icon>
        {{ 'decisions.versions' | dcTranslate }}
      </a>
      <button
        matIconButton
        type="button"
        [matMenuTriggerFor]="more"
        [attr.aria-label]="'mx.more' | dcTranslate"
        data-testid="mx-more"
      >
        <mat-icon fontSet="material-symbols-outlined">more_vert</mat-icon>
      </button>
      <mat-menu #more="matMenu">
        <button mat-menu-item type="button" (click)="jsonOpen.set(true)" data-testid="mx-open-json">
          <mat-icon fontSet="material-symbols-outlined">data_object</mat-icon>
          {{ 'mx.jsonMenu' | dcTranslate }}
        </button>
      </mat-menu>
    </div>

    @if (notes().length) {
      <details class="mx-page__notes" open>
        <summary>{{ 'mx.importNotes' | dcTranslate }} ({{ notes().length }})</summary>
        <ul>
          @for (n of notes(); track $index) {
            <li dir="auto">{{ n }}</li>
          }
        </ul>
      </details>
    }

    <p class="mx-page__stats dc-muted">
      {{ matrix().rows.length }} {{ 'mx.grid.rows' | dcTranslate }} · {{ matrix().columns.length }}
      {{ 'mx.grid.columns' | dcTranslate }} · {{ matrix().inputs.length }}
      {{ 'mx.tab.inputs' | dcTranslate }}
      @if (dirty()) {
        · <strong>{{ 'mx.unsaved' | dcTranslate }}</strong>
      }
    </p>

    @if (ruleSet()) {
      <section class="mx-used" data-testid="mx-used-by">
        <span class="mx-used__label">{{ 'mx.used.title' | dcTranslate }}</span>
        @for (u of usedBy(); track u.workflow) {
          <div class="mx-used__flow">
            @if (u.service) {
              <span class="mx-node">
                <mat-icon fontSet="material-symbols-outlined">design_services</mat-icon>
                <span>
                  <small>{{ 'mx.used.service' | dcTranslate }}</small>
                  <strong>{{ u.serviceName | dcLocalize }}</strong>
                  <span dir="ltr" class="mx-code"
                    >{{ u.service }}
                    @if (u.version) {
                      · v{{ u.version }}
                    }
                  </span>
                </span>
                <a
                  matButton
                  [routerLink]="['/services/builder']"
                  [queryParams]="{ service: u.service }"
                  data-testid="mx-used-builder"
                  >{{ 'mx.used.openBuilder' | dcTranslate }}</a
                >
              </span>
              <mat-icon class="mx-used__arrow" fontSet="material-symbols-outlined"
                >arrow_forward</mat-icon
              >
            }
            <span class="mx-node">
              <mat-icon fontSet="material-symbols-outlined">account_tree</mat-icon>
              <span>
                <small>{{ 'mx.used.workflow' | dcTranslate }}</small>
                <strong>{{ u.workflowName | dcLocalize }}</strong>
                <span class="mx-used__steps">
                  @for (st of u.steps; track st.code) {
                    {{ 'mx.used.decisionStep' | dcTranslate }}
                    <b>{{ st.name | dcLocalize }}</b>
                    <span dir="ltr" class="mx-code">{{ st.code }}</span>
                  }
                  @if (regradedAfter(u.workflow); as tasks) {
                    @if (tasks.length) {
                      · {{ 'mx.used.regraded' | dcTranslate }}
                      @for (t of tasks; track t.step; let last = $last) {
                        <b>{{ t.stepName | dcLocalize }}</b
                        >{{ last ? '' : '، ' }}
                      }
                    }
                  }
                </span>
              </span>
              <a
                matButton
                [routerLink]="['/workflows/designer']"
                [queryParams]="{ workflowCode: u.workflow }"
                data-testid="mx-used-designer"
                >{{ 'mx.used.openDesigner' | dcTranslate }}</a
              >
            </span>
            <mat-icon class="mx-used__arrow" fontSet="material-symbols-outlined"
              >arrow_forward</mat-icon
            >
            <span class="mx-node mx-node--this">
              <mat-icon fontSet="material-symbols-outlined">grid_on</mat-icon>
              <span>
                <small>{{ 'mx.used.thisMatrix' | dcTranslate }}</small>
                <strong dir="ltr" class="mx-code">{{ ruleSet() }}</strong>
              </span>
            </span>
            @if (u.liveResult) {
              <span class="mx-used__live" [matTooltip]="'mx.used.liveHint' | dcTranslate">
                <mat-icon fontSet="material-symbols-outlined">bolt</mat-icon>
                {{ 'mx.used.live' | dcTranslate }}
              </span>
            }
          </div>
        } @empty {
          <div class="mx-used__flow mx-used__none" data-testid="mx-used-none">
            <mat-icon fontSet="material-symbols-outlined">link_off</mat-icon>
            <span>{{ 'mx.used.none' | dcTranslate }}</span>
            <a matButton routerLink="/workflows/designer">{{
              'mx.used.openDesigner' | dcTranslate
            }}</a>
          </div>
        }
      </section>
    }

    @if (jsonOpen()) {
      <section class="mx-json" data-testid="mx-json-panel">
        <header>
          <strong>JSON</strong>
          <span class="dc-muted">{{ 'mx.jsonHint' | dcTranslate }}</span>
          <button
            matIconButton
            type="button"
            (click)="jsonOpen.set(false)"
            [attr.aria-label]="'mx.close' | dcTranslate"
          >
            <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
          </button>
        </header>
        <textarea
          class="mx-page__json"
          dir="ltr"
          rows="20"
          spellcheck="false"
          [ngModel]="jsonText()"
          (ngModelChange)="jsonText.set($event)"
          data-testid="mx-json"
        ></textarea>
        <button matButton="tonal" type="button" (click)="applyJson()" data-testid="mx-json-apply">
          {{ 'mx.applyJson' | dcTranslate }}
        </button>
      </section>
    }

    <mat-tab-group
      mat-stretch-tabs="false"
      animationDuration="0ms"
      [selectedIndex]="tab()"
      (selectedIndexChange)="tab.set($event)"
    >
      <mat-tab [label]="'mx.tab.grid' | dcTranslate">
        <div class="mx-page__tab">
          <div class="mx-page__rows-input">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'mx.grid.rowsInput' | dcTranslate }}</mat-label>
              <mat-select
                [ngModel]="matrix().rowsInput ?? null"
                (ngModelChange)="update({ ...matrix(), rowsInput: $event })"
                [disabled]="!canCreate()"
                data-testid="mx-rows-input"
              >
                <mat-option [value]="null">{{ 'mx.grid.rowsInputAll' | dcTranslate }}</mat-option>
                @for (i of listInputs(); track i.code) {
                  <mat-option [value]="i.code"
                    >{{ { ar: i.nameAr, en: i.nameEn } | dcLocalize }}</mat-option
                  >
                }
              </mat-select>
            </mat-form-field>
            <span class="dc-muted">{{ 'mx.grid.rowsInputHint' | dcTranslate }}</span>
          </div>
          <dc-matrix-grid
            [matrix]="matrix()"
            [references]="refs"
            [editable]="canCreate()"
            (matrixChange)="update($event)"
          />
        </div>
      </mat-tab>
      <mat-tab [label]="'mx.tab.sectors' | dcTranslate">
        <div class="mx-page__tab">
          <dc-matrix-sectors
            [matrix]="matrix()"
            [references]="refs"
            [editable]="canCreate()"
            [form]="form()"
            (matrixChange)="update($event)"
            (formChange)="form.set($event)"
            (openShared)="openShared($event)"
          />
        </div>
      </mat-tab>
      <mat-tab [label]="'mx.tab.setup' | dcTranslate">
        <div class="mx-page__tab">
          <nav class="mx-parts" role="tablist">
            @for (p of sharedParts; track p) {
              <button
                type="button"
                role="tab"
                class="mx-part"
                [class.mx-part--on]="sharedPart() === p"
                [attr.aria-selected]="sharedPart() === p"
                (click)="sharedPart.set(p)"
                [attr.data-testid]="'mx-shared-' + p"
              >
                {{ 'mx.setup.part.' + p | dcTranslate }}
              </button>
            }
          </nav>
          <p class="mx-parts__hint">
            <mat-icon fontSet="material-symbols-outlined">info</mat-icon>
            {{ 'mx.setup.hint.' + sharedPart() | dcTranslate }}
          </p>
          @if (sharedPart() === 'data') {
            <table class="dc-table mx-inputs" data-testid="mx-inputs">
              <thead>
                <tr>
                  <th>{{ 'mx.source.title' | dcTranslate }}</th>
                  <th>{{ 'mx.code' | dcTranslate }}</th>
                  <th>{{ 'config.name' | dcTranslate }}</th>
                  <th>{{ 'mx.input.type' | dcTranslate }}</th>
                  <th>{{ 'mx.inputs.from' | dcTranslate }}</th>
                  <th>{{ 'mx.inputs.usedIn' | dcTranslate }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (i of matrix().inputs; track i.code) {
                  <tr>
                    <td>
                      <span class="mx-inputs__src"
                        ><mat-icon fontSet="material-symbols-outlined">{{
                          icons[i.source.kind]
                        }}</mat-icon>
                        {{ 'mx.source.' + i.source.kind | dcTranslate }}</span
                      >
                    </td>
                    <td dir="ltr" class="mx-code">{{ i.code }}</td>
                    <td>{{ { ar: i.nameAr, en: i.nameEn } | dcLocalize }}</td>
                    <td>{{ 'mx.type.' + i.type | dcTranslate }}</td>
                    <td dir="ltr" class="mx-code">{{ detail(i) }}</td>
                    <td>{{ usage().get(i.code) ?? 0 }}</td>
                    <td>
                      @if (canCreate()) {
                        <button
                          matIconButton
                          type="button"
                          (click)="editInput(i)"
                          [attr.aria-label]="'mx.input.edit' | dcTranslate"
                          [attr.data-testid]="'mx-edit-input-' + i.code"
                        >
                          <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>
                        </button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
            @if (canCreate()) {
              <button matButton type="button" (click)="newInput()" data-testid="mx-add-input">
                <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
                {{ 'mx.input.new' | dcTranslate }}
              </button>
            }
          } @else {
            <dc-matrix-settings
              [matrix]="matrix()"
              [part]="sharedPart() === 'grades' ? 'grades' : 'rules'"
              (matrixChange)="update($event)"
            />
          }
        </div>
      </mat-tab>
      <mat-tab [label]="'mx.tab.simulator' | dcTranslate">
        <div class="mx-page__tab">
          <dc-matrix-simulator [matrix]="matrix()" [answersOf]="refs.answersOf" />
        </div>
      </mat-tab>
      <mat-tab [label]="'mx.tab.connections' | dcTranslate">
        <div class="mx-page__tab">
          <dc-matrix-connections
            [matrix]="matrix()"
            [references]="refs"
            [ruleSet]="ruleSet()"
            [editable]="canCreate()"
            (fix)="editInput($event)"
          />
        </div>
      </mat-tab>
    </mat-tab-group>

    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (e of errors(); track $index) {
          <li dir="ltr">{{ e }}</li>
        }
      </ul>
    }
    @if (canCreate()) {
      <div class="mx-page__save">
        @if (ruleSet(); as code) {
          <span class="dc-muted">
            {{ 'versions.new' | dcTranslate }}
            <span dir="ltr" class="mx-code">{{ code }}/{{ nextNumber() }}</span>
          </span>
        } @else {
          <span class="dc-muted">{{ 'mx.pickToSave' | dcTranslate }}</span>
        }
        <dc-submit-button
          [entityType]="'dec.rule-set-version'"
          [disabled]="!ruleSet()"
          (submitted)="save(true)"
        />
        <button
          matButton="filled"
          type="button"
          [disabled]="!ruleSet()"
          (click)="save()"
          data-testid="mx-save"
        >
          <mat-icon fontSet="material-symbols-outlined">save</mat-icon>
          {{ drafts.saveLabel('dec.rule-set-version') | dcTranslate }}
        </button>
      </div>
    }
  `,
  styles: `
    .mx-used {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      margin: 0 0 1rem;
      padding: 0.75rem 1rem;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 12px;
      background: var(--dga-gray-25, #fcfcfd);
    }
    .mx-used__label {
      font-weight: 600;
    }
    .mx-used__flow {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
    }
    @media (min-width: 1100px) {
      .mx-used__flow {
        flex-wrap: nowrap;
      }
    }
    .mx-node {
      display: inline-flex;
      flex: 1 1 0;
      flex-wrap: wrap;
      min-width: 12rem;
      align-items: center;
      gap: 0.25rem 0.5rem;
      padding: 0.4rem 0.6rem;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 10px;
      background: #fff;
    }
    .mx-node > span {
      display: flex;
      flex: 1 1 0;
      min-width: 0;
      flex-direction: column;
      line-height: 1.35;
    }
    .mx-node small {
      color: var(--dga-gray-500, #6c737f);
      font-size: 12px;
    }
    .mx-node > a {
      flex-basis: 100%;
      justify-content: flex-start;
    }
    .mx-node .mx-code {
      margin-inline-start: 4px;
    }
    .mx-node--this {
      flex: none;
      min-width: 0;
      border-color: var(--dynamic-cube-gold-600, #b8862f);
      background: var(--dynamic-cube-gold-25, #fdfaf3);
    }
    .mx-used__steps {
      font-size: 13px;
    }
    .mx-used__arrow {
      color: var(--dga-gray-400, #9da4ae);
    }
    :host-context([dir='rtl']) .mx-used__arrow {
      transform: scaleX(-1);
    }
    .mx-used__live {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 2px 10px;
      border-radius: 999px;
      background: var(--dga-success-50, #ecfdf3);
      color: var(--dga-success-700, #067647);
      font-size: 13px;
    }
    .mx-used__live mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
    .mx-used__none {
      color: var(--dga-warning-700, #b54708);
    }
    .mx-json {
      margin-bottom: 1rem;
      padding: 0.75rem 1rem;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 12px;
    }
    .mx-json header {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    .mx-json header button {
      margin-inline-start: auto;
    }
    .mx-parts {
      display: flex;
      gap: 6px;
      margin-bottom: 0.5rem;
    }
    .mx-part {
      padding: 6px 14px;
      border: 1px solid var(--dga-gray-300, #d2d6db);
      border-radius: 999px;
      background: #fff;
      font: inherit;
      cursor: pointer;
    }
    .mx-part--on {
      border-color: var(--dynamic-cube-gold-600, #b8862f);
      background: var(--dynamic-cube-gold-25, #fdfaf3);
      font-weight: 600;
    }
    .mx-parts__hint {
      display: flex;
      align-items: flex-start;
      gap: 6px;
      margin: 0 0 1rem;
      color: var(--dga-gray-600, #4d5761);
    }
    .mx-parts__hint mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
      flex: none;
    }
    .mx-page__bar {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      align-items: center;
    }
    .mx-page__pick {
      width: min(30rem, 100%);
    }
    .mx-page__base {
      width: 12rem;
    }
    .mx-page__spacer {
      flex: 1;
    }
    .mx-page__notes {
      margin: 0.5rem 0;
      padding: 0.5rem 0.75rem;
      border-radius: 8px;
      background: var(--dga-warning-25, #fffcf5);
      border: 1px solid var(--dga-warning-200, #fedf89);
      font-size: 13px;
    }
    .mx-page__notes ul {
      max-height: 10rem;
      overflow: auto;
      margin: 0.25rem 0 0;
    }
    .mx-page__stats {
      margin: 0.5rem 0;
      font-size: 13px;
    }
    .mx-page__tab {
      padding-top: 1rem;
    }
    .mx-page__rows-input {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 0.75rem;
    }
    .mx-page__rows-input mat-form-field {
      width: min(26rem, 100%);
    }
    .mx-page__json {
      width: 100%;
      font-family: ui-monospace, monospace;
      font-size: 12px;
      border: 1px solid var(--dga-gray-300, #d2d6db);
      border-radius: 6px;
      padding: 8px;
    }
    .mx-page__save {
      position: sticky;
      bottom: 0;
      display: flex;
      justify-content: flex-end;
      align-items: center;
      gap: 0.75rem;
      padding: 0.75rem 0;
      margin-top: 1rem;
      background: var(--dga-bg, #fff);
      border-top: 1px solid var(--dga-gray-200, #e5e7eb);
      z-index: 2;
    }
    .mx-inputs__src {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      white-space: nowrap;
    }
    .mx-inputs__src mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .mx-code {
      font-family: ui-monospace, monospace;
      font-size: 13px;
    }
  `,
})
export class MatrixPage {
  private readonly api = inject(AppsControlApi);
  private readonly decisions = inject(DecisionsApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(I18nService);
  protected readonly drafts = inject(DraftsService);
  private readonly privileges = inject(PrivilegesService);
  protected readonly refs = inject(MatrixReferences);
  protected readonly icons = SOURCE_ICONS;

  private readonly ruleSets = rxResource({ stream: () => this.api.list('dec.rule-set') });
  private readonly versionRows = rxResource({
    stream: () => this.api.list('dec.rule-set-version'),
  });
  protected readonly matrixRuleSets = computed(() =>
    (this.ruleSets.value() ?? []).filter(
      (r) => String(r['engine'] ?? '').toUpperCase() === 'MATRIX',
    ),
  );
  protected readonly ruleSet = signal<string | null>(
    inject(ActivatedRoute).snapshot.queryParamMap.get('ruleSet'),
  );
  protected readonly base = signal<string | null>(null);
  protected readonly matrix = signal<Matrix>(emptyMatrix());
  private readonly saved = signal<string>(JSON.stringify(emptyMatrix()));
  /** The application form of the service deciding with this matrix, edited on the sector page and saved with it. */
  protected readonly form = signal<FormLink | null>(null);
  private readonly formSaved = signal<string>('null');
  protected readonly formDirty = computed(() => JSON.stringify(this.form()) !== this.formSaved());
  protected readonly dirty = computed(
    () => JSON.stringify(this.matrix()) !== this.saved() || this.formDirty(),
  );
  protected readonly notes = signal<string[]>([]);
  protected readonly errors = signal<string[]>([]);
  protected readonly tab = signal(0);
  /** The parts of the Shared setup tab: what the matrix reads, grades and scales, rules applied to every sector. */
  protected readonly sharedParts = ['data', 'grades', 'rules'] as const;
  protected readonly sharedPart = signal<'data' | 'grades' | 'rules'>('data');
  protected readonly jsonOpen = signal(false);
  /** The services and workflows deciding with this rule set. */
  protected readonly usedBy = computed(() => this.refs.usedBy(this.ruleSet()));
  protected readonly jsonText = signal('');
  protected readonly canCreate = computed(() => this.privileges.can('decisions.config', 'CREATE'));

  protected readonly versions = computed(() => {
    const prefix = `${this.ruleSet()}/`;
    return (this.versionRows.value() ?? [])
      .filter((r) => r.code.startsWith(prefix))
      .sort((a, b) => number(b.code) - number(a.code));
  });
  protected readonly nextNumber = computed(
    () => 1 + Math.max(0, ...this.versions().map((r) => number(r.code))),
  );

  /** Inputs that can list the rows applied for (lists and choices). */
  protected readonly listInputs = computed(() =>
    this.matrix().inputs.filter(
      (i) => i.type === 'list' || i.type === 'option' || i.type === 'text',
    ),
  );

  /** How many cells read each input. */
  protected readonly usage = computed(() => {
    const m = this.matrix();
    const counts = new Map<string, number>();
    for (const cell of m.cells)
      for (const i of cellInputs(m, cell)) counts.set(i.code, (counts.get(i.code) ?? 0) + 1);
    return counts;
  });

  constructor() {
    this.i18n.register(MATRIX_TRANSLATIONS);
    // Opening with ?ruleSet= (or after the versions load) starts from that rule set's latest version.
    let loaded = false;
    effect(() => {
      const rows = this.versionRows.value();
      if (!loaded && rows && this.ruleSet()) {
        loaded = true;
        this.choose(this.ruleSet()!);
      }
    });
    effect(() => {
      this.jsonText.set(JSON.stringify(this.matrix(), null, 2));
    });
    // The linked service's latest form. Once read it is kept (with what was changed and saved to a draft here) until
    // a newer version of the service is published and nothing is changed here, or another rule set is chosen.
    effect(() => {
      const found = this.refs.formsOf(this.ruleSet())[0] ?? null;
      const current = untracked(() => this.form());
      if (current && found?.service === current.service && found.version <= current.version) return;
      if (current && untracked(() => this.formDirty())) return;
      const form = found ? { ...found, newQuestions: [] } : null;
      this.form.set(form);
      this.formSaved.set(JSON.stringify(form));
    });
    // Questions added on the page read like the bank's own until they are saved.
    effect(() =>
      this.refs.pendingQuestions.set(
        (this.form()?.newQuestions ?? []).map((q) => ({
          ...questionPayload(q),
          code: q.code,
          nameAr: q.nameAr,
          nameEn: q.nameEn,
          isActive: true,
          owner: 'ENTITY' as const,
        })),
      ),
    );
  }

  /** The workflow's tasks whose fields the matrix reads: the grade is worked out again after them. */
  protected regradedAfter(workflow: string) {
    const steps = new Set(
      this.matrix()
        .inputs.filter((i) => i.source.kind === 'reviewer' && i.source.step)
        .map((i) => i.source.step!.toLowerCase()),
    );
    return this.refs
      .tasks()
      .filter((t) => t.workflow === workflow && steps.has(t.step.toLowerCase()));
  }

  /** Opens a part of the Shared setup tab (from a link on the sector page). */
  protected openShared(part: 'data' | 'grades' | 'rules'): void {
    this.sharedPart.set(part);
    this.tab.set(2);
  }

  protected choose(code: string): void {
    this.ruleSet.set(code);
    // The form of the new rule set's service is read afresh.
    this.form.set(null);
    this.formSaved.set('null');
    this.errors.set([]);
    const latest = this.versions()[0];
    if (latest) this.load(latest);
    else this.base.set(null);
  }

  protected startFrom(code: string): void {
    const row = this.versions().find((v) => v.code === code);
    if (row && (!this.dirty() || confirm(this.i18n.translate('mx.confirmReplace')))) this.load(row);
  }

  private load(row: ConfigRow): void {
    this.base.set(row.code);
    const m = readMatrix(row['configuration']);
    this.matrix.set(m);
    this.saved.set(JSON.stringify(m));
    this.notes.set([]);
  }

  protected startEmpty(): void {
    if (this.dirty() && !confirm(this.i18n.translate('mx.confirmReplace'))) return;
    this.matrix.set(emptyMatrix());
    this.notes.set([]);
  }

  protected update(m: Matrix): void {
    this.matrix.set(m);
  }

  protected detail(i: MatrixInput): string {
    const s = i.source;
    switch (s.kind) {
      case 'applicant':
        return s.question ?? '—';
      case 'reviewer':
        return `${s.path ? s.path + ' › ' : ''}${s.step ?? '?'} › ${s.field ?? '?'}`;
      case 'integration':
        return `${s.operation ?? '?'} › ${s.output ?? '?'}`;
      case 'party':
        return s.path ?? '—';
      case 'projects':
        return `${s.function ?? 'sum'}(${s.column ?? ''})`;
      case 'derived':
        return 'ƒ';
      default:
        return '';
    }
  }

  protected async editInput(input: MatrixInput): Promise<void> {
    await this.openInput(input, false);
  }

  protected async newInput(): Promise<void> {
    const code = freeCode(
      this.matrix().inputs.map((i) => i.code),
      'input',
    );
    await this.openInput(
      { code, nameAr: '', nameEn: '', type: 'number', source: { kind: 'applicant' } },
      true,
    );
  }

  private async openInput(input: MatrixInput, isNew: boolean): Promise<void> {
    const m = this.matrix();
    const result = await firstValueFrom(
      this.dialog
        .open<
          MatrixInputEditor,
          MatrixInputEditorData,
          { input: MatrixInput | null; removed?: boolean }
        >(MatrixInputEditor, {
          data: { matrix: m, input, isNew, references: this.refs },
          width: '60rem',
          maxWidth: '96vw',
        })
        .afterClosed(),
    );
    if (!result) return;
    const inputs = result.removed
      ? m.inputs.filter((i) => i.code !== input.code)
      : isNew
        ? [...m.inputs, result.input!]
        : m.inputs.map((i) => (i.code === input.code ? result.input! : i));
    this.matrix.set({ ...m, inputs });
  }

  /** Reads a classification guide export (JSON) and converts it on the server. */
  protected async importFile(event: Event): Promise<void> {
    const target = event.target as HTMLInputElement;
    const file = target.files?.[0];
    target.value = '';
    if (!file) return;
    if (this.dirty() && !confirm(this.i18n.translate('mx.confirmReplace'))) return;
    this.errors.set([]);
    try {
      const guide = JSON.parse(await file.text()) as unknown;
      const result = await firstValueFrom(this.decisions.importGuide(guide));
      // Cells that are the same in several sectors become one shared calculation (any sector can get its own copy later).
      let imported = readMatrix(result.configuration);
      for (const column of imported.columns) imported = shareIdentical(imported, column.code);
      const linked = imported.cells.filter((c) => c.kind === 'shared').length;
      this.matrix.set(imported);
      const sharing = linked
        ? [
            this.i18n
              .translate('mx.importShared')
              .replace('{n}', String(linked))
              .replace('{k}', String(imported.shared?.length ?? 0)),
          ]
        : [];
      const extras = Object.entries(result.extras ?? {})
        .filter(([, v]) => Array.isArray(v) && v.length)
        .map(
          ([k, v]) =>
            `${this.i18n.translate('mx.importExtra')}: ${k}${Array.isArray(v) ? ` (${v.length})` : ''}`,
        );
      this.notes.set([...sharing, ...(result.warnings ?? []), ...extras]);
      this.tab.set(0);
    } catch (error) {
      this.errors.set(
        error instanceof SyntaxError
          ? [this.i18n.translate('decisions.invalidJson')]
          : problemMessages(error),
      );
    }
  }

  protected applyJson(): void {
    try {
      this.matrix.set(readMatrix(JSON.parse(this.jsonText())));
      this.errors.set([]);
    } catch {
      this.errors.set([this.i18n.translate('decisions.invalidJson')]);
    }
  }

  /**
   * Saves the matrix as the rule set's next version and, when its application form changed here, the service's next
   * version (with the bank questions added here) in the same click: each area's changes go into one change set — my
   * draft, or one sent for approval at once. The service goes first: a matrix is not saved over a form that failed.
   */
  protected async save(submit = false): Promise<void> {
    const code = this.ruleSet();
    if (!code) return;
    this.errors.set([]);
    const n = this.nextNumber();
    const form = this.formDirty() ? this.form() : null;
    try {
      if (form) {
        const next = this.refs.formsOf(code).find((f) => f.service === form.service);
        const number = Math.max(form.version, next?.version ?? 0) + 1;
        await this.saveTogether(
          [
            ...form.newQuestions.map((q) => ({
              entityType: 'svc.question',
              entityCode: q.code,
              payload: questionPayload(q),
            })),
            {
              entityType: 'svc.service-version',
              entityCode: `${form.service}/${number}`,
              payload: {
                nameAr: `الإصدار ${number}`,
                nameEn: `Version ${number}`,
                definition: form.definition,
              },
            },
          ],
          submit,
        );
        // The questions now wait in the services change set; what was saved stays on the page as it is.
        const kept = { ...form, newQuestions: [] };
        this.form.set(kept);
        this.formSaved.set(JSON.stringify(kept));
        this.refs.loadQuestions();
        this.refs.reloadServiceVersions();
      }
      await this.saveTogether(
        [
          {
            entityType: 'dec.rule-set-version',
            entityCode: `${code}/${n}`,
            payload: {
              nameAr: `الإصدار ${n}`,
              nameEn: `Version ${n}`,
              configuration: syncFromBank(this.matrix(), this.refs.answersOf),
            },
          },
        ],
        submit,
      );
      this.saved.set(JSON.stringify(this.matrix()));
      this.versionRows.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  /**
   * Adds new rows of one area in one change set: my draft (replacing my earlier copy of the same row), or a change set
   * of their own sent at once (published at once where the area needs no approval).
   */
  private async saveTogether(
    items: { entityType: string; entityCode: string; payload: unknown }[],
    submit: boolean,
  ): Promise<void> {
    const types = await firstValueFrom(this.drafts.types());
    const type = types.find((t) => t.entityType === items[0].entityType);
    if (!type) throw new Error(`Unknown entity type ${items[0].entityType}`);
    const toDraft = type.requiresApproval && !submit;
    let id: string;
    if (toDraft) {
      for (const item of items) await this.dropDraftCopy(item.entityType, item.entityCode);
      const drafts = await firstValueFrom(
        this.api.changeSets({ status: 'DRAFT', area: type.area, mine: true }),
      );
      id =
        drafts[0]?.id ??
        (await firstValueFrom(this.api.createChangeSet(type.area, this.i18n.translate('mx.title'))))
          .changeSet.id;
    } else {
      id = (
        await firstValueFrom(this.api.createChangeSet(type.area, this.i18n.translate('mx.title')))
      ).changeSet.id;
    }
    try {
      for (const item of items)
        await firstValueFrom(
          this.api.addItem(id, item.entityType, item.entityCode, 'CREATE', item.payload),
        );
      if (!toDraft) await firstValueFrom(this.api.submit(id));
    } catch (error) {
      // A change set made for this save is not left behind half filled.
      if (!toDraft) await firstValueFrom(this.api.discard(id)).catch(() => undefined);
      throw error;
    }
    this.snackBar.open(
      this.i18n.translate(
        toDraft
          ? 'drafts.added'
          : submit && type.requiresApproval
            ? 'drafts.submitted'
            : 'drafts.saved',
      ),
      undefined,
      { duration: 4000 },
    );
  }

  /** Saving again while designing replaces this version in my own draft instead of failing as a duplicate. */
  private async dropDraftCopy(entityType: string, entityCode: string): Promise<void> {
    const type = (await firstValueFrom(this.drafts.types())).find(
      (t) => t.entityType === entityType,
    );
    if (!type?.requiresApproval) return;
    for (const draft of await firstValueFrom(
      this.api.changeSets({ status: 'DRAFT', area: type.area, mine: true }),
    )) {
      const detail = await firstValueFrom(this.api.changeSet(draft.id));
      const item = detail.items.find(
        (i) =>
          i.entityType === entityType && i.entityCode === entityCode && i.operation === 'CREATE',
      );
      if (item) await firstValueFrom(this.api.removeItem(draft.id, item.id));
    }
  }
}

function number(code: string): number {
  return Number(code.slice(code.lastIndexOf('/') + 1));
}
