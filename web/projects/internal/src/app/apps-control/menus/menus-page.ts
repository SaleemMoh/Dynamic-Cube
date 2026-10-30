import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import {
  AppsControlApi,
  ConfigRow,
  I18nService,
  LocalizePipe,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { catchError, firstValueFrom, forkJoin, of } from 'rxjs';
import { ConfigEditor, ConfigEditorData } from '../config/config-editor';
import { CONFIG_SECTIONS, ConfigType } from '../config/config-types';
import { ConfirmData, ConfirmDialog } from '../confirm-dialog';
import { DraftsService } from '../drafts.service';
import { DetailsDialog, detailsOf } from '../master/details-dialog';
import { PrivilegesService } from '../privileges.service';
import { ExportButton } from '../change-sets/export-button';
import { JsonImportButton } from '../change-sets/json-import-button';

const RESOURCE = 'apps-control.menus';
const DEFAULT_TREE = 'INTERNAL';

const types = CONFIG_SECTIONS.find((s) => s.path === 'menus')!.types;
const MENU: ConfigType = types.find((t) => t.entityType === 'apps.menu')!;
const GROUP: ConfigType = types.find((t) => t.entityType === 'apps.menu-group')!;

/** A node of the tree: a group (first level) or an item, with the items under it. */
export interface MenuTreeNode {
  row: ConfigRow;
  group: boolean;
  children: MenuTreeNode[];
}

/** A tree to edit: a channel's (the platform's screens and app items) or a connected app's. */
interface TreeChoice {
  code: string;
  name: { ar: string; en: string };
  connected: boolean;
}

const order = (r: ConfigRow) => Number(r['displayOrder'] ?? 0);
const byOrder = (a: ConfigRow, b: ConfigRow) => order(a) - order(b) || a.code.localeCompare(b.code);

/** Groups with their items, items under their parents at any depth; items without a group or parent come last. */
export function buildMenuTree(groups: ConfigRow[], menus: ConfigRow[]): MenuTreeNode[] {
  const codes = new Set(menus.map((m) => m.code));
  const itemsUnder = (parent: string): MenuTreeNode[] =>
    menus
      .filter((m) => m['parentCode'] === parent)
      .sort(byOrder)
      .map((m) => ({ row: m, group: false, children: itemsUnder(m.code) }));
  const tops = (group: string | null) =>
    menus
      .filter(
        (m) =>
          !(m['parentCode'] && codes.has(m['parentCode'] as string)) &&
          ((m['groupCode'] as string | null) ?? null) === group,
      )
      .sort(byOrder)
      .map((m) => ({ row: m, group: false, children: itemsUnder(m.code) }));
  const known = new Set(groups.map((g) => g.code));
  const loose = menus
    .filter(
      (m) =>
        !(m['parentCode'] && codes.has(m['parentCode'] as string)) &&
        (!m['groupCode'] || !known.has(m['groupCode'] as string)),
    )
    .sort(byOrder)
    .map((m) => ({ row: m, group: false, children: itemsUnder(m.code) }));
  return [
    ...[...groups].sort(byOrder).map((g) => ({ row: g, group: true, children: tops(g.code) })),
    ...loose,
  ];
}

/**
 * Menus as trees, like the side menu shows them: pick a channel (the platform's screens, with the items Dynamic Cube apps
 * add) or a connected app (its own tree), then add groups, items under items at any depth, reorder and edit.
 */
@Component({
  selector: 'dc-menus-page',
  imports: [
    ExportButton,
    JsonImportButton,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTooltipModule,
    NgTemplateOutlet,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header dc-page__header--actions">
      <div>
        <h1>{{ 'menus.title' | dcTranslate }}</h1>
        <p>{{ 'menus.hint' | dcTranslate }}</p>
      </div>
      <dc-export-button entityType="apps.menu" [title]="{ ar: 'القوائم', en: 'Menus' }" />
      <dc-json-import-button
        [title]="{ ar: 'القوائم', en: 'Menus' }"
        [target]="{ entityType: 'apps.menu', key: 'code' }"
      />
    </header>
    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (error of errors(); track $index) {
          <li dir="auto">{{ error }}</li>
        }
      </ul>
    }
    <section class="dc-panel dc-menu-panel" data-testid="menu-tree">
      <div class="dc-tree__toolbar">
        <mat-form-field subscriptSizing="dynamic" class="dc-tree__choice">
          <mat-label>{{ 'menus.tree' | dcTranslate }}</mat-label>
          <mat-select
            [value]="tree()"
            (selectionChange)="choose($event.value)"
            data-testid="menu-tree-choice"
          >
            <mat-optgroup [label]="'menus.channels' | dcTranslate">
              @for (choice of channels(); track choice.code) {
                <mat-option [value]="choice.code">{{ choice.name | dcLocalize }}</mat-option>
              }
            </mat-optgroup>
            @if (connectedApps().length) {
              <mat-optgroup [label]="'menus.connectedApps' | dcTranslate">
                @for (choice of connectedApps(); track choice.code) {
                  <mat-option [value]="choice.code">{{ choice.name | dcLocalize }}</mat-option>
                }
              </mat-optgroup>
            }
          </mat-select>
        </mat-form-field>
        <span class="dc-spacer"></span>
        <button
          matButton="outlined"
          type="button"
          (click)="expandAll()"
          data-testid="menu-expand-all"
        >
          <mat-icon fontSet="material-symbols-outlined">unfold_more</mat-icon
          >{{ 'menus.expandAll' | dcTranslate }}
        </button>
        <button
          matButton="outlined"
          type="button"
          (click)="collapseAll()"
          data-testid="menu-collapse-all"
        >
          <mat-icon fontSet="material-symbols-outlined">unfold_less</mat-icon
          >{{ 'menus.collapseAll' | dcTranslate }}
        </button>
        @if (privileges.can(resource, 'CREATE')) {
          <button
            matButton="filled"
            type="button"
            (click)="addGroup()"
            data-testid="menu-add-group"
          >
            <mat-icon fontSet="material-symbols-outlined">create_new_folder</mat-icon
            >{{ 'menus.addGroup' | dcTranslate }}
          </button>
        }
      </div>
      @if (data.isLoading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (nodes().length) {
        <p class="dc-tree__legend dc-muted">
          <mat-icon fontSet="material-symbols-outlined">lock</mat-icon
          >{{ 'menus.lockedLegend' | dcTranslate }}
        </p>
        <ul class="dc-tree" role="tree">
          @for (node of nodes(); track node.row.code; let i = $index) {
            <ng-container
              *ngTemplateOutlet="
                branch;
                context: { $implicit: node, siblings: nodes(), index: i, depth: 0 }
              "
            />
          }
        </ul>
      } @else if (!data.isLoading()) {
        <p class="dc-empty">{{ 'menus.noTree' | dcTranslate }}</p>
      }
    </section>

    <ng-template #branch let-node let-siblings="siblings" let-index="index" let-depth="depth">
      <li
        role="treeitem"
        aria-selected="false"
        [attr.aria-expanded]="node.children.length ? isOpen(node) : null"
      >
        <div
          class="dc-tree__row"
          [style.padding-inline-start.px]="depth * 24"
          [attr.data-testid]="'menu-node-' + node.row.code"
        >
          @if (node.children.length) {
            <button
              matIconButton
              type="button"
              class="dc-tree__toggle"
              (click)="toggle(node)"
              [attr.aria-label]="node.row.code"
            >
              <mat-icon fontSet="material-symbols-outlined">{{
                isOpen(node) ? 'expand_more' : 'chevron_right'
              }}</mat-icon>
            </button>
          } @else {
            <span class="dc-tree__toggle"></span>
          }
          <mat-icon
            fontSet="material-symbols-outlined"
            class="dc-tree__icon"
            [class.dc-tree__icon--folder]="isFolder(node)"
            >{{
              isFolder(node)
                ? isOpen(node) && node.children.length
                  ? 'folder_open'
                  : 'folder'
                : node.row['icon'] || 'description'
            }}</mat-icon
          >
          <span class="mono dc-tree__code">{{ node.row.code }}</span>
          <span class="dc-tree__name">{{
            { ar: node.row.nameAr, en: node.row.nameEn } | dcLocalize
          }}</span>
          @if (node.row['businessApp'] && !current()?.connected) {
            <span class="dc-pill" [matTooltip]="'roles.businessApp' | dcTranslate">{{
              appName(node.row['businessApp']) | dcLocalize
            }}</span>
          }
          @if (!node.row.isActive || node.row['isVisible'] === false) {
            <span class="dc-pill">{{
              (node.row.isActive ? 'menus.hidden' : 'config.inactive') | dcTranslate
            }}</span>
          }
          @if (lockReason(node); as reason) {
            <mat-icon
              fontSet="material-symbols-outlined"
              class="dc-tree__lock"
              [matTooltip]="reason | dcTranslate"
              [attr.aria-label]="reason | dcTranslate"
              [attr.data-testid]="'menu-locked-' + node.row.code"
              >lock</mat-icon
            >
          }
          <span class="dc-tree__actions">
            @if (privileges.can(resource, 'CREATE')) {
              <button
                matIconButton
                type="button"
                (click)="addUnder(node)"
                [matTooltip]="'menus.addChild' | dcTranslate"
                [attr.data-testid]="'menu-add-under-' + node.row.code"
              >
                <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
              </button>
            }
            @if (editable(node)) {
              <button
                matIconButton
                type="button"
                (click)="move(siblings, index, -1)"
                [disabled]="index === 0 || !canMove(siblings)"
                [matTooltip]="'menus.moveUp' | dcTranslate"
                [attr.data-testid]="'menu-up-' + node.row.code"
              >
                <mat-icon fontSet="material-symbols-outlined">arrow_upward</mat-icon>
              </button>
              <button
                matIconButton
                type="button"
                (click)="move(siblings, index, 1)"
                [disabled]="index === siblings.length - 1 || !canMove(siblings)"
                [matTooltip]="'menus.moveDown' | dcTranslate"
                [attr.data-testid]="'menu-down-' + node.row.code"
              >
                <mat-icon fontSet="material-symbols-outlined">arrow_downward</mat-icon>
              </button>
              <button
                matIconButton
                type="button"
                (click)="edit(node)"
                [matTooltip]="'config.edit' | dcTranslate"
                [attr.data-testid]="'menu-edit-' + node.row.code"
              >
                <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>
              </button>
              @if (privileges.can(resource, 'DELETE') && !node.children.length) {
                <button
                  matIconButton
                  type="button"
                  (click)="remove(node)"
                  [matTooltip]="'config.delete' | dcTranslate"
                  [attr.data-testid]="'menu-delete-' + node.row.code"
                >
                  <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                </button>
              }
            } @else {
              <button
                matIconButton
                type="button"
                (click)="view(node)"
                [matTooltip]="'config.view' | dcTranslate"
              >
                <mat-icon fontSet="material-symbols-outlined">visibility</mat-icon>
              </button>
            }
          </span>
        </div>
        @if (node.children.length && isOpen(node)) {
          <ul role="group">
            @for (child of node.children; track child.row.code; let i = $index) {
              <ng-container
                *ngTemplateOutlet="
                  branch;
                  context: { $implicit: child, siblings: node.children, index: i, depth: depth + 1 }
                "
              />
            }
          </ul>
        }
      </li>
    </ng-template>
  `,
  styles: `
    .dc-menu-panel {
      padding: 16px 20px 20px;
    }
    .dc-tree__legend {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0 0 8px;
      font-size: 13px;
    }
    .dc-tree__legend mat-icon,
    .dc-tree__lock {
      font-size: 16px;
      width: 16px;
      height: 16px;
      color: var(--dga-gray-500);
    }
    .dc-tree__toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
      margin-bottom: 12px;
    }
    .dc-tree__choice {
      min-width: 18rem;
    }
    .dc-spacer {
      flex: 1;
    }
    .dc-tree,
    .dc-tree ul {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .dc-tree {
      padding: 8px 12px;
      border-radius: var(--shell-radius);
      background: var(--dga-gray-50);
    }
    .dc-tree__row {
      display: flex;
      align-items: center;
      gap: 6px;
      min-height: 36px;
      border-radius: 6px;
    }
    .dc-tree__row:hover {
      background: var(--dga-gray-100);
    }
    .dc-tree__toggle {
      display: inline-flex;
      width: 32px;
      flex: none;
    }
    .dc-tree__icon {
      color: var(--dga-gray-500);
      font-size: 20px;
      width: 20px;
      height: 20px;
    }
    .dc-tree__icon--folder {
      color: var(--dynamic-cube-gold-700);
    }
    .dc-tree__code {
      color: var(--dga-gray-600);
      font-size: 12px;
    }
    .dc-tree__name {
      font-weight: 500;
    }
    .dc-tree__actions {
      display: inline-flex;
      margin-inline-start: auto;
      opacity: 0.35;
    }
    .dc-tree__row:hover .dc-tree__actions,
    .dc-tree__row:focus-within .dc-tree__actions {
      opacity: 1;
    }
  `,
})
export class MenusPage {
  /** Route parameter: the tree (a channel or a connected app; the internal system when absent). */
  readonly code = input<string | undefined>(undefined);

  private readonly api = inject(AppsControlApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly drafts = inject(DraftsService);
  private readonly i18n = inject(I18nService);
  protected readonly privileges = inject(PrivilegesService);
  protected readonly resource = RESOURCE;

  protected readonly errors = signal<string[]>([]);
  protected readonly tree = computed(() => this.code() ?? DEFAULT_TREE);
  protected readonly expanded = signal(new Set<string>());

  private readonly choices = rxResource({
    stream: () =>
      forkJoin({
        channels: this.api.list('apps.app').pipe(catchError(() => of([]))),
        apps: this.api.list('apps.business-app').pipe(catchError(() => of([]))),
      }),
  });
  protected readonly channels = computed<TreeChoice[]>(() =>
    (this.choices.value()?.channels ?? []).map((c) => ({
      code: c.code,
      name: { ar: c.nameAr, en: c.nameEn },
      connected: false,
    })),
  );
  protected readonly connectedApps = computed<TreeChoice[]>(() =>
    (this.choices.value()?.apps ?? [])
      .filter((a) => a['kind'] === 'CONNECTED')
      .map((a) => ({ code: a.code, name: { ar: a.nameAr, en: a.nameEn }, connected: true })),
  );
  protected readonly current = computed(
    () => [...this.channels(), ...this.connectedApps()].find((c) => c.code === this.tree()) ?? null,
  );

  protected readonly data = rxResource({
    params: () => this.tree(),
    stream: ({ params }) =>
      forkJoin({
        groups: this.api.list('apps.menu-group', params),
        menus: this.api.list('apps.menu', params),
      }),
  });
  protected readonly nodes = computed(() =>
    this.data.hasValue() ? buildMenuTree(this.data.value().groups, this.data.value().menus) : [],
  );

  constructor() {
    // Each tree opens with its groups expanded, as in the side menu.
    let shown: string | null = null;
    effect(() => {
      const nodes = this.nodes();
      const tree = this.tree();
      if (!this.data.hasValue() || shown === tree) return;
      shown = tree;
      untracked(() =>
        this.expanded.set(new Set(nodes.filter((n) => n.group).map((n) => n.row.code))),
      );
    });
  }

  protected isOpen(node: MenuTreeNode): boolean {
    return this.expanded().has(node.row.code);
  }

  protected isFolder(node: MenuTreeNode): boolean {
    return node.group || node.row['itemType'] === 'GROUP';
  }

  protected toggle(node: MenuTreeNode): void {
    const next = new Set(this.expanded());
    if (next.has(node.row.code)) next.delete(node.row.code);
    else next.add(node.row.code);
    this.expanded.set(next);
  }

  protected expandAll(): void {
    const all = new Set<string>();
    const walk = (nodes: MenuTreeNode[]) =>
      nodes.forEach((n) => {
        if (n.children.length) all.add(n.row.code);
        walk(n.children);
      });
    walk(this.nodes());
    this.expanded.set(all);
  }

  protected collapseAll(): void {
    this.expanded.set(new Set());
  }

  protected choose(code: string): void {
    void this.router.navigate(['/apps-control/menus', code]);
  }

  protected appName(code: unknown): { ar: string; en: string } {
    const app = (this.choices.value()?.apps ?? []).find((a) => a.code === code);
    return app ? { ar: app.nameAr, en: app.nameEn } : { ar: String(code), en: String(code) };
  }

  protected editable(node: MenuTreeNode): boolean {
    return node.row.owner !== 'SYSTEM' && this.privileges.can(RESOURCE, 'EDIT');
  }

  /** Why a row cannot be changed here (a translation key), or null when it can. */
  protected lockReason(node: MenuTreeNode): string | null {
    if (node.row.owner === 'SYSTEM') return 'menus.lockedSystem';
    return this.privileges.can(RESOURCE, 'EDIT') ? null : 'menus.lockedNoPrivilege';
  }

  /** Product rows keep their order; siblings can be reordered when none of them is the product's. */
  protected canMove(siblings: MenuTreeNode[]): boolean {
    return siblings.every((s) => s.row.owner !== 'SYSTEM');
  }

  protected view(node: MenuTreeNode): void {
    this.dialog.open(DetailsDialog, {
      data: detailsOf(node.group ? GROUP : MENU, node.row),
      width: '40rem',
      maxWidth: '95vw',
    });
  }

  protected addGroup(): void {
    const next =
      Math.max(
        0,
        ...this.nodes()
          .filter((n) => n.group)
          .map((n) => order(n.row)),
      ) + 10;
    void this.open(GROUP, null, { ...this.treeDefaults(), displayOrder: next });
  }

  protected addUnder(node: MenuTreeNode): void {
    const next = Math.max(0, ...node.children.map((c) => order(c.row))) + 10;
    const placement = node.group
      ? { groupCode: node.row.code }
      : { parentCode: node.row.code, groupCode: (node.row['groupCode'] as string | null) ?? null };
    void this.open(MENU, null, {
      ...this.treeDefaults(),
      ...placement,
      itemType: 'SCREEN',
      displayOrder: next,
    });
  }

  protected async edit(node: MenuTreeNode): Promise<void> {
    const type = node.group ? GROUP : MENU;
    const full = await this.full(type, node.row.code);
    if (full) await this.open(type, full);
  }

  /** Swaps an item with its neighbour; the siblings are numbered 10, 20, 30… so the order is unambiguous. */
  protected async move(siblings: MenuTreeNode[], index: number, step: number): Promise<void> {
    const ordered = [...siblings];
    [ordered[index], ordered[index + step]] = [ordered[index + step], ordered[index]];
    this.errors.set([]);
    try {
      for (const [i, node] of ordered.entries()) {
        const wanted = (i + 1) * 10;
        if (order(node.row) === wanted) continue;
        const type = node.group ? GROUP : MENU;
        const full = await this.full(type, node.row.code);
        if (!full) return;
        await this.drafts.addChange(type.entityType, node.row.code, 'UPDATE', {
          ...full,
          displayOrder: wanted,
        });
      }
      this.data.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  protected async remove(node: MenuTreeNode): Promise<void> {
    const code = node.row.code;
    const confirmed = await firstValueFrom(
      this.dialog
        .open<ConfirmDialog, ConfirmData, string>(ConfirmDialog, {
          data: {
            title: `${this.i18n.translate('config.delete')} ${code}`,
            message: this.i18n.translate('config.deleteHint'),
            confirmLabel: this.i18n.translate('config.delete'),
            danger: true,
          },
        })
        .afterClosed(),
    );
    if (confirmed === undefined) return;
    this.errors.set([]);
    try {
      await this.drafts.addChange((node.group ? GROUP : MENU).entityType, code, 'DELETE', null);
      this.data.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  /** A new row's place: the channel, or for a connected app no channel and the app itself. */
  private treeDefaults(): Record<string, unknown> {
    const tree = this.current();
    return tree?.connected
      ? { appCode: null, businessApps: [tree.code] }
      : { appCode: this.tree() };
  }

  private async full(type: ConfigType, code: string): Promise<ConfigRow | null> {
    try {
      return await firstValueFrom(this.api.get(type.entityType, code));
    } catch (error) {
      this.errors.set(problemMessages(error));
      return null;
    }
  }

  private async open(
    type: ConfigType,
    row: ConfigRow | null,
    defaults?: Record<string, unknown>,
  ): Promise<void> {
    const saved = await firstValueFrom(
      this.dialog
        .open<ConfigEditor, ConfigEditorData, boolean>(ConfigEditor, {
          data: { type, row, defaults },
          autoFocus: 'first-tabbable',
          width: '52rem',
          maxWidth: '95vw',
        })
        .afterClosed(),
    );
    if (saved) this.data.reload();
  }
}
