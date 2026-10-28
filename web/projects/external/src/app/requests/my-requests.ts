import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { ActingPartyService, LocalizePipe, ServicesApi, TranslatePipe } from '@dynamic-cube/shared';

/** Requests and drafts of the acting party. */
@Component({
  selector: 'dc-my-requests',
  imports: [DatePipe, DecimalPipe, MatButtonModule, MatTableModule, RouterLink, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'requests.title' | dcTranslate }}</h1>
      <a matButton="filled" routerLink="/services" data-testid="new-request">{{ 'requests.new' | dcTranslate }}</a>
    </header>
    <table mat-table [dataSource]="list.value() ?? []" class="dc-table">
      <ng-container matColumnDef="number">
        <th mat-header-cell *matHeaderCellDef>{{ 'requests.number' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let r">
          <a class="dc-link" [routerLink]="['/requests', r.id]" [attr.data-testid]="'request-' + (r.number ?? 'draft')" dir="ltr">{{ r.number ?? ('displayStatus.DRAFT' | dcTranslate) }}</a>
        </td>
      </ng-container>
      <ng-container matColumnDef="service">
        <th mat-header-cell *matHeaderCellDef>{{ 'requests.service' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let r">{{ { ar: r.serviceNameAr, en: r.serviceNameEn } | dcLocalize }}</td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef>{{ 'requests.status' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let r"><span
            class="dc-badge"
            [class.dc-badge--success]="r.status === 'APPROVED'"
            [class.dc-badge--danger]="r.status === 'REJECTED'"
            [class.dc-badge--warning]="r.status === 'PENDING_CUSTOMER'"
            [class.dc-badge--info]="r.status === 'SUBMITTED' || r.status === 'IN_PROGRESS'"
            >{{ 'displayStatus.' + r.displayStatus | dcTranslate }}</span></td>
      </ng-container>
      <ng-container matColumnDef="date">
        <th mat-header-cell *matHeaderCellDef>{{ 'requests.date' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let r" dir="ltr">{{ (r.submittedAt ?? r.createdAt) | date: 'yyyy-MM-dd' }}</td>
      </ng-container>
      <ng-container matColumnDef="fees">
        <th mat-header-cell *matHeaderCellDef>{{ 'fees.grandTotal' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let r" dir="ltr">{{ r.status === 'DRAFT' ? '—' : (r.feeTotal | number: '1.0-2') }}</td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
    @if (!list.isLoading() && !(list.value() ?? []).length) {
      <p class="dc-muted">{{ 'requests.empty' | dcTranslate }}</p>
    }
  `,
})
export class MyRequests {
  private readonly api = inject(ServicesApi);
  private readonly acting = inject(ActingPartyService);
  protected readonly columns = ['number', 'service', 'status', 'date', 'fees'];
  protected readonly list = rxResource({ params: () => this.acting.current()?.partyId ?? 'none', stream: () => this.api.myRequests() });
}
