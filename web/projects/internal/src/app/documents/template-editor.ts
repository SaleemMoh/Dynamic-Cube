import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { DocumentsApi, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { catchError, debounceTime, firstValueFrom, switchMap } from 'rxjs';
import { DraftsService } from '../apps-control/drafts.service';
import { ConfigEditorData } from '../apps-control/config/config-editor';
import { SubmitButton } from '../apps-control/submit-button';
import { PrintDesigner } from './print-designer/print-designer';
import { PrintDesign, readDesign, starterDesign, toHtml } from './print-designer/print-model';

const STARTER = `<html dir="rtl" lang="ar">
<head>
  <style>
    body { font-family: 'IBM Plex Sans Arabic'; margin: 2cm; }
    h1 { color: #1b8354; }
    table { width: 100%; border-collapse: collapse; }
    td, th { border: 1px solid #d2d6db; padding: 6px 10px; }
  </style>
</head>
<body>
  <h1>{{ document.typeNameAr }}</h1>
  <p>رقم المستند: {{ document.number }} — تاريخ الإصدار: {{ document.issuedOn }} — تاريخ الانتهاء: {{ document.expiresOn }}</p>
  <p>{{ holder.nameAr }} — {{ holder.identifiers.CR_NUMBER }}</p>
  <table>
    {% for line in lines %}<tr><td>{{ line.sector }}</td><td>{{ line.grade }}</td></tr>{% endfor %}
  </table>
  <p><img src="{{ qr }}" width="120" alt="QR"><br>{{ verifyUrl }}</p>
</body>
</html>`;

const SAMPLE_DATA = { city: 'الرياض' };
const SAMPLE_LINES = [
  { sector: 'المباني', grade: 2 },
  { sector: 'الطرق', grade: 3 },
];

/**
 * Print template editor: HTML with Liquid on one side, the template printed on sample data on the other (refreshed as you
 * type). Saving adds the change to the draft change set, like every configuration screen.
 */
@Component({
  selector: 'dc-document-template-editor',
  imports: [
    SubmitButton,
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    PrintDesigner,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title class="dc-tpl__title">
      <span>
        {{ 'docTemplates.editor' | dcTranslate }}
        @if (data.row) {
          <span dir="ltr">{{ data.row.code }}</span>
        }
      </span>
      <span class="dc-tpl__grow"></span>
      <mat-button-toggle-group [value]="mode()" (change)="setMode($event.value)" data-testid="tpl-mode">
        <mat-button-toggle value="design" data-testid="tpl-mode-design">
          <mat-icon fontSet="material-symbols-outlined">dashboard_customize</mat-icon>
          {{ 'print.modeDesign' | dcTranslate }}
        </mat-button-toggle>
        <mat-button-toggle value="html" data-testid="tpl-mode-html">
          <mat-icon fontSet="material-symbols-outlined">code</mat-icon>
          HTML
        </mat-button-toggle>
      </mat-button-toggle-group>
      <button matIconButton type="button" (click)="toggleMax()" [matTooltip]="(maximized() ? 'designer.exitFullWindow' : 'designer.fullWindow') | dcTranslate" data-testid="tpl-max">
        <mat-icon fontSet="material-symbols-outlined">{{ maximized() ? 'close_fullscreen' : 'open_in_full' }}</mat-icon>
      </button>
    </h2>
    @if (mode() === 'design') {
      <mat-dialog-content class="dc-tpl-design">
        <div class="dc-tpl__row">
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'config.code' | dcTranslate }}</mat-label>
            <input matInput dir="ltr" [ngModel]="code()" (ngModelChange)="code.set($event)" [readonly]="!!data.row" required data-testid="tpl-code" />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'config.nameAr' | dcTranslate }}</mat-label>
            <input matInput [ngModel]="nameAr()" (ngModelChange)="nameAr.set($event)" required data-testid="tpl-name-ar" />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'config.nameEn' | dcTranslate }}</mat-label>
            <input matInput dir="ltr" [ngModel]="nameEn()" (ngModelChange)="nameEn.set($event)" required data-testid="tpl-name-en" />
          </mat-form-field>
          <mat-checkbox [ngModel]="active()" (ngModelChange)="active.set($event)">{{ 'config.active' | dcTranslate }}</mat-checkbox>
          <mat-button-toggle-group [value]="designView()" (change)="designView.set($event.value)" data-testid="tpl-design-view">
            <mat-button-toggle value="layout">{{ 'print.layout' | dcTranslate }}</mat-button-toggle>
            <mat-button-toggle value="preview" data-testid="tpl-design-preview">{{ 'docTemplates.preview' | dcTranslate }}</mat-button-toggle>
          </mat-button-toggle-group>
        </div>
        @if (fromHtml()) {
          <p class="dc-tpl__note" role="status">{{ 'print.replacesHtml' | dcTranslate }}</p>
        }
        @if (designView() === 'layout') {
          <dc-print-designer class="dc-tpl__designer" [(design)]="design" />
        } @else {
          <div class="dc-tpl__preview dc-tpl__preview--full">
            <div class="dc-tpl__preview-bar">
              <span>{{ 'docTemplates.preview' | dcTranslate }}</span>
              <button matButton type="button" (click)="pdf()" data-testid="tpl-pdf">
                <mat-icon fontSet="material-symbols-outlined">picture_as_pdf</mat-icon>
                {{ 'docTemplates.pdf' | dcTranslate }}
              </button>
            </div>
            @if (previewError()) {
              <p class="dc-errors" role="alert" dir="ltr">{{ previewError() }}</p>
            }
            <iframe class="dc-tpl__frame" sandbox="" [srcdoc]="preview()" [title]="'docTemplates.preview' | dcTranslate" data-testid="tpl-preview"></iframe>
          </div>
        }
      </mat-dialog-content>
    } @else {
    <mat-dialog-content class="dc-tpl">
      <div class="dc-tpl__form">
        <div class="dc-tpl__row">
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'config.code' | dcTranslate }}</mat-label>
            <input matInput dir="ltr" [ngModel]="code()" (ngModelChange)="code.set($event)" [readonly]="!!data.row" required data-testid="tpl-code" />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'docTemplates.pageSize' | dcTranslate }}</mat-label>
            <mat-select [ngModel]="pageSize()" (ngModelChange)="pageSize.set($event)">
              @for (size of sizes; track size) {
                <mat-option [value]="size">{{ size }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-checkbox [ngModel]="landscape()" (ngModelChange)="landscape.set($event)">{{ 'docTemplates.landscape' | dcTranslate }}</mat-checkbox>
          <mat-checkbox [ngModel]="active()" (ngModelChange)="active.set($event)">{{ 'config.active' | dcTranslate }}</mat-checkbox>
        </div>
        <div class="dc-tpl__row">
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'config.nameAr' | dcTranslate }}</mat-label>
            <input matInput [ngModel]="nameAr()" (ngModelChange)="nameAr.set($event)" required data-testid="tpl-name-ar" />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'config.nameEn' | dcTranslate }}</mat-label>
            <input matInput dir="ltr" [ngModel]="nameEn()" (ngModelChange)="nameEn.set($event)" required data-testid="tpl-name-en" />
          </mat-form-field>
        </div>
        <mat-form-field subscriptSizing="dynamic" class="dc-tpl__code">
          <mat-label>{{ 'docTemplates.html' | dcTranslate }}</mat-label>
          <textarea matInput dir="ltr" rows="22" spellcheck="false" [ngModel]="html()" (ngModelChange)="html.set($event)" data-testid="tpl-html"></textarea>
          <mat-hint>{{ 'docTemplates.variables' | dcTranslate }}</mat-hint>
        </mat-form-field>
        <details>
          <summary>{{ 'docTemplates.sample' | dcTranslate }}</summary>
          <div class="dc-tpl__row">
            <mat-form-field subscriptSizing="dynamic" class="dc-tpl__code">
              <mat-label>data</mat-label>
              <textarea matInput dir="ltr" rows="5" [ngModel]="sampleData()" (ngModelChange)="sampleData.set($event)"></textarea>
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic" class="dc-tpl__code">
              <mat-label>lines</mat-label>
              <textarea matInput dir="ltr" rows="5" [ngModel]="sampleLines()" (ngModelChange)="sampleLines.set($event)"></textarea>
            </mat-form-field>
          </div>
        </details>
      </div>
      <div class="dc-tpl__preview">
        <div class="dc-tpl__preview-bar">
          <span>{{ 'docTemplates.preview' | dcTranslate }}</span>
          <button matButton type="button" (click)="pdf()" data-testid="tpl-pdf">
            <mat-icon fontSet="material-symbols-outlined">picture_as_pdf</mat-icon>
            {{ 'docTemplates.pdf' | dcTranslate }}
          </button>
        </div>
        @if (previewError()) {
          <p class="dc-errors" role="alert" dir="ltr">{{ previewError() }}</p>
        }
        <iframe class="dc-tpl__frame" sandbox="" [srcdoc]="preview()" [title]="'docTemplates.preview' | dcTranslate" data-testid="tpl-preview"></iframe>
      </div>
    </mat-dialog-content>
    }
    <mat-dialog-actions>
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li>{{ e }}</li>
          }
        </ul>
      }
      <button matButton type="button" mat-dialog-close>{{ 'form.cancel' | dcTranslate }}</button>
      <dc-submit-button [entityType]="data.type.entityType" [disabled]="!valid()" (submitted)="save(true)" />
      <button matButton="filled" type="button" [disabled]="!valid()" (click)="save()" data-testid="tpl-save">
        {{ drafts.saveLabel(data.type.entityType) | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-tpl__title {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .dc-tpl__grow {
      flex: 1;
    }
    .dc-tpl-design {
      display: flex;
      flex-direction: column;
      gap: 8px;
      height: 74vh;
      max-height: none;
    }
    :host-context(.dc-dialog-max) .dc-tpl-design {
      height: calc(100vh - 150px);
    }
    .dc-tpl__designer {
      flex: 1;
      min-height: 0;
    }
    .dc-tpl__note {
      margin: 0;
      padding: 6px 10px;
      border-radius: 6px;
      background: var(--dynamic-cube-gold-50);
      color: var(--dynamic-cube-gold-700);
      font-size: 13px;
    }
    .dc-tpl__preview--full {
      flex: 1;
    }
    .dc-tpl {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
      gap: 1.5rem;
      max-height: 78vh;
    }
    @media (max-width: 1100px) {
      .dc-tpl {
        grid-template-columns: 1fr;
      }
    }
    .dc-tpl__form {
      display: grid;
      gap: 0.75rem;
      align-content: start;
    }
    .dc-tpl__row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.75rem;
    }
    .dc-tpl__row mat-form-field {
      flex: 1;
      min-width: 10rem;
    }
    .dc-tpl__code textarea {
      font-family: ui-monospace, monospace;
      font-size: 13px;
    }
    .dc-tpl__code {
      width: 100%;
    }
    .dc-tpl__preview {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      min-height: 32rem;
    }
    .dc-tpl__preview-bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      font-weight: 600;
    }
    .dc-tpl__frame {
      flex: 1;
      width: 100%;
      min-height: 30rem;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 8px;
      background: #ffffff;
    }
  `,
})
export class DocumentTemplateEditor {
  protected readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<DocumentTemplateEditor, boolean>);
  private readonly api = inject(DocumentsApi);
  private readonly sanitizer = inject(DomSanitizer);
  protected readonly drafts = inject(DraftsService);

  protected readonly sizes = ['A4', 'A5', 'LETTER'];
  protected readonly code = signal(this.data.row?.code ?? '');
  protected readonly nameAr = signal(this.data.row?.nameAr ?? '');
  protected readonly nameEn = signal(this.data.row?.nameEn ?? '');
  protected readonly active = signal(this.data.row?.isActive ?? true);
  protected readonly pageSize = signal((this.data.row?.['pageSize'] as string) ?? 'A4');
  protected readonly landscape = signal((this.data.row?.['landscape'] as boolean) ?? false);
  protected readonly html = signal((this.data.row?.['html'] as string) ?? STARTER);
  /** Designed in the visual designer (the HTML is generated) or written as HTML. */
  private readonly stored = readDesign(this.data.row?.['design']);
  protected readonly mode = signal<'design' | 'html'>(this.data.row && !this.stored ? 'html' : 'design');
  protected readonly design = signal<PrintDesign>(this.stored ?? starterDesign());
  protected readonly designView = signal<'layout' | 'preview'>('layout');
  /** The template was written as HTML and is being redesigned: saving replaces that HTML. */
  protected readonly fromHtml = signal(false);
  protected readonly maximized = signal(false);
  protected readonly sampleData = signal(JSON.stringify(SAMPLE_DATA, null, 2));
  protected readonly sampleLines = signal(JSON.stringify(SAMPLE_LINES, null, 2));
  protected readonly preview = signal<SafeHtml>('');
  protected readonly previewError = signal<string | null>(null);
  protected readonly errors = signal<string[]>([]);
  protected readonly valid = computed(() => !!this.code().trim() && !!this.nameAr().trim() && !!this.nameEn().trim() && !!this.html().trim());

  private readonly request = computed(() => ({
    html: this.html(),
    pageSize: this.pageSize(),
    landscape: this.landscape(),
    data: parse(this.sampleData(), SAMPLE_DATA),
    lines: parse(this.sampleLines(), SAMPLE_LINES),
  }));

  constructor() {
    // In the designer, the HTML (and page) follow the layout.
    effect(() => {
      if (this.mode() !== 'design') return;
      const design = this.design();
      this.html.set(toHtml(design));
      this.pageSize.set(design.pageSize);
      this.landscape.set(design.landscape);
    });
    toObservable(this.request)
      .pipe(
        debounceTime(500),
        switchMap((request) =>
          this.api.preview({ ...request, format: 'html' }).pipe(
            catchError(async (error: unknown) => {
              this.previewError.set((await problemText(error)) ?? problemMessages(error).join(' '));
              return null;
            }),
          ),
        ),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe(async (blob) => {
        if (blob) {
          this.previewError.set(null);
          // The frame is sandboxed without scripts: the template's markup is shown, never run.
          this.preview.set(this.sanitizer.bypassSecurityTrustHtml(await blob.text()));
        }
      });
  }

  protected setMode(mode: 'design' | 'html'): void {
    if (mode === 'design' && this.mode() === 'html' && !this.stored) this.fromHtml.set(true);
    this.mode.set(mode);
  }

  protected toggleMax(): void {
    const max = !this.maximized();
    this.maximized.set(max);
    if (max) {
      this.ref.updateSize('100vw', '100vh');
      this.ref.addPanelClass('dc-dialog-max');
    } else {
      this.ref.updateSize('96rem', '');
      this.ref.removePanelClass('dc-dialog-max');
    }
  }

  protected async pdf(): Promise<void> {
    try {
      const blob = await firstValueFrom(this.api.preview({ ...this.request(), format: 'pdf' }));
      window.open(URL.createObjectURL(blob), '_blank', 'noopener');
    } catch (error) {
      this.previewError.set((await problemText(error)) ?? problemMessages(error).join(' '));
    }
  }

  protected async save(submit = false): Promise<void> {
    this.errors.set([]);
    try {
      await this.drafts.addChange(this.data.type.entityType, this.code().trim(), this.data.row ? 'UPDATE' : 'CREATE', {
        nameAr: this.nameAr().trim(),
        nameEn: this.nameEn().trim(),
        isActive: this.active(),
        html: this.html(),
        pageSize: this.pageSize(),
        landscape: this.landscape(),
        // Kept to reopen the layout in the designer; hand-written HTML has none.
        design: this.mode() === 'design' ? this.design() : null,
      }, { submit });
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}

function parse(text: string, fallback: unknown): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return fallback;
  }
}

/** The problem detail of an error whose body came back as a blob. */
async function problemText(error: unknown): Promise<string | null> {
  const body = (error as { error?: unknown })?.error;
  if (body instanceof Blob) {
    try {
      const problem = JSON.parse(await body.text()) as { detail?: string; errors?: Record<string, string[]> };
      return (
        problem.detail ??
        Object.values(problem.errors ?? {})
          .flat()
          .join(' ')
      );
    } catch {
      return null;
    }
  }
  return null;
}
