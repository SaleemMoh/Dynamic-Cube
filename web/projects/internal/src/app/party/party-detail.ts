import { DatePipe, DecimalPipe, JsonPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import { AppsControlApi, FormOption, FormValue, I18nService, LocalizePipe, PartyApi, PartySummary, SanctionInfo, RelationshipInfo, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { catchError, firstValueFrom, of } from 'rxjs';
import { ConfirmData, ConfirmDialog } from '../apps-control/confirm-dialog';
import { PortalAccountData, PortalAccountDialog } from '../apps-control/portal-accounts/portal-account-dialog';
import { PrivilegesService } from '../apps-control/privileges.service';
import { FormDialog, FormDialogData } from '../common/form-dialog';

/** One party: identifiers, profile (refreshable), relationships, credentials, projects and sanctions. */
@Component({
  selector: 'dc-party-detail',
  imports: [
    DatePipe,
    DecimalPipe,
    JsonPipe,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    MatTabsModule,
    RouterLink,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (detail.value(); as d) {
      <header class="dc-page__header">
        <div>
          <a routerLink="/party/parties" class="dc-link">{{ 'party.parties' | dcTranslate }}</a>
          <h1 data-testid="party-name">{{ { ar: d.party.nameAr, en: d.party.nameEn } | dcLocalize }}</h1>
          <p class="dc-muted">
            {{ 'party.kind.' + d.party.kind | dcTranslate }} ·
            @for (i of d.identifiers; track i.type) {
              <span>{{ 'identifier.' + i.type | dcTranslate }} <span dir="ltr">{{ i.value }}</span></span>
              @if (!$last) { · }
            }
          </p>
        </div>
        <div class="dc-actions">
          @if (canEdit()) {
            <button matButton="outlined" (click)="refreshProfile()" [disabled]="busy()" data-testid="refresh-profile">
              <mat-icon fontSet="material-symbols-outlined">sync</mat-icon>
              {{ 'party.refreshProfile' | dcTranslate }}
            </button>
            @if (d.party.kind === 'INDIVIDUAL') {
              <button matButton="outlined" (click)="refreshContexts()" [disabled]="busy()" data-testid="refresh-contexts">
                <mat-icon fontSet="material-symbols-outlined">switch_account</mat-icon>
                {{ 'party.refreshContexts' | dcTranslate }}
              </button>
            }
          }
        </div>
      </header>
      @if (busy()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (messages().length) {
        <ul class="dc-errors" role="status">
          @for (m of messages(); track $index) {
            <li>{{ m }}</li>
          }
        </ul>
      }

      @if (portalId(); as nationalId) {
        <section class="dc-party__portal" data-testid="party-portal-account">
          <span class="dc-party__portal-icon" aria-hidden="true"><mat-icon fontSet="material-symbols-outlined">badge</mat-icon></span>
          <div class="dc-party__portal-text">
            <strong>{{ 'portalAccount.title' | dcTranslate }}</strong>
            @if (portalAccount.value(); as a) {
              @if (a.exists) {
                <span>
                  {{ 'portalAccount.userName' | dcTranslate }}: <span class="mono" dir="ltr" data-testid="party-portal-user">{{ a.userName }}</span>
                </span>
              } @else {
                <span class="dc-muted">{{ 'portalAccount.none' | dcTranslate }}</span>
              }
            }
          </div>
          @if (portalAccount.value()?.exists) {
            <span class="dc-pill" [class.dc-pill--green]="portalAccount.value()?.enabled">{{
              (portalAccount.value()?.enabled ? 'account.active' : 'account.inactive') | dcTranslate
            }}</span>
          }
          <button matButton="outlined" type="button" (click)="openPortalAccount(nationalId, d.party)" data-testid="party-portal-open">
            {{ (portalAccount.value()?.exists ? 'portalAccount.manage' : 'portalAccount.create') | dcTranslate }}
          </button>
        </section>
      }

      <mat-tab-group mat-stretch-tabs="false" animationDuration="0ms">
        <mat-tab [label]="'party.profile' | dcTranslate">
          <table mat-table [dataSource]="d.profile" class="dc-table">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>{{ 'config.name' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row">{{ { ar: row.nameAr, en: row.nameEn } | dcLocalize }}</td>
            </ng-container>
            <ng-container matColumnDef="value">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.value' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row" [attr.data-testid]="'profile-' + row.code">{{ display(row.value) }}</td>
            </ng-container>
            <ng-container matColumnDef="refreshed">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.refreshedAt' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row">
                <span dir="ltr">{{ row.refreshedAt | date: 'yyyy-MM-dd HH:mm' }}</span>
                @if (row.isStale) {
                  <span class="dc-badge dc-badge--warning">{{ 'party.stale' | dcTranslate }}</span>
                }
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="['name', 'value', 'refreshed']"></tr>
            <tr mat-row *matRowDef="let row; columns: ['name', 'value', 'refreshed']"></tr>
          </table>
          @if (!d.profile.length) {
            <p class="dc-muted">{{ 'config.empty' | dcTranslate }}</p>
          }
        </mat-tab>

        <mat-tab [label]="'party.relationships' | dcTranslate">
          @if (canEdit() && d.party.kind === 'ESTABLISHMENT') {
            <div class="dc-toolbar">
              <button matButton="filled" (click)="addDelegate()" data-testid="add-relationship">
                <mat-icon fontSet="material-symbols-outlined">person_add</mat-icon>
                {{ 'party.addRelationship' | dcTranslate }}
              </button>
            </div>
          }
          <table mat-table [dataSource]="d.relationships" class="dc-table">
            <ng-container matColumnDef="other">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.party' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row">
                <a class="dc-link" [routerLink]="['/party/parties', other(row).id]">{{ other(row).name }}</a>
                <span class="dc-muted" dir="ltr"> {{ other(row).identifier }}</span>
              </td>
            </ng-container>
            <ng-container matColumnDef="type">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.relationship' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row">
                {{ { ar: row.relationshipNameAr, en: row.relationshipNameEn } | dcLocalize }}
                @if (row.canAct) {
                  <span class="dc-badge dc-badge--info">{{ 'party.canAct' | dcTranslate }}</span>
                }
              </td>
            </ng-container>
            <ng-container matColumnDef="validity">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.validity' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row" dir="ltr">{{ row.validFrom ?? '…' }} → {{ row.validTo ?? '…' }}</td>
            </ng-container>
            <ng-container matColumnDef="services">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.services' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row" dir="ltr">{{ row.services.length ? row.services.join(', ') : ('party.allServices' | dcTranslate) }}</td>
            </ng-container>
            <ng-container matColumnDef="source">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.source' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row">
                {{ 'source.' + row.source | dcTranslate }}
                @if (row.endedAt) {
                  <span class="dc-badge">{{ 'party.ended' | dcTranslate }}</span>
                }
              </td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let row" class="dc-table__actions">
                @if (canEdit() && row.source === 'MANUAL' && !row.endedAt) {
                  <button matIconButton (click)="endRelationship(row)" [attr.aria-label]="'party.end' | dcTranslate">
                    <mat-icon fontSet="material-symbols-outlined">link_off</mat-icon>
                  </button>
                }
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="relationshipColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: relationshipColumns" [class.dc-row--ended]="row.endedAt"></tr>
          </table>
        </mat-tab>

        <mat-tab [label]="'party.credentials' | dcTranslate">
          <table mat-table [dataSource]="d.credentials" class="dc-table">
            <ng-container matColumnDef="type">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.credentialType' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row">{{ { ar: row.typeNameAr, en: row.typeNameEn } | dcLocalize }}</td>
            </ng-container>
            <ng-container matColumnDef="number">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.number' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row" dir="ltr">{{ row.number }}</td>
            </ng-container>
            <ng-container matColumnDef="dates">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.validity' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row" dir="ltr">{{ row.issuedOn ?? '…' }} → {{ row.expiresOn ?? '…' }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.status' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row">{{ 'credential.' + row.status | dcTranslate }}</td>
            </ng-container>
            <ng-container matColumnDef="data">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.data' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row" dir="ltr" class="dc-code dc-data">{{ row.data ? (row.data | json) : '' }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="['type', 'number', 'dates', 'status', 'data']"></tr>
            <tr mat-row *matRowDef="let row; columns: ['type', 'number', 'dates', 'status', 'data']"></tr>
          </table>
        </mat-tab>

        <mat-tab [label]="'party.projects' | dcTranslate">
          <table mat-table [dataSource]="d.projects" class="dc-table">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>{{ 'config.name' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row">{{ row.nameAr }} <span class="dc-muted" dir="ltr">{{ row.number }}</span></td>
            </ng-container>
            <ng-container matColumnDef="client">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.client' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row">{{ row.clientName }}</td>
            </ng-container>
            <ng-container matColumnDef="value">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.contractValue' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row" dir="ltr">{{ row.contractValue | number: '1.0-2' }}</td>
            </ng-container>
            <ng-container matColumnDef="dates">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.validity' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row" dir="ltr">{{ row.startOn ?? '…' }} → {{ row.endOn ?? '…' }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.status' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row">{{ 'project.' + row.status | dcTranslate }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="['name', 'client', 'value', 'dates', 'status']"></tr>
            <tr mat-row *matRowDef="let row; columns: ['name', 'client', 'value', 'dates', 'status']"></tr>
          </table>
        </mat-tab>

        <mat-tab [label]="'party.sanctions' | dcTranslate">
          @if (privileges.can('party.sanctions', 'CREATE')) {
            <div class="dc-toolbar">
              <button matButton="filled" (click)="addSanction()" data-testid="add-sanction">
                <mat-icon fontSet="material-symbols-outlined">gavel</mat-icon>
                {{ 'party.addSanction' | dcTranslate }}
              </button>
            </div>
          }
          <table mat-table [dataSource]="d.sanctions" class="dc-table">
            <ng-container matColumnDef="type">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.sanctionType' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row">{{ { ar: row.typeNameAr, en: row.typeNameEn } | dcLocalize }}</td>
            </ng-container>
            <ng-container matColumnDef="reason">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.reason' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row">{{ row.reason }}</td>
            </ng-container>
            <ng-container matColumnDef="period">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.validity' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row" dir="ltr">{{ row.startsOn }} → {{ row.endsOn ?? '…' }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>{{ 'party.status' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row">
                @if (row.isActive) {
                  <span class="dc-badge dc-badge--danger" data-testid="sanction-active">{{ 'party.active' | dcTranslate }}</span>
                } @else if (row.liftedAt) {
                  <span class="dc-badge">{{ 'party.lifted' | dcTranslate }}</span> <span class="dc-muted">{{ row.liftReason }}</span>
                } @else {
                  <span class="dc-badge">{{ 'party.inactive' | dcTranslate }}</span>
                }
              </td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let row" class="dc-table__actions">
                @if (row.isActive && privileges.can('party.sanctions', 'EDIT')) {
                  <button matButton (click)="liftSanction(row)" data-testid="lift-sanction">{{ 'party.lift' | dcTranslate }}</button>
                }
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="sanctionColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: sanctionColumns"></tr>
          </table>
        </mat-tab>
      </mat-tab-group>
    } @else if (detail.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (detail.error()) {
      <p class="dc-errors" role="alert">{{ 'party.notFound' | dcTranslate }}</p>
    }
  `,
  styles: `
    .dc-party__portal {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 10px 14px;
      margin-block-end: 12px;
      padding: 12px 14px;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius);
      background: var(--dga-white, #fff);
    }
    .dc-party__portal-icon {
      display: grid;
      place-items: center;
      width: 36px;
      height: 36px;
      border-radius: 8px;
      background: var(--dynamic-cube-gold-50);
      color: var(--dynamic-cube-gold-700);
    }
    .dc-party__portal-text {
      display: grid;
      flex: 1;
      min-width: 12rem;
      gap: 2px;
    }
    .dc-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .dc-row--ended {
      opacity: 0.6;
    }
    .dc-data {
      max-width: 24rem;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  `,
})
export class PartyDetail {
  readonly id = input.required<string>();
  protected readonly privileges = inject(PrivilegesService);
  private readonly api = inject(PartyApi);
  private readonly appsControl = inject(AppsControlApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(I18nService);
  protected readonly relationshipColumns = ['other', 'type', 'validity', 'services', 'source', 'actions'];
  protected readonly sanctionColumns = ['type', 'reason', 'period', 'status', 'actions'];
  protected readonly busy = signal(false);
  protected readonly messages = signal<string[]>([]);
  protected readonly canEdit = computed(() => this.privileges.can('party.parties', 'EDIT'));

  protected readonly detail = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.get(params) });

  /** An individual's portal account: its user name is the national ID or iqama number. */
  protected readonly portalId = computed(() => {
    const d = this.detail.hasValue() ? this.detail.value() : null;
    if (d?.party.kind !== 'INDIVIDUAL' || !this.privileges.can('apps-control.portal-accounts', 'VIEW')) return null;
    return d.identifiers.find((i) => (i.type === 'NATIONAL_ID' || i.type === 'IQAMA') && /^[12][0-9]{9}$/.test(i.value))?.value ?? null;
  });
  protected readonly portalAccount = rxResource({
    params: () => this.portalId() ?? undefined,
    stream: ({ params }) => this.appsControl.portalAccount(params).pipe(catchError(() => of(null))),
  });

  protected openPortalAccount(nationalId: string, party: PartySummary): void {
    const [firstName, ...rest] = (party.nameAr || party.nameEn || '').split(' ');
    const data: PortalAccountData = { nationalId, firstName, lastName: rest.join(' ') || null };
    this.dialog
      .open(PortalAccountDialog, { data, width: '640px', maxWidth: '95vw', autoFocus: false })
      .afterClosed()
      .subscribe((changed) => {
        if (changed) this.portalAccount.reload();
      });
  }

  protected other(row: RelationshipInfo): { id: string; name: string; identifier: string } {
    return row.principalId === this.id()
      ? { id: row.individualId, name: row.individualName, identifier: row.individualIdentifier }
      : { id: row.principalId, name: row.principalName, identifier: row.principalIdentifier };
  }

  protected display(value: unknown): string {
    if (value === null || value === undefined) return '—';
    if (Array.isArray(value)) return value.join(', ');
    return typeof value === 'object' ? JSON.stringify(value) : String(value);
  }

  protected async refreshProfile(): Promise<void> {
    await this.run(async () => {
      const results = await firstValueFrom(this.api.refreshProfile(this.id()));
      this.messages.set(results.filter((r) => r.outcome !== 'SUCCESS' && r.outcome !== 'NOT_FOUND').map((r) => `${r.operationCode}: ${this.i18n.localize({ ar: r.messageAr ?? '', en: r.messageEn ?? '' })}`));
    });
  }

  protected async refreshContexts(): Promise<void> {
    await this.run(async () => {
      const result = await firstValueFrom(this.api.refreshContexts(this.id()));
      this.messages.set(result.warnings.map((w) => `${w.source}: ${this.i18n.localize({ ar: w.messageAr ?? '', en: w.messageEn ?? '' })}`));
    });
  }

  protected async addDelegate(): Promise<void> {
    const types = await firstValueFrom(this.appsControl.list('party.relationship-type'));
    const options: FormOption[] = types.filter((t) => t.isActive).map((t) => ({ value: t.code, label: { ar: t.nameAr, en: t.nameEn } }));
    this.openForm({
      title: this.i18n.translate('party.addRelationship'),
      submitLabel: this.i18n.translate('config.save'),
      initialValue: { relationshipCode: 'DELEGATE' },
      schema: {
        code: 'relationship',
        sections: [
          {
            code: 'relationship',
            title: this.text('party.relationship'),
            fields: [
              { code: 'individualIdentifier', type: 'text', label: this.text('party.individualId'), required: true, hint: this.text('party.individualIdHint') },
              { code: 'relationshipCode', type: 'select', label: this.text('party.relationship'), required: true, options },
              { code: 'services', type: 'textarea', label: this.text('party.services'), hint: this.text('party.servicesHint') },
              { code: 'validFrom', type: 'date', label: this.text('party.validFrom') },
              { code: 'validTo', type: 'date', label: this.text('party.validTo') },
            ],
          },
        ],
      },
      submit: (v: FormValue) =>
        firstValueFrom(
          this.api.addRelationship(this.id(), {
            individualIdentifier: String(v['individualIdentifier'] ?? '').trim(),
            relationshipCode: String(v['relationshipCode']),
            services: String(v['services'] ?? '').split(/[\s,]+/).filter(Boolean),
            validFrom: dateOnly(v['validFrom']),
            validTo: dateOnly(v['validTo']),
          }),
        ),
    });
  }

  protected endRelationship(row: RelationshipInfo): void {
    this.confirm({ title: this.i18n.translate('party.end'), message: `${this.other(row).name} — ${this.i18n.localize({ ar: row.relationshipNameAr, en: row.relationshipNameEn })}`, confirmLabel: this.i18n.translate('party.end'), danger: true }, () =>
      firstValueFrom(this.api.endRelationship(this.id(), row.id)),
    );
  }

  protected async addSanction(): Promise<void> {
    const types = await firstValueFrom(this.appsControl.list('party.sanction-type'));
    const options: FormOption[] = types.filter((t) => t.isActive).map((t) => ({ value: t.code, label: { ar: t.nameAr, en: t.nameEn } }));
    this.openForm({
      title: this.i18n.translate('party.addSanction'),
      submitLabel: this.i18n.translate('config.save'),
      initialValue: { typeCode: options[0]?.value ?? null },
      schema: {
        code: 'sanction',
        sections: [
          {
            code: 'sanction',
            title: this.text('party.sanction'),
            fields: [
              { code: 'typeCode', type: 'select', label: this.text('party.sanctionType'), required: true, options },
              { code: 'reason', type: 'textarea', label: this.text('party.reason'), required: true },
              { code: 'startsOn', type: 'date', label: this.text('party.validFrom') },
              { code: 'endsOn', type: 'date', label: this.text('party.validTo'), hint: this.text('party.endsOnHint') },
            ],
          },
        ],
      },
      submit: (v: FormValue) =>
        firstValueFrom(this.api.addSanction(this.id(), { typeCode: String(v['typeCode']), reason: String(v['reason'] ?? ''), startsOn: dateOnly(v['startsOn']), endsOn: dateOnly(v['endsOn']) })),
    });
  }

  protected liftSanction(row: SanctionInfo): void {
    this.confirm({ title: this.i18n.translate('party.lift'), message: row.reason, confirmLabel: this.i18n.translate('party.lift'), comment: true, commentRequired: true }, (reason) =>
      firstValueFrom(this.api.liftSanction(this.id(), row.id, reason)),
    );
  }

  private text(key: string) {
    return { ar: this.i18n.translate(key, 'ar'), en: this.i18n.translate(key, 'en') };
  }

  private openForm(data: FormDialogData): void {
    this.dialog
      .open(FormDialog, { data, width: '36rem', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((ok) => ok && this.detail.reload());
  }

  private confirm(data: ConfirmData, action: (comment: string) => Promise<unknown>): void {
    this.dialog
      .open(ConfirmDialog, { data, width: '32rem', maxWidth: '95vw' })
      .afterClosed()
      .subscribe(async (comment?: string) => {
        if (comment === undefined) return;
        await this.run(() => action(comment));
      });
  }

  private async run(action: () => Promise<unknown>): Promise<void> {
    this.busy.set(true);
    this.messages.set([]);
    try {
      await action();
      this.detail.reload();
    } catch (error) {
      for (const message of problemMessages(error)) {
        this.snackBar.open(message, undefined, { duration: 6000 });
      }
    } finally {
      this.busy.set(false);
    }
  }
}

function dateOnly(value: unknown): string | null {
  if (!value) return null;
  const date = value instanceof Date ? value : new Date(String(value));
  return Number.isNaN(date.getTime()) ? null : `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}
