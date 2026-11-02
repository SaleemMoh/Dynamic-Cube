import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { fileSize, UploadedFile } from '../forms/file-input';
import { I18nService } from '../i18n/i18n.service';
import { LocalizePipe } from '../i18n/translate.pipe';
import { answeredSections, columnQuestion, displayAnswer } from './service-form';
import { QuestionSnapshot, ResolvedVersion, RowColumn } from './service.models';

/** The answers of a request by section, each question once. */
@Component({
  selector: 'dc-request-answers',
  imports: [MatIconModule, LocalizePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (section of sections(); track section.titleEn) {
      <section class="dc-answers__section">
        <h3>{{ { ar: section.titleAr, en: section.titleEn } | dcLocalize }}</h3>
        <dl class="dc-answers">
          @for (q of section.questions; track q.code) {
            <dt [class.dc-answers__wide]="q.type === 'ROWS'">{{ { ar: q.labelAr, en: q.labelEn } | dcLocalize }}</dt>
            <dd [attr.data-testid]="'answer-' + q.code" [class.dc-answers__wide]="q.type === 'ROWS'">
              @if (q.type === 'ROWS') {
                @if (rowsOf(q); as rows) {
                  <div class="dc-answers__rows">
                    <table>
                      <thead>
                        <tr>
                          <th scope="col">#</th>
                          @for (c of q.columns ?? []; track c.code) {
                            <th scope="col">{{ { ar: c.labelAr, en: c.labelEn } | dcLocalize }}</th>
                          }
                        </tr>
                      </thead>
                      <tbody>
                        @for (row of rows; track $index; let i = $index) {
                          <tr>
                            <td>{{ i + 1 }}</td>
                            @for (c of q.columns ?? []; track c.code) {
                              <td>
                                @if (c.type === 'FILE') {
                                  @for (f of cellFiles(row[c.code]); track f.id) {
                                    <button type="button" class="dc-answers__file" (click)="openFile.emit(f)" [attr.data-file]="f.fileName">
                                      <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">attach_file</mat-icon>
                                      {{ f.fileName }}
                                    </button>
                                  } @empty {
                                    —
                                  }
                                } @else {
                                  {{ cellText(q, c, row[c.code]) }}
                                }
                              </td>
                            }
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                } @else {
                  —
                }
              } @else if (q.type === 'FILE') {
                @for (f of filesOf(q); track f.id) {
                  <button type="button" class="dc-answers__file" (click)="openFile.emit(f)" [attr.data-file]="f.fileName">
                    <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">attach_file</mat-icon>
                    {{ f.fileName }} <small dir="ltr">{{ size(f.length) }}</small>
                  </button>
                } @empty {
                  —
                }
              } @else {
                {{ text(q) }}
              }
            </dd>
          }
        </dl>
      </section>
    }
  `,
  styles: `
    .dc-answers__section h3 {
      margin: 1rem 0 0.5rem;
      font-size: 16px;
    }
    .dc-answers {
      display: grid;
      grid-template-columns: minmax(10rem, 1fr) 2fr;
      gap: 0.5rem 1rem;
      margin: 0;
    }
    .dc-answers dt {
      color: var(--dga-text-3, #6c737f);
    }
    .dc-answers dd {
      margin: 0;
    }
    .dc-answers__file {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      margin-inline-end: 12px;
      padding: 0;
      border: 0;
      background: none;
      color: var(--dga-primary-700, #166a45);
      font: inherit;
      text-decoration: underline;
      cursor: pointer;
    }
    .dc-answers .dc-answers__wide {
      grid-column: 1 / -1;
    }
    .dc-answers__rows {
      overflow-x: auto;
    }
    .dc-answers__rows table {
      border-collapse: collapse;
      font-size: 0.875rem;
      min-width: 100%;
    }
    .dc-answers__rows th,
    .dc-answers__rows td {
      padding: 4px 8px;
      border: 1px solid var(--dga-border, #e5e7eb);
      text-align: start;
      vertical-align: top;
    }
    .dc-answers__rows th {
      background: var(--dga-surface-2, #f9fafb);
      font-weight: 600;
    }
    .dc-answers__file mat-icon {
      width: 18px;
      height: 18px;
      font-size: 18px;
    }
    .dc-answers__file small {
      color: var(--dga-text-3, #6c737f);
    }
  `,
})
export class RequestAnswers {
  readonly version = input.required<ResolvedVersion>();
  readonly data = input.required<Record<string, unknown>>();
  /** Files uploaded with the request (for file answers). */
  readonly files = input<UploadedFile[] | null | undefined>([]);
  /** A file answer was clicked: the page opens or downloads it. */
  readonly openFile = output<UploadedFile>();
  private readonly i18n = inject(I18nService);
  protected readonly sections = computed(() => answeredSections(this.version(), this.data()));

  protected text(question: QuestionSnapshot): string {
    return displayAnswer(question, this.data()[question.code], this.i18n.language(), this.files() ?? []);
  }

  protected filesOf(question: QuestionSnapshot): UploadedFile[] {
    return this.cellFiles(this.data()[question.code]);
  }

  /** The rows of a rows answer (null when there are none). */
  protected rowsOf(question: QuestionSnapshot): Record<string, unknown>[] | null {
    const value = this.data()[question.code];
    const rows = Array.isArray(value) ? value.filter((r): r is Record<string, unknown> => !!r && typeof r === 'object') : [];
    return rows.length ? rows : null;
  }

  protected cellText(question: QuestionSnapshot, column: RowColumn, value: unknown): string {
    return displayAnswer(columnQuestion(question, column), value, this.i18n.language(), this.files() ?? []);
  }

  protected cellFiles(value: unknown): UploadedFile[] {
    const ids = Array.isArray(value) ? value.map(String) : value ? [String(value)] : [];
    return ids.map((id) => (this.files() ?? []).find((f) => f.id === id) ?? { id, fileName: id, length: 0 });
  }

  protected size(bytes: number): string {
    return bytes ? fileSize(bytes) : '';
  }
}
