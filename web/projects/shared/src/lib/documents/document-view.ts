import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { DocumentDetail, DocumentFieldView, DocumentStatus, DocumentVersionView, fieldText } from './documents.models';

/** Badge modifier of a document status. */
export function docStatusBadge(status: DocumentStatus): string {
  switch (status) {
    case 'ACTIVE':
      return 'dc-badge--success';
    case 'SUSPENDED':
      return 'dc-badge--warning';
    case 'CANCELLED':
      return 'dc-badge--danger';
    default:
      return '';
  }
}

/** An issued document: its summary, the data and line items of one version (the current one by default) and its versions. */
@Component({
  selector: 'dc-document-view',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatTableModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let d = detail().document;
    <section class="dc-card dc-docview__summary">
      <dl class="dc-docview__facts">
        <div>
          <dt>{{ 'documents.number' | dcTranslate }}</dt>
          <dd dir="ltr" class="dc-docview__number" data-testid="document-number">{{ d.number }}</dd>
        </div>
        <div>
          <dt>{{ 'documents.type' | dcTranslate }}</dt>
          <dd>{{ { ar: d.typeNameAr, en: d.typeNameEn } | dcLocalize }}</dd>
        </div>
        <div>
          <dt>{{ 'documents.holder' | dcTranslate }}</dt>
          <dd>{{ { ar: d.holderNameAr, en: d.holderNameEn } | dcLocalize }}</dd>
        </div>
        <div>
          <dt>{{ 'documents.status' | dcTranslate }}</dt>
          <dd>
            <span class="dc-badge" [class]="badge()" data-testid="document-status">{{ 'docStatus.' + d.status | dcTranslate }}</span>
          </dd>
        </div>
        <div>
          <dt>{{ 'documents.issuedOn' | dcTranslate }}</dt>
          <dd dir="ltr">{{ d.issuedAt | date: 'yyyy-MM-dd' }}</dd>
        </div>
        <div>
          <dt>{{ 'documents.expiresOn' | dcTranslate }}</dt>
          <dd dir="ltr">
            @if (d.expiresAt) {
              {{ d.expiresAt | date: 'yyyy-MM-dd' }}
            } @else {
              {{ 'documents.noExpiry' | dcTranslate }}
            }
          </dd>
        </div>
        <div>
          <dt>{{ 'documents.version' | dcTranslate }}</dt>
          <dd>{{ d.currentVersion }}</dd>
        </div>
      </dl>
      <ng-content />
    </section>

    @if (shown(); as v) {
      @if (dataRows().length) {
        <section class="dc-card">
          <h2>{{ 'documents.data' | dcTranslate }}</h2>
          <dl class="dc-docview__facts">
            @for (row of dataRows(); track row.code) {
              <div>
                <dt>{{ row.label | dcLocalize }}</dt>
                <dd>{{ row.value }}</dd>
              </div>
            }
          </dl>
        </section>
      }
      @if (v.lines.length) {
        <section class="dc-card">
          <h2>{{ 'documents.lines' | dcTranslate }}</h2>
          <table mat-table [dataSource]="v.lines" class="dc-table" data-testid="document-lines">
            @for (column of lineColumns(); track column.code) {
              <ng-container [matColumnDef]="column.code">
                <th mat-header-cell *matHeaderCellDef>{{ { ar: column.nameAr, en: column.nameEn } | dcLocalize }}</th>
                <td mat-cell *matCellDef="let line">{{ text(line[column.code]) }}</td>
              </ng-container>
            }
            <tr mat-header-row *matHeaderRowDef="lineCodes()"></tr>
            <tr mat-row *matRowDef="let row; columns: lineCodes()"></tr>
          </table>
        </section>
      }
    }

    @if (showVersions() && detail().versions.length) {
      <section class="dc-card">
        <h2>{{ 'documents.version' | dcTranslate }}</h2>
        <table mat-table [dataSource]="detail().versions" class="dc-table" data-testid="document-versions">
          <ng-container matColumnDef="version">
            <th mat-header-cell *matHeaderCellDef>{{ 'documents.version' | dcTranslate }}</th>
            <td mat-cell *matCellDef="let v">{{ v.version }}</td>
          </ng-container>
          <ng-container matColumnDef="issued">
            <th mat-header-cell *matHeaderCellDef>{{ 'documents.issuedOn' | dcTranslate }}</th>
            <td mat-cell *matCellDef="let v" dir="ltr">{{ v.issuedAt | date: 'yyyy-MM-dd HH:mm' }}</td>
          </ng-container>
          <ng-container matColumnDef="source">
            <th mat-header-cell *matHeaderCellDef>{{ 'documents.source' | dcTranslate }}</th>
            <td mat-cell *matCellDef="let v" dir="ltr">{{ v.sourceNumber }}</td>
          </ng-container>
          <ng-container matColumnDef="reason">
            <th mat-header-cell *matHeaderCellDef>{{ 'documents.reason' | dcTranslate }}</th>
            <td mat-cell *matCellDef="let v">{{ v.reason }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let v" class="dc-table__actions">
              <button
                matIconButton
                type="button"
                (click)="selected.emit(v)"
                [attr.aria-label]="'documents.versionN' | dcTranslate"
                [attr.data-testid]="'show-version-' + v.version"
              >
                <mat-icon fontSet="material-symbols-outlined">visibility</mat-icon>
              </button>
              <button
                matIconButton
                type="button"
                (click)="download.emit(v)"
                [attr.aria-label]="'documents.download' | dcTranslate"
                [attr.data-testid]="'download-version-' + v.version"
              >
                <mat-icon fontSet="material-symbols-outlined">download</mat-icon>
              </button>
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="versionColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: versionColumns" [class.dc-docview__current]="row.version === shownVersion()"></tr>
        </table>
      </section>
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: 1rem;
    }
    h2 {
      margin-top: 0;
      font-size: 18px;
    }
    .dc-docview__facts {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(12rem, 1fr));
      gap: 1rem 1.5rem;
      margin: 0;
    }
    .dc-docview__facts dt {
      color: var(--dga-gray-500, #6c737f);
      font-size: 13px;
    }
    .dc-docview__facts dd {
      margin: 2px 0 0;
      font-weight: 500;
    }
    .dc-docview__number {
      font-family: ui-monospace, monospace;
      text-align: start;
    }
    .dc-docview__summary {
      display: grid;
      gap: 1rem;
    }
    .dc-docview__current {
      background: var(--dga-gray-50, #f9fafb);
    }
  `,
})
export class DocumentView {
  readonly detail = input.required<DocumentDetail>();
  /** The version whose data is shown; the current one when null. */
  readonly version = input<number | null>(null);
  readonly showVersions = input(true);
  readonly selected = output<DocumentVersionView>();
  readonly download = output<DocumentVersionView>();

  protected readonly versionColumns = ['version', 'issued', 'source', 'reason', 'actions'];
  protected readonly badge = computed(() => docStatusBadge(this.detail().document.status));
  protected readonly shown = computed(() => {
    const versions = this.detail().versions;
    const wanted = this.version() ?? this.detail().document.currentVersion;
    return versions.find((v) => v.version === wanted) ?? versions[0] ?? null;
  });

  /** Data values with their labels; fields without a label keep their code. */
  protected readonly dataRows = computed(() => {
    const data = this.shown()?.data ?? {};
    const labelled = this.detail().fields;
    const codes = [...labelled.map((f) => f.code).filter((c) => c in data), ...Object.keys(data).filter((c) => !labelled.some((f) => f.code === c))];
    return codes.map((code) => {
      const field = labelled.find((f) => f.code === code);
      return { code, label: field ? { ar: field.nameAr, en: field.nameEn } : { ar: code, en: code }, value: fieldText(data[code]) };
    });
  });

  protected readonly shownVersion = computed(() => this.shown()?.version ?? null);

  protected readonly lineColumns = computed<DocumentFieldView[]>(() => {
    const lines = this.shown()?.lines ?? [];
    const labelled = this.detail().lineFields.filter((f) => lines.some((l) => f.code in l));
    const extra = [...new Set(lines.flatMap((l) => Object.keys(l)))].filter((c) => !labelled.some((f) => f.code === c));
    return [...labelled, ...extra.map((code) => ({ code, nameAr: code, nameEn: code, isPublic: false }))];
  });

  protected readonly lineCodes = computed(() => this.lineColumns().map((c) => c.code));

  protected text(value: unknown): string {
    return fieldText(value);
  }
}
