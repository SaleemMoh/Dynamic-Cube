import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { AccountView, AppsControlApi, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { catchError, debounceTime, of } from 'rxjs';
import { PrivilegesService } from '../privileges.service';
import { PortalAccountData, PortalAccountDialog } from './portal-account-dialog';

/**
 * Applicants' portal accounts. The user name each applicant signs in with is the national ID (or iqama number) shown
 * in the first column; staff search by it, the name or the email, create accounts and open one to manage it.
 */
@Component({
  selector: 'dc-portal-accounts-page',
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatTableModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <div>
        <h1>{{ 'portalAccount.list' | dcTranslate }}</h1>
        <p class="dc-muted">{{ 'portalAccount.listHint' | dcTranslate }}</p>
      </div>
      @if (canCreate()) {
        <div class="dc-actions">
          <button matButton="filled" type="button" (click)="open({})" data-testid="portal-account-new">
            <mat-icon fontSet="material-symbols-outlined">person_add</mat-icon>{{ 'portalAccount.new' | dcTranslate }}
          </button>
        </div>
      }
    </header>
    <div class="dc-toolbar">
      <mat-form-field class="dc-toolbar__search" subscriptSizing="dynamic">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input
          matInput
          [placeholder]="'portalAccount.searchHint' | dcTranslate"
          [ngModel]="search()"
          (ngModelChange)="search.set($event); skip.set(0)"
          data-testid="portal-account-search"
        />
      </mat-form-field>
    </div>
    @if (page.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @if (error(); as e) {
      <ul class="dc-errors" role="alert">
        @for (m of e; track $index) {
          <li dir="auto">{{ m }}</li>
        }
      </ul>
    }
    <table mat-table [dataSource]="page.value()?.items ?? []" class="dc-table">
      <ng-container matColumnDef="userName">
        <th mat-header-cell *matHeaderCellDef>{{ 'portalAccount.userName' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let a">
          <button
            type="button"
            class="dc-link dc-pacc-list__user mono"
            dir="ltr"
            (click)="open({ nationalId: a.userName })"
            [attr.data-testid]="'portal-account-' + a.userName"
          >
            {{ a.userName }}
          </button>
        </td>
      </ng-container>
      <ng-container matColumnDef="name">
        <th mat-header-cell *matHeaderCellDef>{{ 'config.name' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let a">{{ name(a) }}</td>
      </ng-container>
      <ng-container matColumnDef="email">
        <th mat-header-cell *matHeaderCellDef>{{ 'portalAccount.email' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let a" dir="ltr">{{ a.email ?? '—' }}</td>
      </ng-container>
      <ng-container matColumnDef="created">
        <th mat-header-cell *matHeaderCellDef>{{ 'account.created' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let a" dir="ltr">{{ a.createdAt ? (a.createdAt | date: 'yyyy-MM-dd') : '—' }}</td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef>{{ 'portalAccount.status' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let a">
          <span class="dc-badge" [class.dc-badge--success]="a.enabled" [class.dc-badge--danger]="!a.enabled">{{
            (a.enabled ? 'account.active' : 'account.inactive') | dcTranslate
          }}</span>
          @if (a.requiredActions.includes('UPDATE_PASSWORD')) {
            <span class="dc-badge dc-badge--warning">{{ 'portalAccount.firstSignIn' | dcTranslate }}</span>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
    @if ((page.value()?.items ?? []).length === 0 && !page.isLoading()) {
      <p class="dc-empty">{{ 'portalAccount.empty' | dcTranslate }}</p>
    }
    <mat-paginator [length]="page.value()?.total ?? 0" [pageSize]="take()" [pageSizeOptions]="[20, 50, 100]" (page)="paginate($event)" />
  `,
  styles: `
    .dc-pacc-list__user {
      padding: 0;
      border: 0;
      background: none;
      font: inherit;
      font-weight: 700;
      cursor: pointer;
    }
    .dc-badge + .dc-badge {
      margin-inline-start: 4px;
    }
  `,
})
export class PortalAccountsPage {
  private readonly api = inject(AppsControlApi);
  private readonly dialog = inject(MatDialog);
  private readonly privileges = inject(PrivilegesService);
  protected readonly columns = ['userName', 'name', 'email', 'created', 'status'];
  protected readonly search = signal('');
  private readonly debounced = toSignal(toObservable(this.search).pipe(debounceTime(300)), { initialValue: '' });
  protected readonly skip = signal(0);
  protected readonly take = signal(20);
  protected readonly error = signal<string[] | null>(null);
  protected readonly canCreate = computed(() => this.privileges.can('apps-control.portal-accounts', 'CREATE'));

  protected readonly page = rxResource({
    params: () => ({ search: this.debounced().trim(), skip: this.skip(), take: this.take() }),
    stream: ({ params }) => {
      this.error.set(null);
      return this.api.portalAccounts(params.search, params.skip, params.take).pipe(
        catchError((e: unknown) => {
          this.error.set(problemMessages(e));
          return of({ items: [], total: 0 });
        }),
      );
    },
  });

  protected name(a: AccountView): string {
    return [a.firstName, a.lastName].filter(Boolean).join(' ') || '—';
  }

  protected paginate(event: PageEvent): void {
    this.take.set(event.pageSize);
    this.skip.set(event.pageIndex * event.pageSize);
  }

  protected open(data: PortalAccountData): void {
    this.dialog
      .open(PortalAccountDialog, { data, width: '640px', maxWidth: '95vw', autoFocus: false })
      .afterClosed()
      .subscribe((changed) => {
        if (changed) this.page.reload();
      });
  }
}
