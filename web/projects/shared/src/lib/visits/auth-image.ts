import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal } from '@angular/core';

/**
 * An image the API serves only to signed-in users (a visit photo, a signature): fetched with the bearer token, which
 * a plain `<img src>` cannot send, and shown from a local object URL.
 */
@Component({
  selector: 'dc-auth-image',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (url(); as u) {
      <img [src]="u" [alt]="alt()" loading="lazy" />
    } @else {
      <span class="dc-auth-image__placeholder" aria-hidden="true"></span>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    img,
    .dc-auth-image__placeholder {
      display: block;
      width: 100%;
      height: 100%;
      object-fit: cover;
      border-radius: inherit;
      background: var(--dga-gray-100, #f3f4f6);
    }
  `,
})
export class AuthImage {
  readonly src = input.required<string>();
  readonly alt = input('');
  private readonly http = inject(HttpClient);
  protected readonly url = signal<string | null>(null);

  constructor() {
    effect((onCleanup) => {
      const src = this.src();
      let objectUrl: string | null = null;
      const sub = this.http.get(src, { responseType: 'blob' }).subscribe({
        next: (blob) => {
          objectUrl = URL.createObjectURL(blob);
          this.url.set(objectUrl);
        },
        error: () => this.url.set(null),
      });
      onCleanup(() => {
        sub.unsubscribe();
        if (objectUrl) URL.revokeObjectURL(objectUrl);
      });
    });
    inject(DestroyRef).onDestroy(() => {
      const u = this.url();
      if (u) URL.revokeObjectURL(u);
    });
  }
}
