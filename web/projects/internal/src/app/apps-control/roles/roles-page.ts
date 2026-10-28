import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { AppsControlApi, I18nService, LocalizePipe, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { catchError, firstValueFrom, forkJoin, map, of } from 'rxjs';
import { ConfirmData, ConfirmDialog } from '../confirm-dialog';
import { DraftsService } from '../drafts.service';
import { MasterItem, MasterList } from '../master/master-list';
import { PrivilegesService } from '../privileges.service';
import { PermissionCards } from './permission-cards';
import { RoleEditor, RoleEditorData } from './role-editor';
import { RoleRow, permissionsOf, roleScreens, screenTree } from './role-models';

const RESOURCE = 'apps-control.roles';

/** Roles on the start side (with how many users hold each); the chosen role's screens and permissions on the end side. */
@Component({
  selector: 'dc-roles-page',
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, MasterList, PermissionCards, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'roles.title' | dcTranslate }}</h1>
      <p>{{ 'roles.hint' | dcTranslate }}</p>
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
        testId="roles"
        [title]="{ ar: 'الأدوار', en: 'Roles' }"
        [addLabel]="{ ar: 'إضافة دور', en: 'Add role' }"
        [items]="items()"
        [selected]="selected()"
        [canCreate]="privileges.can(resource, 'CREATE')"
        (selectItem)="select($event)"
        (view)="open($event, true)"
        (edit)="open($event, false)"
        (remove)="removeRole($event)"
        (add)="open(null, false)"
      />

      <section class="dc-panel" data-testid="role-panel">
        @if (role.value(); as r) {
          <header class="dc-panel__header">
            <div>
              <h2>
                {{ { ar: r.nameAr, en: r.nameEn } | dcLocalize }}
                <span class="dc-count" data-testid="role-screen-count">{{ roleScreenNodes().length }}</span>
              </h2>
              <span class="mono">{{ r.code }}</span>
            </div>
            <div class="dc-role-state">
              @if (r.appCode) {
                <span class="dc-pill">{{ r.appCode }}</span>
              }
              <span class="dc-pill" [class.dc-pill--green]="r.isActive">{{ (r.isActive ? 'config.active' : 'config.inactive') | dcTranslate }}</span>
              @if (canEdit(r)) {
                <button matButton="outlined" (click)="open(r.code, false)" data-testid="edit-role">
                  <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>
                  {{ 'config.edit' | dcTranslate }}
                </button>
              }
            </div>
          </header>
          <div class="dc-panel__body">
            <h3 class="dc-role-section">{{ 'roles.screensAndPermissions' | dcTranslate }}</h3>
            <dc-permission-cards [screens]="roleScreenNodes()" [permissions]="rolePermissions()" [readonly]="true" />
            @if (r.grants.length) {
              <p class="dc-muted dc-role-grants">{{ 'roles.grants' | dcTranslate }}: {{ r.grants.length }}</p>
            }
          </div>
        } @else if (role.isLoading() || lists.isLoading()) {
          <mat-progress-bar mode="indeterminate" />
        } @else {
          <p class="dc-empty dc-panel__body">{{ 'roles.pick' | dcTranslate }}</p>
        }
      </section>
    </div>
  `,
  styles: `
    .dc-role-state {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: flex-end;
      gap: 8px;
    }
    .dc-role-section {
      margin: 0 0 12px;
      font-size: 16px;
      font-weight: 700;
    }
    .dc-role-grants {
      margin: 16px 0 0;
    }
  `,
})
export class RolesPage {
  /** Route parameter: the chosen role (the first one when absent). */
  readonly code = input<string | undefined>(undefined);

  private readonly api = inject(AppsControlApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly drafts = inject(DraftsService);
  private readonly i18n = inject(I18nService);
  protected readonly privileges = inject(PrivilegesService);
  protected readonly resource = RESOURCE;

  protected readonly errors = signal<string[]>([]);
  private readonly roles = rxResource({ stream: () => this.api.list('apps.role') });
  private readonly counts = rxResource({ stream: () => this.api.counts('apps.role') });
  /** Resources, menus, menu groups and apps: the screen tree and the app choices. */
  protected readonly lists = rxResource({
    stream: () =>
      forkJoin({
        resources: this.api.list('apps.resource'),
        menus: this.api.list('apps.menu').pipe(catchError(() => of([]))),
        groups: this.api.list('apps.menu-group').pipe(catchError(() => of([]))),
        apps: this.api.list('apps.app').pipe(catchError(() => of([]))),
      }),
  });
  private readonly tree = computed(() =>
    this.lists.hasValue() ? screenTree(this.lists.value().resources, this.lists.value().menus, this.lists.value().groups) : [],
  );

  private readonly roleRows = computed(() => (this.roles.hasValue() ? this.roles.value() : []));
  protected readonly selected = computed(() => this.code() ?? this.roleRows()[0]?.code ?? null);
  /** The chosen role in full (list rows leave out privileges and grants). */
  protected readonly role = rxResource({
    params: () => this.selected() ?? undefined,
    stream: ({ params }) => this.api.get('apps.role', params).pipe(map((r) => r as RoleRow)),
  });

  protected readonly rolePermissions = computed(() => (this.role.hasValue() ? permissionsOf(this.role.value().privileges) : {}));
  protected readonly roleScreenNodes = computed(() => {
    const codes = roleScreens(this.tree(), this.rolePermissions());
    return this.tree().filter((s) => codes.includes(s.code));
  });

  protected readonly items = computed<MasterItem[]>(() => {
    const counts = this.counts.hasValue() ? this.counts.value() : {};
    const canEdit = this.privileges.can(RESOURCE, 'EDIT');
    const canDelete = this.privileges.can(RESOURCE, 'DELETE');
    return this.roleRows().map((r) => {
      const users = counts[r.code] ?? 0;
      const system = r.owner === 'SYSTEM';
      return {
        code: r.code,
        title: { ar: r.nameAr, en: r.nameEn },
        badge: users
          ? { label: `${users} ${this.i18n.translate(users === 1 ? 'roles.userWord' : 'roles.usersWord')}`, tone: 'green' }
          : { label: this.i18n.translate('roles.noUsers'), tone: 'gray' },
        locked: system,
        canEdit: !system && canEdit,
        canDelete: !system && canDelete && users === 0,
        deleteBlockedReason: !system && canDelete && users > 0 ? this.i18n.translate('roles.deleteHasUsers') : undefined,
      } satisfies MasterItem;
    });
  });

  protected select(code: string): void {
    void this.router.navigate(['/apps-control/roles', code]);
  }

  protected canEdit(role: RoleRow): boolean {
    return role.owner !== 'SYSTEM' && this.privileges.can(RESOURCE, 'EDIT');
  }

  protected async open(code: string | null, readonly: boolean): Promise<void> {
    this.errors.set([]);
    let role: RoleRow | null;
    try {
      role = code ? ((await firstValueFrom(this.api.get('apps.role', code))) as RoleRow) : null;
      if (!this.lists.hasValue()) {
        return;
      }
    } catch (error) {
      this.errors.set(problemMessages(error));
      return;
    }
    const data: RoleEditorData = {
      role,
      screens: this.tree(),
      apps: this.lists.value().apps,
      readonly: readonly || (role !== null && role.owner === 'SYSTEM'),
    };
    const saved = await firstValueFrom(
      this.dialog
        .open<RoleEditor, RoleEditorData, boolean>(RoleEditor, { data, autoFocus: 'first-tabbable', width: '76rem', maxWidth: '96vw', maxHeight: '94vh' })
        .afterClosed(),
    );
    if (saved) {
      this.roles.reload();
      this.role.reload();
    }
  }

  protected async removeRole(code: string): Promise<void> {
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
      await this.drafts.addChange('apps.role', code, 'DELETE', null);
      this.roles.reload();
      this.counts.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}
