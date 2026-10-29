import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppsControlApi, ConfigRow, LocalizePipe, LocalizedText, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { payloadOf } from '../config/form-values';
import { DraftsService } from '../drafts.service';
import { descendantsOf } from './org-tree';

/** A user's placement in a unit, as the user row stores it. */
interface Placement {
  orgUnitCode: string;
  positionCode?: string | null;
  isPrimary?: boolean;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
}

export interface EmployeeRow {
  userName: string;
  name: LocalizedText;
  unitCode: string;
  positionCode: string | null;
  isPrimary: boolean;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  active: boolean;
}

/** Employees placed in the given units (a user placed twice shows twice), by unit order then name. */
export function employeesOf(users: ConfigRow[], units: string[]): EmployeeRow[] {
  const order = new Map(units.map((u, i) => [u, i]));
  return users
    .flatMap((u) =>
      ((u['assignments'] as Placement[] | undefined) ?? [])
        .filter((a) => order.has(a.orgUnitCode))
        .map((a) => ({
          userName: u.code,
          name: { ar: u.nameAr || u.code, en: u.nameEn || u.code },
          unitCode: a.orgUnitCode,
          positionCode: a.positionCode ?? null,
          isPrimary: !!a.isPrimary,
          effectiveFrom: a.effectiveFrom ?? null,
          effectiveTo: a.effectiveTo ?? null,
          active: u['status'] !== 'DISABLED' && u.isActive !== false,
        })),
    )
    .sort((a, b) => order.get(a.unitCode)! - order.get(b.unitCode)! || a.name.en.localeCompare(b.name.en));
}

/**
 * The employees of an org unit: who is placed in it with which position, optionally with its sub-units; employees are
 * placed in (or removed from) the unit here, which changes their user row through a change set.
 */
