import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { AppsControlApi, ConfigRow, LocalizePipe, TranslatePipe, VISIT_STATUSES, VisitStatus, VisitsApi, visitStatusBadge } from '@dynamic-cube/shared';
import { catchError, debounceTime, of } from 'rxjs';

/** Field visits: search by number, request, establishment or address; filter by status, type and overdue. */
@Component({
  selector: 'dc-visit-list',
  imports: [
    DatePipe,
    FormsModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
    RouterLink,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'visits.title' | dcTranslate }}</h1>
    </header>
    <div class="dc-toolbar">
      <mat-form-field class="dc-toolbar__search" subscriptSizing="dynamic">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input
          matInput
          [placeholder]="'visits.searchHint' | dcTranslate"
          [ngModel]="search()"
          (ngModelChange)="search.set($event); skip.set(0)"
          data-testid="visit-search"
        />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'visits.status' | dcTranslate }}</mat-label>
        <mat-select [ngModel]="status()" (ngModelChange)="status.set($event); skip.set(0)" data-testid="visit-status-filter">
          <mat-option [value]="null">{{ 'requests.allStatuses' | dcTranslate }}</mat-option>
          @for (s of statuses; track s) {
            <mat-option [value]="s">{{ 'visit.status.' + s | dcTranslate }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'visits.type' | dcTranslate }}</mat-label>
        <mat-select [ngModel]="type()" (ngModelChange)="type.set($event); skip.set(0)">
          <mat-option [value]="null">{{ 'visits.allTypes' | dcTranslate }}</mat-option>
          @for (t of types(); track t.code) {
            <mat-option [value]="t.code">{{ { ar: t.nameAr, en: t.nameEn } | dcLocalize }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-checkbox [ngModel]="overdue()" (ngModelChange)="overdue.set($event); skip.set(0)" data-testid="visit-overdue">{{
        'visits.overdue' | dcTranslate
      }}</mat-checkbox>
    </div>
    <table mat-table [dataSource]="page.value()?.items ?? []" class="dc-table">
      <ng-container matColumnDef="number">
        <th mat-header-cell *matHeaderCellDef>{{ 'visit.number' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let v">
          <a class="dc-link" dir="ltr" [routerLink]="[v.id]" [attr.data-testid]="'visit-' + v.number">{{ v.number }}</a>
        </td>
      </ng-container>
      <ng-container matColumnDef="type">
        <th mat-header-cell *matHeaderCellDef>{{ 'visits.type' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let v">{{ { ar: v.typeNameAr, en: v.typeNameEn } | dcLocalize }}</td>
      </ng-container>
      <ng-container matColumnDef="subject">
        <th mat-header-cell *matHeaderCellDef>{{ 'visits.request' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let v" dir="ltr">
          @if (v.subjectType === 'SERVICE_REQUEST') {
            <a class="dc-link" [routerLink]="['/services/requests', v.subjectId]">{{ v.subjectNumber }}</a>
          } @else {
            {{ v.subjectNumber }}
          }
        </td>
      </ng-container>
      <ng-container matColumnDef="party">
        <th mat-header-cell *matHeaderCellDef>{{ 'visits.party' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let v">
          {{ v.partyName }}
          @if (v.siteAddress) {
            <span class="dc-muted dc-visits__address">{{ v.siteAddress }}</span>
          }
        </td>
      </ng-container>
      <ng-container matColumnDef="inspector">
        <th mat-header-cell *matHeaderCellDef>{{ 'visits.inspector' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let v">
          @if (v.mode === 'EXTERNAL') {
            <span class="dc-muted">{{ 'visits.mode.EXTERNAL' | dcTranslate }}</span>
          } @else {
            {{ v.inspectorName ?? '—' }}
          }
        </td>
      </ng-container>
      <ng-container matColumnDef="when">
        <th mat-header-cell *matHeaderCellDef>{{ 'visits.when' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let v" dir="ltr">
          {{ v.completedAt ?? v.scheduledAt ?? v.dueAt | date: (v.scheduledAt && !v.completedAt ? 'yyyy-MM-dd HH:mm' : 'yyyy-MM-dd') }}
        </td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef>{{ 'visits.status' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let v">
          <span [class]="badge(v.status)">{{ 'visit.status.' + v.status | dcTranslate }}</span>
          @if (v.overdue) {
            <span class="dc-badge dc-badge--danger">{{ 'visits.overdueBadge' | dcTranslate }}</span>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
    @if ((page.value()?.items ?? []).length === 0 && !page.isLoading()) {
      <p class="dc-empty">{{ 'visits.none' | dcTranslate }}</p>
    }
    <mat-paginator [length]="page.value()?.total ?? 0" [pageSize]="take()" [pageSizeOptions]="[25, 50, 100]" (page)="paginate($event)" />
  `,
  styles: `
    .dc-visits__address {
      display: block;
      font-size: 13px;
    }
    .dc-badge + .dc-badge {
      margin-inline-start: 4px;
    }
  `,
})
export class VisitList {
  private readonly api = inject(VisitsApi);
  protected readonly columns = ['number', 'type', 'subject', 'party', 'inspector', 'when', 'status'];
  protected readonly statuses = VISIT_STATUSES;
  protected readonly search = signal('');
  private readonly debounced = toSignal(toObservable(this.search).pipe(debounceTime(300)), { initialValue: '' });
  protected readonly status = signal<VisitStatus | null>(null);
  protected readonly type = signal<string | null>(null);
  protected readonly overdue = signal(false);
  protected readonly skip = signal(0);
  protected readonly take = signal(50);
  private readonly typeRows = toSignal(
    inject(AppsControlApi)
      .list('vis.visit-type')
      .pipe(catchError(() => of([] as ConfigRow[]))),
    {
      initialValue: [] as ConfigRow[],
    },
  );
  protected readonly types = computed(() =>
    this.typeRows().map((r) => ({ code: r.code, nameAr: (r.nameAr as string) || r.code, nameEn: (r.nameEn as string) || r.code })),
  );
  protected readonly page = rxResource({
    params: () => ({
      search: this.debounced().trim() || null,
      status: this.status(),
      type: this.type(),
      overdue: this.overdue(),
      skip: this.skip(),
      take: this.take(),
    }),
    stream: ({ params }) => this.api.search(params),
  });

  protected badge(status: VisitStatus): string {
    return visitStatusBadge(status);
  }

  protected paginate(event: PageEvent): void {
    this.take.set(event.pageSize);
    this.skip.set(event.pageIndex * event.pageSize);
  }
}
