import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { DocumentStatus, DocumentSummary, DocumentsApi, LocalizePipe, TranslatePipe, docStatusBadge, saveBlob } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';

/** The documents issued for a request (shown on the request page once there are any). */
@Component({
  selector: 'dc-request-documents',
  imports: [MatButtonModule, MatIconModule, RouterLink, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (documents.value()?.length) {
      <section class="dc-card" data-testid="request-documents">
        <h2>{{ 'documents.issuedForRequest' | dcTranslate }}</h2>
        <ul class="dc-reqdocs">
          @for (d of documents.value(); track d.id) {
            <li>
              <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">workspace_premium</mat-icon>
              <div class="dc-reqdocs__text">
                <a class="dc-link" [routerLink]="['/documents', d.id]">{{ { ar: d.typeNameAr, en: d.typeNameEn } | dcLocalize }}</a>
                <span class="dc-muted" dir="ltr">{{ d.number }}</span>
              </div>
              <span class="dc-badge" [class]="badge(d.status)">{{ 'docStatus.' + d.status | dcTranslate }}</span>
              <button matButton="outlined" type="button" (click)="download(d)" [attr.data-testid]="'download-' + d.number">
                <mat-icon fontSet="material-symbols-outlined">download</mat-icon>
                {{ 'documents.download' | dcTranslate }}
              </button>
            </li>
          }
        </ul>
      </section>
    }
  `,
  styles: `
    h2 {
      margin-top: 0;
    }
    .dc-reqdocs {
      display: grid;
      gap: 0.75rem;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .dc-reqdocs li {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.75rem;
    }
    .dc-reqdocs mat-icon {
      color: var(--dga-primary, #1b8354);
    }
    .dc-reqdocs__text {
      display: grid;
      flex: 1;
      min-width: 12rem;
    }
  `,
})
export class RequestDocuments {
  readonly requestId = input.required<string>();
  private readonly api = inject(DocumentsApi);
  protected readonly documents = rxResource({ params: () => this.requestId(), stream: ({ params }) => this.api.myBySource('SERVICE_REQUEST', params) });

  protected badge(status: DocumentStatus): string {
    return docStatusBadge(status);
  }

  protected async download(d: DocumentSummary): Promise<void> {
    saveBlob(await firstValueFrom(this.api.myPdf(d.id)), `${d.number}.pdf`);
  }
}
