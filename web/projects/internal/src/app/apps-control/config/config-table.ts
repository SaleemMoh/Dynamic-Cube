import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { AppsControlApi, ConfigRow, I18nService, LocalizePipe, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { ConfirmData, ConfirmDialog } from '../confirm-dialog';
import { DraftsService } from '../drafts.service';
import { PrivilegesService } from '../privileges.service';
import { ConfigEditor, ConfigEditorData } from './config-editor';
import { ConfigType } from './config-types';

/** Published rows of one configuration type with new / edit / delete actions that go into a draft change set. */
@Component({
  selector: 'dc-config-table',
  imports: [MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatTableModule, MatTooltipModule, RouterLink, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-toolbar">
      <mat-form-field class="dc-toolbar__search" subscriptSizing="dynamic">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input matInput [placeholder]="'config.search' | dcTranslate" (input)="search.set($any($event.target).value)" />
      </mat-form-field>
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
      <ng-container matColumnDef="code">
        <th mat-header-cell *matHeaderCellDef>{{ 'config.code' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr" class="dc-code">{{ row.code }}</td>
      </ng-container>
      <ng-container matColumnDef="name">
        <th mat-header-cell *matHeaderCellDef>{{ 'config.name' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row">{{ { ar: row.nameAr, en: row.nameEn } | dcLocalize }}</td>
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
    .dc-toolbar {
      display: flex;
      align-items: center;
      gap: 1rem;
      margin-block: 1rem;
    }
    .dc-toolbar__search {
      flex: 1;
      max-width: 24rem;
    }
    .dc-code {
      font-family: monospace;
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

  protected readonly search = signal('');
  protected readonly errors = signal<string[]>([]);
  private readonly list = rxResource({ params: () => this.type().entityType, stream: ({ params }) => this.api.list(params) });

  protected readonly singleton = computed(() => typeof this.type().key === 'object');
  protected readonly canCreate = computed(() => !this.singleton() && !this.type().noCreate && this.privileges.can(this.resource(), 'CREATE'));
  protected readonly canEdit = computed(() => this.privileges.can(this.resource(), 'EDIT'));
  protected readonly canDelete = computed(() => !this.singleton() && this.privileges.can(this.resource(), 'DELETE'));
  protected readonly columns = computed(() => ['code', 'name', ...(this.type().columns ?? []), 'state', 'actions']);
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

  protected async edit(listed: ConfigRow | null): Promise<void> {
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
