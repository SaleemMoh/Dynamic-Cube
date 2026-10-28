import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { DocumentView, DocumentsApi, I18nService, TranslatePipe, problemMessages, saveBlob } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';

/** One of the acting party's documents: its data and line items, the PDF and the verification link. */
@Component({
  selector: 'dc-my-document-page',
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, RouterLink, DocumentView, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <a matButton routerLink="/documents">
        <mat-icon fontSet="material-symbols-outlined">{{ i18n.direction() === 'rtl' ? 'arrow_forward' : 'arrow_back' }}</mat-icon>
        {{ 'documents.mine' | dcTranslate }}
      </a>
    </header>
    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (e of errors(); track $index) {
          <li>{{ e }}</li>
        }
      </ul>
    }
    @if (detail.value(); as d) {
      <dc-document-view [detail]="d" [showVersions]="false">
        <div class="dc-mydoc__actions">
          <button matButton="filled" type="button" (click)="download()" data-testid="my-document-download">
            <mat-icon fontSet="material-symbols-outlined">download</mat-icon>
            {{ 'documents.download' | dcTranslate }}
          </button>
          <a matButton [href]="d.verifyUrl" target="_blank" rel="noopener">
            <mat-icon fontSet="material-symbols-outlined">qr_code_2</mat-icon>
            {{ 'documents.verifyLink' | dcTranslate }}
          </a>
        </div>
      </dc-document-view>
    } @else if (detail.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (detail.error()) {
      <p class="dc-errors" role="alert">{{ 'documents.none' | dcTranslate }}</p>
    }
  `,
  styles: `
    .dc-mydoc__actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
  `,
})
export class MyDocumentPage {
  readonly id = input.required<string>();
  protected readonly i18n = inject(I18nService);
  private readonly api = inject(DocumentsApi);
  protected readonly detail = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.myDocument(params) });
  protected readonly errors = signal<string[]>([]);

  protected async download(): Promise<void> {
    const d = this.detail.value();
    if (!d) return;
    try {
      saveBlob(await firstValueFrom(this.api.myPdf(d.document.id)), `${d.document.number}.pdf`);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}
