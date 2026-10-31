import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  model,
  signal,
  untracked,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  AppsControlApi,
  ConfigRow,
  I18nService,
  LocalizePipe,
  LocalizedText,
  TranslatePipe,
} from '@dynamic-cube/shared';
import { catchError, forkJoin, of } from 'rxjs';
import { MenuTreeNode, buildMenuTree } from '../menus/menu-tree';
import { ScreenList, actionLabel, screenLists } from '../roles/role-models';
import { GrantValues } from '../roles/screen-data';
import {
  ALL,
  HeldRole,
  PersonalGrant,
  PersonalPrivilege,
  RoleContents,
  RoleGiven,
  roleAccess,
  setPersonalActions,
  setPersonalDenied,
  setPersonalGrant,
  setPersonalPeriod,
} from './access-model';

/** What the person's roles contain, to show what they already give on each screen. */
export interface RoleSource {
  held: HeldRole[];
  roles: Map<string, RoleContents>;
  /** Menu item code → the screen it opens. */
  menuScreens: Map<string, string>;
}

/** A tree to load: a Dynamic Cube channel (internal system, portal, field app) or an app with its own menus. */
interface AppChoice {
  code: string;
  name: LocalizedText;
}

/**
 * Personal privileges on an app's menu tree (ADR-0036): pick the app and the company, then tick the actions of each
 * screen and, under Data, the values of its lists this person alone gets. A summary lists every personal privilege.
 */
