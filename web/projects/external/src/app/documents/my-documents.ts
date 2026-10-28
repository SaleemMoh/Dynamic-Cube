import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { ActingPartyService, DocumentStatus, DocumentsApi, LocalizePipe, TranslatePipe, docStatusBadge } from '@dynamic-cube/shared';

/** Certificates, licenses and other documents held by the acting party. */
@Component({
  selector: 'dc-my-documents',
  imports: [DatePipe, MatTableModule, RouterLink, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'documents.mine' | dcTranslate }}</h1>
    </header>
    <table mat-table [dataSource]="list.value()?.items ?? []" class="dc-table">
      <ng-container matColumnDef="type">
        <th mat-header-cell *matHeaderCellDef>{{ 'documents.type' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let d">
          <a class="dc-link" [routerLink]="['/documents', d.id]" [attr.data-testid]="'my-document-' + d.number">{{
            { ar: d.typeNameAr, en: d.typeNameEn } | dcLocalize
          }}</a>
        </td>
      </ng-container>
      <ng-container matColumnDef="number">
        <th mat-header-cell *matHeaderCellDef>{{ 'documents.number' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let d" dir="ltr">{{ d.number }}</td>
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
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
    @if (!list.isLoading() && !(list.value()?.items ?? []).length) {
      <p class="dc-muted">{{ 'documents.none' | dcTranslate }}</p>
    }
  `,
})
export class MyDocuments {
  private readonly api = inject(DocumentsApi);
  private readonly acting = inject(ActingPartyService);
  protected readonly columns = ['type', 'number', 'status', 'issued', 'expires'];
  protected readonly list = rxResource({ params: () => this.acting.current()?.partyId ?? 'none', stream: () => this.api.mine() });

  protected badge(status: DocumentStatus): string {
    return docStatusBadge(status);
  }
}
