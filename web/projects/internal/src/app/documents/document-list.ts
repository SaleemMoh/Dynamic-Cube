import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { AppsControlApi, BusinessAppContext, docStatusBadge, DOCUMENT_STATUSES, DocumentsApi, DocumentStatus, LocalizePipe, TranslatePipe } from '@dynamic-cube/shared';
import { debounceTime } from 'rxjs';
import { BranchNames } from '../common/branch-names';

/** The issued-document registry: search by number or holder, filter by type and status. */
@Component({
  selector: 'dc-document-list',
  imports: [
    DatePipe,
    FormsModule,
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
      <h1>{{ 'registry.title' | dcTranslate }}</h1>
    </header>
    <div class="dc-toolbar">
      <mat-form-field class="dc-toolbar__search" subscriptSizing="dynamic">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input
          matInput
          [placeholder]="'registry.searchHint' | dcTranslate"
          [ngModel]="search()"
          (ngModelChange)="search.set($event); skip.set(0)"
          data-testid="document-search"
        />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'documents.type' | dcTranslate }}</mat-label>
        <mat-select [ngModel]="type()" (ngModelChange)="type.set($event); skip.set(0)">
          <mat-option [value]="null">{{ 'registry.allTypes' | dcTranslate }}</mat-option>
          @for (t of types.value() ?? []; track t.code) {
            <mat-option [value]="t.code">{{ { ar: t.nameAr, en: t.nameEn } | dcLocalize }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'documents.status' | dcTranslate }}</mat-label>
        <mat-select [ngModel]="status()" (ngModelChange)="status.set($event); skip.set(0)">
          <mat-option [value]="null">{{ 'requests.allStatuses' | dcTranslate }}</mat-option>
          @for (s of statuses; track s) {
            <mat-option [value]="s">{{ 'docStatus.' + s | dcTranslate }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>
    <table mat-table [dataSource]="page.value()?.items ?? []" class="dc-table">
      <ng-container matColumnDef="number">
        <th mat-header-cell *matHeaderCellDef>{{ 'documents.number' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let d">
          <a class="dc-link" dir="ltr" [routerLink]="[d.id]" [attr.data-testid]="'document-' + d.number">{{ d.number }}</a>
        </td>
      </ng-container>
      <ng-container matColumnDef="type">
        <th mat-header-cell *matHeaderCellDef>{{ 'documents.type' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let d">{{ { ar: d.typeNameAr, en: d.typeNameEn } | dcLocalize }}</td>
      </ng-container>
      <ng-container matColumnDef="branch">
        <th mat-header-cell *matHeaderCellDef>{{ 'requests.branch' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let d">{{ branches.name(d.branchCode) | dcLocalize }}</td>
      </ng-container>
      <ng-container matColumnDef="holder">
        <th mat-header-cell *matHeaderCellDef>{{ 'documents.holder' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let d">{{ { ar: d.holderNameAr, en: d.holderNameEn } | dcLocalize }}</td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef>{{ 'documents.status' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let d">
          <span class="dc-badge" [class]="badge(d.status)">{{ 'docStatus.' + d.status | dcTranslate }}</span>
        </td>
      </ng-container>
      <ng-container matColumnDef="issued">
        <th mat-header-cell *matHeaderCellDef>{{ 'documents.issuedOn' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let d" dir="ltr">{{ d.issuedAt | date: 'yyyy-MM-dd' }}</td>
      </ng-container>
      <ng-container matColumnDef="expires">
        <th mat-header-cell *matHeaderCellDef>{{ 'documents.expiresOn' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let d" dir="ltr">{{ d.expiresAt | date: 'yyyy-MM-dd' }}</td>
      </ng-container>
      <ng-container matColumnDef="version">
        <th mat-header-cell *matHeaderCellDef>{{ 'documents.version' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let d">{{ d.currentVersion }}</td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns()"></tr>
      <tr mat-row *matRowDef="let row; columns: columns()"></tr>
    </table>
    @if ((page.value()?.items ?? []).length === 0 && !page.isLoading()) {
      <p class="dc-empty">{{ 'documents.none' | dcTranslate }}</p>
    }
    <mat-paginator [length]="page.value()?.total ?? 0" [pageSize]="take()" [pageSizeOptions]="[25, 50, 100]" (page)="paginate($event)" />
  `,
})
export class DocumentList {
  private readonly api = inject(DocumentsApi);
  private readonly appContext = inject(BusinessAppContext);
  private readonly config = inject(AppsControlApi);
  protected readonly branches = inject(BranchNames);
  /** The branch shows when there is more than one. */
  protected readonly columns = computed(() => [
    'number',
    'type',
    ...(this.branches.several() ? ['branch'] : []),
    'holder',
    'status',
    'issued',
    'expires',
    'version',
  ]);
  protected readonly statuses = DOCUMENT_STATUSES;
  protected readonly types = rxResource({ stream: () => this.config.list('doc.document-type') });
  protected readonly search = signal('');
  private readonly debounced = toSignal(toObservable(this.search).pipe(debounceTime(300)), { initialValue: '' });
  protected readonly type = signal<string | null>(null);
  protected readonly status = signal<DocumentStatus | null>(null);
  protected readonly skip = signal(0);
  protected readonly take = signal(50);
  protected readonly page = rxResource({
    params: () => ({
      search: this.debounced().trim() || null,
      type: this.type(),
      status: this.status(),
      app: this.appContext.current(),
      skip: this.skip(),
      take: this.take(),
    }),
    stream: ({ params }) => this.api.search(params),
  });

  protected badge(status: DocumentStatus): string {
    return docStatusBadge(status);
  }

  protected paginate(event: PageEvent): void {
    this.take.set(event.pageSize);
    this.skip.set(event.pageIndex * event.pageSize);
  }
}
