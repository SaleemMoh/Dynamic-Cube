import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { AppsControlApi, I18nService, LocalizePipe, LocalizedText, TranslatePipe, localize } from '@dynamic-cube/shared';
import { catchError, firstValueFrom, of } from 'rxjs';
import { ImportTarget, toPackage } from './json-package';
import { withoutHelp } from './package-template';

export interface JsonImportData {
  /** What is imported (the screen's title). */
  title: LocalizedText;
  /** Rows pasted without entity types are of this type; packages and package items may carry any type. */
  target: ImportTarget | null;
  /** Shown as an example of the expected JSON. */
  example?: unknown;
}

/** The package built from the pasted JSON, ready for the review dialog. */
export interface JsonImportResult {
  fileName: string;
  text: string;
}

/**
 * Paste JSON (or choose a .json file) to import into one screen: a row, a list of rows, package items or a whole
 * package. Rows that exist are updated, the others created; the result is reviewed before anything is saved.
 */
@Component({
  selector: 'dc-json-import-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'jsonImport.title' | dcTranslate }} — {{ data.title | dcLocalize }}</h2>
    <mat-dialog-content class="dc-ji">
      <p class="dc-muted">{{ 'jsonImport.lead' | dcTranslate }}</p>
      <div class="dc-ji__bar">
        <input #file type="file" accept="application/json,.json" hidden (change)="load(file)" data-testid="json-import-file" />
        <button matButton="outlined" type="button" (click)="file.click()">
          <mat-icon fontSet="material-symbols-outlined">upload_file</mat-icon>
          {{ 'jsonImport.chooseFile' | dcTranslate }}
        </button>
        @if (data.example !== undefined) {
          <button matButton type="button" (click)="insertExample()" data-testid="json-import-example">
            <mat-icon fontSet="material-symbols-outlined">data_object</mat-icon>
            {{ 'jsonImport.example' | dcTranslate }}
          </button>
        }
        @if (fileName()) {
          <span class="mono dc-muted">{{ fileName() }}</span>
        }
      </div>
      <mat-form-field subscriptSizing="dynamic" class="dc-ji__text">
        <mat-label>JSON</mat-label>
        <textarea matInput dir="ltr" rows="16" [ngModel]="text()" (ngModelChange)="text.set($event); error.set('')" data-testid="json-import-text"></textarea>
      </mat-form-field>
      @if (error()) {
        <p class="dc-errors" role="alert" dir="auto" data-testid="json-import-error">{{ error() }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      <button matButton mat-dialog-close type="button">{{ 'form.cancel' | dcTranslate }}</button>
      <button matButton="filled" type="button" [disabled]="!text().trim() || busy()" (click)="review()" data-testid="json-import-review">
        <mat-icon fontSet="material-symbols-outlined">fact_check</mat-icon>
        {{ 'jsonImport.review' | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-ji {
      display: grid;
      gap: 10px;
      min-width: min(48rem, 90vw);
    }
    .dc-ji p {
      margin: 0;
    }
    .dc-ji__bar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .dc-ji__text textarea {
      font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
      font-size: 12px;
      line-height: 1.5;
    }
  `,
})
export class JsonImportDialog {
  protected readonly data = inject<JsonImportData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<JsonImportDialog, JsonImportResult>);
  private readonly api = inject(AppsControlApi);
  private readonly i18n = inject(I18nService);
  protected readonly text = signal('');
  protected readonly fileName = signal('');
  protected readonly error = signal('');
  protected readonly busy = signal(false);

  protected async load(input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.fileName.set(file.name);
    this.text.set(await file.text());
    this.error.set('');
  }

  protected insertExample(): void {
    this.text.set(JSON.stringify(this.data.example, null, 2));
  }

  protected async review(): Promise<void> {
    let json: unknown;
    try {
      json = withoutHelp(JSON.parse(this.text()));
    } catch (error) {
      this.error.set(`${this.i18n.translate('form.invalidJson')} ${(error as Error).message}`);
      return;
    }
    this.busy.set(true);
    try {
      const target = this.data.target;
      const rows = target ? await firstValueFrom(this.api.list(target.entityType).pipe(catchError(() => of([])))) : [];
      const existing = new Set(rows.map((r) => r.code));
      const title = `${localize(this.data.title, this.i18n.language())} — JSON`;
      const pkg = toPackage(json, target, existing, title);
      this.ref.close({ fileName: this.fileName() || title, text: JSON.stringify(pkg) });
    } catch (error) {
      const message = (error as Error).message;
      this.error.set(message === 'empty' ? this.i18n.translate('jsonImport.empty') : `${this.i18n.translate('jsonImport.invalid')} ${message}`);
    } finally {
      this.busy.set(false);
    }
  }
}
