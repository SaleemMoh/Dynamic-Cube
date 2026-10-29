import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { ActingPartyService, I18nService, LocalizePipe, ServicesApi, TranslatePipe } from '@dynamic-cube/shared';
import { FilterOption, ListFilter } from '../portal/list-filter';
import { PageHero } from '../portal/page-hero';

/** Requests and drafts of the acting party, filtered by status or searched, under the title band. */
@Component({
  selector: 'dc-my-requests',
  imports: [DatePipe, DecimalPipe, MatButtonModule, MatIconModule, MatTableModule, RouterLink, LocalizePipe, TranslatePipe, ListFilter, PageHero],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dc-page-hero [title]="'requests.title' | dcTranslate" [text]="'requests.lead' | dcTranslate" />
    <div class="dc-portal-block dc-page-body">
      <div class="dc-section-head">
        <div>
          <h2>{{ 'requests.listTitle' | dcTranslate }}</h2>
          <p>{{ 'requests.listLead' | dcTranslate }}</p>
        </div>
        <a matButton="filled" class="dc-head-button" routerLink="/services" data-testid="new-request">
          <mat-icon fontSet="material-symbols-outlined">add</mat-icon>{{ 'requests.new' | dcTranslate }}
        </a>
      </div>
      <dc-list-filter [statuses]="statuses()" [placeholder]="'requests.search' | dcTranslate" [(status)]="status" [(term)]="term" />
      <table mat-table [dataSource]="rows()" class="dc-table dc-table--portal">
        <ng-container matColumnDef="service">
          <th mat-header-cell *matHeaderCellDef>{{ 'requests.service' | dcTranslate }}</th>
          <td mat-cell *matCellDef="let r">
            <a class="dc-link" [routerLink]="['/requests', r.id]" [attr.data-testid]="'request-' + (r.number ?? 'draft')">{{
              { ar: r.serviceNameAr, en: r.serviceNameEn } | dcLocalize
            }}</a>
          </td>
        </ng-container>
        <ng-container matColumnDef="number">
          <th mat-header-cell *matHeaderCellDef>{{ 'requests.number' | dcTranslate }}</th>
          <td mat-cell *matCellDef="let r">
            <span dir="ltr">{{ r.number ?? '—' }}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="date">
          <th mat-header-cell *matHeaderCellDef>{{ 'requests.date' | dcTranslate }}</th>
          <td mat-cell *matCellDef="let r">
            <span dir="ltr">{{ r.submittedAt ?? r.createdAt | date: 'dd/MM/yyyy' }}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="fees">
          <th mat-header-cell *matHeaderCellDef>{{ 'fees.grandTotal' | dcTranslate }}</th>
          <td mat-cell *matCellDef="let r">
            <span dir="ltr">{{ r.status === 'DRAFT' ? '—' : (r.feeTotal | number: '1.0-2') }}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>{{ 'requests.status' | dcTranslate }}</th>
          <td mat-cell *matCellDef="let r">
            <span
              class="dc-badge"
              [class.dc-badge--success]="r.status === 'APPROVED'"
              [class.dc-badge--danger]="r.status === 'REJECTED'"
              [class.dc-badge--warning]="r.status === 'PENDING_CUSTOMER' || r.status === 'PENDING_PAYMENT'"
              [class.dc-badge--info]="r.status === 'SUBMITTED' || r.status === 'IN_PROGRESS'"
              >{{ 'displayStatus.' + r.displayStatus | dcTranslate }}</span
            >
          </td>
        </ng-container>
        <ng-container matColumnDef="open">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let r" class="dc-table__actions">
            <a matIconButton [routerLink]="['/requests', r.id]" [attr.aria-label]="'requests.open' | dcTranslate">
              <mat-icon fontSet="material-symbols-outlined">{{ r.status === 'DRAFT' || r.status === 'PENDING_CUSTOMER' ? 'edit' : 'visibility' }}</mat-icon>
            </a>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
      </table>
      @if (!list.isLoading() && !rows().length) {
        <p class="dc-empty">{{ ((list.value() ?? []).length ? 'list.noMatch' : 'requests.empty') | dcTranslate }}</p>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }
  `,
})
export class MyRequests {
  private readonly api = inject(ServicesApi);
  private readonly acting = inject(ActingPartyService);
  private readonly i18n = inject(I18nService);
  protected readonly columns = ['service', 'number', 'date', 'fees', 'status', 'open'];
  protected readonly list = rxResource({ params: () => this.acting.current()?.partyId ?? 'none', stream: () => this.api.myRequests() });
  protected readonly status = signal('');
  protected readonly term = signal('');
  protected readonly statuses = computed<FilterOption[]>(() =>
    [...new Set((this.list.value() ?? []).map((r) => r.displayStatus))].map((value) => ({ value, label: this.i18n.translate('displayStatus.' + value) })),
  );
  protected readonly rows = computed(() => {
    const term = this.term().trim().toLowerCase();
    return (this.list.value() ?? []).filter(
      (r) =>
        (!this.status() || r.displayStatus === this.status()) &&
        (!term || `${r.number ?? ''} ${r.serviceNameAr} ${r.serviceNameEn}`.toLowerCase().includes(term)),
    );
  });
}
