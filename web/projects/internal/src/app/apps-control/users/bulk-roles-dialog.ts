import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
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
import { catchError, firstValueFrom, forkJoin, of } from 'rxjs';
import { ConfigBulkData } from '../config/config-types';
import { payloadOf } from '../config/form-values';
import { DraftsService } from '../drafts.service';

interface RoleEntry {
  roleCode: string;
  companyCode?: string | null;
  branches?: string[];
  orgUnitCode?: string | null;
}

/** What a bulk change does to one user: roles added or removed, and those left as they were (with why). */
export interface BulkPlan {
  user: ConfigRow;
  roles: RoleEntry[];
  added: string[];
  removed: string[];
  unchanged: string[];
}

/** Adds or removes roles (in a company and branches) for one user; never replaces what is there. */
export function planRoles(
  user: ConfigRow,
  action: 'ADD' | 'REMOVE',
  company: string | null,
  branches: string[],
  roles: string[],
): BulkPlan {
  const current = ((user['roles'] as RoleEntry[] | undefined) ?? []).map((r) => ({ ...r }));
  const same = (r: RoleEntry, code: string) =>
    r.roleCode === code && (r.companyCode ?? null) === company && !r.orgUnitCode;
  const plan: BulkPlan = { user, roles: current, added: [], removed: [], unchanged: [] };
  for (const code of roles) {
    const held = current.find((r) => same(r, code));
    if (action === 'ADD') {
      if (held) plan.unchanged.push(code);
      else {
        current.push({ roleCode: code, companyCode: company, branches: company ? branches : [] });
        plan.added.push(code);
      }
    } else if (held) {
      current.splice(current.indexOf(held), 1);
      plan.removed.push(code);
    } else {
      plan.unchanged.push(code);
    }
  }
  return plan;
}

/**
 * Adds or removes roles for the ticked users in one go: first a review of what changes for each of them (roles already
 * held, or not held, are listed and left alone), then every change goes into the draft change set.
 */
