import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  AppsControlApi,
  AuthImageSrc,
  ConfigRow,
  LocalizedText,
  TranslatePipe,
  problemMessages,
  themeVariables,
} from '@dynamic-cube/shared';
import { catchError, firstValueFrom, of } from 'rxjs';
import { DraftsService } from '../drafts.service';
import { SubmitButton } from '../submit-button';

const ACCEPT = 'image/png,image/jpeg,image/webp,image/svg+xml';
const HEX = /^#[0-9a-f]{6}$/i;
const PRESETS = [
  '#ca9f45',
  '#1b8354',
  '#136f40',
  '#0e7490',
  '#1d4ed8',
  '#4338ca',
  '#7e22ce',
  '#9f1239',
  '#b45309',
  '#374151',
];

type Slot = 'logoFileKey' | 'footerImageKey' | 'loginImageKey' | 'heroImageKey';
const IMAGES: {
  slot: Slot;
  key: string;
  icon: string;
  inherited: 'logoUrl' | 'footerImageUrl' | 'loginImageUrl' | 'portalHeroImageUrl';
  wide?: boolean;
}[] = [
  { slot: 'logoFileKey', key: 'logo', icon: 'image', inherited: 'logoUrl' },
  {
    slot: 'footerImageKey',
    key: 'footerImage',
    icon: 'branding_watermark',
    inherited: 'footerImageUrl',
  },
  {
    slot: 'loginImageKey',
    key: 'loginImage',
    icon: 'wallpaper',
    inherited: 'loginImageUrl',
    wide: true,
  },
  {
    slot: 'heroImageKey',
    key: 'heroImage',
    icon: 'panorama',
    inherited: 'portalHeroImageUrl',
    wide: true,
  },
];

/** A channel or an app whose look is edited here. */
export interface BrandingTarget {
  code: string;
  name: LocalizedText;
  /** A channel (internal system, portal, field app) rather than an app. */
  channel: boolean;
}

/**
 * The look of one channel or app: its title, colour and images. Whatever is left empty is taken from the channel (for
 * an app) and then from the organization, and the page shows what that is. Saving goes through the system area's change
 * set like the organization's branding.
 */
