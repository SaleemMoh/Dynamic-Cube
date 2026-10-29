import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, forwardRef, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe } from '../i18n/translate.pipe';

/** A file kept for a field (uploaded to the server, or held by a preview). */
export interface UploadedFile {
  id: string;
  fileName: string;
  length: number;
  contentType?: string;
}

/**
 * Where a form's files go: the portal uploads them to the request; a preview keeps them in the browser. The field's value
 * is the list of file ids.
 */
export interface FileUploader {
  /** Files already uploaded, to show the names of the ids in a value. */
  files(): UploadedFile[];
  upload(field: string, file: File): Promise<UploadedFile>;
  remove(field: string, id: string): Promise<void>;
  /** Opens or downloads a file. */
  open(id: string): void;
}

/** Usual documents and images, when a field names no extensions (the server's default too). */
export const DEFAULT_FILE_EXTENSIONS = ['pdf', 'jpg', 'jpeg', 'png', 'doc', 'docx', 'xls', 'xlsx'];

/** A size for people: 1.2 MB, 350 KB. */
export function fileSize(bytes: number): string {
  return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

/** Why a file cannot be taken (a translation key), or null. */
export function fileProblem(file: { name: string; size: number }, accept: string[], maxSizeMb: number): string | null {
  const extension = file.name.includes('.') ? file.name.split('.').pop()!.toLowerCase() : '';
  if (!accept.includes(extension)) return 'files.badType';
  if (file.size > maxSizeMb * 1024 * 1024) return 'files.tooLarge';
  return null;
}

/**
 * Upload control of a file field: choose or drop files, see them with their size, open or remove them. Each file is
 * uploaded at once through the form's {@link FileUploader}; the value is the list of file ids.
 */
@Component({
  selector: 'dc-file-input',
  imports: [DecimalPipe, MatButtonModule, MatIconModule, MatProgressBarModule, MatTooltipModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => FileInput), multi: true }],
  template: `
    <div
      class="dc-file"
      [class.dc-file--over]="over()"
      [class.dc-file--disabled]="disabled()"
      (dragover)="$event.preventDefault(); over.set(!disabled())"
      (dragleave)="over.set(false)"
      (drop)="drop($event)"
      [attr.data-testid]="testId()"
    >
      @for (f of shown(); track f.id) {
        <div class="dc-file__item" [attr.data-file]="f.fileName">
          <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{ f.fileName.endsWith('.pdf') ? 'picture_as_pdf' : 'description' }}</mat-icon>
          <button type="button" class="dc-file__name" (click)="uploader()?.open(f.id)" [disabled]="!uploader()">{{ f.fileName }}</button>
          <span class="dc-file__size" dir="ltr">{{ size(f.length) }}</span>
          @if (!disabled()) {
            <button
              matIconButton
              type="button"
              (click)="remove(f.id)"
              [matTooltip]="'files.remove' | dcTranslate"
              [attr.aria-label]="'files.remove' | dcTranslate"
            >
              <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
            </button>
          }
        </div>
      }
      @if (busy()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (canAdd()) {
        <label class="dc-file__drop">
          <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">upload_file</mat-icon>
          <span>
            <strong>{{ 'files.choose' | dcTranslate }}</strong>
            <small>
              {{ 'files.rules' | dcTranslate }} {{ extensions().join(', ') }} · {{ maxSizeMb() | number }} MB
              @if (multiple()) {
                · {{ 'files.most' | dcTranslate }} {{ maxFiles() }}
              }
            </small>
          </span>
          <input type="file" [accept]="acceptAttr()" [multiple]="multiple()" (change)="chosen($event)" [disabled]="disabled() || !uploader()" />
        </label>
      }
      @if (!uploader()) {
        <small class="dc-file__note">{{ 'files.noUploader' | dcTranslate }}</small>
      }
      @if (error()) {
        <small class="dc-file__error" role="alert">{{ error()! | dcTranslate }}</small>
      }
    </div>
  `,
  styles: `
    .dc-file {
      display: grid;
      gap: 6px;
      padding: 8px;
      border: 1px dashed var(--dga-gray-300, #d0d5dd);
      border-radius: var(--shell-radius, 8px);
      background: #fff;
    }
    .dc-file--over {
      border-color: var(--dynamic-cube-gold, #1b8354);
      background: var(--dga-gray-50, #f9fafb);
    }
    .dc-file__item {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 4px 6px;
      border-radius: 6px;
      background: var(--dga-gray-50, #f9fafb);
    }
    .dc-file__item mat-icon {
      color: var(--dga-gray-500, #667085);
    }
    .dc-file__name {
      flex: 1;
      min-width: 0;
      overflow: hidden;
      padding: 0;
      border: 0;
      background: none;
      color: inherit;
      font: inherit;
      text-align: start;
      text-decoration: underline;
      text-overflow: ellipsis;
      white-space: nowrap;
      cursor: pointer;
    }
    .dc-file__size {
      color: var(--dga-gray-500, #667085);
      font-size: 12px;
    }
    .dc-file__drop {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 8px;
      cursor: pointer;
    }
    .dc-file__drop span {
      display: grid;
    }
    .dc-file__drop small,
    .dc-file__note {
      color: var(--dga-gray-500, #667085);
      font-size: 12px;
    }
    .dc-file__drop input {
      position: absolute;
      width: 1px;
      height: 1px;
      opacity: 0;
    }
    .dc-file__drop:focus-within {
      outline: 2px solid var(--dynamic-cube-gold, #1b8354);
      border-radius: 6px;
    }
    .dc-file__error {
      color: var(--dga-error-600, #d92d20);
    }
  `,
})
export class FileInput implements ControlValueAccessor {
  readonly field = input.required<string>();
  readonly uploader = input<FileUploader | null>(null);
  readonly accept = input<string[] | undefined>(undefined);
  readonly maxSizeMb = input<number>(10);
  readonly maxFiles = input<number>(5);
  readonly multiple = input(false);
  readonly testId = input<string | null>(null);

