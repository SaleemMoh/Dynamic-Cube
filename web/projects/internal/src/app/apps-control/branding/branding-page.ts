import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSelectModule } from '@angular/material/select';
import {
  AppsControlApi,
  AuthImageSrc,
  ConfigRow,
  EntityContact,
  FooterLink,
  I18nService,
  LocalizePipe,
  SocialLink,
  TranslatePipe,
  problemMessages,
  themeVariables,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { ExportButton } from '../change-sets/export-button';
import { AppBrandingEditor, BrandingTarget } from './app-branding';
import { JsonImportButton } from '../change-sets/json-import-button';
import { payloadOf } from '../config/form-values';
import { DraftsService } from '../drafts.service';
import { PrivilegesService } from '../privileges.service';
import { SubmitButton } from '../submit-button';

const RESOURCE = 'apps-control.installation';
const ACCEPT = 'image/png,image/jpeg,image/webp,image/svg+xml';
const HEX = /^#[0-9a-f]{6}$/i;

type LinkForm = FormGroup<{
  labelAr: FormControl<string>;
  labelEn: FormControl<string>;
  url: FormControl<string>;
}>;

/** The images the entity uploads: its field in the entity row and what the card says. */
type ImageSlot = 'logoFileKey' | 'footerImageKey' | 'loginImageKey' | 'portalHeroImageKey';
const IMAGES: { slot: ImageSlot; key: string; icon: string; wide?: boolean }[] = [
  { slot: 'logoFileKey', key: 'logo', icon: 'image' },
  { slot: 'footerImageKey', key: 'footerImage', icon: 'branding_watermark' },
  { slot: 'loginImageKey', key: 'loginImage', icon: 'wallpaper', wide: true },
  { slot: 'portalHeroImageKey', key: 'heroImage', icon: 'panorama', wide: true },
];

/** Social networks the portal shows icons for. */
const NETWORKS = ['X', 'LINKEDIN', 'INSTAGRAM', 'YOUTUBE', 'FACEBOOK', 'SNAPCHAT', 'TIKTOK', 'WHATSAPP'];

type SocialForm = FormGroup<{ network: FormControl<string>; url: FormControl<string> }>;

type ThemeField = 'internalThemeColor' | 'portalThemeColor';
const THEMES: { field: ThemeField; key: string; kind: 'internal' | 'portal'; fallback: string }[] = [
  { field: 'internalThemeColor', key: 'internal', kind: 'internal', fallback: '#ca9f45' },
  { field: 'portalThemeColor', key: 'portal', kind: 'portal', fallback: '#1b8354' },
];
const PRESETS = ['#ca9f45', '#1b8354', '#136f40', '#0e7490', '#1d4ed8', '#4338ca', '#7e22ce', '#9f1239', '#b45309', '#374151'];

/**
 * The entity's look in every app: logo, theme colours of the internal system and the portal, the footer image, the
 * image beside the sign-in form and the footer links. Saving goes through the change set of the system area; the
 * whole entity can also be imported as JSON.
 */
@Component({
  selector: 'dc-branding-page',
  imports: [
    SubmitButton,
    AuthImageSrc,
    FormsModule,
    AppBrandingEditor,
    ExportButton,
    JsonImportButton,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header dc-page__header--actions">
      <div>
        <h1>{{ 'branding.title' | dcTranslate }}</h1>
        <p>{{ 'branding.hint' | dcTranslate }}</p>
      </div>
      <div>
        @if (target() === organization) {
        <dc-export-button entityType="apps.entity" [title]="{ ar: 'الهوية والشعار', en: 'Branding' }" />
        @if (canEdit()) {
          <dc-json-import-button
            [title]="{ ar: 'الهوية والشعار', en: 'Branding' }"
            [target]="{ entityType: 'apps.entity', key: { singleton: 'ENTITY' } }"
            [example]="example()"
            (imported)="entity.reload()"
          />
        }
        @if (canEdit()) {
          <dc-submit-button [entityType]="'apps.entity'" [disabled]="busy()" (submitted)="save(true)" />
          <button matButton="filled" type="button" (click)="save()" [disabled]="busy()" data-testid="save-branding">
            {{ drafts.saveLabel('apps.entity') | dcTranslate }}
          </button>
        }
        }
      </div>
    </header>
    <section class="dc-card dc-targets" data-testid="branding-targets">
      <mat-form-field subscriptSizing="dynamic" class="dc-targets__pick">
        <mat-label>{{ 'branding.target' | dcTranslate }}</mat-label>
        <mat-select [value]="target()" (selectionChange)="target.set($event.value)" data-testid="branding-target">
          <mat-option [value]="organization">{{ 'branding.organization' | dcTranslate }}</mat-option>
          <mat-optgroup [label]="'menus.channels' | dcTranslate">
            @for (t of channelTargets(); track t.code) {
              <mat-option [value]="t.code">{{ t.name | dcLocalize }}</mat-option>
            }
          </mat-optgroup>
          @if (appTargets().length) {
            <mat-optgroup [label]="'branding.apps' | dcTranslate">
              @for (t of appTargets(); track t.code) {
                <mat-option [value]="t.code">{{ t.name | dcLocalize }}</mat-option>
              }
            </mat-optgroup>
          }
        </mat-select>
      </mat-form-field>
      <p class="dc-muted">{{ 'branding.targetHint' | dcTranslate }}</p>
    </section>
    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (error of errors(); track $index) {
          <li>{{ error }}</li>
        }
      </ul>
    }

    @if (target() !== organization && currentTarget(); as t) {
      <dc-app-branding [target]="t" [canEdit]="canEdit()" />
    } @else {
    <section class="dc-card">
      <h2>{{ 'branding.colors' | dcTranslate }}</h2>
      <p class="dc-muted">{{ 'branding.colorsHint' | dcTranslate }}</p>
      <div class="dc-themes">
        @for (t of themes; track t.field) {
          <div class="dc-theme" [attr.data-testid]="'theme-' + t.key">
            <strong>{{ 'branding.theme.' + t.key | dcTranslate }}</strong>
            <div class="dc-theme__pick">
              <input
                type="color"
                class="dc-theme__swatch"
                [value]="colorOf(t)"
                (input)="setColor(t.field, $any($event.target).value)"
                [disabled]="!canEdit()"
                [attr.aria-label]="'branding.theme.' + t.key | dcTranslate"
              />
              <mat-form-field subscriptSizing="dynamic" class="dc-theme__hex">
                <input
                  matInput
                  dir="ltr"
                  maxlength="7"
                  [placeholder]="t.fallback"
                  [ngModel]="colors()[t.field] ?? ''"
                  (ngModelChange)="setColor(t.field, $event)"
                  [disabled]="!canEdit()"
                  [attr.data-testid]="'theme-hex-' + t.key"
                />
              </mat-form-field>
              @if (colors()[t.field]) {
                <button matButton type="button" (click)="setColor(t.field, null)" [disabled]="!canEdit()">{{ 'branding.resetColor' | dcTranslate }}</button>
              }
            </div>
            <div class="dc-theme__presets">
              @for (p of presets; track p) {
                <button
                  type="button"
                  class="dc-theme__preset"
                  [style.background]="p"
                  [class.dc-theme__preset--on]="colorOf(t) === p"
                  (click)="setColor(t.field, p)"
                  [disabled]="!canEdit()"
                  [matTooltip]="p"
                  [attr.aria-label]="p"
                ></button>
              }
            </div>
            @if (badColor(t.field)) {
              <span class="dc-theme__bad">{{ 'branding.badColor' | dcTranslate }}</span>
            }
            <div class="dc-theme__preview" [style]="previewStyle(t)">
              <span class="dc-theme__button">{{ 'branding.previewButton' | dcTranslate }}</span>
              <span class="dc-theme__chip">{{ 'branding.previewSelected' | dcTranslate }}</span>
              <span class="dc-theme__link">{{ 'branding.previewLink' | dcTranslate }}</span>
            </div>
          </div>
        }
      </div>
    </section>

    <div class="dc-images">
      @for (img of images; track img.slot) {
        <section class="dc-card dc-image" [attr.data-testid]="'image-' + img.key">
          <h2>{{ 'branding.' + img.key | dcTranslate }}</h2>
          <p class="dc-muted">{{ 'branding.' + img.key + 'Hint' | dcTranslate }}</p>
          <div class="dc-image__preview" [class.dc-image__preview--wide]="img.wide">
            @if (urlOf(img.slot); as url) {
              <img [dcAuthSrc]="url" [alt]="'branding.' + img.key | dcTranslate" />
            } @else {
              <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{ img.icon }}</mat-icon>
              <span class="dc-muted">{{ 'branding.noImage' | dcTranslate }}</span>
            }
          </div>
          @if (canEdit()) {
            <div class="dc-image__actions">
              <input #file type="file" [accept]="accept" hidden (change)="upload(img.slot, file)" [attr.data-testid]="'file-' + img.key" />
              <button matButton="outlined" type="button" (click)="file.click()" [disabled]="busy()">
                <mat-icon fontSet="material-symbols-outlined">upload</mat-icon>
                {{ 'branding.upload' | dcTranslate }}
              </button>
              @if (keys()[img.slot]) {
                <button matButton type="button" (click)="setKey(img.slot, null)" [disabled]="busy()" [attr.data-testid]="'remove-' + img.key">
                  {{ 'branding.removeImage' | dcTranslate }}
                </button>
              }
            </div>
          }
        </section>
      }
    </div>

    <section class="dc-card" [formGroup]="contact" data-testid="branding-contact">
      <h2>{{ 'branding.contact' | dcTranslate }}</h2>
      <p class="dc-muted">{{ 'branding.contactHint' | dcTranslate }}</p>
      <div class="dc-contact">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'branding.phone' | dcTranslate }}</mat-label>
          <input matInput dir="ltr" formControlName="phone" data-testid="contact-phone" />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'branding.email' | dcTranslate }}</mat-label>
          <input matInput dir="ltr" formControlName="email" data-testid="contact-email" />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'branding.addressAr' | dcTranslate }}</mat-label>
          <input matInput formControlName="addressAr" />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'branding.addressEn' | dcTranslate }}</mat-label>
          <input matInput formControlName="addressEn" />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic" class="dc-contact__wide">
          <mat-label>{{ 'branding.mapUrl' | dcTranslate }}</mat-label>
          <input matInput dir="ltr" formControlName="mapUrl" placeholder="https://" />
        </mat-form-field>
      </div>
      <h3>{{ 'branding.social' | dcTranslate }}</h3>
      @for (link of social.controls; track link; let i = $index) {
        <div class="dc-link-row" [formGroup]="link" [attr.data-social]="i">
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'branding.network' | dcTranslate }}</mat-label>
            <mat-select formControlName="network">
              @for (n of networks; track n) {
                <mat-option [value]="n">{{ 'branding.network.' + n | dcTranslate }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic" class="dc-link-row__url">
            <mat-label>{{ 'branding.url' | dcTranslate }}</mat-label>
            <input matInput dir="ltr" formControlName="url" placeholder="https://" />
          </mat-form-field>
          @if (canEdit()) {
            <button matIconButton type="button" (click)="social.removeAt(i)" [attr.aria-label]="'config.delete' | dcTranslate">
              <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
            </button>
          }
        </div>
      }
      @if (canEdit()) {
        <button matButton type="button" (click)="addSocial()" data-testid="add-social">
          <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
          {{ 'branding.addSocial' | dcTranslate }}
        </button>
      }
    </section>

    <section class="dc-card">
      <h2>{{ 'branding.footerLinks' | dcTranslate }}</h2>
      @for (link of links.controls; track link; let i = $index) {
        <div class="dc-link-row" [formGroup]="link" [attr.data-link]="i">
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'branding.labelAr' | dcTranslate }}</mat-label>
            <input matInput formControlName="labelAr" />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'branding.labelEn' | dcTranslate }}</mat-label>
            <input matInput formControlName="labelEn" />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic" class="dc-link-row__url">
            <mat-label>{{ 'branding.url' | dcTranslate }}</mat-label>
            <input matInput dir="ltr" formControlName="url" placeholder="https://" />
          </mat-form-field>
          @if (canEdit()) {
            <button matIconButton type="button" (click)="links.removeAt(i)" [attr.aria-label]="'config.delete' | dcTranslate">
              <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
            </button>
          }
        </div>
      } @empty {
        <p class="dc-muted">{{ 'branding.noLinks' | dcTranslate }}</p>
      }
      @if (canEdit()) {
        <button matButton type="button" (click)="addLink()" data-testid="add-link">
          <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
          {{ 'branding.addLink' | dcTranslate }}
        </button>
      }
    </section>
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .dc-targets {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px 16px;
    }
    .dc-targets__pick {
      min-width: 18rem;
    }
    .dc-targets p {
      flex: 1 1 20rem;
      margin: 0;
    }
    .dc-card > p.dc-muted {
      margin: -6px 0 12px;
      font-size: 13px;
    }
    .dc-themes {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(22rem, 1fr));
      gap: 16px;
    }
    .dc-theme {
      display: grid;
      gap: 10px;
      padding: 12px;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius);
    }
    .dc-theme__pick {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .dc-theme__swatch {
      width: 40px;
      height: 40px;
      padding: 2px;
      border: 1px solid var(--dga-gray-300);
      border-radius: var(--shell-radius);
      background: #fff;
      cursor: pointer;
    }
    .dc-theme__hex {
      width: 9rem;
    }
    .dc-theme__presets {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }
    .dc-theme__preset {
      width: 24px;
      height: 24px;
      border: 2px solid #fff;
      border-radius: 50%;
      box-shadow: 0 0 0 1px var(--dga-gray-300);
      cursor: pointer;
    }
    .dc-theme__preset--on {
      box-shadow: 0 0 0 2px var(--dga-gray-900);
    }
    .dc-theme__bad {
      color: #b42318;
      font-size: 13px;
    }
    .dc-theme__preview {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 10px;
      padding: 10px;
      border-radius: var(--shell-radius);
      background: var(--dga-gray-50);
    }
    .dc-theme__button {
      padding: 6px 14px;
      border-radius: var(--shell-radius);
      background: var(--p);
      color: #fff;
      font-weight: 600;
      font-size: 13px;
    }
    .dc-theme__chip {
      padding: 4px 10px;
      border-inline-start: 3px solid var(--p);
      border-radius: 4px;
      background: var(--p50);
      color: var(--p700);
      font-size: 13px;
      font-weight: 600;
    }
    .dc-theme__link {
      color: var(--p700);
      font-weight: 600;
      font-size: 13px;
      text-decoration: underline;
    }
    .dc-contact {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr));
      gap: 12px;
    }
    .dc-contact__wide {
      grid-column: 1 / -1;
    }
    .dc-card h3 {
      margin: 16px 0 8px;
      font-size: 15px;
    }
    .dc-images {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(18rem, 1fr));
      gap: 12px;
    }
    .dc-image {
      display: flex;
      flex-direction: column;
    }
    .dc-image__preview {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 4px;
      height: 7rem;
      margin-bottom: 10px;
      padding: 10px;
      border: 1px dashed var(--dga-gray-300);
      border-radius: var(--shell-radius);
      background: repeating-conic-gradient(#f3f4f6 0% 25%, #fff 0% 50%) 50% / 16px 16px;
      color: var(--dga-gray-400);
    }
    .dc-image__preview img {
      max-width: 100%;
      max-height: 100%;
      object-fit: contain;
    }
    .dc-image__preview--wide img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      border-radius: 4px;
    }
    .dc-image__actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-top: auto;
    }
    .dc-link-row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 10px;
      margin-bottom: 10px;
    }
    .dc-link-row mat-form-field {
      flex: 1 1 12rem;
    }
    .dc-link-row .dc-link-row__url {
      flex: 2 1 18rem;
    }
  `,
})
export class BrandingPage {
  private readonly api = inject(AppsControlApi);
  protected readonly drafts = inject(DraftsService);
  private readonly i18n = inject(I18nService);
  private readonly privileges = inject(PrivilegesService);

  protected readonly accept = ACCEPT;
  protected readonly images = IMAGES;
  protected readonly themes = THEMES;
  protected readonly presets = PRESETS;
  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly keys = signal<Record<ImageSlot, string | null>>({ logoFileKey: null, footerImageKey: null, loginImageKey: null, portalHeroImageKey: null });
  protected readonly networks = NETWORKS;
  protected readonly contact = new FormGroup({
    phone: new FormControl('', { nonNullable: true }),
    email: new FormControl('', { nonNullable: true }),
    addressAr: new FormControl('', { nonNullable: true }),
    addressEn: new FormControl('', { nonNullable: true }),
    mapUrl: new FormControl('', { nonNullable: true }),
  });
  protected readonly social = new FormArray<SocialForm>([]);
  protected readonly colors = signal<Record<ThemeField, string | null>>({ internalThemeColor: null, portalThemeColor: null });
  protected readonly links = new FormArray<LinkForm>([]);
  protected readonly canEdit = computed(() => this.privileges.can(RESOURCE, 'EDIT'));

  /** Whose look is edited: the organization (the default of every app) or a channel or app with its own. */
  protected readonly organization = 'ORGANIZATION';
  protected readonly target = signal(this.organization);
  private readonly channelRows = rxResource({ stream: () => this.api.list('apps.app') });
  private readonly appRows = rxResource({ stream: () => this.api.list('apps.business-app') });
  protected readonly channelTargets = computed<BrandingTarget[]>(() =>
    (this.channelRows.value() ?? []).map((r) => ({ code: r.code, name: { ar: r.nameAr, en: r.nameEn }, channel: true })),
  );
  protected readonly appTargets = computed<BrandingTarget[]>(() =>
    (this.appRows.value() ?? [])
      .filter((r) => r['kind'] !== 'CONNECTED')
      .map((r) => ({ code: r.code, name: { ar: r.nameAr, en: r.nameEn }, channel: false })),
  );
  protected readonly currentTarget = computed(() => [...this.channelTargets(), ...this.appTargets()].find((t) => t.code === this.target()) ?? null);

  protected readonly entity = rxResource({ stream: () => this.api.get('apps.entity', 'ENTITY') });
  /** The entity as it is, offered as the example of the JSON to import. */
  protected readonly example = computed(() => {
    const row = this.entity.hasValue() ? this.entity.value() : null;
    return row ? payloadOf(row) : undefined;
  });

  constructor() {
    effect(() => {
      const row = this.entity.hasValue() ? this.entity.value() : null;
      if (!row) {
        return;
      }
      this.keys.set({
        logoFileKey: (row['logoFileKey'] as string | null) ?? null,
        footerImageKey: (row['footerImageKey'] as string | null) ?? null,
        loginImageKey: (row['loginImageKey'] as string | null) ?? null,
        portalHeroImageKey: (row['portalHeroImageKey'] as string | null) ?? null,
      });
      const contact = (row['contact'] as EntityContact | null) ?? null;
      this.contact.reset({
        phone: contact?.phone ?? '',
        email: contact?.email ?? '',
        addressAr: contact?.addressAr ?? '',
        addressEn: contact?.addressEn ?? '',
        mapUrl: contact?.mapUrl ?? '',
      });
      this.social.clear();
      for (const link of contact?.social ?? []) {
        this.addSocial(link);
      }
      this.colors.set({
        internalThemeColor: (row['internalThemeColor'] as string | null) ?? null,
        portalThemeColor: (row['portalThemeColor'] as string | null) ?? null,
      });
      this.links.clear();
      for (const link of (row['footerLinks'] as FooterLink[] | undefined) ?? []) {
        this.addLink(link);
      }
    });
  }

  protected urlOf(slot: ImageSlot): string | null {
    const key = this.keys()[slot];
    return key ? `/api/apps-control/v1/branding/assets/${key}` : null;
  }

  protected setKey(slot: ImageSlot, key: string | null): void {
    this.keys.update((k) => ({ ...k, [slot]: key }));
  }

  protected colorOf(theme: (typeof THEMES)[number]): string {
    const value = this.colors()[theme.field];
    return value && HEX.test(value) ? value.toLowerCase() : theme.fallback;
  }

  protected setColor(field: ThemeField, value: string | null): void {
    const v = value?.trim() ?? '';
    this.colors.update((c) => ({ ...c, [field]: v ? v.toLowerCase() : null }));
  }

  protected badColor(field: ThemeField): boolean {
    const value = this.colors()[field];
    return !!value && !HEX.test(value);
  }

  /** The preview's colours, from the same derivation the app uses. */
  protected previewStyle(theme: (typeof THEMES)[number]): Record<string, string> {
    const color = this.colorOf(theme);
    const vars = themeVariables(color, 'internal');
    return { '--p': color, '--p50': vars['--dynamic-cube-gold-50'], '--p700': vars['--dynamic-cube-gold-700'] };
  }

  protected addLink(link?: FooterLink): void {
    this.links.push(
      new FormGroup({
        labelAr: new FormControl(link?.labelAr ?? '', { nonNullable: true, validators: Validators.required }),
        labelEn: new FormControl(link?.labelEn ?? '', { nonNullable: true, validators: Validators.required }),
        url: new FormControl(link?.url ?? '', { nonNullable: true, validators: Validators.required }),
      }),
    );
  }

  protected addSocial(link?: SocialLink): void {
    this.social.push(
      new FormGroup({
        network: new FormControl(link?.network ?? 'X', { nonNullable: true }),
        url: new FormControl(link?.url ?? '', { nonNullable: true, validators: Validators.required }),
      }),
    );
  }

  protected async upload(slot: ImageSlot, input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
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
    this.links.markAllAsTouched();
    const row = this.entity.hasValue() ? (this.entity.value() as ConfigRow) : null;
    if (!row) {
      return;
    }
    if (this.links.invalid) {
      this.errors.set([this.i18n.translate('branding.linksInvalid')]);
      return;
    }
    if (THEMES.some((t) => this.badColor(t.field))) {
      this.errors.set([this.i18n.translate('branding.badColor')]);
      return;
    }
    this.busy.set(true);
    this.errors.set([]);
    try {
      await this.drafts.addChange('apps.entity', 'ENTITY', 'UPDATE', {
        ...payloadOf(row),
        ...this.keys(),
        ...this.colors(),
        footerLinks: this.links.getRawValue(),
        contact: { ...this.contact.getRawValue(), social: this.social.getRawValue() },
      }, { submit });
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }
}
