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
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import {
  AppsControlApi,
  ConfigRow,
  LocalizePipe,
  LocalizedText,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { catchError, forkJoin, of } from 'rxjs';
import { ConfigEditorData } from '../config/config-editor';
import { payloadOf } from '../config/form-values';
import { DraftsService } from '../drafts.service';
import { PrivilegesService } from '../privileges.service';
import { actionLabel } from '../roles/role-models';

/** A role as the user row stores it (one per role and company). */
interface RoleEntry {
  roleCode: string;
  orgUnitCode?: string | null;
  companyCode?: string | null;
  branches?: string[];
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
}

/** A line of the Access tab: roles held in a company (or every company) and some of its branches (or all). */
export interface AccessRow {
  company: string | null;
  branches: string[];
  roles: string[];
  /** Kept from rows that hold a role only within an org unit. */
  orgUnitCode: string | null;
}

/** Actions on a screen given to the person alone. */
export interface PersonalRow {
  company: string | null;
  resourceCode: string | null;
  actions: string[];
}

/** A department role: what the person receives from a unit they are placed in. */
export interface DepartmentRole {
  unit: ConfigRow;
  roles: string[];
  /** The branch the unit serves (it or a unit above it), where the roles are held; none: every company. */
  branch: ConfigRow | null;
}

/** The user's roles grouped by company, branches and unit, as the Access tab shows them. */
export function groupRoles(entries: RoleEntry[]): AccessRow[] {
  const rows = new Map<string, AccessRow>();
  for (const entry of entries) {
    const branches = [...(entry.branches ?? [])].sort();
    const key = `${entry.companyCode ?? ''}|${branches.join(',')}|${entry.orgUnitCode ?? ''}`;
    const row = rows.get(key) ?? {
      company: entry.companyCode ?? null,
      branches,
      roles: [],
      orgUnitCode: entry.orgUnitCode ?? null,
    };
    row.roles.push(entry.roleCode);
    rows.set(key, row);
  }
  return [...rows.values()];
}

/** The rows back as one entry per role, keeping the dates of entries that stay. */
export function flattenRoles(rows: AccessRow[], existing: RoleEntry[]): RoleEntry[] {
  return rows.flatMap((row) =>
    row.roles.map((code) => {
      const before = existing.find(
        (e) =>
          e.roleCode === code &&
          (e.companyCode ?? null) === row.company &&
          (e.orgUnitCode ?? null) === row.orgUnitCode,
      );
      return {
        ...before,
        roleCode: code,
        companyCode: row.company,
        branches: row.company ? row.branches : [],
        orgUnitCode: row.orgUnitCode,
      };
    }),
  );
}

/** The roles of the units a person is placed in, with the branch each unit serves (looking up the tree). */
export function departmentRoles(
  assignments: { orgUnitCode: string }[],
  units: ConfigRow[],
  branches: ConfigRow[],
): DepartmentRole[] {
  const byCode = new Map(units.map((u) => [u.code, u]));
  const served = (code: string): ConfigRow | null => {
    for (
      let unit = byCode.get(code), depth = 0;
      unit && depth < 50;
      unit = byCode.get(unit['parentCode'] as string), depth++
    ) {
      const branch = branches.find((b) => b['orgUnitCode'] === unit!.code);
      if (branch) return branch;
    }
    return null;
  };
  return assignments
    .map((a) => byCode.get(a.orgUnitCode))
    .filter(
      (u): u is ConfigRow => !!u && ((u['defaultRoles'] as string[] | undefined) ?? []).length > 0,
    )
    .map((unit) => ({ unit, roles: unit['defaultRoles'] as string[], branch: served(unit.code) }));
}

/**
 * One place for a person's access (ADR-0036): roles by company and branches, the roles their departments give them, and
 * screens given to them alone. Saving adds the change to the draft change set.
 */
@Component({
  selector: 'dc-access-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ 'access.title' | dcTranslate }} —
      {{ { ar: data.row!.nameAr, en: data.row!.nameEn } | dcLocalize }}
      <span class="mono dc-muted">{{ data.row!.code }}</span>
    </h2>
    <mat-dialog-content>
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (error of errors(); track $index) {
            <li dir="auto">{{ error }}</li>
          }
        </ul>
      }
      @if (!lists.hasValue()) {
        <mat-progress-bar mode="indeterminate" />
      } @else {
        <h3>{{ 'access.roles' | dcTranslate }}</h3>
        <p class="dc-muted">{{ 'access.rolesHint' | dcTranslate }}</p>
        <div class="dc-access" data-testid="access-rows">
          @if (rows().length) {
            <div class="dc-access__head">
              <span>{{ 'access.company' | dcTranslate }}</span>
              <span>{{ 'access.branches' | dcTranslate }}</span>
              <span>{{ 'access.roleList' | dcTranslate }}</span>
              <span></span>
            </div>
          }
          @for (row of rows(); track $index; let i = $index) {
            <div class="dc-access__row" [attr.data-testid]="'access-row-' + i">
              <mat-form-field subscriptSizing="dynamic">
                <mat-select
                  [value]="row.company"
                  (selectionChange)="patch(i, { company: $event.value, branches: [] })"
                  [disabled]="readonly()"
                  data-testid="access-company"
                >
                  <mat-option [value]="null">{{ 'access.allCompanies' | dcTranslate }}</mat-option>
                  @for (c of companies(); track c.code) {
                    <mat-option [value]="c.code">{{ name(c) | dcLocalize }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-select
                  multiple
                  [value]="row.branches"
                  (selectionChange)="patch(i, { branches: $event.value })"
                  [disabled]="readonly() || !row.company"
                  [placeholder]="'access.allBranches' | dcTranslate"
                  data-testid="access-branches"
                >
                  @for (b of branchesOf(row.company); track b.code) {
                    <mat-option [value]="b.code">{{ name(b) | dcLocalize }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-select
                  multiple
                  [value]="row.roles"
                  (selectionChange)="patch(i, { roles: $event.value })"
                  [disabled]="readonly()"
                  data-testid="access-roles"
                >
                  @for (r of roles(); track r.code) {
                    <mat-option [value]="r.code">{{ name(r) | dcLocalize }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              @if (!readonly()) {
                <button
                  matIconButton
                  type="button"
                  (click)="removeRow(i)"
                  [attr.aria-label]="'config.delete' | dcTranslate"
                >
                  <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                </button>
              }
              @if (row.orgUnitCode) {
                <small class="dc-access__note dc-muted"
                  >{{ 'access.onlyInUnit' | dcTranslate }}
                  {{ unitName(row.orgUnitCode) | dcLocalize }}</small
                >
              }
            </div>
          }
        </div>
        @if (!readonly()) {
          <button matButton type="button" (click)="addRow()" data-testid="access-add-row">
            <mat-icon fontSet="material-symbols-outlined">add</mat-icon
            >{{ 'access.addRow' | dcTranslate }}
          </button>
        }

        <h3>{{ 'access.department' | dcTranslate }}</h3>
        <p class="dc-muted">{{ 'access.departmentHint' | dcTranslate }}</p>
        @if (department().length) {
          <ul class="dc-access__department" data-testid="access-department">
            @for (d of department(); track d.unit.code) {
              <li>
                <strong>{{ name(d.unit) | dcLocalize }}</strong
                >:
                @for (code of d.roles; track code; let last = $last) {
                  {{ roleName(code) | dcLocalize }}{{ last ? '' : '، ' }}
                }
                <span class="dc-muted">
                  —
                  @if (d.branch; as b) {
                    {{ 'access.inBranch' | dcTranslate }} {{ name(b) | dcLocalize }}
                  } @else {
                    {{ 'access.inEveryCompany' | dcTranslate }}
                  }
                </span>
              </li>
            }
          </ul>
        } @else {
          <p class="dc-muted">{{ 'access.noDepartment' | dcTranslate }}</p>
        }

        <h3>{{ 'access.personal' | dcTranslate }}</h3>
        <p class="dc-muted">{{ 'access.personalHint' | dcTranslate }}</p>
        <div class="dc-access" data-testid="access-personal">
          @for (row of personal(); track $index; let i = $index) {
            <div class="dc-access__row">
              <mat-form-field subscriptSizing="dynamic">
                <mat-select
                  [value]="row.company"
                  (selectionChange)="patchPersonal(i, { company: $event.value })"
                  [disabled]="readonly()"
                >
                  <mat-option [value]="null">{{ 'access.allCompanies' | dcTranslate }}</mat-option>
                  @for (c of companies(); track c.code) {
                    <mat-option [value]="c.code">{{ name(c) | dcLocalize }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-select
                  [value]="row.resourceCode"
                  (selectionChange)="
                    patchPersonal(i, { resourceCode: $event.value, actions: ['VIEW'] })
                  "
                  [disabled]="readonly()"
                  [placeholder]="'access.screen' | dcTranslate"
                  data-testid="personal-screen"
                >
                  @for (r of screens(); track r.code) {
                    <mat-option [value]="r.code">{{ name(r) | dcLocalize }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-select
                  multiple
                  [value]="row.actions"
                  (selectionChange)="patchPersonal(i, { actions: $event.value })"
                  [disabled]="readonly() || !row.resourceCode"
                  [placeholder]="'access.actions' | dcTranslate"
                  data-testid="personal-actions"
                >
                  @for (a of actionsOf(row.resourceCode); track a) {
                    <mat-option [value]="a">{{ actionName(a) | dcLocalize }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              @if (!readonly()) {
                <button
                  matIconButton
                  type="button"
                  (click)="removePersonal(i)"
                  [attr.aria-label]="'config.delete' | dcTranslate"
                >
                  <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                </button>
              }
            </div>
          }
        </div>
        @if (!readonly()) {
          <button matButton type="button" (click)="addPersonal()" data-testid="personal-add">
            <mat-icon fontSet="material-symbols-outlined">add</mat-icon
            >{{ 'access.addPersonal' | dcTranslate }}
          </button>
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      <button matButton="outlined" mat-dialog-close>
        {{ (readonly() ? 'common.close' : 'form.cancel') | dcTranslate }}
      </button>
      @if (!readonly()) {
        <button
          matButton="filled"
          type="button"
          (click)="save()"
          [disabled]="saving() || !lists.hasValue()"
          data-testid="access-save"
        >
          {{ drafts.saveLabel('apps.user') | dcTranslate }}
        </button>
      }
    </mat-dialog-actions>
  `,
  styles: `
    h3 {
      margin: 20px 0 2px;
    }
    .dc-access {
      display: grid;
      gap: 6px;
      margin: 8px 0;
    }
    .dc-access__head,
    .dc-access__row {
      display: grid;
      grid-template-columns: 1fr 1fr 1.6fr 40px;
      gap: 8px;
      align-items: center;
    }
    .dc-access__head {
      color: var(--dga-gray-600);
      font-size: 13px;
      font-weight: 600;
    }
    .dc-access__note {
      grid-column: 1 / -1;
    }
    .dc-access__department {
      margin: 6px 0;
      padding-inline-start: 18px;
    }
  `,
})
export class AccessDialog {
  protected readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<AccessDialog, boolean>);
  private readonly api = inject(AppsControlApi);
  protected readonly drafts = inject(DraftsService);
  private readonly privileges = inject(PrivilegesService);

  protected readonly errors = signal<string[]>([]);
  protected readonly saving = signal(false);
  protected readonly readonly = computed(
    () => this.data.row?.owner === 'SYSTEM' || !this.privileges.can('apps-control.users', 'EDIT'),
  );
  protected readonly rows = signal<AccessRow[]>([]);
  protected readonly personal = signal<PersonalRow[]>([]);

  protected readonly lists = rxResource({
    stream: () => {
      const list = (type: string) =>
        this.api.list(type).pipe(catchError(() => of([] as ConfigRow[])));
      return forkJoin({
        user: this.api.get('apps.user', this.data.row!.code),
        companies: list('apps.company'),
        branches: list('apps.branch'),
        roles: list('apps.role'),
        units: list('apps.org-unit'),
        resources: list('apps.resource'),
      });
    },
  });
  protected readonly companies = computed(() => this.lists.value()?.companies ?? []);
  protected readonly roles = computed(() => this.lists.value()?.roles ?? []);
  protected readonly screens = computed(() =>
    (this.lists.value()?.resources ?? []).filter((r) => r['resourceType'] !== 'API'),
  );
  protected readonly department = computed(() => {
    const lists = this.lists.value();
    return lists
      ? departmentRoles(
          (lists.user['assignments'] as { orgUnitCode: string }[] | undefined) ?? [],
          lists.units,
          lists.branches,
        )
      : [];
  });

  constructor() {
    // The rows start from the user's stored access once loaded.
    let loaded = false;
    effect(() => {
      const lists = this.lists.value();
      if (!lists || loaded) return;
      loaded = true;
      untracked(() => {
        this.rows.set(groupRoles((lists.user['roles'] as RoleEntry[] | undefined) ?? []));
        this.personal.set(
          (
            (lists.user['privileges'] as
              | { companyCode?: string | null; resourceCode: string; actions: string[] }[]
              | undefined) ?? []
          ).map((p) => ({
            company: p.companyCode ?? null,
            resourceCode: p.resourceCode,
            actions: p.actions,
          })),
        );
      });
    });
  }

  protected actionName(action: string): LocalizedText {
    return actionLabel(action);
  }

  protected name(row: ConfigRow): LocalizedText {
    return { ar: row.nameAr, en: row.nameEn };
  }

  protected roleName(code: string): LocalizedText {
    const role = this.roles().find((r) => r.code === code);
    return role ? this.name(role) : { ar: code, en: code };
  }

  protected unitName(code: string): LocalizedText {
    const unit = (this.lists.value()?.units ?? []).find((u) => u.code === code);
    return unit ? this.name(unit) : { ar: code, en: code };
  }

  protected branchesOf(company: string | null): ConfigRow[] {
    return company
      ? (this.lists.value()?.branches ?? []).filter((b) => b['companyCode'] === company)
      : [];
  }

  protected actionsOf(resource: string | null): string[] {
    return (
      (this.screens().find((r) => r.code === resource)?.['availableActions'] as
        string[] | undefined) ?? ['VIEW']
    ).slice();
  }

  protected patch(index: number, change: Partial<AccessRow>): void {
    this.rows.update((rows) => rows.map((r, i) => (i === index ? { ...r, ...change } : r)));
  }

  protected addRow(): void {
    this.rows.update((rows) => [
      ...rows,
      {
        company: this.companies().length === 1 ? this.companies()[0].code : null,
        branches: [],
        roles: [],
        orgUnitCode: null,
      },
    ]);
  }

  protected removeRow(index: number): void {
    this.rows.update((rows) => rows.filter((_, i) => i !== index));
  }

  protected patchPersonal(index: number, change: Partial<PersonalRow>): void {
    this.personal.update((rows) => rows.map((r, i) => (i === index ? { ...r, ...change } : r)));
  }

  protected addPersonal(): void {
    this.personal.update((rows) => [...rows, { company: null, resourceCode: null, actions: [] }]);
  }

  protected removePersonal(index: number): void {
    this.personal.update((rows) => rows.filter((_, i) => i !== index));
  }

  protected async save(): Promise<void> {
    const lists = this.lists.value();
    if (!lists) return;
    const rows = this.rows().filter((r) => r.roles.length);
    const personal = this.personal().filter((p) => p.resourceCode && p.actions.length);
    this.errors.set([]);
    this.saving.set(true);
    try {
      const payload = {
        ...payloadOf(lists.user),
        roles: flattenRoles(rows, (lists.user['roles'] as RoleEntry[] | undefined) ?? []),
        privileges: personal.map((p) => ({
          companyCode: p.company,
          resourceCode: p.resourceCode,
          actions: p.actions,
        })),
      };
      await this.drafts.addChange('apps.user', this.data.row!.code, 'UPDATE', payload);
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.saving.set(false);
    }
  }
}
