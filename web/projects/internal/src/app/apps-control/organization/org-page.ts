import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppsControlApi, ConfigRow, I18nService, LocalizePipe, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { ExportButton } from '../change-sets/export-button';
import { JsonImportButton } from '../change-sets/json-import-button';
import { ConfigEditor, ConfigEditorData } from '../config/config-editor';
import { ConfigTable } from '../config/config-table';
import { CONFIG_SECTIONS } from '../config/config-types';
import { ConfirmData, ConfirmDialog } from '../confirm-dialog';
import { DraftsService } from '../drafts.service';
import { PrivilegesService } from '../privileges.service';
import { buildOrgTree, matchingCodes, pathTo } from './org-tree';

const RESOURCE = 'apps-control.organization';

/**
 * The organization structure as a tree: units under their parents, expanded and collapsed, searched in place; the chosen
 * unit's details beside it with adding a sub-unit, editing (including moving it under another unit) and deleting. Unit
 * types and positions are tabs of the same screen. Every change goes through a change set.
 */
@Component({
  selector: 'dc-org-page',
  imports: [
    NgTemplateOutlet,
    ExportButton,
    FormsModule,
    JsonImportButton,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTabsModule,
    MatTooltipModule,
    ConfigTable,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header dc-page__header--actions">
      <div>
        <h1>{{ 'org.title' | dcTranslate }}</h1>
        <p>{{ 'org.lead' | dcTranslate }}</p>
      </div>
      <div>
        <dc-export-button entityType="apps.org-unit" [title]="{ ar: 'الهيكل التنظيمي', en: 'Organization structure' }" />
        @if (canEdit()) {
          <dc-json-import-button
            [title]="{ ar: 'الهيكل التنظيمي', en: 'Organization structure' }"
            [target]="{ entityType: 'apps.org-unit', key: 'code' }"
            (imported)="units.reload()"
          />
        }
      </div>
    </header>
    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (e of errors(); track $index) {
          <li>{{ e }}</li>
        }
      </ul>
    }
    <mat-tab-group mat-stretch-tabs="false" animationDuration="0ms">
      <mat-tab [label]="'org.tree' | dcTranslate">
        <div class="dc-split dc-org">
          <section class="dc-panel" data-testid="org-tree">
            <div class="dc-panel__header">
              <h2>
                {{ 'org.units' | dcTranslate }} <span class="dc-count">{{ rows().length }}</span>
              </h2>
              @if (canCreate()) {
                <button matButton="filled" type="button" (click)="add(null)" data-testid="org-add-root">
                  <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
                  {{ 'org.addRoot' | dcTranslate }}
                </button>
              }
            </div>
            <div class="dc-panel__search dc-org__tools">
              <mat-form-field subscriptSizing="dynamic">
                <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
                <input matInput [placeholder]="'org.search' | dcTranslate" [ngModel]="search()" (ngModelChange)="search.set($event)" data-testid="org-search" />
              </mat-form-field>
              <button matIconButton type="button" (click)="expandAll()" [matTooltip]="'org.expandAll' | dcTranslate" data-testid="org-expand">
                <mat-icon fontSet="material-symbols-outlined">unfold_more</mat-icon>
              </button>
              <button matIconButton type="button" (click)="collapseAll()" [matTooltip]="'org.collapseAll' | dcTranslate">
                <mat-icon fontSet="material-symbols-outlined">unfold_less</mat-icon>
              </button>
            </div>
            @if (units.isLoading()) {
              <mat-progress-bar mode="indeterminate" />
            }
            <ul class="dc-org__tree" role="tree" [attr.aria-label]="'org.units' | dcTranslate">
              @for (node of tree(); track node.row.code) {
                <ng-container *ngTemplateOutlet="branch; context: { $implicit: node, level: 1 }" />
              } @empty {
                @if (!units.isLoading()) {
                  <li class="dc-muted dc-org__empty">{{ (search().trim() ? 'org.noMatch' : 'org.empty') | dcTranslate }}</li>
                }
              }
            </ul>
          </section>

          <section class="dc-panel" data-testid="org-details">
            @if (selected(); as unit) {
              <div class="dc-panel__header">
                <div>
                  <h2>{{ { ar: unit.nameAr || unit.code, en: unit.nameEn || unit.code } | dcLocalize }}</h2>
                  <span class="mono">{{ unit.code }}</span>
                </div>
                <div class="dc-org__actions">
                  @if (canCreate()) {
                    <button matButton="outlined" type="button" (click)="add(unit.code)" data-testid="org-add-child">
                      <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
                      {{ 'org.addChild' | dcTranslate }}
                    </button>
                  }
                  @if (canEdit()) {
                    <button matButton="outlined" type="button" (click)="edit(unit)" data-testid="org-edit">
                      <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>
                      {{ 'config.edit' | dcTranslate }}
                    </button>
                  }
                  @if (canDelete()) {
                    <button matIconButton type="button" (click)="remove(unit)" [matTooltip]="'config.delete' | dcTranslate" data-testid="org-delete">
                      <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                    </button>
                  }
                </div>
              </div>
              <div class="dc-panel__body">
                <nav class="dc-org__path" [attr.aria-label]="'org.path' | dcTranslate">
                  @for (code of path(); track code) {
                    <button type="button" class="dc-org__crumb" (click)="select(code)">{{ nameOf(code) | dcLocalize }}</button>
                    <mat-icon fontSet="material-symbols-outlined" aria-hidden="true" class="dc-org__sep">chevron_left</mat-icon>
                  }
                  <strong>{{ { ar: unit.nameAr || unit.code, en: unit.nameEn || unit.code } | dcLocalize }}</strong>
                </nav>
                <dl class="dc-org__facts">
                  <div>
                    <dt>{{ 'org.type' | dcTranslate }}</dt>
                    <dd>{{ typeName(unit['typeCode']) | dcLocalize }}</dd>
                  </div>
                  <div>
                    <dt>{{ 'org.parent' | dcTranslate }}</dt>
                    <dd>{{ unit['parentCode'] ? (nameOf(unit['parentCode']) | dcLocalize) : ('org.noParent' | dcTranslate) }}</dd>
                  </div>
                  <div>
                    <dt>{{ 'org.subUnits' | dcTranslate }}</dt>
                    <dd>{{ childrenOf(unit.code).length }}</dd>
                  </div>
                  <div>
                    <dt>{{ 'org.state' | dcTranslate }}</dt>
                    <dd>
                      <span class="dc-pill" [class.dc-pill--green]="unit.isActive">{{ (unit.isActive ? 'org.active' : 'org.inactive') | dcTranslate }}</span>
                    </dd>
                  </div>
                  @if (unit['effectiveFrom'] || unit['effectiveTo']) {
                    <div>
                      <dt>{{ 'org.effective' | dcTranslate }}</dt>
                      <dd dir="ltr">{{ unit['effectiveFrom'] ?? '…' }} → {{ unit['effectiveTo'] ?? '…' }}</dd>
                    </div>
                  }
                </dl>
                @if (attributes(unit).length) {
                  <h3>{{ 'org.attributes' | dcTranslate }}</h3>
                  <dl class="dc-org__facts">
                    @for (a of attributes(unit); track a.key) {
                      <div>
                        <dt class="mono">{{ a.key }}</dt>
                        <dd>{{ a.value }}</dd>
                      </div>
                    }
                  </dl>
                }
                @if (childrenOf(unit.code).length) {
                  <h3>{{ 'org.subUnits' | dcTranslate }}</h3>
                  <div class="dc-org__children">
                    @for (child of childrenOf(unit.code); track child.code) {
                      <button type="button" class="dc-org__child" (click)="select(child.code)">
                        <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">account_tree</mat-icon>
                        {{ { ar: child.nameAr || child.code, en: child.nameEn || child.code } | dcLocalize }}
                      </button>
                    }
                  </div>
                }
              </div>
            } @else {
              <div class="dc-org__placeholder">
                <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">account_tree</mat-icon>
                <p class="dc-muted">{{ 'org.choose' | dcTranslate }}</p>
              </div>
            }
          </section>
        </div>
      </mat-tab>
      @for (type of otherTypes; track type.entityType) {
        <mat-tab [label]="type.title | dcLocalize">
          <dc-config-table [type]="type" [resource]="resource" />
        </mat-tab>
      }
    </mat-tab-group>

    <ng-template #branch let-node let-level="level">
      @if (!visible() || visible()!.has(node.row.code)) {
        <li
          role="treeitem"
          [attr.aria-level]="level"
          [attr.aria-expanded]="node.children.length ? isOpen(node.row.code) : null"
          [attr.aria-selected]="node.row.code === selectedCode()"
        >
          <div class="dc-org__row" [class.dc-org__row--on]="node.row.code === selectedCode()" [style.padding-inline-start.px]="8 + (level - 1) * 20">
            @if (node.children.length) {
              <button type="button" class="dc-org__toggle" (click)="toggle(node.row.code)" [attr.aria-label]="'org.toggle' | dcTranslate">
                <mat-icon fontSet="material-symbols-outlined">{{ isOpen(node.row.code) ? 'expand_more' : 'chevron_left' }}</mat-icon>
              </button>
            } @else {
              <span class="dc-org__toggle dc-org__toggle--leaf" aria-hidden="true"></span>
            }
            <button type="button" class="dc-org__name" (click)="select(node.row.code)" [attr.data-unit]="node.row.code">
              <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{
                level === 1 ? 'domain' : node.children.length ? 'account_tree' : 'groups'
              }}</mat-icon>
              <span class="dc-org__label" [class.dc-org__label--off]="!node.row.isActive">{{
                { ar: node.row.nameAr || node.row.code, en: node.row.nameEn || node.row.code } | dcLocalize
              }}</span>
              <span class="dc-org__type">{{ typeName(node.row['typeCode']) | dcLocalize }}</span>
              @if (node.descendants) {
                <span class="dc-count">{{ node.descendants }}</span>
              }
            </button>
          </div>
          @if (node.children.length && isOpen(node.row.code)) {
            <ul role="group">
              @for (child of node.children; track child.row.code) {
                <ng-container *ngTemplateOutlet="branch; context: { $implicit: child, level: level + 1 }" />
              }
            </ul>
          }
        </li>
      }
    </ng-template>
  `,
  styles: `
    .dc-org {
      margin-top: 12px;
      grid-template-columns: minmax(20rem, 30rem) minmax(0, 1fr);
    }
    .dc-org__tools {
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .dc-org__tools mat-form-field {
      flex: 1;
    }
    .dc-org__tree,
    .dc-org__tree ul {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .dc-org__tree {
      max-height: 34rem;
      overflow: auto;
      padding-block: 6px;
    }
    .dc-org__empty {
      padding: 16px;
    }
    .dc-org__row {
      position: relative;
      display: flex;
      align-items: center;
      gap: 2px;
      padding-inline-end: 8px;
    }
    .dc-org__row:hover {
      background: var(--dga-gray-50);
    }
    .dc-org__row--on {
      background: var(--dynamic-cube-gold-50);
    }
    .dc-org__row--on::before {
      content: '';
      position: absolute;
      inset-block: 4px;
      inset-inline-start: 0;
      width: 3px;
      border-radius: 9999px;
      background: var(--dynamic-cube-gold);
    }
    .dc-org__toggle {
      display: grid;
      place-items: center;
      flex: none;
      width: 28px;
      height: 28px;
      border: 0;
      border-radius: 6px;
      background: none;
      color: var(--dga-gray-500);
      cursor: pointer;
    }
    .dc-org__toggle:hover {
      background: var(--dga-gray-100);
    }
    .dc-org__toggle--leaf {
      cursor: default;
    }
    :host-context([dir='ltr']) .dc-org__toggle mat-icon {
      transform: scaleX(-1);
    }
    .dc-org__name {
      display: flex;
      flex: 1;
      align-items: center;
      gap: 8px;
      min-width: 0;
      padding: 7px 4px;
      border: 0;
      background: none;
      font: inherit;
      text-align: start;
      cursor: pointer;
    }
    .dc-org__name mat-icon {
      flex: none;
      width: 20px;
      height: 20px;
      font-size: 20px;
      color: var(--dynamic-cube-gold-700);
    }
    .dc-org__label {
      overflow: hidden;
      color: var(--dga-gray-900);
      font-weight: 600;
      white-space: nowrap;
      text-overflow: ellipsis;
    }
    .dc-org__label--off {
      color: var(--dga-gray-400);
      text-decoration: line-through;
    }
    .dc-org__type {
      flex: none;
      color: var(--dga-gray-500);
      font-size: 12px;
    }
    .dc-org__name .dc-count {
      margin-inline-start: auto;
    }
    .dc-org__actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .dc-org__path {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 2px;
      margin-bottom: 12px;
      font-size: 13px;
    }
    .dc-org__crumb {
      padding: 2px 6px;
      border: 0;
      border-radius: 6px;
      background: none;
      color: var(--dynamic-cube-gold-700);
      font: inherit;
      cursor: pointer;
    }
    .dc-org__crumb:hover {
      background: var(--dynamic-cube-gold-50);
    }
    .dc-org__sep {
      width: 16px;
      height: 16px;
      font-size: 16px;
      color: var(--dga-gray-400);
    }
    :host-context([dir='ltr']) .dc-org__sep {
      transform: scaleX(-1);
    }
    .dc-org__facts {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(12rem, 1fr));
      gap: 10px 16px;
      margin: 0 0 12px;
    }
    .dc-org__facts dt {
      color: var(--dga-gray-500);
      font-size: 12px;
    }
    .dc-org__facts dd {
      margin: 2px 0 0;
      font-weight: 600;
    }
    .dc-panel__body h3 {
      margin: 8px 0;
      font-size: 14px;
    }
    .dc-org__children {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }
    .dc-org__child {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 6px 10px;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius);
      background: #fff;
      font: inherit;
      font-size: 13px;
      cursor: pointer;
    }
    .dc-org__child:hover {
      border-color: var(--dynamic-cube-gold);
    }
    .dc-org__child mat-icon {
      width: 18px;
      height: 18px;
      font-size: 18px;
      color: var(--dynamic-cube-gold-700);
    }
    .dc-org__placeholder {
      display: grid;
      place-items: center;
      gap: 6px;
      padding: 48px 16px;
      color: var(--dga-gray-300);
    }
    .dc-org__placeholder mat-icon {
      width: 40px;
      height: 40px;
      font-size: 40px;
    }
  `,
})
export class OrgPage {
  private readonly api = inject(AppsControlApi);
  private readonly dialog = inject(MatDialog);
  private readonly drafts = inject(DraftsService);
  private readonly privileges = inject(PrivilegesService);
  private readonly i18n = inject(I18nService);
  protected readonly resource = RESOURCE;

  private readonly section = CONFIG_SECTIONS.find((s) => s.path === 'organization')!;
  private readonly unitType = this.section.types.find((t) => t.entityType === 'apps.org-unit')!;
  protected readonly otherTypes = this.section.types.filter((t) => t.entityType !== 'apps.org-unit');

  protected readonly units = rxResource({ stream: () => this.api.list('apps.org-unit') });
  private readonly types = rxResource({ stream: () => this.api.list('apps.org-unit-type') });
  protected readonly rows = computed(() => (this.units.hasValue() ? this.units.value() : []));
  protected readonly tree = computed(() => buildOrgTree(this.rows()));
  protected readonly search = signal('');
  protected readonly visible = computed(() => matchingCodes(this.rows(), this.search()));
  protected readonly expanded = signal<Set<string>>(new Set());
  protected readonly selectedCode = signal<string | null>(null);
  protected readonly selected = computed(() => this.rows().find((r) => r.code === this.selectedCode()) ?? null);
  protected readonly path = computed(() => (this.selectedCode() ? pathTo(this.rows(), this.selectedCode()!) : []));
  protected readonly errors = signal<string[]>([]);

  protected readonly canCreate = computed(() => this.privileges.can(RESOURCE, 'CREATE'));
  protected readonly canEdit = computed(() => this.privileges.can(RESOURCE, 'EDIT'));
  protected readonly canDelete = computed(() => this.privileges.can(RESOURCE, 'DELETE'));

  /** Open while searching (so matches show), else as the user left it; the first level starts open. */
  protected isOpen(code: string): boolean {
    if (this.visible()) return true;
    return this.expanded().has(code) || (this.expanded().size === 0 && this.tree().some((n) => n.row.code === code));
  }

  protected toggle(code: string): void {
    const next = new Set(this.expanded().size ? this.expanded() : this.tree().map((n) => n.row.code));
    if (!next.delete(code)) next.add(code);
    if (!next.size) next.add('\u0000');
    this.expanded.set(next);
  }

  /** Every unit closed (a marker keeps the first level from opening by default). */
  protected collapseAll(): void {
    this.expanded.set(new Set(['\u0000']));
  }

  protected expandAll(): void {
    this.expanded.set(new Set(this.rows().map((r) => r.code)));
  }

  protected select(code: string): void {
    this.selectedCode.set(code);
    const next = new Set(this.expanded().size ? this.expanded() : this.tree().map((n) => n.row.code));
    for (const c of pathTo(this.rows(), code)) next.add(c);
    this.expanded.set(next);
  }

  protected childrenOf(code: string): ConfigRow[] {
    return this.rows().filter((r) => r['parentCode'] === code);
  }

  protected nameOf(code: unknown) {
    const row = this.rows().find((r) => r.code === code);
    return { ar: (row?.nameAr as string) ?? String(code), en: (row?.nameEn as string) ?? String(code) };
  }

  protected typeName(code: unknown) {
    const type = (this.types.hasValue() ? this.types.value() : []).find((t) => t.code === code);
    return { ar: (type?.nameAr as string) ?? String(code ?? ''), en: (type?.nameEn as string) ?? String(code ?? '') };
  }

  protected attributes(unit: ConfigRow): { key: string; value: string }[] {
    let value = unit['attributes'];
    if (typeof value === 'string') {
      try {
        value = JSON.parse(value);
      } catch {
        return [];
      }
    }
    return value && typeof value === 'object'
      ? Object.entries(value).map(([key, v]) => ({ key, value: typeof v === 'object' ? JSON.stringify(v) : String(v) }))
      : [];
  }

  protected async add(parent: string | null): Promise<void> {
    await this.open(null, parent ? { parentCode: parent } : undefined);
  }

  protected async edit(unit: ConfigRow): Promise<void> {
    try {
      await this.open(await firstValueFrom(this.api.get('apps.org-unit', unit.code)));
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  protected async remove(unit: ConfigRow): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog
        .open<ConfirmDialog, ConfirmData, string>(ConfirmDialog, {
          data: {
            title: `${this.i18n.translate('config.delete')} ${unit.code}`,
            message: this.i18n.translate('org.deleteHint'),
            confirmLabel: this.i18n.translate('config.delete'),
            danger: true,
          },
        })
        .afterClosed(),
    );
    if (confirmed === undefined) return;
    this.errors.set([]);
    try {
      await this.drafts.addChange('apps.org-unit', unit.code, 'DELETE', null);
      this.units.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  private async open(row: ConfigRow | null, defaults?: Record<string, unknown>): Promise<void> {
    const saved = await firstValueFrom(
      this.dialog
        .open<ConfigEditor, ConfigEditorData, boolean>(ConfigEditor, {
          data: { type: this.unitType, row, defaults },
          autoFocus: 'first-tabbable',
          width: '52rem',
          maxWidth: '96vw',
        })
        .afterClosed(),
    );
    if (saved) this.units.reload();
  }
}