  protected readonly ids = signal<string[]>([]);
  protected readonly disabled = signal(false);
  protected readonly busy = signal(false);
  protected readonly over = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly extensions = computed(() => (this.accept()?.length ? this.accept()! : DEFAULT_FILE_EXTENSIONS));
  protected readonly acceptAttr = computed(() =>
    this.extensions()
      .map((e) => '.' + e)
      .join(','),
  );
  protected readonly limit = computed(() => (this.multiple() ? this.maxFiles() : 1));
  /** The value's files with their names (an id the uploader does not know shows as itself). */
  protected readonly shown = computed(() => {
    const known = this.uploader()?.files() ?? [];
    return this.ids().map((id) => known.find((f) => f.id === id) ?? { id, fileName: id, length: 0 });
  });
  protected readonly canAdd = computed(() => !this.disabled() && (this.limit() === 1 || this.ids().length < this.limit()));
  private onChange: (value: string[]) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  writeValue(value: unknown): void {
    this.ids.set(Array.isArray(value) ? value.map(String) : typeof value === 'string' && value ? [value] : []);
  }

  registerOnChange(fn: (value: string[]) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.disabled.set(disabled);
  }

  protected size(bytes: number): string {
    return bytes ? fileSize(bytes) : '';
  }

  protected chosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    void this.add([...(input.files ?? [])]);
    input.value = '';
  }

  protected drop(event: DragEvent): void {
    event.preventDefault();
    this.over.set(false);
    if (!this.disabled()) void this.add([...(event.dataTransfer?.files ?? [])]);
  }

  private async add(files: File[]): Promise<void> {
    const uploader = this.uploader();
    if (!uploader || !files.length) return;
    this.error.set(null);
    this.onTouched();
    // One file: the new one replaces the answer; several: up to the limit.
    const room = this.limit() === 1 ? 1 : this.limit() - this.ids().length;
    if (files.length > room) this.error.set('files.tooMany');
    for (const file of files.slice(0, room)) {
      const problem = fileProblem(file, this.extensions(), this.maxSizeMb());
      if (problem) {
        this.error.set(problem);
        continue;
      }
      this.busy.set(true);
      try {
        const uploaded = await uploader.upload(this.field(), file);
        this.ids.set(this.limit() === 1 ? [uploaded.id] : [...this.ids(), uploaded.id]);
        this.onChange(this.ids());
      } catch {
        this.error.set('files.failed');
      } finally {
        this.busy.set(false);
      }
    }
  }

  protected async remove(id: string): Promise<void> {
    const uploader = this.uploader();
    if (!uploader) return;
    this.busy.set(true);
    try {
      await uploader.remove(this.field(), id);
      this.ids.set(this.ids().filter((i) => i !== id));
      this.onChange(this.ids());
    } catch {
      this.error.set('files.failed');
    } finally {
      this.busy.set(false);
    }
  }
}
