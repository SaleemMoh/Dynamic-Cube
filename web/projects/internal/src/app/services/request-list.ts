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
import { BusinessAppContext, LocalizePipe, ServicesApi, StatusCategory, TranslatePipe } from '@dynamic-cube/shared';
import { debounceTime } from 'rxjs';

/** Submitted requests (processing arrives with the workflow engine and Action Center). */
@Component({
  selector: 'dc-request-list',
  imports: [DatePipe, DecimalPipe, FormsModule, MatFormFieldModule, MatIconModule, MatInputModule, MatPaginatorModule, MatSelectModule, MatTableModule, RouterLink, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'requests.title' | dcTranslate }}</h1>
    </header>
    <div class="dc-toolbar">
      <mat-form-field class="dc-toolbar__search" subscriptSizing="dynamic">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input matInput [placeholder]="'requests.searchHint' | dcTranslate" [ngModel]="search()" (ngModelChange)="search.set($event); skip.set(0)" data-testid="request-search" />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-select [ngModel]="status()" (ngModelChange)="status.set($event); skip.set(0)" [attr.aria-label]="'requests.status' | dcTranslate">
          <mat-option [value]="null">{{ 'requests.allStatuses' | dcTranslate }}</mat-option>
          @for (s of statuses; track s) {
            <mat-option [value]="s">{{ 'status.category.' + s | dcTranslate }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>
    <table mat-table [dataSource]="page.value()?.items ?? []" class="dc-table">
      <ng-container matColumnDef="number">
        <th mat-header-cell *matHeaderCellDef>{{ 'requests.number' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let r"><a class="dc-link" dir="ltr" [routerLink]="[r.id]" [attr.data-testid]="'request-' + r.number">{{ r.number }}</a></td>
      </ng-container>
      <ng-container matColumnDef="service">
        <th mat-header-cell *matHeaderCellDef>{{ 'requests.service' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let r">{{ { ar: r.serviceNameAr, en: r.serviceNameEn } | dcLocalize }}</td>
      </ng-container>
      <ng-container matColumnDef="beneficiary">
        <th mat-header-cell *matHeaderCellDef>{{ 'requests.beneficiary' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let r">{{ { ar: r.beneficiaryNameAr, en: r.beneficiaryNameEn } | dcLocalize }}</td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef>{{ 'requests.status' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let r"><span class="dc-badge dc-badge--info">{{ 'displayStatus.' + r.displayStatus | dcTranslate }}</span></td>
      </ng-container>
      <ng-container matColumnDef="submitted">
        <th mat-header-cell *matHeaderCellDef>{{ 'requests.submittedAt' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let r" dir="ltr">{{ r.submittedAt | date: 'yyyy-MM-dd HH:mm' }}</td>
      </ng-container>
      <ng-container matColumnDef="fees">
        <th mat-header-cell *matHeaderCellDef>{{ 'fees.grandTotal' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let r" dir="ltr">{{ r.feeTotal | number: '1.0-2' }}</td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
    <mat-paginator [length]="page.value()?.total ?? 0" [pageSize]="take()" [pageSizeOptions]="[25, 50, 100]" (page)="paginate($event)" />
  `,
})
export class RequestList {
  private readonly api = inject(ServicesApi);
  private readonly appContext = inject(BusinessAppContext);
  protected readonly columns = ['number', 'service', 'beneficiary', 'status', 'submitted', 'fees'];
  protected readonly statuses: StatusCategory[] = ['SUBMITTED', 'IN_PROGRESS', 'PENDING_CUSTOMER', 'PENDING_EXTERNAL', 'APPROVED', 'REJECTED', 'CANCELLED', 'CLOSED'];
  protected readonly search = signal('');
  private readonly debounced = toSignal(toObservable(this.search).pipe(debounceTime(300)), { initialValue: '' });
  protected readonly status = signal<StatusCategory | null>(null);
  protected readonly skip = signal(0);
  protected readonly take = signal(50);
  protected readonly page = rxResource({
    params: () => ({ search: this.debounced().trim() || null, status: this.status(), app: this.appContext.current(), skip: this.skip(), take: this.take() }),
    stream: ({ params }) => this.api.searchRequests(params),
  });

  protected paginate(event: PageEvent): void {
    this.take.set(event.pageSize);
    this.skip.set(event.pageIndex * event.pageSize);
  }
}