@Component({
  selector: 'dc-bulk-roles-dialog',
  imports: [
    MatButtonModule,
    MatButtonToggleModule,
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
    <h2 mat-dialog-title>{{ 'bulk.title' | dcTranslate }} ({{ data.rows.length }})</h2>
    <mat-dialog-content>
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (error of errors(); track $index) {
            <li dir="auto">{{ error }}</li>
          }
        </ul>
      }
      <mat-button-toggle-group
        [value]="action()"
        (change)="action.set($event.value); plans.set(null)"
        data-testid="bulk-action"
      >
        <mat-button-toggle value="ADD">{{ 'bulk.add' | dcTranslate }}</mat-button-toggle>
        <mat-button-toggle value="REMOVE">{{ 'bulk.remove' | dcTranslate }}</mat-button-toggle>
      </mat-button-toggle-group>
      <div class="dc-bulk">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'access.company' | dcTranslate }}</mat-label>
          <mat-select
            [value]="company()"
            (selectionChange)="company.set($event.value); branches.set([]); plans.set(null)"
            data-testid="bulk-company"
          >
            <mat-option [value]="null">{{ 'access.allCompanies' | dcTranslate }}</mat-option>
            @for (c of lists.value()?.companies ?? []; track c.code) {
              <mat-option [value]="c.code">{{ name(c) | dcLocalize }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (action() === 'ADD') {
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'access.branches' | dcTranslate }}</mat-label>
            <mat-select
              multiple
              [value]="branches()"
              (selectionChange)="branches.set($event.value); plans.set(null)"
              [disabled]="!company()"
              [placeholder]="'access.allBranches' | dcTranslate"
            >
              @for (b of companyBranches(); track b.code) {
                <mat-option [value]="b.code">{{ name(b) | dcLocalize }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'access.roleList' | dcTranslate }}</mat-label>
          <mat-select
            multiple
            [value]="roles()"
            (selectionChange)="roles.set($event.value); plans.set(null)"
            data-testid="bulk-roles"
          >
            @for (r of lists.value()?.roles ?? []; track r.code) {
              <mat-option [value]="r.code">{{ name(r) | dcLocalize }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>
      @if (busy()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (plans(); as list) {
        <h3>{{ 'bulk.review' | dcTranslate }}</h3>
        <ul class="dc-bulk__plan" data-testid="bulk-plan">
          @for (p of list; track p.user.code) {
            <li>
              <strong>{{ name(p.user) | dcLocalize }}</strong>
              <span class="mono dc-muted">{{ p.user.code }}</span>
              @for (code of p.added; track code) {
                <span class="dc-pill dc-pill--green">+ {{ roleName(code) | dcLocalize }}</span>
              }
              @for (code of p.removed; track code) {
                <span class="dc-pill">− {{ roleName(code) | dcLocalize }}</span>
              }
              @for (code of p.unchanged; track code) {
                <span class="dc-muted"
                  >{{ (action() === 'ADD' ? 'bulk.alreadyHas' : 'bulk.doesNotHave') | dcTranslate }}
                  {{ roleName(code) | dcLocalize }}</span
                >
              }
            </li>
          }
        </ul>
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      <button matButton="outlined" mat-dialog-close>{{ 'form.cancel' | dcTranslate }}</button>
      @if (!plans()) {
        <button
          matButton="filled"
          type="button"
          (click)="review()"
          [disabled]="busy() || !roles().length"
          data-testid="bulk-review"
        >
          {{ 'bulk.reviewButton' | dcTranslate }}
        </button>
      } @else {
        <button
          matButton="filled"
          type="button"
          (click)="apply()"
          [disabled]="busy() || !changes()"
          data-testid="bulk-apply"
        >
          {{ drafts.saveLabel('apps.user') | dcTranslate }} ({{ changes() }})
        </button>
      }
    </mat-dialog-actions>
  `,
  styles: `
    .dc-bulk {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr));
      gap: 8px;
      margin: 12px 0;
    }
    .dc-bulk__plan {
      display: grid;
      gap: 6px;
      padding-inline-start: 18px;
    }
    .dc-bulk__plan li > * + * {
      margin-inline-start: 6px;
    }
  `,
})
export class BulkRolesDialog {
  protected readonly data = inject<ConfigBulkData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<BulkRolesDialog, boolean>);
  private readonly api = inject(AppsControlApi);
  protected readonly drafts = inject(DraftsService);

  protected readonly action = signal<'ADD' | 'REMOVE'>('ADD');
  protected readonly company = signal<string | null>(null);
  protected readonly branches = signal<string[]>([]);
  protected readonly roles = signal<string[]>([]);
  protected readonly plans = signal<BulkPlan[] | null>(null);
  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly changes = computed(
    () => (this.plans() ?? []).filter((p) => p.added.length || p.removed.length).length,
  );

  protected readonly lists = rxResource({
    stream: () =>
      forkJoin({
        companies: this.api.list('apps.company').pipe(catchError(() => of([] as ConfigRow[]))),
        branches: this.api.list('apps.branch').pipe(catchError(() => of([] as ConfigRow[]))),
        roles: this.api.list('apps.role').pipe(catchError(() => of([] as ConfigRow[]))),
      }),
  });
  protected readonly companyBranches = computed(() =>
    (this.lists.value()?.branches ?? []).filter((b) => b['companyCode'] === this.company()),
  );

  protected name(row: ConfigRow): LocalizedText {
    return { ar: row.nameAr, en: row.nameEn };
  }

  protected roleName(code: string): LocalizedText {
    const role = (this.lists.value()?.roles ?? []).find((r) => r.code === code);
    return role ? this.name(role) : { ar: code, en: code };
  }

  protected async review(): Promise<void> {
    this.busy.set(true);
    this.errors.set([]);
    try {
      const users = await Promise.all(
        this.data.rows.map((r) => firstValueFrom(this.api.get('apps.user', String(r['code'])))),
      );
      this.plans.set(
        users.map((u) =>
          planRoles(u, this.action(), this.company(), this.branches(), this.roles()),
        ),
      );
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }

  protected async apply(): Promise<void> {
    this.busy.set(true);
    this.errors.set([]);
    try {
      for (const plan of (this.plans() ?? []).filter((p) => p.added.length || p.removed.length)) {
        await this.drafts.addChange('apps.user', plan.user.code, 'UPDATE', {
          ...payloadOf(plan.user),
          roles: plan.roles,
        });
      }
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }
}
