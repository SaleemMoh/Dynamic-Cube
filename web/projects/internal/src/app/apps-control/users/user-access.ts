import { ChangeDetectionStrategy, Component, computed, inject, input, model } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
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
} from '@dynamic-cube/shared';
import { catchError, forkJoin, of } from 'rxjs';
import {
  ALL,
  AccessRow,
  HeldRole,
  PersonalPrivilege,
  RoleContents,
  departmentRoles,
  pickWithAll,
} from './access-model';
import { PersonalTree, RoleSource } from './personal-tree';

/**
 * A person's access (ADR-0036), the Access tab of the user editor: roles by company and branches ("all" is an option
 * that is chosen, never an empty field), the roles their departments give them, and personal privileges on the menu
 * tree of an app.
 */
@Component({
  selector: 'dc-user-access',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    PersonalTree,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
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
                (selectionChange)="setCompany(i, $event.value)"
                [disabled]="readonly()"
                [placeholder]="'access.chooseCompany' | dcTranslate"
                data-testid="access-company"
              >
                <mat-option [value]="all">{{ 'access.allCompanies' | dcTranslate }}</mat-option>
                @for (c of companies(); track c.code) {
                  <mat-option [value]="c.code">{{ name(c) | dcLocalize }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-select
                multiple
                [value]="row.branches"
                (selectionChange)="patch(i, { branches: pick(row.branches, $event.value) })"
                [disabled]="readonly() || !row.company || row.company === all"
                [placeholder]="'access.chooseBranches' | dcTranslate"
                data-testid="access-branches"
              >
                <mat-option [value]="all">{{ 'access.allBranches' | dcTranslate }}</mat-option>
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
                  {{ 'access.inWholeOrganization' | dcTranslate }}
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
      <dc-personal-tree
        [(value)]="personal"
        [companies]="companies()"
        [resources]="lists.value().resources"
        [lovs]="lists.value().lovs"
        [roleSource]="roleSource()"
        [readonly]="readonly()"
      />
    }
  `,
  styles: `
    h3 {
      margin: 20px 0 2px;
    }
    h3:first-child {
      margin-top: 4px;
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
export class UserAccess {
  /** The full user row (its placements give the department roles). */
  readonly user = input.required<ConfigRow>();
  readonly readonly = input(false);
  /** Roles by company and branches, as edited. */
  readonly rows = model.required<AccessRow[]>();
  readonly personal = model.required<PersonalPrivilege[]>();

  private readonly api = inject(AppsControlApi);
  protected readonly all = ALL;

  protected readonly lists = rxResource({
    stream: () => {
      const list = (type: string) =>
        this.api.list(type).pipe(catchError(() => of([] as ConfigRow[])));
      return forkJoin({
        companies: list('apps.company'),
        branches: list('apps.branch'),
        roles: list('apps.role'),
        units: list('apps.org-unit'),
        resources: list('apps.resource'),
        lovs: list('apps.lov'),
        menus: list('apps.menu'),
      });
    },
  });
  protected readonly companies = computed(() => this.lists.value()?.companies ?? []);
  protected readonly roles = computed(() => this.lists.value()?.roles ?? []);
  protected readonly department = computed(() => {
    const lists = this.lists.value();
    return lists
      ? departmentRoles(
          (this.user()['assignments'] as { orgUnitCode: string }[] | undefined) ?? [],
          lists.units,
          lists.branches,
        )
      : [];
  });

  /** Roles the person holds as edited here (and from their departments), with where they hold them. */
  private readonly held = computed<HeldRole[]>(
    () => [
      ...this.rows().flatMap((row) =>
        row.company ? row.roles.map((roleCode) => ({ roleCode, company: row.company! })) : [],
      ),
      ...this.department().flatMap((d) =>
        d.roles.map((roleCode) => ({
          roleCode,
          company: (d.branch?.['companyCode'] as string | undefined) ?? ALL,
        })),
      ),
    ],
    { equal: (a, b) => JSON.stringify(a) === JSON.stringify(b) },
  );
  /** Full rows of the held roles (lists leave out their contents). */
  private readonly roleRows = rxResource({
    params: () => [...new Set(this.held().map((h) => h.roleCode))].sort(),
    stream: ({ params }) =>
      params.length
        ? forkJoin(
            params.map((code) => this.api.get('apps.role', code).pipe(catchError(() => of(null)))),
          )
        : of([] as (ConfigRow | null)[]),
  });
  protected readonly roleSource = computed<RoleSource | null>(() => {
    const lists = this.lists.value();
    const rows = this.roleRows.value();
    if (!lists || !rows) return null;
    return {
      held: this.held(),
      roles: new Map(
        rows
          .filter((r): r is ConfigRow => !!r)
          .map((r) => [r.code, r as unknown as RoleContents] as const),
      ),
      menuScreens: new Map(
        lists.menus
          .filter((m) => m['resourceCode'])
          .map((m) => [m.code, m['resourceCode'] as string] as const),
      ),
    };
  });

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
    return company && company !== ALL
      ? (this.lists.value()?.branches ?? []).filter((b) => b['companyCode'] === company)
      : [];
  }

  protected pick(previous: string[], next: string[]): string[] {
    return pickWithAll(previous, next);
  }

  protected setCompany(index: number, company: string): void {
    // All companies hold the role in all branches; another company starts without branches chosen.
    this.patch(index, { company, branches: company === ALL ? [ALL] : [] });
  }

  protected patch(index: number, change: Partial<AccessRow>): void {
    this.rows.update((rows) => rows.map((r, i) => (i === index ? { ...r, ...change } : r)));
  }

  protected addRow(): void {
    const only = this.companies().length === 1 ? this.companies()[0].code : null;
    this.rows.update((rows) => [
      ...rows,
      { company: only, branches: [], roles: [], orgUnitCode: null },
    ]);
  }

  protected removeRow(index: number): void {
    this.rows.update((rows) => rows.filter((_, i) => i !== index));
  }
}
