import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { AppsControlApi, BusinessAppContext, configLevel, ConfigRow, I18nService, LocalizePipe, problemMessages, TranslatePipe } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { JsonImport } from '../change-sets/json-import';
import { ConfirmData, ConfirmDialog } from '../confirm-dialog';
import { DraftsService } from '../drafts.service';
import { PrivilegesService } from '../privileges.service';
import { ConfigEditor, ConfigEditorData } from './config-editor';
import { ConfigRowAction, ConfigType, scopeOf } from './config-types';

/** Published rows of one configuration type with new / edit / delete actions that go into a draft change set. */
@Component({
  selector: 'dc-config-table',
  imports: [MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatIconModule, MatInputModule, MatTableModule, MatTooltipModule, RouterLink, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-toolbar">
      <mat-form-field class="dc-toolbar__search" subscriptSizing="dynamic">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input matInput [placeholder]="'config.search' | dcTranslate" (input)="search.set($any($event.target).value)" />
      </mat-form-field>
      <span class="dc-toolbar__spacer"></span>
      <button matButton="outlined" type="button" (click)="exportPackage()" [disabled]="!rows().length" [attr.data-testid]="'export-' + type().entityType">
        <mat-icon fontSet="material-symbols-outlined">download</mat-icon>
        {{ 'export.button' | dcTranslate }}
        @if (selected().size) {
          ({{ selected().size }})
        }
      </button>
      @if (canCreate() || canEdit()) {
        <button matButton="outlined" type="button" (click)="importJson()" [attr.data-testid]="'import-' + type().entityType">
          <mat-icon fontSet="material-symbols-outlined">upload</mat-icon>
          {{ 'jsonImport.button' | dcTranslate }}
        </button>
      }
      @if (canCreate()) {
        <button matButton="filled" (click)="edit(null)" [attr.data-testid]="'new-' + type().entityType">
          <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
          {{ 'config.new' | dcTranslate }}
        </button>
      }
    </div>
    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (error of errors(); track $index) {
          <li>{{ error }}</li>
        }
      </ul>
    }
    <table mat-table [dataSource]="rows()" class="dc-table">
      <ng-container matColumnDef="select">
        <th mat-header-cell *matHeaderCellDef class="dc-table__select">
          <mat-checkbox
            [checked]="allSelected()"
            [indeterminate]="selected().size > 0 && !allSelected()"
            (change)="selectAll($event.checked)"
            [attr.aria-label]="'export.selectAll' | dcTranslate"
          />
        </th>
        <td mat-cell *matCellDef="let row" class="dc-table__select">
          <mat-checkbox [checked]="selected().has(row.code)" (change)="select(row.code, $event.checked)" [attr.aria-label]="row.code" [attr.data-testid]="'select-' + row.code" />
        </td>
      </ng-container>
      <ng-container matColumnDef="code">
        <th mat-header-cell *matHeaderCellDef>{{ 'config.code' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr" class="dc-code">{{ row.code }}</td>
      </ng-container>
      <ng-container matColumnDef="name">
        <th mat-header-cell *matHeaderCellDef>{{ 'config.name' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row">{{ { ar: row.nameAr, en: row.nameEn } | dcLocalize }}</td>
      </ng-container>
      <ng-container matColumnDef="level">
        <th mat-header-cell *matHeaderCellDef>{{ 'config.level' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row">
          @switch (level(row)) {
            @case ('APP') {
              @for (app of appsOf(row); track app) {
                <span class="dc-level dc-level--app" [attr.data-level]="'APP'">{{ appContext.name(app) | dcLocalize }}</span>
              }
            }
            @case ('ENTITY') {
              <span class="dc-level dc-level--entity" [attr.data-level]="'ENTITY'">{{ 'businessApp.level.ENTITY' | dcTranslate }} · {{ row.entityCode }}</span>
            }
            @default {
              <span class="dc-level" [attr.data-level]="'SYSTEM'">{{ 'businessApp.level.SYSTEM' | dcTranslate }}</span>
            }
          }
        </td>
      </ng-container>
      @for (column of type().columns ?? []; track column) {
        <ng-container [matColumnDef]="column">
          <th mat-header-cell *matHeaderCellDef>{{ label(column) | dcLocalize }}</th>
          <td mat-cell *matCellDef="let row">{{ display(row[column]) }}</td>
        </ng-container>
      }
      <ng-container matColumnDef="state">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let row">
          @if (row.owner === 'SYSTEM') {
            <mat-icon fontSet="material-symbols-outlined" [matTooltip]="'config.system' | dcTranslate">lock</mat-icon>
          }
          @if (!row.isActive) {
            <span class="dc-muted">{{ 'config.inactive' | dcTranslate }}</span>
          }
        </td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let row" class="dc-table__actions">
          @for (link of type().rowLinks ?? []; track link.testId) {
            <a
              matIconButton
              [routerLink]="link.route(row).commands"
              [queryParams]="link.route(row).queryParams"
              [matTooltip]="link.label | dcLocalize"
              [attr.aria-label]="link.label | dcLocalize"
              [attr.data-testid]="link.testId + '-' + row.code"
            >
              <mat-icon fontSet="material-symbols-outlined">{{ link.icon }}</mat-icon>
            </a>
          }
          @for (action of type().rowActions ?? []; track action.testId) {
            <button
              matIconButton
              type="button"
              (click)="openAction(action, row)"
              [matTooltip]="action.label | dcLocalize"
              [attr.aria-label]="action.label | dcLocalize"
              [attr.data-testid]="action.testId + '-' + row.code"
            >
              <mat-icon fontSet="material-symbols-outlined">{{ action.icon }}</mat-icon>
            </button>
          }
          @if (row.owner !== 'SYSTEM' && canEdit()) {
            <button matIconButton (click)="edit(row)" [attr.aria-label]="'config.edit' | dcTranslate" [attr.data-testid]="'edit-' + row.code">
              <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>
            </button>
          }
          @if (row.owner !== 'SYSTEM' && canDelete()) {
            <button matIconButton (click)="remove(row)" [attr.aria-label]="'config.delete' | dcTranslate">
              <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
            </button>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns()"></tr>
      <tr mat-row *matRowDef="let row; columns: columns()"></tr>
    </table>
    @if (rows().length === 0) {
      <p class="dc-empty">{{ 'config.empty' | dcTranslate }}</p>
    }
  `,
  styles: `
    .dc-code {
      font-family: monospace;
    }
    .dc-table__select {
      width: 40px;
    }
    .dc-level {
      display: inline-block;
      padding: 2px 8px;
      border-radius: 9999px;
      background: var(--dga-gray-100);
      color: var(--dga-gray-700);
      font-size: 12px;
      white-space: nowrap;
    }
    .dc-level--entity {
      background: #eff8ff;
      color: #175cd3;
    }
    .dc-level--app {
      background: var(--dynamic-cube-gold-50);
      color: var(--dynamic-cube-gold-700);
    }
  `,
})
export class ConfigTable {
  readonly type = input.required<ConfigType>();
  readonly resource = input.required<string>();

  private readonly api = inject(AppsControlApi);
  private readonly dialog = inject(MatDialog);
  private readonly drafts = inject(DraftsService);
  private readonly privileges = inject(PrivilegesService);
  private readonly i18n = inject(I18nService);
  private readonly jsonImport = inject(JsonImport);

  protected readonly search = signal('');
  protected readonly errors = signal<string[]>([]);
  protected readonly appContext = inject(BusinessAppContext);
  /** Rows of the chosen business app and the shared ones (types without levels list every row). */
  private readonly list = rxResource({
    params: () => ({ type: this.type().entityType, app: scopeOf(this.type()) === 'none' ? null : this.appContext.current() }),
    stream: ({ params }) => this.api.list(params.type, null, params.app),
  });

  protected readonly singleton = computed(() => typeof this.type().key === 'object');
  protected readonly canCreate = computed(() => !this.singleton() && !this.type().noCreate && this.privileges.can(this.resource(), 'CREATE'));
  protected readonly canEdit = computed(() => this.privileges.can(this.resource(), 'EDIT'));
  protected readonly canDelete = computed(() => !this.singleton() && this.privileges.can(this.resource(), 'DELETE'));
  protected readonly columns = computed(() => [
    'select',
    'code',
    'name',
    ...(scopeOf(this.type()) === 'none' ? [] : ['level']),
    ...(this.type().columns ?? []),
    'state',
    'actions',
  ]);
  protected readonly level = configLevel;

  /** The row's business apps: its own and the ones it is shared with. */
  protected appsOf(row: ConfigRow): string[] {
    const apps = row['businessApps'] as string[] | undefined;
    return apps?.length ? apps : row['businessApp'] ? [row['businessApp'] as string] : [];
  }
  /** Rows ticked for export (by code); kept while searching. */
  protected readonly selected = signal(new Set<string>());
  protected readonly allSelected = computed(() => this.rows().length > 0 && this.rows().every((r) => this.selected().has(r.code)));
  protected readonly rows = computed(() => {
    const rows = this.list.hasValue() ? this.list.value() : [];
    const term = this.search().trim().toLowerCase();
    return term ? rows.filter((r) => `${r.code} ${r.nameAr} ${r.nameEn}`.toLowerCase().includes(term)) : rows;
  });

  protected label(column: string) {
    return this.type().fields.find((f) => f.code === column)?.label ?? { ar: column, en: column };
  }

  protected display(value: unknown): string {
    if (value === null || value === undefined) {
      return '';
    }
    if (typeof value === 'boolean') {
      return value ? '✓' : '—';
    }
    return Array.isArray(value) ? value.map((v) => (typeof v === 'object' ? JSON.stringify(v) : String(v))).join('، ') : typeof value === 'object' ? JSON.stringify(value) : String(value);
  }

  protected async openAction(action: ConfigRowAction, row: ConfigRow): Promise<void> {
    const changed = await firstValueFrom(
      this.dialog
        .open<unknown, ConfigEditorData, boolean>(await action.dialog(), { data: { type: this.type(), row }, width: '44rem', maxWidth: '96vw', autoFocus: 'first-tabbable' })
        .afterClosed(),
    );
    if (changed) this.list.reload();
  }

  protected async edit(listed: ConfigRow | null): Promise<void> {
    if (!listed && this.type().createDialog) {
      const created = await firstValueFrom(
        this.dialog
          .open<unknown, ConfigEditorData, boolean>(await this.type().createDialog!(), { data: { type: this.type(), row: null }, width: '48rem', maxWidth: '96vw', autoFocus: 'first-tabbable' })
          .afterClosed(),
      );
      if (created) this.list.reload();
      return;
    }
    this.errors.set([]);
    let row: ConfigRow | null;
    try {
      // List rows may leave out nested data (a role's privileges, a user's roles); edit the full row.
      row = listed ? await firstValueFrom(this.api.get(this.type().entityType, listed.code)) : null;
    } catch (error) {
      this.errors.set(problemMessages(error));
      return;
    }
    await firstValueFrom(
      this.dialog
        .open<unknown, ConfigEditorData, boolean>(this.type().editor ? await this.type().editor!() : ConfigEditor, {
          data: { type: this.type(), row },
          autoFocus: 'first-tabbable',
          width: this.type().editor ? '96rem' : '52rem',
          maxWidth: '96vw',
        })
        .afterClosed(),
    );
  }

  /** Pastes or loads rows of this type (or a package) as JSON; they are reviewed, then drafted. */
  protected async importJson(): Promise<void> {
    const listed = this.list.hasValue() ? this.list.value() : [];
    const example = listed[0] ?? { code: 'CODE', nameAr: '', nameEn: '', ...Object.fromEntries(this.type().fields.map((f) => [f.code, f.defaultValue ?? null])) };
    if (await this.jsonImport.run({ title: this.type().title, target: { entityType: this.type().entityType, key: this.type().key }, example: [example] })) {
      this.list.reload();
    }
  }

  protected select(code: string, on: boolean): void {
    const next = new Set(this.selected());
    if (on) next.add(code);
    else next.delete(code);
    this.selected.set(next);
  }

  protected selectAll(on: boolean): void {
    const next = new Set(this.selected());
    for (const row of this.rows()) {
      if (on) next.add(row.code);
      else next.delete(row.code);
    }
    this.selected.set(next);
  }

  /** Exports the ticked rows (or the whole type) as a package, with what they refer to. */
  protected async exportPackage(): Promise<void> {
    const { ExportDialog } = await import('../change-sets/export-dialog');
    this.dialog.open(ExportDialog, {
      data: { entityType: this.type().entityType, title: this.type().title, selected: [...this.selected()] },
      width: '46rem',
      maxWidth: '96vw',
    });
  }

  protected async remove(row: ConfigRow): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog
        .open<ConfirmDialog, ConfirmData, string>(ConfirmDialog, {
          data: { title: `${this.i18n.translate('config.delete')} ${row.code}`, message: this.i18n.translate('config.deleteHint'), confirmLabel: this.i18n.translate('config.delete'), danger: true },
        })
        .afterClosed(),
    );
    if (confirmed === undefined) {
      return;
    }
    this.errors.set([]);
    try {
      await this.drafts.addChange(this.type().entityType, row.code, 'DELETE', null);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}