@Component({
  selector: 'dc-personal-tree',
  imports: [
    NgTemplateOutlet,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTooltipModule,
    GrantValues,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-pt__bar">
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'access.app' | dcTranslate }}</mat-label>
        <mat-select
          [value]="app()"
          (selectionChange)="app.set($event.value)"
          data-testid="personal-app"
        >
          <mat-optgroup [label]="'access.dcApps' | dcTranslate">
            @for (c of channels(); track c.code) {
              <mat-option [value]="c.code">{{ c.name | dcLocalize }}</mat-option>
            }
          </mat-optgroup>
          @if (managed().length) {
            <mat-optgroup [label]="'access.managedApps' | dcTranslate">
              @for (c of managed(); track c.code) {
                <mat-option [value]="c.code">{{ c.name | dcLocalize }}</mat-option>
              }
            </mat-optgroup>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'access.personalCompany' | dcTranslate }}</mat-label>
        <mat-select
          [value]="company()"
          (selectionChange)="company.set($event.value)"
          data-testid="personal-company"
        >
          <mat-option [value]="all">{{ 'access.allCompanies' | dcTranslate }}</mat-option>
          @for (c of companies(); track c.code) {
            <mat-option [value]="c.code">{{ name(c) | dcLocalize }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>

    <p class="dc-pt__legend dc-muted">
      <span class="dc-pt__key dc-pt__key--role"></span>{{ 'access.legendRole' | dcTranslate }}
      <span class="dc-pt__key dc-pt__key--personal"></span
      >{{ 'access.legendPersonal' | dcTranslate }}
      <span class="dc-pt__key dc-pt__key--denied"></span>{{ 'access.legendDenied' | dcTranslate }}
    </p>
    @if (tree.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @if (tree.hasValue()) {
      @if (nodes().length) {
        <ul class="dc-pt" role="tree" data-testid="personal-tree">
          @for (node of nodes(); track node.row.code) {
            <ng-container *ngTemplateOutlet="item; context: { $implicit: node, depth: 0 }" />
          }
        </ul>
      } @else {
        <p class="dc-muted">{{ 'access.emptyTree' | dcTranslate }}</p>
      }
    }

    <ng-template #item let-node let-depth="depth">
      <li
        role="treeitem"
        aria-selected="false"
        [attr.aria-expanded]="node.children.length ? isOpen(node) : null"
        [attr.data-node]="node.row.code"
      >
        <div
          class="dc-pt__row"
          [class.dc-pt__row--group]="node.group"
          [class.dc-pt__row--given]="givenUnder(node) > 0"
          [style.padding-inline-start.px]="8 + depth * 22"
          [style.--dc-pt-indent]="depth * 22 + 'px'"
        >
          @if (node.children.length) {
            <button
              matIconButton
              type="button"
              class="dc-pt__toggle"
              (click)="toggle(node)"
              [attr.aria-label]="name(node.row) | dcLocalize"
            >
              <mat-icon fontSet="material-symbols-outlined">{{
                isOpen(node) ? 'expand_more' : 'chevron_right'
              }}</mat-icon>
            </button>
          } @else {
            <span class="dc-pt__toggle"></span>
          }
          <mat-icon fontSet="material-symbols-outlined" class="dc-pt__icon">{{
            node.row['icon'] || (node.group ? 'folder' : 'description')
          }}</mat-icon>
          <span class="dc-pt__name">
            {{ name(node.row) | dcLocalize }}
            @if (!isOpen(node) && givenUnder(node); as count) {
              <span class="dc-pt__count">{{ count }}</span>
            }
          </span>
          @if (screenOf(node); as screen) {
            <span class="dc-pt__actions" [attr.data-screen]="screen">
              @for (action of actionsOf(screen); track action) {
                @let state = stateOf(screen, action);
                <mat-checkbox
                  [checked]="state === 'role' || state === 'personal'"
                  [class]="'dc-pt__action dc-pt__action--' + state"
                  (change)="toggleAction(screen, action)"
                  [disabled]="readonly()"
                  [matTooltip]="tipOf(screen, action, state)"
                  [attr.data-testid]="'personal-action-' + action"
                  [attr.data-state]="state"
                  >{{ actionName(action) | dcLocalize }}</mat-checkbox
                >
              }
              @if (listsOf(screen).length) {
                <button
                  matButton
                  type="button"
                  class="dc-pt__data-button"
                  [class.dc-pt__data-button--on]="grantsOn(screen) > 0"
                  (click)="toggleData(screen)"
                  data-testid="personal-data"
                >
                  <mat-icon fontSet="material-symbols-outlined">dataset</mat-icon>
                  {{ 'access.data' | dcTranslate }} ({{ grantsOn(screen) }}/{{
                    listsOf(screen).length
                  }})
                </button>
              }
            </span>
          }
        </div>
        @if (screenOf(node); as screen) {
          @if (dataOpen().has(screen)) {
            <div
              class="dc-pt__data"
              [style.margin-inline-start.px]="40 + depth * 22"
              data-testid="personal-data-panel"
            >
              <p class="dc-muted">{{ 'access.dataHint' | dcTranslate }}</p>
              @for (list of listsOf(screen); track list.lovCode + list.resourceCode) {
                <div class="dc-pt__list" [attr.data-list]="list.lovCode">
                  <span class="dc-pt__list-name">
                    <strong>{{ list.name | dcLocalize }}</strong>
                    <span class="dc-muted mono" dir="ltr">{{ list.lovCode }}</span>
                  </span>
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-select
                      [value]="grantOf(list)?.grantType ?? ''"
                      (selectionChange)="setGrantType(list, $event.value)"
                      [disabled]="readonly()"
                      [attr.data-testid]="'personal-grant-' + list.lovCode"
                    >
                      <mat-option value="">{{ 'access.fromRoles' | dcTranslate }}</mat-option>
                      <mat-option value="ALL">{{ 'access.allValues' | dcTranslate }}</mat-option>
                      <mat-option value="VALUES">{{
                        'access.someValues' | dcTranslate
                      }}</mat-option>
                    </mat-select>
                  </mat-form-field>
                  @if (grantOf(list)?.grantType === 'VALUES') {
                    <dc-grant-values
                      [lovCode]="list.lovCode"
                      [value]="grantOf(list)!.grantedValues"
                      [readonly]="readonly()"
                      (valueChange)="setValues(list, $event)"
                    />
                  } @else {
                    <span></span>
                  }
                </div>
              }
            </div>
          }
        }
        @if (node.children.length && isOpen(node)) {
          <ul role="group">
            @for (child of node.children; track child.row.code) {
              <ng-container
                *ngTemplateOutlet="item; context: { $implicit: child, depth: depth + 1 }"
              />
            }
          </ul>
        }
      </li>
    </ng-template>

    <h4 class="dc-pt__summary-title">{{ 'access.summary' | dcTranslate }}</h4>
    <p class="dc-muted">{{ 'access.summaryHint' | dcTranslate }}</p>
    @if (value().length) {
      <table class="dc-pt__summary" data-testid="personal-summary">
        <thead>
          <tr>
            <th>{{ 'access.company' | dcTranslate }}</th>
            <th>{{ 'access.screen' | dcTranslate }}</th>
            <th>{{ 'access.actions' | dcTranslate }}</th>
            <th>{{ 'access.removed' | dcTranslate }}</th>
            <th>{{ 'access.data' | dcTranslate }}</th>
            <th>{{ 'access.from' | dcTranslate }}</th>
            <th>{{ 'access.to' | dcTranslate }}</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (p of sorted(); track p.companyCode + '|' + p.resourceCode) {
            <tr>
              <td>{{ companyName(p.companyCode) | dcLocalize }}</td>
              <td>
                {{ screenName(p.resourceCode) | dcLocalize }}
                <span class="dc-muted mono" dir="ltr">{{ p.resourceCode }}</span>
              </td>
              <td>
                @for (a of p.actions; track a; let last = $last) {
                  {{ actionName(a) | dcLocalize }}{{ last ? '' : '، ' }}
                }
              </td>
              <td class="dc-pt__removed">
                @for (a of p.deniedActions; track a; let last = $last) {
                  {{ actionName(a) | dcLocalize }}{{ last ? '' : '، ' }}
                }
              </td>
              <td>
                @for (g of p.grants; track g.lovCode; let last = $last) {
                  <span class="mono" dir="ltr">{{ g.lovCode }}</span
                  >:
                  {{
                    g.grantType === 'ALL'
                      ? ('access.allValues' | dcTranslate)
                      : g.grantedValues.join(', ')
                  }}{{ last ? '' : '؛ ' }}
                }
              </td>
              <td>
                <input
                  type="date"
                  class="dc-pt__date"
                  [value]="p.effectiveFrom ?? ''"
                  [disabled]="readonly()"
                  (change)="setPeriod(p, $any($event.target).value || null, p.effectiveTo)"
                  [attr.aria-label]="'access.from' | dcTranslate"
                  data-testid="personal-from"
                />
              </td>
              <td>
                <input
                  type="date"
                  class="dc-pt__date"
                  [value]="p.effectiveTo ?? ''"
                  [disabled]="readonly()"
                  (change)="setPeriod(p, p.effectiveFrom, $any($event.target).value || null)"
                  [attr.aria-label]="'access.to' | dcTranslate"
                  data-testid="personal-to"
                />
              </td>
              <td>
                @if (!readonly()) {
                  <button
                    matIconButton
                    type="button"
                    (click)="remove(p)"
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
    } @else {
      <p class="dc-muted">{{ 'access.none' | dcTranslate }}</p>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .dc-pt__bar {
      display: grid;
      grid-template-columns: minmax(14rem, 1fr) minmax(12rem, 1fr);
      gap: 12px;
      margin: 8px 0 12px;
    }
    .dc-pt,
    .dc-pt ul {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .dc-pt {
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius-lg, 8px);
      padding: 6px 0;
    }
    .dc-pt__row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 4px 6px;
      min-height: 40px;
      padding-inline-end: 8px;
    }
    .dc-pt__row:hover {
      background: var(--dga-gray-50);
    }
    .dc-pt__row--group > .dc-pt__name {
      font-weight: 600;
    }
    .dc-pt__toggle {
      display: inline-grid;
      place-items: center;
      width: 32px;
      height: 32px;
      flex: none;
    }
    .dc-pt__icon {
      flex: none;
      color: var(--dga-gray-500);
      font-size: 20px;
      width: 20px;
      height: 20px;
    }
    .dc-pt__name {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      min-width: 10rem;
    }
    /* Screens line their action ticks up in one column next to the names. */
    .dc-pt__row:has(.dc-pt__actions) > .dc-pt__name {
      flex: 0 0 calc(var(--dc-pt-name, 20rem) - var(--dc-pt-indent, 0px));
    }
    .dc-pt__count {
      min-width: 20px;
      padding: 0 6px;
      border-radius: 10px;
      background: var(--dga-primary-100, #fdf3d7);
      font-size: 12px;
      text-align: center;
    }
    .dc-pt__actions {
      display: inline-flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0 4px;
    }
    .dc-pt__data-button--on {
      font-weight: 600;
    }
    .dc-pt__legend {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 4px 8px;
      margin: 0 0 8px;
      font-size: 13px;
    }
    .dc-pt__key {
      display: inline-block;
      width: 14px;
      height: 14px;
      border-radius: 3px;
      margin-inline-start: 8px;
    }
    .dc-pt__key--role {
      background: var(--dga-gray-500, #6c737f);
    }
    .dc-pt__key--personal {
      background: var(--mat-sys-primary, #b08d3c);
    }
    .dc-pt__key--denied {
      border: 2px solid var(--dga-red-600, #d92d20);
    }
    /* Given by roles: grey tick; given to the person: the primary colour; removed: crossed out in red. */
    .dc-pt__action--role {
      --mat-checkbox-selected-icon-color: var(--dga-gray-500, #6c737f);
      --mat-checkbox-selected-focus-icon-color: var(--dga-gray-500, #6c737f);
      --mat-checkbox-selected-hover-icon-color: var(--dga-gray-500, #6c737f);
    }
    .dc-pt__action--denied {
      color: var(--dga-red-600, #d92d20);
      text-decoration: line-through;
      --mat-checkbox-unselected-icon-color: var(--dga-red-600, #d92d20);
    }
    .dc-pt__removed {
      color: var(--dga-red-600, #d92d20);
    }
    .dc-pt__date {
      max-width: 9.5rem;
      padding: 4px 6px;
      border: 1px solid var(--dga-gray-300);
      border-radius: 6px;
      font: inherit;
    }
    .dc-pt__data {
      display: grid;
      gap: 6px;
      margin: 0 12px 10px;
      padding: 10px 12px;
      border: 1px dashed var(--dga-gray-300);
      border-radius: 8px;
    }
    .dc-pt__data p {
      margin: 0;
      font-size: 13px;
    }
    .dc-pt__list {
      display: grid;
      grid-template-columns: minmax(10rem, 1fr) 12rem minmax(12rem, 1.4fr);
      gap: 10px;
      align-items: center;
    }
    .dc-pt__list-name {
      display: grid;
      gap: 2px;
    }
    .dc-pt__list-name span {
      font-size: 12px;
    }
    .dc-pt__summary-title {
      margin: 18px 0 2px;
    }
    .dc-pt__summary {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
    }
    .dc-pt__summary th,
    .dc-pt__summary td {
      padding: 6px 8px;
      border-bottom: 1px solid var(--dga-gray-100);
      text-align: start;
      vertical-align: top;
    }
    .dc-pt__summary th {
      color: var(--dga-gray-600);
      font-weight: 600;
    }
    @media (max-width: 900px) {
      .dc-pt__bar,
      .dc-pt__list {
        grid-template-columns: minmax(0, 1fr);
      }
    }
  `,
})
export class PersonalTree {
  /** The person's personal privileges in every company. */
  readonly value = model.required<PersonalPrivilege[]>();
  readonly companies = input<ConfigRow[]>([]);
  readonly resources = input<ConfigRow[]>([]);
  readonly lovs = input<ConfigRow[]>([]);
  readonly readonly = input(false);
  /** The person's roles, to show what they already give (and let them be removed for this person). */
  readonly roleSource = input<RoleSource | null>(null);

  private readonly api = inject(AppsControlApi);
  private readonly i18n = inject(I18nService);
  protected readonly all = ALL;
  protected readonly app = signal('INTERNAL');
  protected readonly company = signal(ALL);
  private readonly expanded = signal(new Set<string>());
  private readonly collapsed = signal(new Set<string>());
  protected readonly dataOpen = signal(new Set<string>());

  private readonly choices = rxResource({
    stream: () =>
      forkJoin({
        channels: this.api.list('apps.app').pipe(catchError(() => of([] as ConfigRow[]))),
        apps: this.api.list('apps.business-app').pipe(catchError(() => of([] as ConfigRow[]))),
      }),
  });
  /** The internal system, the portal and the field app. */
  protected readonly channels = computed<AppChoice[]>(() =>
    (this.choices.value()?.channels ?? []).map((c) => ({ code: c.code, name: this.name(c) })),
  );
  /** Other apps Dynamic Cube manages, with their own menus. */
  protected readonly managed = computed<AppChoice[]>(() =>
    (this.choices.value()?.apps ?? [])
      .filter((a) => a['kind'] === 'CONNECTED')
      .map((a) => ({ code: a.code, name: this.name(a) })),
  );

  protected readonly tree = rxResource({
    params: () => this.app(),
    stream: ({ params }) =>
      forkJoin({
        groups: this.api
          .list('apps.menu-group', params)
          .pipe(catchError(() => of([] as ConfigRow[]))),
        menus: this.api.list('apps.menu', params).pipe(catchError(() => of([] as ConfigRow[]))),
      }),
  });
  protected readonly nodes = computed(() => {
    const data = this.tree.value();
    return data ? buildMenuTree(data.groups, data.menus) : [];
  });

  private readonly resourcesByCode = computed(
    () => new Map(this.resources().map((r) => [r.code, r])),
  );
  /** Sub-resources of each resource (menus and actions inside a screen). */
  private readonly childrenOf = computed(() => {
    const map = new Map<string, ConfigRow[]>();
    for (const r of this.resources()) {
      const parent = r['parentCode'] as string | null;
      if (parent && parent !== r.code) map.set(parent, [...(map.get(parent) ?? []), r]);
    }
    return map;
  });

  constructor() {
    // The tree opens on the company where the person already has personal privileges (all companies otherwise).
    let placed = false;
    effect(() => {
      const first = this.value()[0];
      if (placed) return;
      placed = true;
      if (first) untracked(() => this.company.set(first.companyCode));
    });
    // A newly loaded tree opens at its groups; items under them stay as the user left them.
    effect(() => {
      const nodes = this.nodes();
      untracked(() => {
        const open = new Set(this.expanded());
        for (const node of nodes) if (node.group) open.add(node.row.code);
        this.expanded.set(open);
      });
    });
  }

  protected name(row: ConfigRow): LocalizedText {
    return { ar: row.nameAr, en: row.nameEn };
  }

  protected actionName(action: string): LocalizedText {
    return actionLabel(action);
  }

  protected companyName(code: string): LocalizedText {
    if (code === ALL)
      return {
        ar: this.i18n.translate('access.allCompanies', 'ar'),
        en: this.i18n.translate('access.allCompanies', 'en'),
      };
    const company = this.companies().find((c) => c.code === code);
    return company ? this.name(company) : { ar: code, en: code };
  }

  protected screenName(code: string): LocalizedText {
    const resource = this.resourcesByCode().get(code);
    return resource ? this.name(resource) : { ar: code, en: code };
  }

  /** The screen a menu item opens (none for groups and folders). */
  protected screenOf(node: MenuTreeNode): string | null {
    const code = node.group ? null : ((node.row['resourceCode'] as string | null) ?? null);
    return code && this.resourcesByCode().has(code) ? code : null;
  }

  protected actionsOf(screen: string): string[] {
    return (
      (this.resourcesByCode().get(screen)?.['availableActions'] as string[] | undefined) ?? ['VIEW']
    );
  }

  protected isOpen(node: MenuTreeNode): boolean {
    return node.group
      ? !this.collapsed().has(node.row.code)
      : this.expanded().has(node.row.code) && !this.collapsed().has(node.row.code);
  }

  protected toggle(node: MenuTreeNode): void {
    const open = this.isOpen(node);
    const code = node.row.code;
    this.collapsed.update((s) => {
      const next = new Set(s);
      if (open) next.add(code);
      else next.delete(code);
      return next;
    });
    if (!open) this.expanded.update((s) => new Set(s).add(code));
  }

  protected toggleData(screen: string): void {
    this.dataOpen.update((s) => {
      const next = new Set(s);
      if (next.has(screen)) next.delete(screen);
      else next.add(screen);
      return next;
    });
  }

  private privilegeOf(resource: string): PersonalPrivilege | undefined {
    return this.value().find(
      (p) => p.companyCode === this.company() && p.resourceCode === resource,
    );
  }

  /** What the roles give in the chosen company, per screen. */
  private readonly given = computed(() => {
    const source = this.roleSource();
    return source
      ? roleAccess(this.company(), source.held, source.roles, source.menuScreens)
      : new Map<string, RoleGiven>();
  });

  /** An action's state: given by roles, given to the person, removed from the person, or none. */
  protected stateOf(screen: string, action: string): 'role' | 'personal' | 'denied' | 'none' {
    const privilege = this.privilegeOf(screen);
    if (privilege?.deniedActions.includes(action)) return 'denied';
    if (this.given().get(screen)?.actions.has(action)) return 'role';
    return privilege?.actions.includes(action) ? 'personal' : 'none';
  }

  protected tipOf(screen: string, action: string, state: string): string {
    const roles = (this.given().get(screen)?.roles ?? [])
      .map((r) => this.i18n.localize({ ar: r.nameAr, en: r.nameEn }))
      .join('، ');
    switch (state) {
      case 'role':
        return `${this.i18n.translate('access.tipRole')} ${roles}`;
      case 'denied':
        return this.i18n.translate('access.tipDenied') + (roles ? ` (${roles})` : '');
      case 'personal':
        return this.i18n.translate('access.tipPersonal');
      default:
        return '';
    }
  }

  /** Roles' actions are removed for the person (or given back); other actions are given to the person (or taken back). */
  protected toggleAction(screen: string, action: string): void {
    const privilege = this.privilegeOf(screen);
    const fromRoles = this.given().get(screen)?.actions.has(action) ?? false;
    const denied = privilege?.deniedActions ?? [];
    if (denied.includes(action)) {
      this.value.set(
        setPersonalDenied(
          this.value(),
          this.company(),
          screen,
          denied.filter((a) => a !== action),
        ),
      );
    } else if (fromRoles) {
      this.value.set(setPersonalDenied(this.value(), this.company(), screen, [...denied, action]));
    } else {
      const current = privilege?.actions ?? [];
      const actions = current.includes(action)
        ? current.filter((a) => a !== action)
        : [...current, action];
      this.value.set(setPersonalActions(this.value(), this.company(), screen, actions));
    }
  }

  protected setPeriod(privilege: PersonalPrivilege, from: string | null, to: string | null): void {
    this.value.set(setPersonalPeriod(this.value(), privilege, from, to));
  }

  /** The lists shown on a screen: those bound to its fields or to its sub-resources'. */
  protected listsOf(screen: string): ScreenList[] {
    const resource = this.resourcesByCode().get(screen);
    if (!resource) return [];
    const children = (this.childrenOf().get(screen) ?? []).map((c) => ({
      code: c.code,
      name: this.name(c),
      place: this.name(c),
      actions: [],
      children: [],
    }));
    return screenLists(
      {
        code: screen,
        name: this.name(resource),
        place: this.name(resource),
        actions: [],
        children,
      },
      this.lovs(),
      [],
    );
  }

  /** Lists of a screen the person was given values of. */
  protected grantsOn(screen: string): number {
    return this.listsOf(screen).filter((l) => this.grantOf(l)).length;
  }

  protected grantOf(list: ScreenList): PersonalGrant | undefined {
    return this.privilegeOf(list.resourceCode)?.grants.find((g) => g.lovCode === list.lovCode);
  }

  protected setGrantType(list: ScreenList, type: '' | 'ALL' | 'VALUES'): void {
    const current = this.grantOf(list);
    this.value.set(
      setPersonalGrant(
        this.value(),
        this.company(),
        list.resourceCode,
        list.lovCode,
        type
          ? {
              grantType: type,
              grantedValues: type === 'VALUES' ? (current?.grantedValues ?? []) : [],
            }
          : null,
      ),
    );
  }

  protected setValues(list: ScreenList, values: string[]): void {
    this.value.set(
      setPersonalGrant(this.value(), this.company(), list.resourceCode, list.lovCode, {
        grantType: 'VALUES',
        grantedValues: values,
      }),
    );
  }

  /** Screens under a node (it included) with a personal privilege in the chosen company. */
  protected givenUnder(node: MenuTreeNode): number {
    const own = this.screenOf(node);
    const self = own && this.privilegeOf(own) ? 1 : 0;
    return self + node.children.reduce((sum, child) => sum + this.givenUnder(child), 0);
  }

  protected sorted(): PersonalPrivilege[] {
    return [...this.value()].sort(
      (a, b) =>
        a.companyCode.localeCompare(b.companyCode) || a.resourceCode.localeCompare(b.resourceCode),
    );
  }

  protected remove(privilege: PersonalPrivilege): void {
    this.value.set(this.value().filter((p) => p !== privilege));
  }
}
