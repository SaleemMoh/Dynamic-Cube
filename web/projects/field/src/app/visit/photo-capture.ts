import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@dynamic-cube/shared';

export interface CapturedPhoto {
  fileName: string;
  /** A JPEG data URL, already reduced for sending. */
  content: string;
  caption: string | null;
  takenAt: string | null;
}

/** Largest side of a photo sent (pixels) and its JPEG quality: a few hundred kilobytes per photo. */
const MAX_SIDE = 1600;
const QUALITY = 0.8;

/** Reduces a camera photo to at most {@link MAX_SIDE} pixels as JPEG, keeping its orientation. */
export async function compressPhoto(file: Blob, maxSide = MAX_SIDE, quality = QUALITY): Promise<string> {
  const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
  const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height));
  const canvas = document.createElement('canvas');
  canvas.width = Math.round(bitmap.width * scale);
  canvas.height = Math.round(bitmap.height * scale);
  canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  bitmap.close();
  return canvas.toDataURL('image/jpeg', quality);
}

/** Takes photos with the device camera (or picks them), reduced in size, each with an optional caption. */
@Component({
  selector: 'dc-photo-capture',
  imports: [FormsModule, MatButtonModule, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-photos">
      @for (p of photos(); track $index; let i = $index) {
        <figure class="dc-photos__item">
          <img [src]="p.content" [alt]="p.caption ?? p.fileName" />
          <input
            class="dc-photos__caption"
            dir="auto"
            [placeholder]="'field.caption' | dcTranslate"
            [ngModel]="p.caption ?? ''"
            (ngModelChange)="setCaption(i, $event)"
          />
          <button matIconButton type="button" class="dc-photos__remove" (click)="remove(i)" [attr.aria-label]="'field.removePhoto' | dcTranslate">
            <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
          </button>
        </figure>
      }
    </div>
    <label class="dc-photos__add" [class.dc-photos__add--busy]="busy()">
      <mat-icon fontSet="material-symbols-outlined">photo_camera</mat-icon>
      {{ 'field.addPhoto' | dcTranslate }}
      <input
        type="file"
        accept="image/*"
        capture="environment"
        multiple
        (change)="add($event)"
        [disabled]="busy() || photos().length >= max()"
        data-testid="field-photo-input"
      />
    </label>
  `,
  styles: `
    :host {
      display: grid;
      gap: 8px;
    }
    .dc-photos {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(8.5rem, 1fr));
      gap: 8px;
    }
    .dc-photos__item {
      position: relative;
      margin: 0;
    }
    .dc-photos__item img {
      display: block;
      width: 100%;
      aspect-ratio: 4 / 3;
      object-fit: cover;
      border-radius: 8px;
    }
    .dc-photos__caption {
      width: 100%;
      margin-top: 4px;
      padding: 4px 6px;
      border: 1px solid var(--dga-border, #d2d6db);
      border-radius: 6px;
      font: inherit;
      font-size: 13px;
    }
    .dc-photos__remove {
      position: absolute;
      top: 2px;
      inset-inline-end: 2px;
      background: rgb(255 255 255 / 85%);
    }
    .dc-photos__add {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 6px;
      min-height: 48px;
      border: 1px dashed var(--dynamic-cube-green, #136f40);
      border-radius: 8px;
      color: var(--dynamic-cube-green, #136f40);
      font-weight: 600;
      cursor: pointer;
    }
    .dc-photos__add--busy {
      opacity: 0.6;
    }
    .dc-photos__add input {
      position: absolute;
      width: 1px;
      height: 1px;
      opacity: 0;
    }
  `,
})
export class PhotoCapture {
  readonly photos = input.required<CapturedPhoto[]>();
  readonly max = input(8);
  readonly photosChange = output<CapturedPhoto[]>();
  protected readonly busy = signal(false);

  protected async add(event: Event): Promise<void> {
    const target = event.target as HTMLInputElement;
    const files = [...(target.files ?? [])].slice(0, this.max() - this.photos().length);
    target.value = '';
    if (!files.length) return;
    this.busy.set(true);
    try {
      const added: CapturedPhoto[] = [];
      for (const file of files) {
        added.push({
          fileName: file.name.replace(/\.[^.]+$/, '') + '.jpg',
          content: await compressPhoto(file),
          caption: null,
          takenAt: new Date().toISOString(),
        });
      }
      this.photosChange.emit([...this.photos(), ...added]);
    } finally {
      this.busy.set(false);
    }
  }

  protected setCaption(index: number, caption: string): void {
    this.photosChange.emit(this.photos().map((p, i) => (i === index ? { ...p, caption: caption.trim() || null } : p)));
  }

  protected remove(index: number): void {
    this.photosChange.emit(this.photos().filter((_, i) => i !== index));
  }
}
