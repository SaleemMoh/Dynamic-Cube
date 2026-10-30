import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ConfigRow, I18nService, LocalizePipe, StructuredInput, StructuredShape, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { DraftsService } from '../drafts.service';
import { PermissionCards } from './permission-cards';
import { RoleMenus } from './role-menus';
import {
  GrantRow,
  MenuChoiceGroup,
  Permissions,
  PrivilegeRow,
  RoleRow,
  ScreenNode,
  permissionsOf,
  privilegesOf,
  roleScreens,
  screenCodes,
  splitGrants,
} from './role-models';
import { ScreenData } from './screen-data';
import { SubmitButton } from '../submit-button';

export interface RoleEditorData {
  /** The full role (with privileges and grants), or null for a new one. */
  role: RoleRow | null;
  screens: ScreenNode[];
  apps: ConfigRow[];
  /** Business apps a role can belong to. */
  businessApps: ConfigRow[];
  /** The business app a new role starts in (the one chosen in the header). */
  defaultBusinessApp?: string | null;
  /** Lists of values (with the screens they are bound to). */
  lovs: ConfigRow[];
  menus: MenuChoiceGroup[];
  /** Companies the role can differ in (the selector shows with two or more). */
  companies?: ConfigRow[];
  readonly?: boolean;
}

/** What the role contains in one place (all companies, or one company's override). */
interface Contents {
  permissions: Permissions;
  selected: string[];
  screenGrants: GrantRow[];
  otherGrants: GrantRow[];
  menus: string[];
  /** As stored (their field rules are kept). */
  privileges: PrivilegeRow[];
}

const CODE = /^[A-Za-z][A-Za-z0-9_-]{0,99}$/;

/**
 * A role: names and code, the screens it opens (picked from the available screens) and, per screen, the actions it
 * may take on the screen and on the menus and actions inside it. Saving adds the change to the draft change set.
 */
