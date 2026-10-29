import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import { AppsControlApi, ConfigRow, I18nService, LocalizePipe, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { ConfigEditor, ConfigEditorData } from '../config/config-editor';
import { CONFIG_SECTIONS, ConfigType } from '../config/config-types';
import { ConfirmData, ConfirmDialog } from '../confirm-dialog';
import { DraftsService } from '../drafts.service';
import { DetailsDialog, detailsOf } from '../master/details-dialog';
import { MasterItem, MasterList } from '../master/master-list';
import { PrivilegesService } from '../privileges.service';
import { JsonImportButton } from '../change-sets/json-import-button';

const RESOURCE = 'apps-control.menus';
/** Pseudo group for menus that belong to no group. */
const UNGROUPED = '_';

const types = CONFIG_SECTIONS.find((s) => s.path === 'menus')!.types;
const MENU: ConfigType = types.find((t) => t.entityType === 'apps.menu')!;
const GROUP: ConfigType = types.find((t) => t.entityType === 'apps.menu-group')!;

/** Menu groups on the start side (with how many menus each holds); the chosen group's menus on the end side. */
@Component({
  selector: 'dc-menus-page',
  imports: [
    JsonImportButton,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    MasterList,
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
      <dc-json-import-button [title]="{ ar: 'القوائم', en: 'Menus' }" [target]="{ entityType: 'apps.menu-group', key: 'code' }" />
    </header>
    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (error of errors(); track $index) {
          <li dir="auto">{{ error }}</li>
        }
      </ul>
    }
    <div class="dc-split">
      <dc-master-list
        testId="menu-groups"
        [title]="{ ar: 'مجموعات القوائم', en: 'Menu groups' }"
        [addLabel]="{ ar: 'إضافة مجموعة', en: 'Add group' }"
        [items]="items()"
        [selected]="selected()"
        [canCreate]="privileges.can(resource, 'CREATE')"
        (selectItem)="select($event)"
        (view)="viewGroup($event)"
        (edit)="editGroup($event)"
        (remove)="remove(group, $event)"
        (add)="editGroup(null)"
      />

      <section class="dc-panel" data-testid="menus-panel">
        @if (selected(); as code) {
          <header class="dc-panel__header">
            <div>
              <h2>
                {{ selectedTitle() | dcLocalize }}
                <span class="dc-count" data-testid="menus-count">{{ groupMenus().length }}</span>
              </h2>
              @if (code !== ungrouped) {
                <span class="mono">{{ code }}</span>
              }
            </div>
            @if (privileges.can(resource, 'CREATE')) {
              <button matButton="filled" (click)="editMenu(null)" data-testid="new-menu">
                <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
                {{ 'menus.add' | dcTranslate }}
              </button>
            }
          </header>
          <div class="dc-panel__search">
            <mat-form-field subscriptSizing="dynamic">
              <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
              <input
                matInput
                [placeholder]="'config.search' | dcTranslate"
                [value]="search()"
                (input)="search.set($any($event.target).value)"
                data-testid="menus-search"
              />
            </mat-form-field>
          </div>
          @if (lists.isLoading()) {
            <mat-progress-bar mode="indeterminate" />
          }
          <div class="dc-scroll">
            <table mat-table [dataSource]="dataSource" matSort class="dc-table" data-testid="menus-table">
              <ng-container matColumnDef="icon">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let row">
                  @if (row.icon) {
                    <mat-icon fontSet="material-symbols-outlined" class="dc-menu-icon">{{ row.icon }}</mat-icon>
                  }
                </td>
              </ng-container>
              <ng-container matColumnDef="name">
                <th mat-header-cell *matHeaderCellDef mat-sort-header="nameEn">{{ 'config.name' | dcTranslate }}</th>
                <td mat-cell *matCellDef="let row">
                  {{ { ar: row.nameAr, en: row.nameEn } | dcLocalize }}
                  <span class="mono dc-muted dc-menu-code">{{ row.code }}</span>
                </td>
              </ng-container>
              <ng-container matColumnDef="target">
                <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'menus.target' | dcTranslate }}</th>
                <td mat-cell *matCellDef="let row" class="mono dc-menu-target" [title]="row.target ?? ''">{{ row.target }}</td>
              </ng-container>
              <ng-container matColumnDef="displayOrder">
                <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'menus.order' | dcTranslate }}</th>
                <td mat-cell *matCellDef="let row">{{ row.displayOrder }}</td>
              </ng-container>
              <ng-container matColumnDef="isActive">
                <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'config.status' | dcTranslate }}</th>
                <td mat-cell *matCellDef="let row">
                  <span class="dc-pill" [class.dc-pill--green]="row.isActive && row.isVisible">{{ state(row) | dcTranslate }}</span>
                </td>
              </ng-container>
              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let row" class="dc-table__actions">
                  <button matIconButton (click)="viewMenu(row)" [attr.aria-label]="'config.view' | dcTranslate" [attr.data-testid]="'view-menu-' + row.code">
                    <mat-icon fontSet="material-symbols-outlined">visibility</mat-icon>
                  </button>
                  @if (row.owner !== 'SYSTEM' && privileges.can(resource, 'EDIT')) {
                    <button matIconButton (click)="editMenu(row)" [attr.aria-label]="'config.edit' | dcTranslate" [attr.data-testid]="'edit-menu-' + row.code">
                      <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>
                    </button>
                  }
                  @if (row.owner !== 'SYSTEM' && privileges.can(resource, 'DELETE')) {
                    <button
                      matIconButton
                      (click)="remove(menu, row.code)"
                      [attr.aria-label]="'config.delete' | dcTranslate"
                      [attr.data-testid]="'delete-menu-' + row.code"
                    >
                      <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                    </button>
                  }
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="columns; sticky: true"></tr>
              <tr mat-row *matRowDef="let row; columns: columns"></tr>
              <tr class="mat-mdc-row" *matNoDataRow>
                <td class="mat-mdc-cell dc-empty" [attr.colspan]="columns.length">{{ 'menus.none' | dcTranslate }}</td>
              </tr>
            </table>
          </div>
        } @else if (!lists.isLoading()) {
          <p class="dc-empty dc-panel__body">{{ 'menus.pick' | dcTranslate }}</p>
        }
      </section>
    </div>
  `,
  styles: `
    .dc-scroll {
      max-height: calc(100vh - 320px);
      min-height: 12rem;
      overflow: auto;
    }
    .dc-menu-icon {
      color: var(--dga-gray-500);
    }
    .dc-menu-code {
      display: block;
      font-size: 12px;
    }
    .dc-menu-target {
      max-width: 16rem;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  `,
})
export class MenusPage {
  /** Route parameter: the chosen group (the first one when absent). */
  readonly code = input<string | undefined>(undefined);

  private readonly api = inject(AppsControlApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly drafts = inject(DraftsService);
  private readonly i18n = inject(I18nService);
  protected readonly privileges = inject(PrivilegesService);
  protected readonly resource = RESOURCE;
  protected readonly menu = MENU;
  protected readonly group = GROUP;
  protected readonly ungrouped = UNGROUPED;
  protected readonly columns = ['icon', 'name', 'target', 'displayOrder', 'isActive', 'actions'];

  protected readonly errors = signal<string[]>([]);
  protected readonly search = signal('');
  protected readonly lists = rxResource({ stream: () => this.api.list('apps.menu-group') });
  private readonly menus = rxResource({ stream: () => this.api.list('apps.menu') });
  private readonly groups = computed(() => (this.lists.hasValue() ? this.lists.value() : []));
  private readonly menuRows = computed(() => (this.menus.hasValue() ? this.menus.value() : []));

  protected readonly selected = computed(() => this.code() ?? this.groups()[0]?.code ?? null);
  protected readonly groupMenus = computed(() => {
    const code = this.selected();
    return this.menuRows()
      .filter((m) => (code === UNGROUPED ? !m['groupCode'] : m['groupCode'] === code))
      .sort((a, b) => Number(a['displayOrder'] ?? 0) - Number(b['displayOrder'] ?? 0));
  });
  protected readonly selectedTitle = computed(() => {
    const group = this.groups().find((g) => g.code === this.selected());
    return group ? { ar: group.nameAr, en: group.nameEn } : { ar: 'بدون مجموعة', en: 'Without a group' };
  });

  protected readonly items = computed<MasterItem[]>(() => {
    const canEdit = this.privileges.can(RESOURCE, 'EDIT');
    const canDelete = this.privileges.can(RESOURCE, 'DELETE');
    const count = (code: string) => this.menuRows().filter((m) => m['groupCode'] === code).length;
    const badge = (n: number): MasterItem['badge'] =>
      n
        ? { label: `${n} ${this.i18n.translate(n === 1 ? 'menus.itemWord' : 'menus.itemsWord')}`, tone: 'green' }
        : { label: this.i18n.translate('menus.empty'), tone: 'gray' };
    const items: MasterItem[] = this.groups().map((g) => {
      const n = count(g.code);
      const system = g.owner === 'SYSTEM';
      return {
        code: g.code,
        title: { ar: g.nameAr, en: g.nameEn },
        badge: badge(n),
        locked: system,
        canEdit: !system && canEdit,
        canDelete: !system && canDelete && n === 0,
        deleteBlockedReason: !system && canDelete && n > 0 ? this.i18n.translate('menus.deleteHasItems') : undefined,
      };
    });
    const loose = this.menuRows().filter((m) => !m['groupCode']).length;
    return loose
      ? [...items, { code: UNGROUPED, title: { ar: 'بدون مجموعة', en: 'Without a group' }, badge: badge(loose), canEdit: false, canDelete: false }]
      : items;
  });

  protected readonly dataSource = new MatTableDataSource<ConfigRow>([]);
  private readonly sort = viewChild(MatSort);

  constructor() {
    this.dataSource.filterPredicate = (row, term) => `${row.code} ${row.nameAr} ${row.nameEn} ${row['target'] ?? ''}`.toLowerCase().includes(term);
    effect(() => (this.dataSource.data = this.groupMenus()));
    effect(() => (this.dataSource.filter = this.search().trim().toLowerCase()));
    effect(() => {
      const sort = this.sort();
      if (sort) this.dataSource.sort = sort;
    });
  }

  protected select(code: string): void {
    this.search.set('');
    void this.router.navigate(['/apps-control/menus', code]);
  }

  protected state(row: ConfigRow): string {
    return !row.isActive ? 'config.inactive' : row['isVisible'] === false ? 'menus.hidden' : 'config.active';
  }

  protected viewGroup(code: string): void {
    const row = this.groups().find((g) => g.code === code);
    if (row) this.dialog.open(DetailsDialog, { data: detailsOf(GROUP, row), width: '40rem', maxWidth: '95vw' });
    else if (code === UNGROUPED) this.select(code);
  }

  protected viewMenu(row: ConfigRow): void {
    this.dialog.open(DetailsDialog, { data: detailsOf(MENU, row), width: '40rem', maxWidth: '95vw' });
  }

  protected async editGroup(code: string | null): Promise<void> {
    const row = code ? await this.full(GROUP, code) : null;
    if (code && !row) return;
    await this.edit(GROUP, row);
  }

  protected async editMenu(row: ConfigRow | null): Promise<void> {
    const group = this.groups().find((g) => g.code === this.selected());
    const full = row ? await this.full(MENU, row.code) : null;
    if (row && !full) return;
    await this.edit(MENU, full, group ? { groupCode: group.code, appCode: group['appCode'] } : undefined);
  }

  protected async remove(type: ConfigType, code: string): Promise<void> {
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
      await this.drafts.addChange(type.entityType, code, 'DELETE', null);
      this.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  private async full(type: ConfigType, code: string): Promise<ConfigRow | null> {
    try {
      return await firstValueFrom(this.api.get(type.entityType, code));
    } catch (error) {
      this.errors.set(problemMessages(error));
      return null;
    }
  }

  private async edit(type: ConfigType, row: ConfigRow | null, defaults?: Record<string, unknown>): Promise<void> {
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
    if (saved) this.reload();
  }

  private reload(): void {
    this.lists.reload();
    this.menus.reload();
  }
}
