import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import {
  BILL_STATUSES,
  BillStatus,
  PaymentsApi,
  TranslatePipe,
  billStatusBadge,
} from '@dynamic-cube/shared';
import { debounceTime } from 'rxjs';

/** Bills: search by number, SADAD number, request or payer; filter by status. */
@Component({
  selector: 'dc-bill-list',
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
    RouterLink,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'bills.title' | dcTranslate }}</h1>
    </header>
    <div class="dc-toolbar">
      <mat-form-field class="dc-toolbar__search" subscriptSizing="dynamic">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input
          matInput
          [placeholder]="'bills.searchHint' | dcTranslate"
          [ngModel]="search()"
          (ngModelChange)="search.set($event); skip.set(0)"
          data-testid="bill-search"
        />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'bills.status' | dcTranslate }}</mat-label>
        <mat-select [ngModel]="status()" (ngModelChange)="status.set($event); skip.set(0)">
          <mat-option [value]="null">{{ 'requests.allStatuses' | dcTranslate }}</mat-option>
          @for (s of statuses; track s) {
            <mat-option [value]="s">{{ 'bill.status.' + s | dcTranslate }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>
    <table mat-table [dataSource]="page.value()?.items ?? []" class="dc-table">
      <ng-container matColumnDef="number">
        <th mat-header-cell *matHeaderCellDef>{{ 'bill.platformNumber' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let b">
          <a
            class="dc-link"
            dir="ltr"
            [routerLink]="[b.id]"
            [attr.data-testid]="'bill-' + b.number"
            >{{ b.number }}</a
          >
        </td>
      </ng-container>
      <ng-container matColumnDef="provider">
        <th mat-header-cell *matHeaderCellDef>{{ 'bill.number' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let b" dir="ltr">
          {{ b.providerCode }} · {{ b.providerBillNumber }}
        </td>
      </ng-container>
      <ng-container matColumnDef="subject">
        <th mat-header-cell *matHeaderCellDef>{{ 'bills.request' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let b" dir="ltr">
          @if (b.subjectType === 'SERVICE_REQUEST') {
            <a class="dc-link" [routerLink]="['/services/requests', b.subjectId]">{{
              b.subjectNumber
            }}</a>
          } @else {
            {{ b.subjectNumber }}
          }
        </td>
      </ng-container>
      <ng-container matColumnDef="payer">
        <th mat-header-cell *matHeaderCellDef>{{ 'bills.payer' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let b">{{ b.payerName }}</td>
      </ng-container>
      <ng-container matColumnDef="total">
        <th mat-header-cell *matHeaderCellDef>{{ 'bill.amount' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let b" dir="ltr">{{ b.total | number: '1.2-2' }}</td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef>{{ 'bills.status' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let b">
          <span [class]="badge(b.status)">{{ 'bill.status.' + b.status | dcTranslate }}</span>
        </td>
      </ng-container>
      <ng-container matColumnDef="due">
        <th mat-header-cell *matHeaderCellDef>{{ 'bill.dueOn' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let b" dir="ltr">{{ b.dueAt | date: 'yyyy-MM-dd' }}</td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
    @if ((page.value()?.items ?? []).length === 0 && !page.isLoading()) {
      <p class="dc-empty">{{ 'bills.none' | dcTranslate }}</p>
    }
    <mat-paginator
      [length]="page.value()?.total ?? 0"
      [pageSize]="take()"
      [pageSizeOptions]="[25, 50, 100]"
      (page)="paginate($event)"
    />
  `,
})
export class BillList {
  private readonly api = inject(PaymentsApi);
  protected readonly columns = ['number', 'provider', 'subject', 'payer', 'total', 'status', 'due'];
  protected readonly statuses = BILL_STATUSES;
  protected readonly search = signal('');
  private readonly debounced = toSignal(toObservable(this.search).pipe(debounceTime(300)), {
    initialValue: '',
  });
  protected readonly status = signal<BillStatus | null>(null);
  protected readonly skip = signal(0);
  protected readonly take = signal(50);
  protected readonly page = rxResource({
    params: () => ({
      search: this.debounced().trim() || null,
      status: this.status(),
      skip: this.skip(),
      take: this.take(),
    }),
    stream: ({ params }) => this.api.search(params),
  });

  protected badge(status: BillStatus): string {
    return billStatusBadge(status);
  }

  protected paginate(event: PageEvent): void {
    this.take.set(event.pageSize);
    this.skip.set(event.pageIndex * event.pageSize);
  }
}