@Component({
  selector: 'dc-role-editor',
  imports: [
    SubmitButton,
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    PermissionCards,
    RoleMenus,
    ScreenData,
    StructuredInput,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ (data.readonly ? 'roles.view' : data.role ? 'roles.edit' : 'roles.new') | dcTranslate }}
      @if (data.role) {
        <span class="mono dc-muted">{{ data.role.code }}</span>
      }
    </h2>
    <mat-dialog-content>
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (error of errors(); track $index) {
            <li dir="auto">{{ error }}</li>
          }
        </ul>
      }
      <form [formGroup]="form" id="dc-role-form" (ngSubmit)="save()" novalidate>
        <div class="dc-role-fields">
          <div>
            <label class="dc-field-label" for="role-nameEn">{{ 'roles.nameEn' | dcTranslate }} <span class="dc-required">*</span></label>
            <mat-form-field subscriptSizing="dynamic">
              <input matInput id="role-nameEn" formControlName="nameEn" dir="ltr" data-testid="role-name-en" />
            </mat-form-field>
          </div>
          <div>
            <label class="dc-field-label" for="role-nameAr">{{ 'roles.nameAr' | dcTranslate }} <span class="dc-required">*</span></label>
            <mat-form-field subscriptSizing="dynamic">
              <input matInput id="role-nameAr" formControlName="nameAr" dir="rtl" data-testid="role-name-ar" />
            </mat-form-field>
          </div>
          <div>
            <label class="dc-field-label" for="role-code">{{ 'config.code' | dcTranslate }} <span class="dc-required">*</span></label>
            <mat-form-field subscriptSizing="dynamic">
              <input matInput id="role-code" formControlName="code" dir="ltr" class="mono" data-testid="role-code" />
            </mat-form-field>
          </div>
          <div>
            <label class="dc-field-label" for="role-app">{{ 'roles.app' | dcTranslate }}</label>
            <mat-form-field subscriptSizing="dynamic">
              <mat-select id="role-app" formControlName="appCode">
                <mat-option [value]="null">—</mat-option>
                @for (app of data.apps; track app.code) {
                  <mat-option [value]="app.code">{{ { ar: app.nameAr, en: app.nameEn } | dcLocalize }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>
          <div>
            <label class="dc-field-label" for="role-business-app">{{ 'roles.businessApp' | dcTranslate }}</label>
            <mat-form-field subscriptSizing="dynamic">
              <mat-select
                id="role-business-app"
                formControlName="businessApps"
                multiple
                [placeholder]="'roles.allBusinessApps' | dcTranslate"
                data-testid="role-business-app"
              >
                @for (app of data.businessApps; track app.code) {
                  <mat-option [value]="app.code">{{ { ar: app.nameAr, en: app.nameEn } | dcLocalize }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>
          <div>
            <label class="dc-field-label" for="role-reference">{{ 'roles.referenceCode' | dcTranslate }}</label>
            <mat-form-field subscriptSizing="dynamic">
              <input matInput id="role-reference" formControlName="referenceCode" dir="ltr" />
            </mat-form-field>
          </div>
          <div class="dc-role-active">
            <mat-checkbox formControlName="isActive">{{ 'config.active' | dcTranslate }}</mat-checkbox>
          </div>
        </div>
      </form>

      @if ((data.companies?.length ?? 0) > 1) {
        <div class="dc-role-company" data-testid="role-company-bar">
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'roles.company' | dcTranslate }}</mat-label>
            <mat-select [value]="company()" (selectionChange)="chooseCompany($event.value)" data-testid="role-company">
              <mat-option [value]="null">{{ 'access.allCompanies' | dcTranslate }}</mat-option>
              @for (c of data.companies ?? []; track c.code) {
                <mat-option [value]="c.code">
                  {{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}
                  @if (overrides().includes(c.code)) {
                    — {{ 'roles.differs' | dcTranslate }}
                  }
                </mat-option>
              }
            </mat-select>
          </mat-form-field>
          @if (company(); as c) {
            @if (overrides().includes(c)) {
              <span class="dc-pill dc-pill--gold" data-testid="role-override-on">{{ 'roles.overrideOn' | dcTranslate }}</span>
              @if (!data.readonly) {
                <button matButton type="button" (click)="dropOverride()" data-testid="role-drop-override">{{ 'roles.dropOverride' | dcTranslate }}</button>
              }
            } @else {
              <span class="dc-muted">{{ 'roles.sameAsAll' | dcTranslate }}</span>
              @if (!data.readonly) {
                <button matButton="outlined" type="button" (click)="customize()" data-testid="role-customize">
                  <mat-icon fontSet="material-symbols-outlined">tune</mat-icon>
                  {{ 'roles.customize' | dcTranslate }}
                </button>
              }
            }
          } @else if (overrides().length) {
            <span class="dc-muted">{{ 'roles.differsIn' | dcTranslate }} {{ overrideNames() }}</span>
          }
          <p class="dc-muted dc-role-company__hint">{{ 'roles.companyHint' | dcTranslate }}</p>
        </div>
      }

      <h3 class="dc-step"><span>1</span> {{ 'roles.selectPrivileges' | dcTranslate }}</h3>
      <div class="dc-picker" [class.dc-picker--readonly]="contentReadonly()">
        @if (!contentReadonly()) {
          <section class="dc-picker__list" data-testid="available-screens">
            <header>{{ 'roles.availableScreens' | dcTranslate }}</header>
            <div class="dc-picker__search">
              <mat-form-field subscriptSizing="dynamic">
                <input
                  matInput
                  [placeholder]="'config.search' | dcTranslate"
                  (input)="availableSearch.set($any($event.target).value)"
                  data-testid="available-search"
                />
                <mat-icon matSuffix fontSet="material-symbols-outlined">search</mat-icon>
              </mat-form-field>
            </div>
            <ul role="listbox" aria-multiselectable="true" [attr.aria-label]="'roles.availableScreens' | dcTranslate">
              @for (screen of available(); track screen.code) {
                <li
                  role="option"
                  tabindex="0"
                  [attr.aria-selected]="pickedAvailable().has(screen.code)"
                  [class.dc-picked]="pickedAvailable().has(screen.code)"
                  (click)="toggle(pickedAvailable, screen.code)"
                  (keydown.enter)="toggle(pickedAvailable, screen.code)"
                  (dblclick)="add([screen.code])"
                  [attr.data-testid]="'available-' + screen.code"
                >
                  <strong>{{ screen.name | dcLocalize }}</strong>
                  <span>{{ screen.place | dcLocalize }}</span>
                </li>
              } @empty {
                <li class="dc-empty">{{ 'config.empty' | dcTranslate }}</li>
              }
            </ul>
          </section>
          <div class="dc-picker__moves">
            <button
              matIconButton
              type="button"
              (click)="add(picked(pickedAvailable(), available()))"
              [disabled]="!pickedAvailable().size"
              [matTooltip]="'roles.addPicked' | dcTranslate"
              data-testid="add-picked"
            >
              <mat-icon fontSet="material-symbols-outlined">{{ forward() }}</mat-icon>
            </button>
            <button
              matIconButton
              type="button"
              (click)="add(codes(available()))"
              [disabled]="!available().length"
              [matTooltip]="'roles.addAll' | dcTranslate"
              data-testid="add-all"
            >
              <mat-icon fontSet="material-symbols-outlined">{{
                forward() === 'chevron_right' ? 'keyboard_double_arrow_right' : 'keyboard_double_arrow_left'
              }}</mat-icon>
            </button>
            <button
              matIconButton
              type="button"
              (click)="remove(picked(pickedChosen(), chosen()))"
              [disabled]="!pickedChosen().size"
              [matTooltip]="'roles.removePicked' | dcTranslate"
              data-testid="remove-picked"
            >
              <mat-icon fontSet="material-symbols-outlined">{{ back() }}</mat-icon>
            </button>
            <button
              matIconButton
              type="button"
              (click)="remove(codes(chosen()))"
              [disabled]="!chosen().length"
              [matTooltip]="'roles.removeAll' | dcTranslate"
              data-testid="remove-all"
            >
              <mat-icon fontSet="material-symbols-outlined">{{
                back() === 'chevron_left' ? 'keyboard_double_arrow_left' : 'keyboard_double_arrow_right'
              }}</mat-icon>
            </button>
          </div>
        }
        <section class="dc-picker__list" data-testid="role-screens">
          <header>
            {{ 'roles.roleScreens' | dcTranslate }} <span class="dc-count">{{ selected().length }}</span>
          </header>
          <div class="dc-picker__search">
            <mat-form-field subscriptSizing="dynamic">
              <input matInput [placeholder]="'config.search' | dcTranslate" (input)="chosenSearch.set($any($event.target).value)" />
              <mat-icon matSuffix fontSet="material-symbols-outlined">search</mat-icon>
            </mat-form-field>
          </div>
          <ul role="listbox" aria-multiselectable="true" [attr.aria-label]="'roles.roleScreens' | dcTranslate">
            @for (screen of chosen(); track screen.code) {
              <li
                role="option"
                tabindex="0"
                [attr.aria-selected]="pickedChosen().has(screen.code)"
                [class.dc-picked]="pickedChosen().has(screen.code)"
                (click)="!contentReadonly() && toggle(pickedChosen, screen.code)"
                (keydown.enter)="!contentReadonly() && toggle(pickedChosen, screen.code)"
                (dblclick)="!contentReadonly() && remove([screen.code])"
                [attr.data-testid]="'chosen-' + screen.code"
              >
                <strong>{{ screen.name | dcLocalize }}</strong>
                <span>{{ screen.place | dcLocalize }}</span>
              </li>
            } @empty {
              <li class="dc-empty">{{ 'roles.noScreens' | dcTranslate }}</li>
            }
          </ul>
        </section>
      </div>

      <h3 class="dc-step"><span>2</span> {{ 'roles.selectPermissions' | dcTranslate }}</h3>
      <dc-permission-cards
        [screens]="selectedScreens()"
        [permissions]="permissions()"
        [readonly]="contentReadonly()"
        (toggled)="grant($event)"
        (removed)="remove([$event])"
      />

      <h3 class="dc-step"><span>3</span> {{ 'roles.data.title' | dcTranslate }}</h3>
      <p class="dc-muted dc-step__hint">{{ 'roles.data.hint' | dcTranslate }}</p>
      <dc-screen-data
        [screens]="selectedScreens()"
        [lovs]="data.lovs"
        [grants]="screenGrants()"
        [readonly]="contentReadonly()"
        (grantsChange)="screenGrants.set($event)"
      />

      <h3 class="dc-step"><span>4</span> {{ 'roles.menus.title' | dcTranslate }}</h3>
      <p class="dc-muted dc-step__hint">{{ 'roles.menus.hint' | dcTranslate }}</p>
      <dc-role-menus [groups]="data.menus" [selected]="menus()" [readonly]="contentReadonly()" (selectedChange)="menus.set($event)" />

      <mat-expansion-panel class="dc-grants">
        <mat-expansion-panel-header>
          <mat-panel-title>{{ 'roles.grants' | dcTranslate }}</mat-panel-title>
          <mat-panel-description>{{ 'roles.grantsHint' | dcTranslate }}</mat-panel-description>
        </mat-expansion-panel-header>
        <dc-structured-input [shape]="grantsShape" [formControl]="grants" testId="role-grants" />
      </mat-expansion-panel>
    </mat-dialog-content>
    <mat-dialog-actions>
      <button matButton="outlined" mat-dialog-close>{{ (data.readonly ? 'common.close' : 'form.cancel') | dcTranslate }}</button>
      @if (!data.readonly) {
        <dc-submit-button entityType="apps.role" (submitted)="submitForApproval()" />
        <button matButton="filled" type="submit" form="dc-role-form" data-testid="save-role">{{ drafts.saveLabel('apps.role') | dcTranslate }}</button>
      }
    </mat-dialog-actions>
  `,
  styles: `
    .dc-role-company {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px 12px;
      margin: 16px 0 0;
      padding: 12px 14px;
      border: 1px solid var(--dga-gray-200);
      border-radius: 10px;
      background: var(--dga-gray-50);
    }
    .dc-role-company mat-form-field {
      min-width: 16rem;
    }
    .dc-role-company__hint {
      flex-basis: 100%;
      margin: 0;
      font-size: 13px;
    }
    .dc-role-fields {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 16px 20px;
    }
    .dc-role-fields mat-form-field {
      width: 100%;
    }
    .dc-role-active {
      display: flex;
      align-items: flex-end;
    }
    .dc-required {
      color: #d92d20;
    }
    .dc-step {
      display: flex;
      align-items: center;
      gap: 10px;
      margin: 28px 0 14px;
      font-size: 18px;
      font-weight: 700;
      color: var(--dga-gray-900);
    }
    .dc-step__hint {
      margin: -8px 0 12px;
      font-size: 13px;
    }
    .dc-step span {
      display: inline-grid;
      place-items: center;
      width: 28px;
      height: 28px;
      border-radius: 9999px;
      background: var(--dynamic-cube-gold);
      color: #ffffff;
      font-size: 14px;
    }
    .dc-picker {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr);
      gap: 16px;
      align-items: stretch;
    }
    .dc-picker--readonly {
      grid-template-columns: minmax(0, 1fr);
    }
    .dc-picker__list {
      display: flex;
      flex-direction: column;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius-lg);
      overflow: hidden;
    }
    .dc-picker__list header {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px 16px;
      border-bottom: 1px solid var(--dga-gray-200);
      background: var(--dga-gray-50);
      font-weight: 600;
    }
    .dc-picker__search {
      padding: 10px 12px;
      border-bottom: 1px solid var(--dga-gray-100);
    }
    .dc-picker__search mat-form-field {
      width: 100%;
    }
    .dc-picker__list ul {
      height: 18rem;
      margin: 0;
      padding: 0;
      overflow-y: auto;
      list-style: none;
    }
    .dc-picker__list li {
      display: grid;
      gap: 2px;
      padding: 10px 16px;
      border-inline-start: 3px solid transparent;
      cursor: pointer;
      user-select: none;
    }
    .dc-picker__list li:hover {
      background: var(--dga-gray-50);
    }
    .dc-picker__list li:focus-visible {
      outline: none;
      box-shadow: var(--shell-focus);
    }
    .dc-picker__list li.dc-picked {
      border-inline-start-color: var(--dynamic-cube-gold);
      background: var(--dynamic-cube-gold-50);
    }
    .dc-picker__list li strong {
      font-weight: 500;
      color: var(--dga-gray-900);
    }
    .dc-picker__list li span {
      font-size: 13px;
      color: var(--dga-gray-500);
    }
    .dc-picker__list li.dc-empty {
      cursor: default;
    }
    .dc-picker__moves {
      display: flex;
      flex-direction: column;
      justify-content: center;
      gap: 10px;
    }
    .dc-picker__moves button {
      border: 1px solid var(--dga-gray-300);
      border-radius: var(--shell-radius);
    }
    .dc-grants {
      margin-top: 24px;
    }
    .dc-grants__json {
      width: 100%;
    }
    @media (max-width: 760px) {
      .dc-role-fields,
      .dc-picker {
        grid-template-columns: minmax(0, 1fr);
      }
      .dc-picker__moves {
        flex-direction: row;
        justify-content: center;
      }
    }
  `,
})
export class RoleEditor {
  protected readonly data = inject<RoleEditorData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<RoleEditor, boolean>);
  private submitNext = false;
  protected readonly drafts = inject(DraftsService);
  private readonly i18n = inject(I18nService);

  protected readonly errors = signal<string[]>([]);
  protected readonly permissions = signal<Permissions>(permissionsOf(this.data.role?.privileges ?? []));
  /** Screens in the role, in the order they were added. */
  protected readonly selected = signal<string[]>(roleScreens(this.data.screens, this.permissions()));
  protected readonly availableSearch = signal('');
  protected readonly chosenSearch = signal('');
  protected readonly pickedAvailable = signal(new Set<string>());
  protected readonly pickedChosen = signal(new Set<string>());

  private readonly byCode = new Map(this.data.screens.map((s) => [s.code, s]));
  protected readonly selectedScreens = computed(() =>
    this.selected()
      .map((c) => this.byCode.get(c)!)
      .filter(Boolean),
  );
  protected readonly available = computed(() =>
    this.filter(
      this.data.screens.filter((s) => !this.selected().includes(s.code)),
      this.availableSearch(),
    ),
  );
  protected readonly chosen = computed(() => this.filter(this.selectedScreens(), this.chosenSearch()));
  protected readonly forward = computed(() => (this.i18n.direction() === 'rtl' ? 'chevron_left' : 'chevron_right'));
  protected readonly back = computed(() => (this.i18n.direction() === 'rtl' ? 'chevron_right' : 'chevron_left'));

  protected readonly form = new FormGroup({
    nameEn: new FormControl(this.data.role?.nameEn ?? '', { nonNullable: true, validators: Validators.required }),
    nameAr: new FormControl(this.data.role?.nameAr ?? '', { nonNullable: true, validators: Validators.required }),
    code: new FormControl(this.data.role?.code ?? '', { nonNullable: true, validators: [Validators.required, Validators.pattern(CODE)] }),
    appCode: new FormControl<string | null>(this.data.role?.appCode ?? null),
    // The role's business apps (none: every app); a role may serve several apps of one entity.
    businessApps: new FormControl<string[]>(
      this.data.role
        ? ((this.data.role['businessApps'] as string[] | undefined) ?? (this.data.role['businessApp'] ? [this.data.role['businessApp'] as string] : []))
        : this.data.defaultBusinessApp
          ? [this.data.defaultBusinessApp]
          : [],
      { nonNullable: true },
    ),
    referenceCode: new FormControl<string | null>(this.data.role?.referenceCode ?? null),
    isActive: new FormControl(this.data.role?.isActive ?? true, { nonNullable: true }),
  });
  private readonly initialGrants = splitGrants(this.data.role?.grants ?? [], this.selectedScreens().flatMap(screenCodes));
  /** Grants on the role's screens, edited per screen. */
  protected readonly screenGrants = signal<GrantRow[]>(this.initialGrants.onScreens);
  /** The other grants (everywhere, or on screens the role does not open). */
  protected readonly grants = new FormControl<GrantRow[]>(this.initialGrants.others, { nonNullable: true });
  protected readonly grantsShape: StructuredShape = {
    kind: 'table',
    columns: [
      {
        key: 'lovCode',
        label: { ar: 'القائمة', en: 'List' },
        type: 'select',
        required: true,
        options: this.data.lovs.map((l) => ({ value: l.code, label: { ar: `${l.nameAr} (${l.code})`, en: `${l.nameEn} (${l.code})` } })),
      },
      {
        key: 'resourceCode',
        label: { ar: 'الشاشة (فارغ: الكل)', en: 'Screen (empty: all)' },
        type: 'select',
        options: this.data.screens.map((r) => ({ value: r.code, label: r.name })),
      },
      {
        key: 'grantType',
        label: { ar: 'الصلاحية', en: 'Access' },
        type: 'select',
        required: true,
        defaultValue: 'ALL',
        options: [
          { value: 'ALL', label: { ar: 'الكل', en: 'All' } },
          { value: 'VALUES', label: { ar: 'قيم محددة', en: 'Some values' } },
          { value: 'RULE', label: { ar: 'حسب قاعدة', en: 'By a rule' } },
          { value: 'NONE', label: { ar: 'لا شيء', en: 'None' } },
        ],
      },
      { key: 'grantedValues', label: { ar: 'القيم', en: 'Values' }, type: 'tags', ltr: true },
      { key: 'rule', label: { ar: 'القاعدة', en: 'Rule' }, type: 'value' },
    ],
  };
  protected readonly menus = signal<string[]>(this.data.role?.menus ?? []);

  /** Whose contents are shown: all companies (null) or one company (its override, or the role as it is). */
  protected readonly company = signal<string | null>(null);
  protected readonly overrides = signal<string[]>((this.data.role?.overrides ?? []).map((o) => o.companyCode));
  private readonly sets = new Map<string | null, Contents>();
  /** A company without an override shows the role as it is, read-only, until it is customized. */
  protected readonly contentReadonly = computed(() => !!this.data.readonly || (this.company() !== null && !this.overrides().includes(this.company()!)));
  protected readonly overrideNames = computed(() =>
    this.overrides()
      .map((code) => {
        const c = (this.data.companies ?? []).find((x) => x.code === code);
        return c ? this.i18n.localize({ ar: c.nameAr, en: c.nameEn }) : code;
      })
      .join('، '),
  );

  constructor() {
    if (this.data.role) {
      this.form.controls.code.disable();
    }
    if (this.data.readonly) {
      this.form.disable();
      this.grants.disable();
    }
    this.sets.set(null, this.contentsOf(this.data.role?.privileges ?? [], this.data.role?.grants ?? [], this.data.role?.menus ?? []));
    for (const o of this.data.role?.overrides ?? []) {
      this.sets.set(o.companyCode, this.contentsOf(o.privileges ?? [], o.grants ?? [], o.menus ?? []));
    }
  }

  private contentsOf(privileges: PrivilegeRow[], grants: GrantRow[], menus: string[]): Contents {
    const permissions = permissionsOf(privileges);
    const selected = roleScreens(this.data.screens, permissions);
    const split = splitGrants(
      grants,
      selected.flatMap((c) => (this.byCode.get(c) ? screenCodes(this.byCode.get(c)!) : [c])),
    );
    return { permissions, selected, screenGrants: split.onScreens, otherGrants: split.others, menus: [...menus], privileges };
  }

  /** Keeps what is on screen for the place shown (when it can be edited there). */
  private stash(): void {
    if (this.contentReadonly() && this.company() !== null) return;
    const current = this.sets.get(this.company());
    this.sets.set(this.company(), {
      permissions: this.permissions(),
      selected: this.selected(),
      screenGrants: this.screenGrants(),
      otherGrants: this.grants.value ?? [],
      menus: this.menus(),
      privileges: current?.privileges ?? [],
    });
  }

  private show(contents: Contents): void {
    this.permissions.set(structuredClone(contents.permissions));
    this.selected.set([...contents.selected]);
    this.screenGrants.set(structuredClone(contents.screenGrants));
    this.grants.setValue(structuredClone(contents.otherGrants));
    this.menus.set([...contents.menus]);
    this.pickedAvailable.set(new Set());
    this.pickedChosen.set(new Set());
    if (this.contentReadonly()) this.grants.disable();
    else this.grants.enable();
  }

  protected chooseCompany(code: string | null): void {
    this.stash();
    this.company.set(code);
    this.show(this.sets.get(code) ?? this.sets.get(null)!);
  }

  /** Starts this company's own version of the role from the role as it is. */
  protected customize(): void {
    const code = this.company();
    if (!code) return;
    this.sets.set(code, structuredClone({ ...this.sets.get(null)!, privileges: this.sets.get(null)!.privileges }));
    this.overrides.update((o) => [...o, code]);
    this.show(this.sets.get(code)!);
  }

  /** The company gets the role as it is again. */
  protected dropOverride(): void {
    const code = this.company();
    if (!code) return;
    this.sets.delete(code);
    this.overrides.update((o) => o.filter((c) => c !== code));
    this.show(this.sets.get(null)!);
  }

  private payloadOf(contents: Contents): { privileges: PrivilegeRow[]; grants: GrantRow[]; menus: string[] } {
    return {
      privileges: privilegesOf(contents.permissions, contents.privileges),
      grants: [...contents.otherGrants, ...contents.screenGrants],
      menus: contents.menus,
    };
  }

  protected codes(screens: ScreenNode[]): string[] {
    return screens.map((s) => s.code);
  }

  /** The picked codes still shown (a search may hide some). */
  protected picked(picked: Set<string>, shown: ScreenNode[]): string[] {
    return shown.filter((s) => picked.has(s.code)).map((s) => s.code);
  }

  protected toggle(list: typeof this.pickedAvailable, code: string): void {
    const next = new Set(list());
    if (!next.delete(code)) next.add(code);
    list.set(next);
  }

  /** Adds screens to the role; each starts with "open the screen" (or its first action). */
  protected add(codes: string[]): void {
    const added = codes.filter((c) => !this.selected().includes(c));
    if (!added.length) return;
    this.selected.update((s) => [...s, ...added]);
    this.permissions.update((p) => {
      const next = { ...p };
      for (const code of added) {
        const screen = this.byCode.get(code)!;
        if (!next[code]?.length && screen.actions.length) {
          next[code] = [screen.actions.includes('VIEW') ? 'VIEW' : screen.actions[0]];
        }
      }
      return next;
    });
    this.pickedAvailable.set(new Set());
  }

  /** Removes screens from the role with every permission on them and on their sub-resources. */
  protected remove(codes: string[]): void {
    if (!codes.length) return;
    this.selected.update((s) => s.filter((c) => !codes.includes(c)));
    const dropped = codes.flatMap((c) => (this.byCode.get(c) ? screenCodes(this.byCode.get(c)!) : [c]));
    this.screenGrants.update((g) => g.filter((x) => !x.resourceCode || !dropped.includes(x.resourceCode)));
    this.permissions.update((p) => {
      const next = { ...p };
      for (const code of codes) {
        for (const node of [this.byCode.get(code)!, ...(this.byCode.get(code)?.children ?? [])]) {
          delete next[node.code];
        }
      }
      return next;
    });
    this.pickedChosen.set(new Set());
  }

  protected grant(change: { resource: string; action: string; granted: boolean }): void {
    this.permissions.update((p) => {
      const current = p[change.resource] ?? [];
      const actions = change.granted ? [...new Set([...current, change.action])] : current.filter((a) => a !== change.action);
      return { ...p, [change.resource]: actions };
    });
  }

  /** Saves like the save button, but sends the change for approval at once. */
  protected submitForApproval(): void {
    this.submitNext = true;
    (document.getElementById('dc-role-form') as HTMLFormElement | null)?.requestSubmit();
  }

  protected async save(): Promise<void> {
    const submit = this.submitNext;
    this.submitNext = false;
    this.form.markAllAsTouched();
    this.errors.set([]);
    if (this.form.invalid) {
      this.errors.set([this.i18n.translate('roles.fillRequired')]);
      return;
    }
    this.stash();
    const own = this.payloadOf(this.sets.get(null)!);
    const value = this.form.getRawValue();
    const payload = {
      nameAr: value.nameAr.trim(),
      nameEn: value.nameEn.trim(),
      isActive: value.isActive,
      appCode: value.appCode,
      // The role's business apps (empty for none, so the server does not keep the old ones); its entity follows the apps.
      businessApps: value.businessApps,
      entityCode: value.businessApps.length ? null : ((this.data.role?.['entityCode'] as string | null) ?? ''),
      referenceCode: value.referenceCode?.trim() || null,
      ...own,
      // Companies where the role differs, each with its own contents.
      overrides: this.overrides()
        .filter((code) => this.sets.has(code))
        .map((code) => ({ companyCode: code, ...this.payloadOf(this.sets.get(code)!) })),
    };
    try {
      await this.drafts.addChange('apps.role', value.code.trim(), this.data.role ? 'UPDATE' : 'CREATE', payload, { submit });
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  private filter(screens: ScreenNode[], search: string): ScreenNode[] {
    const term = search.trim().toLowerCase();
    return term ? screens.filter((s) => `${s.code} ${s.name.ar} ${s.name.en} ${s.place.ar} ${s.place.en}`.toLowerCase().includes(term)) : screens;
  }
}
