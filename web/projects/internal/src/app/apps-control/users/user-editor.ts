import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import {
  DynamicForm,
  I18nService,
  LocalizePipe,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { ConfigEditor } from '../config/config-editor';
import { PrivilegesService } from '../privileges.service';
import { SubmitButton } from '../submit-button';
import {
  AccessRow,
  PersonalPrivilege,
  RoleEntry,
  flattenRoles,
  groupRoles,
  personalPayload,
  readPersonal,
  rowProblems,
} from './access-model';
import { ACCESS_TRANSLATIONS } from './access-translations';
import {
  CompanyAdmission,
  CompanyRow,
  InstanceAdmission,
  advancedRoles,
  companyAdmissions,
  companyProblems,
  companyRows,
  instanceAdmissions,
  tableRoleEntries,
} from './company-access';
import { UserAccess } from './user-access';
import { UserCompanies } from './user-companies';

/**
 * A user's editor (ADR-0036): their details in one tab and their access in the other (roles by company and branches,
 * department roles, personal privileges on the menu tree). Both are saved together as one change.
 */
@Component({
  selector: 'dc-user-editor',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatProgressBarModule,
    MatTabsModule,
    DynamicForm,
    SubmitButton,
    UserAccess,
    UserCompanies,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ data.type.title | dcLocalize }} —
      @if (data.row; as row) {
        {{ { ar: row.nameAr, en: row.nameEn } | dcLocalize }}
        <span class="mono dc-muted" dir="ltr">{{ row.code }}</span>
      }
    </h2>
    <mat-dialog-content>
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (error of errors(); track $index) {
            <li dir="auto">{{ error }}</li>
          }
        </ul>
      }
      <mat-tab-group
        [selectedIndex]="tab()"
        (selectedIndexChange)="tab.set($event)"
        [preserveContent]="true"
        animationDuration="0ms"
        mat-stretch-tabs="false"
      >
        <mat-tab [label]="'user.tabDetails' | dcTranslate">
          <div class="dc-user-editor__tab" data-testid="user-details">
            @if (schema.hasValue()) {
              <dc-dynamic-form
                [schema]="schema.value()"
                [initialValue]="initial"
                [showActions]="false"
              />
            } @else {
              <mat-progress-bar mode="indeterminate" />
            }
          </div>
        </mat-tab>
        @if (data.row; as row) {
          <mat-tab [label]="'user.tabAccess' | dcTranslate">
            <div class="dc-user-editor__tab" data-testid="user-access">
              <dc-user-companies
                [readonly]="readonly()"
                [(rows)]="companies"
                [(defaultOrganization)]="defaultOrganization"
              />
              <details class="dc-user-editor__advanced" [open]="advancedInUse">
                <summary data-testid="user-advanced">{{ 'access.advanced' | dcTranslate }}</summary>
                <p class="dc-muted">{{ 'access.advancedHint' | dcTranslate }}</p>
                <dc-user-access
                  [user]="row"
                  [readonly]="readonly()"
                  [(rows)]="rows"
                  [(personal)]="personal"
                />
              </details>
            </div>
          </mat-tab>
        }
      </mat-tab-group>
    </mat-dialog-content>
    <mat-dialog-actions>
      <button matButton="outlined" type="button" mat-dialog-close data-testid="form-cancel">
        {{ (readonly() ? 'common.close' : 'form.cancel') | dcTranslate }}
      </button>
      @if (!readonly()) {
        <dc-submit-button
          [entityType]="data.type.entityType"
          [disabled]="saving() || !schema.hasValue()"
          (submitted)="saveAll(true)"
        />
        <button
          matButton="filled"
          type="button"
          (click)="saveAll()"
          [disabled]="saving() || !schema.hasValue()"
          data-testid="user-save"
        >
          {{ drafts.saveLabel(data.type.entityType) | dcTranslate }}
        </button>
      }
    </mat-dialog-actions>
  `,
  styles: `
    .dc-user-editor__tab {
      padding: 16px 4px 4px;
    }
    .dc-user-editor__advanced {
      margin-top: 20px;
      border-top: 1px solid var(--dga-gray-200);
      padding-top: 12px;
    }
    .dc-user-editor__advanced > summary {
      cursor: pointer;
      font-weight: 600;
    }
  `,
})
export class UserEditor extends ConfigEditor {
  private readonly i18n = inject(I18nService);
  private readonly privileges = inject(PrivilegesService);
  private readonly form = viewChild(DynamicForm);

  protected readonly tab = signal(0);
  protected readonly saving = signal(false);
  protected readonly readonly = computed(
    () => this.data.row?.owner === 'SYSTEM' || !this.privileges.can('apps-control.users', 'EDIT'),
  );
  private readonly storedRoles = (this.data.row?.['roles'] as RoleEntry[] | undefined) ?? [];
  private readonly storedInstances =
    (this.data.row?.['instances'] as InstanceAdmission[] | undefined) ?? [];
  /** The simple table: companies the person may enter and the roles they hold in each, in all branches. */
  protected readonly companies = signal<CompanyRow[]>(
    companyRows(
      (this.data.row?.['companies'] as CompanyAdmission[] | undefined) ?? [],
      this.storedRoles,
    ),
  );
  protected readonly defaultOrganization = signal<string | null>(
    this.storedInstances.find((i) => i.isDefault)?.entityCode ?? null,
  );
  /** The Advanced part: roles in every company, in some branches, or only within a unit. */
  protected readonly rows = signal<AccessRow[]>(groupRoles(advancedRoles(this.storedRoles)));
  protected readonly personal = signal<PersonalPrivilege[]>(
    readPersonal(this.data.row?.['privileges']),
  );
  /** The Advanced part opens by itself when the person already has something in it. */
  protected readonly advancedInUse = this.rows().length > 0 || this.personal().length > 0;

  constructor() {
    super();
    this.i18n.register(ACCESS_TRANSLATIONS);
  }

  /** Saves the details and the access together; a problem opens the tab it is on. */
  protected async saveAll(submit = false): Promise<void> {
    this.errors.set([]);
    const value = this.form()?.read();
    if (!value) {
      this.tab.set(0);
      return;
    }
    const problems = [...companyProblems(this.companies()), ...rowProblems(this.rows())];
    if (problems.length) {
      this.errors.set(
        problems.map((p) => this.i18n.translate(p.key).replace('{row}', String(p.row))),
      );
      this.tab.set(1);
      return;
    }
    const payload = this.payloadFrom(value);
    if (!payload) return;
    payload['roles'] = [
      ...tableRoleEntries(this.companies(), this.storedRoles),
      ...flattenRoles(
        this.rows().filter((r) => r.roles.length),
        this.storedRoles,
      ),
    ];
    payload['companies'] = companyAdmissions(this.companies());
    payload['instances'] = instanceAdmissions(
      this.companies(),
      this.storedInstances,
      this.defaultOrganization(),
    );
    payload['privileges'] = personalPayload(this.personal());
    this.saving.set(true);
    try {
      await this.drafts.addChange(
        this.data.type.entityType,
        this.entityCode(value),
        this.data.row ? 'UPDATE' : 'CREATE',
        payload,
        { submit },
      );
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.saving.set(false);
    }
  }
}
