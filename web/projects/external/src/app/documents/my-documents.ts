import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { ActingPartyService, DocumentStatus, DocumentsApi, I18nService, LocalizePipe, TranslatePipe, docStatusBadge } from '@dynamic-cube/shared';
import { FilterOption, ListFilter } from '../portal/list-filter';
import { PageHero } from '../portal/page-hero';

/** Certificates, licenses and other documents held by the acting party, filtered by status or searched. */
@Component({
  selector: 'dc-my-documents',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatTableModule, RouterLink, LocalizePipe, TranslatePipe, ListFilter, PageHero],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dc-page-hero [title]="'documents.mine' | dcTranslate" [text]="'documents.lead' | dcTranslate" />
    <div class="dc-portal-block dc-page-body">
      <dc-list-filter [statuses]="statuses()" [placeholder]="'documents.search' | dcTranslate" [(status)]="status" [(term)]="term" />
      <table mat-table [dataSource]="rows()" class="dc-table dc-table--portal">
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
          <td mat-cell *matCellDef="let d">
            <span dir="ltr">{{ d.number }}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="issued">
          <th mat-header-cell *matHeaderCellDef>{{ 'documents.issuedOn' | dcTranslate }}</th>
          <td mat-cell *matCellDef="let d">
            <span dir="ltr">{{ d.issuedAt | date: 'dd/MM/yyyy' }}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="expires">
          <th mat-header-cell *matHeaderCellDef>{{ 'documents.expiresOn' | dcTranslate }}</th>
          <td mat-cell *matCellDef="let d">
            <span dir="ltr">{{ d.expiresAt | date: 'dd/MM/yyyy' }}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>{{ 'documents.status' | dcTranslate }}</th>
          <td mat-cell *matCellDef="let d">
            <span class="dc-badge" [class]="badge(d.status)">{{ 'docStatus.' + d.status | dcTranslate }}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="open">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let d" class="dc-table__actions">
            <a matIconButton [routerLink]="['/documents', d.id]" [attr.aria-label]="'documents.open' | dcTranslate">
              <mat-icon fontSet="material-symbols-outlined">visibility</mat-icon>
            </a>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
      </table>
      @if (!list.isLoading() && !rows().length) {
        <p class="dc-empty">{{ ((list.value()?.items ?? []).length ? 'list.noMatch' : 'documents.none') | dcTranslate }}</p>
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
export class MyDocuments {
  private readonly api = inject(DocumentsApi);
  private readonly acting = inject(ActingPartyService);
  private readonly i18n = inject(I18nService);
  protected readonly columns = ['type', 'number', 'issued', 'expires', 'status', 'open'];
  protected readonly list = rxResource({ params: () => this.acting.current()?.partyId ?? 'none', stream: () => this.api.mine() });
  protected readonly status = signal('');
  protected readonly term = signal('');
  protected readonly statuses = computed<FilterOption[]>(() =>
    [...new Set((this.list.value()?.items ?? []).map((d) => d.status))].map((value) => ({ value, label: this.i18n.translate('docStatus.' + value) })),
  );
  protected readonly rows = computed(() => {
    const term = this.term().trim().toLowerCase();
    return (this.list.value()?.items ?? []).filter(
      (d) => (!this.status() || d.status === this.status()) && (!term || `${d.number} ${d.typeNameAr} ${d.typeNameEn}`.toLowerCase().includes(term)),
    );
  });

  protected badge(status: DocumentStatus): string {
    return docStatusBadge(status);
  }
}
