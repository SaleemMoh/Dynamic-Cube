import { ChangeDetectionStrategy, Component, computed, inject, input, model } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  AppsControlApi,
  ConfigRow,
  LocalizePipe,
  LocalizedText,
  SearchableSelectModule,
  TranslatePipe,
} from '@dynamic-cube/shared';
import { catchError, forkJoin, of } from 'rxjs';
import { CompanyRow, organizationOf, rolesForCompany, setDefaultCompany } from './company-access';

/**
 * The person's companies and their roles in each, like the legacy Apps Control (user ↔ company, user ↔ roles): one line
 * per company, the star marks the default company of its organization. Roles are held in all the company's branches.
 */
@Component({
  selector: 'dc-user-companies',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTooltipModule,
    SearchableSelectModule,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!lists.hasValue()) {
      <mat-progress-bar mode="indeterminate" />
    } @else {
      <h3>{{ 'access.companies' | dcTranslate }}</h3>
      <p class="dc-muted">{{ 'access.companiesHint' | dcTranslate }}</p>
      <div class="dc-companies" data-testid="company-rows">
        @if (rows().length) {
          <div class="dc-companies__head">
            <span></span>
            <span>{{ 'access.company' | dcTranslate }}</span>
            <span>{{ 'access.roleList' | dcTranslate }}</span>
            <span></span>
          </div>
        }
        @for (row of rows(); track $index; let i = $index) {
          <div class="dc-companies__row" [attr.data-testid]="'company-row-' + i">
            <button
              matIconButton
              type="button"
              (click)="makeDefault(i)"
              [disabled]="readonly() || !row.company"
              [matTooltip]="'access.defaultCompany' | dcTranslate"
              [attr.aria-pressed]="row.isDefault"
              [attr.aria-label]="'access.defaultCompany' | dcTranslate"
              data-testid="company-default"
            >
              <mat-icon fontSet="material-symbols-outlined" [class.dc-star]="row.isDefault">{{
                row.isDefault ? 'star' : 'star_outline'
              }}</mat-icon>
            </button>
            <mat-form-field subscriptSizing="dynamic">
              <mat-select
                [value]="row.company"
                (selectionChange)="setCompany(i, $event.value)"
                [disabled]="readonly()"
                [placeholder]="'access.chooseCompany' | dcTranslate"
                data-testid="company-select"
              >
                @for (c of companies(); track c.code) {
                  <mat-option [value]="c.code">
                    {{ name(c) | dcLocalize }}
                    <span class="mono dc-muted" dir="ltr">{{ c.code }}</span>
                  </mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-select
                multiple
                [value]="row.roles"
                (selectionChange)="patch(i, { roles: $event.value })"
                [disabled]="readonly() || !row.company"
                [placeholder]="'access.chooseRoles' | dcTranslate"
                data-testid="company-roles"
              >
                @for (r of rolesOf(row.company); track r.code) {
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
          </div>
        }
      </div>
      @if (!readonly()) {
        <button matButton type="button" (click)="addRow()" data-testid="company-add">
          <mat-icon fontSet="material-symbols-outlined">add</mat-icon
          >{{ 'access.addCompany' | dcTranslate }}
        </button>
      }
      @if (organizations().length > 1) {
        <mat-form-field class="dc-companies__org" subscriptSizing="dynamic">
          <mat-label>{{ 'access.defaultOrganization' | dcTranslate }}</mat-label>
          <mat-select
            [value]="defaultOrganization()"
            (selectionChange)="defaultOrganization.set($event.value)"
            [disabled]="readonly()"
            data-testid="default-organization"
          >
            @for (code of organizations(); track code) {
              <mat-option [value]="code">{{ organizationName(code) | dcLocalize }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      }
    }
  `,
  styles: `
    h3 {
      margin: 4px 0 2px;
    }
    .dc-companies {
      display: grid;
      gap: 6px;
      margin: 8px 0;
    }
    .dc-companies__head,
    .dc-companies__row {
      display: grid;
      grid-template-columns: 40px minmax(0, 1fr) minmax(0, 1.6fr) 40px;
      gap: 8px;
      align-items: center;
    }
    .dc-companies__head {
      color: var(--dga-gray-600);
      font-size: 13px;
      font-weight: 600;
    }
    .dc-star {
      color: var(--dga-warning-500, #dc6803);
    }
    .dc-companies__org {
      display: block;
      max-width: 360px;
      margin-top: 12px;
    }
    @media (max-width: 600px) {
      .dc-companies__head {
        display: none;
      }
      .dc-companies__row {
        grid-template-columns: 40px minmax(0, 1fr) 40px;
      }
      .dc-companies__row > mat-form-field:nth-of-type(2) {
        grid-column: 1 / -1;
      }
    }
  `,
})
export class UserCompanies {
  readonly readonly = input(false);
  readonly rows = model.required<CompanyRow[]>();
  /** The organization the person starts in, when they may enter several. */
  readonly defaultOrganization = model<string | null>(null);

  private readonly api = inject(AppsControlApi);

  protected readonly lists = rxResource({
    stream: () => {
      const list = (type: string) =>
        this.api.list(type).pipe(catchError(() => of([] as ConfigRow[])));
      return forkJoin({
        organizations: list('apps.entity'),
        companies: list('apps.company'),
        roles: list('apps.role'),
      });
    },
  });
  protected readonly companies = computed(() => this.lists.value()?.companies ?? []);
  /** The organizations of the chosen companies. */
  protected readonly organizations = computed(() => [
    ...new Set(
      this.rows()
        .filter((r) => r.company)
        .map((r) => organizationOf(r.company!)),
    ),
  ]);

  protected name(row: ConfigRow): LocalizedText {
    return { ar: row.nameAr, en: row.nameEn };
  }

  protected organizationName(code: string): LocalizedText {
    const row = (this.lists.value()?.organizations ?? []).find((o) => o.code === code);
    return row ? this.name(row) : { ar: code, en: code };
  }

  protected rolesOf(company: string | null): ConfigRow[] {
    return rolesForCompany(company, this.lists.value()?.roles ?? []);
  }

  protected setCompany(index: number, company: string): void {
    // Roles belong to one company: another company starts without roles.
    this.patch(index, { company, roles: [], isDefault: false });
    if (
      !this.rows().some(
        (r, i) => i !== index && r.company && organizationOf(r.company) === organizationOf(company),
      )
    ) {
      this.makeDefault(index);
    }
  }

  protected makeDefault(index: number): void {
    this.rows.update((rows) => setDefaultCompany(rows, index));
  }

  protected patch(index: number, change: Partial<CompanyRow>): void {
    this.rows.update((rows) => rows.map((r, i) => (i === index ? { ...r, ...change } : r)));
  }

  protected addRow(): void {
    this.rows.update((rows) => [
      ...rows,
      { company: null, isDefault: false, roles: [], defaultCurrency: null },
    ]);
  }

  protected removeRow(index: number): void {
    this.rows.update((rows) => rows.filter((_, i) => i !== index));
  }
}