@Component({
  selector: 'dc-app-branding',
  imports: [
    AuthImageSrc,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatTooltipModule,
    SubmitButton,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (error of errors(); track $index) {
          <li dir="auto">{{ error }}</li>
        }
      </ul>
    }
    <section class="dc-card" data-testid="app-branding">
      <div class="dc-ab__head">
        <div>
          <h2>{{ 'branding.ownLook' | dcTranslate }}</h2>
          <p class="dc-muted">
            {{ (target().channel ? 'branding.channelHint' : 'branding.appHint') | dcTranslate }}
          </p>
        </div>
        @if (canEdit()) {
          <div class="dc-ab__actions">
            @if (row()) {
              <button
                matButton
                type="button"
                (click)="remove()"
                [disabled]="busy()"
                data-testid="app-branding-remove"
              >
                {{ 'branding.useInherited' | dcTranslate }}
              </button>
            }
            <dc-submit-button
              entityType="apps.app-branding"
              [disabled]="busy()"
              (submitted)="save(true)"
            />
            <button
              matButton="filled"
              type="button"
              (click)="save()"
              [disabled]="busy()"
              data-testid="app-branding-save"
            >
              {{ drafts.saveLabel('apps.app-branding') | dcTranslate }}
            </button>
          </div>
        }
      </div>
      <div class="dc-ab__titles">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'branding.titleAr' | dcTranslate }}</mat-label>
          <input
            matInput
            [ngModel]="titleAr()"
            (ngModelChange)="titleAr.set($event)"
            [disabled]="!canEdit()"
            data-testid="app-branding-title-ar"
          />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'branding.titleEn' | dcTranslate }}</mat-label>
          <input
            matInput
            dir="ltr"
            [ngModel]="titleEn()"
            (ngModelChange)="titleEn.set($event)"
            [disabled]="!canEdit()"
            data-testid="app-branding-title-en"
          />
        </mat-form-field>
      </div>

      <h3>{{ 'branding.colors' | dcTranslate }}</h3>
      <div class="dc-ab__color">
        <input
          type="color"
          class="dc-ab__swatch"
          [value]="shownColor()"
          (input)="color.set($any($event.target).value)"
          [disabled]="!canEdit()"
          [attr.aria-label]="'branding.colors' | dcTranslate"
        />
        <mat-form-field subscriptSizing="dynamic" class="dc-ab__hex">
          <input
            matInput
            dir="ltr"
            maxlength="7"
            [placeholder]="inheritedColor() ?? ''"
            [ngModel]="color() ?? ''"
            (ngModelChange)="color.set($event ? $event.toLowerCase() : null)"
            [disabled]="!canEdit()"
            data-testid="app-branding-color"
          />
        </mat-form-field>
        @for (p of presets; track p) {
          <button
            type="button"
            class="dc-ab__preset"
            [style.background]="p"
            [class.dc-ab__preset--on]="shownColor() === p"
            (click)="color.set(p)"
            [disabled]="!canEdit()"
            [matTooltip]="p"
            [attr.aria-label]="p"
          ></button>
        }
        @if (color()) {
          <button matButton type="button" (click)="color.set(null)" [disabled]="!canEdit()">
            {{ 'branding.inherit' | dcTranslate }}
          </button>
        } @else {
          <span class="dc-muted">{{ 'branding.inherited' | dcTranslate }}</span>
        }
      </div>
      @if (badColor()) {
        <span class="dc-ab__bad">{{ 'branding.badColor' | dcTranslate }}</span>
      }
      <div class="dc-ab__preview" [style]="previewStyle()">
        <span class="dc-ab__button">{{ 'branding.previewButton' | dcTranslate }}</span>
        <span class="dc-ab__chip">{{ 'branding.previewSelected' | dcTranslate }}</span>
      </div>
    </section>

    <div class="dc-ab__images">
      @for (img of images; track img.slot) {
        <section class="dc-card dc-ab__image" [attr.data-testid]="'app-image-' + img.key">
          <h2>{{ 'branding.' + img.key | dcTranslate }}</h2>
          <div class="dc-ab__frame" [class.dc-ab__frame--wide]="img.wide">
            @if (keys()[img.slot]; as key) {
              <img [dcAuthSrc]="assetUrl(key)" [alt]="'branding.' + img.key | dcTranslate" />
            } @else if (inherited()?.[img.inherited]; as url) {
              <img
                class="dc-ab__faded"
                [dcAuthSrc]="url"
                [alt]="'branding.' + img.key | dcTranslate"
              />
            } @else {
              <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{
                img.icon
              }}</mat-icon>
            }
          </div>
          <span class="dc-muted">{{
            (keys()[img.slot] ? 'branding.ownImage' : 'branding.inherited') | dcTranslate
          }}</span>
          @if (canEdit()) {
            <div class="dc-ab__image-actions">
              <input
                #file
                type="file"
                [accept]="accept"
                hidden
                (change)="upload(img.slot, file)"
                [attr.data-testid]="'app-file-' + img.key"
              />
              <button matButton="outlined" type="button" (click)="file.click()" [disabled]="busy()">
                <mat-icon fontSet="material-symbols-outlined">upload</mat-icon>
                {{ 'branding.upload' | dcTranslate }}
              </button>
              @if (keys()[img.slot]) {
                <button
                  matButton
                  type="button"
                  (click)="setKey(img.slot, null)"
                  [disabled]="busy()"
                >
                  {{ 'branding.inherit' | dcTranslate }}
                </button>
              }
            </div>
          }
        </section>
      }
    </div>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .dc-ab__head {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
    }
    .dc-ab__head h2 {
      margin: 0;
    }
    .dc-ab__actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .dc-ab__titles {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr));
      gap: 12px;
      margin: 12px 0;
    }
    .dc-ab__color {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .dc-ab__swatch {
      width: 40px;
      height: 40px;
      padding: 0;
      border: 1px solid var(--dga-gray-300);
      border-radius: 8px;
      background: none;
    }
    .dc-ab__hex {
      width: 8rem;
    }
    .dc-ab__preset {
      width: 24px;
      height: 24px;
      border: 2px solid #fff;
      border-radius: 50%;
      box-shadow: 0 0 0 1px var(--dga-gray-300);
      cursor: pointer;
    }
    .dc-ab__preset--on {
      box-shadow: 0 0 0 2px var(--dga-gray-900);
    }
    .dc-ab__bad {
      color: var(--dga-red-700, #b42318);
    }
    .dc-ab__preview {
      display: flex;
      gap: 8px;
      margin-top: 12px;
    }
    .dc-ab__button {
      padding: 6px 14px;
      border-radius: 8px;
      background: var(--p);
      color: #fff;
    }
    .dc-ab__chip {
      padding: 6px 14px;
      border-radius: 8px;
      background: var(--p50);
      color: var(--p700);
    }
    .dc-ab__images {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr));
      gap: 12px;
    }
    .dc-ab__image {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .dc-ab__image h2 {
      margin: 0;
      font-size: 16px;
    }
    .dc-ab__frame {
      display: grid;
      place-items: center;
      height: 96px;
      border: 1px dashed var(--dga-gray-300);
      border-radius: 8px;
      background: var(--dga-gray-50);
      color: var(--dga-gray-400);
    }
    .dc-ab__frame--wide {
      height: 120px;
    }
    .dc-ab__frame img {
      max-width: 100%;
      max-height: 100%;
      object-fit: contain;
    }
    .dc-ab__faded {
      opacity: 0.45;
    }
    .dc-ab__image-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
  `,
})
export class AppBrandingEditor {
  private readonly api = inject(AppsControlApi);
  protected readonly drafts = inject(DraftsService);

  readonly target = input.required<BrandingTarget>();
  readonly canEdit = input(false);
  readonly saved = output<void>();

  protected readonly accept = ACCEPT;
  protected readonly images = IMAGES;
  protected readonly presets = PRESETS;
  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly titleAr = signal('');
  protected readonly titleEn = signal('');
  protected readonly color = signal<string | null>(null);
  protected readonly keys = signal<Record<Slot, string | null>>({
    logoFileKey: null,
    footerImageKey: null,
    loginImageKey: null,
    heroImageKey: null,
  });

  private readonly rows = rxResource({
    stream: () => this.api.list('apps.app-branding').pipe(catchError(() => of([] as ConfigRow[]))),
  });
  protected readonly row = computed(
    () => (this.rows.value() ?? []).find((r) => r.code === this.target().code) ?? null,
  );

  /** What the target shows when it sets nothing: the channel's look (for an app) or the organization's. */
  private readonly parent = rxResource({
    params: () => this.target(),
    stream: ({ params }) =>
      this.api
        .branding(params.channel ? params.code : 'INTERNAL', null)
        .pipe(catchError(() => of(null))),
  });
  protected readonly inherited = computed(
    () => (this.target().channel ? this.organizationLook() : this.parent.value()) ?? null,
  );
  private readonly organization = rxResource({
    stream: () => this.api.branding(null, null).pipe(catchError(() => of(null))),
  });
  private readonly organizationLook = computed(() => this.organization.value() ?? null);
  protected readonly inheritedColor = computed(() => {
    const look = this.inherited();
    if (!look) return null;
    return this.target().code === 'EXTERNAL'
      ? (look.portalThemeColor ?? '#1b8354')
      : (look.internalThemeColor ?? '#ca9f45');
  });
  protected readonly shownColor = computed(() => {
    const own = this.color();
    return own && HEX.test(own) ? own : (this.inheritedColor() ?? '#ca9f45');
  });
  protected readonly badColor = computed(() => !!this.color() && !HEX.test(this.color()!));

  constructor() {
    // The form starts from the stored look of the chosen target (or empty, inheriting everything).
    effect(() => {
      const target = this.target();
      const row = this.row();
      untracked(() => {
        this.errors.set([]);
        this.titleAr.set(row?.nameAr ?? target.name.ar);
        this.titleEn.set(row?.nameEn ?? target.name.en);
        this.color.set((row?.['themeColor'] as string | null) ?? null);
        this.keys.set({
          logoFileKey: (row?.['logoFileKey'] as string | null) ?? null,
          footerImageKey: (row?.['footerImageKey'] as string | null) ?? null,
          loginImageKey: (row?.['loginImageKey'] as string | null) ?? null,
          heroImageKey: (row?.['heroImageKey'] as string | null) ?? null,
        });
      });
    });
  }

  protected assetUrl(key: string): string {
    return `/api/apps-control/v1/branding/assets/${key}`;
  }

  protected setKey(slot: Slot, key: string | null): void {
    this.keys.update((k) => ({ ...k, [slot]: key }));
  }

  protected previewStyle(): Record<string, string> {
    const color = this.shownColor();
    const vars = themeVariables(color, 'internal');
    return {
      '--p': color,
      '--p50': vars['--dynamic-cube-gold-50'],
      '--p700': vars['--dynamic-cube-gold-700'],
    };
  }

  protected async upload(slot: Slot, input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.busy.set(true);
    this.errors.set([]);
    try {
      this.setKey(slot, (await firstValueFrom(this.api.uploadBrandingAsset(file))).key);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }

  protected async save(submit = false): Promise<void> {
    if (this.badColor()) return;
    this.busy.set(true);
    this.errors.set([]);
    try {
      const target = this.target();
      await this.drafts.addChange(
        'apps.app-branding',
        target.code,
        this.row() ? 'UPDATE' : 'CREATE',
        {
          nameAr: this.titleAr().trim() || target.name.ar,
          nameEn: this.titleEn().trim() || target.name.en,
          isActive: true,
          themeColor: this.color(),
          ...this.keys(),
        },
        { submit },
      );
      this.rows.reload();
      this.saved.emit();
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }

  /** Drops the target's own look: it shows its channel's (or the organization's) again. */
  protected async remove(): Promise<void> {
    this.busy.set(true);
    this.errors.set([]);
    try {
      await this.drafts.addChange('apps.app-branding', this.target().code, 'DELETE', null);
      this.rows.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }
}
