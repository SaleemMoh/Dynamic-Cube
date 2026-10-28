import { HttpClient } from '@angular/common/http';
import { DestroyRef, Directive, ElementRef, effect, inject, input } from '@angular/core';
import { Subscription } from 'rxjs';

/**
 * Shows an image that needs the user's sign-in (e.g. an uploaded logo still waiting for approval): browsers send no
 * token with `<img src>`, so the image is fetched through HttpClient and shown from a blob URL.
 */
@Directive({ selector: 'img[dcAuthSrc]' })
export class AuthImageSrc {
  readonly dcAuthSrc = input.required<string | null>();

  private readonly http = inject(HttpClient);
  private readonly element = inject<ElementRef<HTMLImageElement>>(ElementRef);
  private objectUrl: string | null = null;
  private subscription?: Subscription;

  constructor() {
    effect(() => {
      const url = this.dcAuthSrc();
      this.subscription?.unsubscribe();
      this.release();
      if (!url) {
        this.element.nativeElement.removeAttribute('src');
        return;
      }
      this.subscription = this.http.get(url, { responseType: 'blob' }).subscribe({
        next: (blob) => {
          this.release();
          this.objectUrl = URL.createObjectURL(blob);
          this.element.nativeElement.src = this.objectUrl;
        },
        error: () => this.element.nativeElement.removeAttribute('src'),
      });
    });
    inject(DestroyRef).onDestroy(() => {
      this.subscription?.unsubscribe();
      this.release();
    });
  }

  private release(): void {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
  }
}
