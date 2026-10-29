import { AfterViewInit, ChangeDetectionStrategy, Component, ElementRef, input, output, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@dynamic-cube/shared';

/** The site representative signs with a finger or stylus; the signature is kept as a PNG data URL. */
@Component({
  selector: 'dc-signature-pad',
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <canvas
      #canvas
      class="dc-signature"
      (pointerdown)="down($event)"
      (pointermove)="move($event)"
      (pointerup)="up()"
      (pointerleave)="up()"
      [attr.aria-label]="'field.signHere' | dcTranslate"
      data-testid="field-signature"
    ></canvas>
    <div class="dc-signature__bar">
      <span class="dc-signature__hint">{{ (value() ? 'field.signed' : 'field.signHere') | dcTranslate }}</span>
      <button matButton type="button" (click)="clear()" [disabled]="!value()">
        <mat-icon fontSet="material-symbols-outlined">ink_eraser</mat-icon>
        {{ 'field.clearSignature' | dcTranslate }}
      </button>
    </div>
  `,
  styles: `
    :host {
      display: grid;
      gap: 4px;
    }
    .dc-signature {
      width: 100%;
      height: 160px;
      border: 1px solid var(--dga-border, #d2d6db);
      border-radius: 8px;
      background: #fff;
      touch-action: none;
    }
    .dc-signature__bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .dc-signature__hint {
      font-size: 13px;
      color: var(--dga-text-3, #384250);
    }
  `,
})
export class SignaturePad implements AfterViewInit {
  readonly value = input<string | null>(null);
  readonly valueChange = output<string | null>();
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private drawing = false;
  private drawn = false;

  ngAfterViewInit(): void {
    const canvas = this.canvas().nativeElement;
    const ratio = window.devicePixelRatio || 1;
    canvas.width = canvas.clientWidth * ratio;
    canvas.height = canvas.clientHeight * ratio;
    const ctx = this.context();
    ctx.scale(ratio, ratio);
    ctx.lineWidth = 2.2;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.strokeStyle = '#111927';
    const saved = this.value();
    if (saved) {
      const image = new Image();
      image.onload = () => ctx.drawImage(image, 0, 0, canvas.clientWidth, canvas.clientHeight);
      image.src = saved;
      this.drawn = true;
    }
  }

  protected down(event: PointerEvent): void {
    this.drawing = true;
    this.canvas().nativeElement.setPointerCapture(event.pointerId);
    const { x, y } = this.point(event);
    const ctx = this.context();
    ctx.beginPath();
    ctx.moveTo(x, y);
    ctx.lineTo(x + 0.1, y + 0.1);
    ctx.stroke();
  }

  protected move(event: PointerEvent): void {
    if (!this.drawing) return;
    const { x, y } = this.point(event);
    const ctx = this.context();
    ctx.lineTo(x, y);
    ctx.stroke();
    this.drawn = true;
  }

  protected up(): void {
    if (!this.drawing) return;
    this.drawing = false;
    if (this.drawn) this.valueChange.emit(this.canvas().nativeElement.toDataURL('image/png'));
  }

  protected clear(): void {
    const canvas = this.canvas().nativeElement;
    this.context().clearRect(0, 0, canvas.width, canvas.height);
    this.drawn = false;
    this.valueChange.emit(null);
  }

  private context(): CanvasRenderingContext2D {
    return this.canvas().nativeElement.getContext('2d')!;
  }

  private point(event: PointerEvent): { x: number; y: number } {
    const rect = this.canvas().nativeElement.getBoundingClientRect();
    return { x: event.clientX - rect.left, y: event.clientY - rect.top };
  }
}