@Component({
  selector: 'dc-org-employees',
  imports: [
    FormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTooltipModule,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-emp__header">
      <h3>
        {{ 'org.employees' | dcTranslate }} <span class="dc-count">{{ rows().length }}</span>
      </h3>
      <mat-slide-toggle [ngModel]="withSubUnits()" (ngModelChange)="withSubUnits.set($event)" data-testid="org-emp-sub">
        {{ 'org.employeesSubUnits' | dcTranslate }}
      </mat-slide-toggle>
    </div>
    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (e of errors(); track $index) {
          <li>{{ e }}</li>
        }
      </ul>
    }
    <div class="dc-table-wrap">
      <table class="dc-emp__table" data-testid="org-employees">
        <thead>
          <tr>
            <th>{{ 'org.employee' | dcTranslate }}</th>
            <th>{{ 'org.position' | dcTranslate }}</th>
            @if (withSubUnits()) {
              <th>{{ 'org.unit' | dcTranslate }}</th>
            }
            <th>{{ 'org.primary' | dcTranslate }}</th>
            <th>{{ 'org.effective' | dcTranslate }}</th>
            @if (canEdit()) {
              <th></th>
            }
          </tr>
        </thead>
        <tbody>
          @for (e of rows(); track e.userName + e.unitCode) {
            <tr [class.dc-emp__off]="!e.active" [attr.data-employee]="e.userName">
              <td>
                <strong>{{ e.name | dcLocalize }}</strong>
                <span class="mono dc-muted dc-emp__user">{{ e.userName }}</span>
              </td>
              <td>{{ e.positionCode ? (positionName(e.positionCode) | dcLocalize) : '—' }}</td>
              @if (withSubUnits()) {
                <td>{{ unitName(e.unitCode) | dcLocalize }}</td>
              }
              <td>
                @if (e.isPrimary) {
                  <mat-icon fontSet="material-symbols-outlined" class="dc-emp__yes">check_circle</mat-icon>
                }
              </td>
              <td dir="ltr" class="dc-muted">{{ e.effectiveFrom || e.effectiveTo ? (e.effectiveFrom ?? '…') + ' → ' + (e.effectiveTo ?? '…') : '' }}</td>
              @if (canEdit()) {
                <td>
                  <button
                    matIconButton
                    type="button"
                    (click)="remove(e)"
                    [matTooltip]="'org.removeEmployee' | dcTranslate"
                    [attr.data-testid]="'org-emp-remove-' + e.userName"
                  >
                    <mat-icon fontSet="material-symbols-outlined">person_remove</mat-icon>
                  </button>
                </td>
              }
            </tr>
          } @empty {
            <tr>
              <td colspan="6" class="dc-muted">{{ (users.isLoading() ? 'org.loading' : 'org.noEmployees') | dcTranslate }}</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
    @if (canEdit()) {
      <form class="dc-emp__add" (ngSubmit)="add()" data-testid="org-emp-add">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'org.employee' | dcTranslate }}</mat-label>
          <input matInput name="user" [ngModel]="userText()" (ngModelChange)="userText.set($event)" [matAutocomplete]="auto" data-testid="org-emp-user" />
          <mat-autocomplete #auto="matAutocomplete" (optionSelected)="userText.set($event.option.value)">
            @for (u of matches(); track u.code) {
              <mat-option [value]="u.code">{{ { ar: u.nameAr || u.code, en: u.nameEn || u.code } | dcLocalize }} ({{ u.code }})</mat-option>
            }
          </mat-autocomplete>
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'org.position' | dcTranslate }}</mat-label>
          <mat-select name="position" [ngModel]="position()" (ngModelChange)="position.set($event)" data-testid="org-emp-position">
            <mat-option value="">—</mat-option>
            @for (p of activePositions(); track p.code) {
              <mat-option [value]="p.code">{{ { ar: p.nameAr, en: p.nameEn } | dcLocalize }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-checkbox name="primary" [ngModel]="primary()" (ngModelChange)="primary.set($event)">{{ 'org.primary' | dcTranslate }}</mat-checkbox>
        <button matButton="filled" type="submit" [disabled]="!chosenUser() || busy()">
          <mat-icon fontSet="material-symbols-outlined">person_add</mat-icon>
          {{ 'org.addEmployee' | dcTranslate }}
        </button>
      </form>
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: 8px;
      margin-top: 12px;
    }
    .dc-emp__header {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }
    .dc-emp__header h3 {
      margin: 0;
      font-size: 14px;
    }
    .dc-emp__table {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
    }
    .dc-emp__table th {
      padding: 6px 8px;
      border-bottom: 1px solid var(--dga-gray-200);
      color: var(--dga-gray-500);
      font-weight: 600;
      text-align: start;
    }
    .dc-emp__table td {
      padding: 4px 8px;
      border-bottom: 1px solid var(--dga-gray-100);
    }
    .dc-emp__user {
      margin-inline-start: 8px;
      font-size: 12px;
    }
    .dc-emp__off td {
      color: var(--dga-gray-400);
    }
    .dc-emp__yes {
      color: var(--dga-success-600, #079455);
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .dc-emp__add {
      display: grid;
      grid-template-columns: minmax(12rem, 1.4fr) minmax(10rem, 1fr) auto auto;
      align-items: center;
      gap: 8px;
    }
    @media (max-width: 900px) {
      .dc-emp__add {
        grid-template-columns: minmax(0, 1fr);
      }
    }
  `,
})
export class OrgEmployees {
  /** The unit, and every unit of the structure (for sub-units and names). */
  readonly unitCode = input.required<string>();
  readonly units = input.required<ConfigRow[]>();
  readonly canEdit = input(false);

  private readonly api = inject(AppsControlApi);
  private readonly drafts = inject(DraftsService);

  protected readonly users = rxResource({ stream: () => this.api.list('apps.user') });
  private readonly positions = rxResource({ stream: () => this.api.list('apps.position') });
  protected readonly withSubUnits = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly busy = signal(false);
  protected readonly userText = signal('');
  protected readonly position = signal('');
  protected readonly primary = signal(false);

  private readonly userRows = computed(() => (this.users.hasValue() ? this.users.value() : []));
  protected readonly activePositions = computed(() => (this.positions.hasValue() ? this.positions.value() : []).filter((p) => p.isActive !== false));
  protected readonly rows = computed(() =>
    employeesOf(this.userRows(), this.withSubUnits() ? [this.unitCode(), ...descendantsOf(this.units(), this.unitCode())] : [this.unitCode()]),
  );
  protected readonly matches = computed(() => {
    const text = this.userText().trim().toLowerCase();
    return this.userRows()
      .filter((u) => u['status'] !== 'DISABLED')
      .filter(
        (u) =>
          !text ||
          [u.code, u.nameAr, u.nameEn].some((v) =>
            String(v ?? '')
              .toLowerCase()
              .includes(text),
          ),
      )
      .slice(0, 20);
  });
  protected readonly chosenUser = computed(() => this.userRows().find((u) => u.code === this.userText().trim()) ?? null);

  protected positionName(code: string): LocalizedText {
    const p = (this.positions.hasValue() ? this.positions.value() : []).find((r) => r.code === code);
    return { ar: p?.nameAr ?? code, en: p?.nameEn ?? code };
  }

  protected unitName(code: string): LocalizedText {
    const u = this.units().find((r) => r.code === code);
    return { ar: u?.nameAr || code, en: u?.nameEn || code };
  }

  protected async add(): Promise<void> {
    const user = this.chosenUser();
    if (!user) return;
    await this.change(user.code, (placements) => [
      ...placements.map((p) => (this.primary() ? { ...p, isPrimary: false } : p)),
      { orgUnitCode: this.unitCode(), positionCode: this.position() || null, isPrimary: this.primary() || placements.length === 0 },
    ]);
    this.userText.set('');
    this.position.set('');
    this.primary.set(false);
  }

  protected async remove(employee: EmployeeRow): Promise<void> {
    await this.change(employee.userName, (placements) =>
      placements.filter((p) => !(p.orgUnitCode === employee.unitCode && (p.positionCode ?? null) === employee.positionCode)),
    );
  }

  /** Saves the user with changed placements (in the user's draft change set, or at once where no approval is needed). */
  private async change(userName: string, update: (placements: Placement[]) => Placement[]): Promise<void> {
    this.errors.set([]);
    this.busy.set(true);
    try {
      const row = await firstValueFrom(this.api.get('apps.user', userName));
      const payload = payloadOf(row);
      payload['assignments'] = update((row['assignments'] as Placement[] | undefined) ?? []);
      await this.drafts.addChange('apps.user', userName, 'UPDATE', payload);
      this.users.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }
}
