import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { DocumentVersionView, DocumentView, DocumentsApi, I18nService, TranslatePipe, problemMessages, saveBlob } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { ConfirmData, ConfirmDialog } from '../apps-control/confirm-dialog';
import { PrivilegesService } from '../apps-control/privileges.service';

/** A version printed as HTML (as sent to the PDF renderer), in a frame that runs no scripts. */
@Component({
  selector: 'dc-document-html-dialog',
  imports: [MatButtonModule, MatDialogModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title dir="ltr">{{ data.title }}</h2>
    <mat-dialog-content><iframe class="dc-docframe" sandbox="" [srcdoc]="data.html" [title]="data.title"></iframe></mat-dialog-content>
    <mat-dialog-actions
      ><button matButton mat-dialog-close>{{ 'common.close' | dcTranslate }}</button></mat-dialog-actions
    >
  `,
  styles: `
    .dc-docframe {
      width: 100%;
      height: 75vh;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      background: #ffffff;
    }
  `,
})
export class DocumentHtmlDialog {
  protected readonly data = inject<{ title: string; html: SafeHtml }>(MAT_DIALOG_DATA);
}

type StatusAction = 'suspend' | 'reinstate' | 'cancel';

/** An issued document in the registry: data, versions, history and — with EDIT — suspend, reinstate or cancel. */
@Component({
  selector: 'dc-document-page',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatProgressBarModule, RouterLink, DocumentView, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <a matButton routerLink=".." class="dc-back">
        <mat-icon fontSet="material-symbols-outlined">{{ i18n.direction() === 'rtl' ? 'arrow_forward' : 'arrow_back' }}</mat-icon>
        {{ 'registry.title' | dcTranslate }}
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
      <dc-document-view [detail]="d" [version]="version()" (selected)="version.set($event.version)" (download)="download($event)">
        <div class="dc-docpage__actions">
          <button matButton="outlined" type="button" (click)="download(null)" data-testid="document-download">
            <mat-icon fontSet="material-symbols-outlined">download</mat-icon>
            {{ 'documents.download' | dcTranslate }}
          </button>
          <button matButton="outlined" type="button" (click)="showHtml()" data-testid="document-html">
            <mat-icon fontSet="material-symbols-outlined">preview</mat-icon>
            {{ 'registry.preview' | dcTranslate }}
          </button>
          <a matButton [href]="d.verifyUrl" target="_blank" rel="noopener" dir="ltr">
            <mat-icon fontSet="material-symbols-outlined">qr_code_2</mat-icon>
            {{ 'documents.verifyLink' | dcTranslate }}
          </a>
          @if (canEdit()) {
            <span class="dc-docpage__spacer"></span>
            @if (d.document.status === 'ACTIVE') {
              <button matButton="outlined" type="button" (click)="change('suspend')" data-testid="document-suspend">
                <mat-icon fontSet="material-symbols-outlined">pause_circle</mat-icon>
                {{ 'registry.suspend' | dcTranslate }}
              </button>
            }
            @if (d.document.status === 'SUSPENDED') {
              <button matButton="outlined" type="button" (click)="change('reinstate')" data-testid="document-reinstate">
                <mat-icon fontSet="material-symbols-outlined">play_circle</mat-icon>
                {{ 'registry.reinstate' | dcTranslate }}
              </button>
            }
            @if (d.document.status === 'ACTIVE' || d.document.status === 'SUSPENDED') {
              <button matButton="outlined" type="button" class="dc-danger" (click)="change('cancel')" data-testid="document-cancel">
                <mat-icon fontSet="material-symbols-outlined">block</mat-icon>
                {{ 'registry.cancel' | dcTranslate }}
              </button>
            }
          }
        </div>
        @if (shownVersion(); as v) {
          @if (v.renderError) {
            <p class="dc-errors" role="alert">
              {{ 'registry.renderError' | dcTranslate }} <span dir="ltr">{{ v.renderError }}</span>
            </p>
          }
        }
      </dc-document-view>

      <section class="dc-card dc-docpage__history">
        <h2>{{ 'registry.history' | dcTranslate }}</h2>
        <ol class="dc-timeline">
          @for (e of events(); track $index) {
            <li>
              <strong>{{ 'docEvent.' + e.type | dcTranslate }}</strong>
              @if (e.version) {
                <span class="dc-muted"> · {{ versionLabel(e.version) }}</span>
              }
              <span class="dc-muted" dir="ltr"> · {{ e.at | date: 'yyyy-MM-dd HH:mm' }} · {{ e.actor }}</span>
              @if (e.comment) {
                <p>{{ e.comment }}</p>
              }
            </li>
          }
        </ol>
      </section>
    } @else if (detail.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: 1rem;
    }
    .dc-docpage__actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      align-items: center;
    }
    .dc-docpage__spacer {
      flex: 1;
    }
    .dc-danger {
      color: #b42318;
    }
    .dc-docpage__history h2 {
      margin-top: 0;
      font-size: 18px;
    }
    .dc-timeline {
      display: grid;
      gap: 0.75rem;
      margin: 0;
      padding-inline-start: 1.25rem;
    }
    .dc-timeline p {
      margin: 0.25rem 0 0;
    }
  `,
})
export class DocumentPage {
  readonly id = input.required<string>();

  private readonly api = inject(DocumentsApi);
  private readonly dialog = inject(MatDialog);
  protected readonly i18n = inject(I18nService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly privileges = inject(PrivilegesService);
  protected readonly detail = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.get(params) });
  protected readonly version = signal<number | null>(null);
  protected readonly errors = signal<string[]>([]);
  protected readonly canEdit = computed(() => this.privileges.can('documents.registry', 'EDIT'));
  protected readonly events = computed(() => [...(this.detail.value()?.events ?? [])].reverse());
  protected readonly shownVersion = computed(() => {
    const d = this.detail.value();
    return d?.versions.find((v) => v.version === (this.version() ?? d.document.currentVersion)) ?? null;
  });

  protected versionLabel(n: number): string {
    return this.i18n.translate('documents.versionN').replace('{n}', String(n));
  }

  protected async download(version: DocumentVersionView | null): Promise<void> {
    const d = this.detail.value();
    if (!d) return;
    const number = version?.version ?? d.document.currentVersion;
    this.errors.set([]);
    try {
      saveBlob(await firstValueFrom(this.api.pdf(d.document.id, number)), `${d.document.number}-v${number}.pdf`);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  protected async showHtml(): Promise<void> {
    const d = this.detail.value();
    const v = this.shownVersion();
    if (!d || !v) return;
    try {
      const html = await firstValueFrom(this.api.html(d.document.id, v.version));
      this.dialog.open(DocumentHtmlDialog, {
        data: { title: `${d.document.number} — ${this.versionLabel(v.version)}`, html: this.sanitizer.bypassSecurityTrustHtml(html) },
        width: '64rem',
        maxWidth: '95vw',
      });
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  protected async change(action: StatusAction): Promise<void> {
    const d = this.detail.value();
    if (!d) return;
    const comment = await firstValueFrom(
      this.dialog
        .open<ConfirmDialog, ConfirmData, string>(ConfirmDialog, {
          data: {
            title: `${this.i18n.translate('registry.' + action)} ${d.document.number}`,
            message: this.i18n.translate(`registry.${action}Hint`),
            confirmLabel: this.i18n.translate('registry.' + action),
            comment: true,
            commentRequired: true,
            danger: action !== 'reinstate',
          },
        })
        .afterClosed(),
    );
    if (!comment) return;
    this.errors.set([]);
    try {
      await firstValueFrom(this.api.changeStatus(d.document.id, action, comment));
      this.detail.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}
