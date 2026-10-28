import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import {
  AppsControlApi,
  AuthImageSrc,
  ConfigRow,
  FooterLink,
  I18nService,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { payloadOf } from '../config/form-values';
import { DraftsService } from '../drafts.service';
import { PrivilegesService } from '../privileges.service';

const RESOURCE = 'apps-control.installation';
const ACCEPT = 'image/png,image/jpeg,image/webp,image/svg+xml';

type LinkForm = FormGroup<{
  labelAr: FormControl<string>;
  labelEn: FormControl<string>;
  url: FormControl<string>;
}>;

/** The entity's logo and footer links, shown by every app. Saving goes through the change set of the system area. */
@Component({
  selector: 'dc-branding-page',
  imports: [
    AuthImageSrc,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'branding.title' | dcTranslate }}</h1>
    </header>
    <p class="dc-muted">{{ 'branding.hint' | dcTranslate }}</p>
    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (error of errors(); track $index) {
          <li>{{ error }}</li>
        }
      </ul>
    }

    <mat-card appearance="outlined">
      <mat-card-header
        ><mat-card-title>{{ 'branding.logo' | dcTranslate }}</mat-card-title></mat-card-header
      >
      <mat-card-content class="dc-logo">
        <div class="dc-logo__preview" data-testid="logo-preview">
          @if (logoUrl(); as url) {
            <img [dcAuthSrc]="url" [alt]="'branding.logo' | dcTranslate" />
          } @else {
            <span class="dc-muted">{{ 'branding.noLogo' | dcTranslate }}</span>
          }
        </div>
        @if (canEdit()) {
          <div class="dc-logo__actions">
            <input
              #file
              type="file"
              [accept]="accept"
              hidden
              (change)="upload(file)"
              data-testid="logo-file"
            />
            <button matButton="outlined" type="button" (click)="file.click()" [disabled]="busy()">
              <mat-icon fontSet="material-symbols-outlined">upload</mat-icon>
              {{ 'branding.upload' | dcTranslate }}
            </button>
            @if (logoKey()) {
              <button
                matButton
                type="button"
                (click)="logoKey.set(null)"
                [disabled]="busy()"
                data-testid="logo-remove"
              >
                {{ 'branding.removeLogo' | dcTranslate }}
              </button>
            }
            <span class="dc-muted">{{ 'branding.logoHint' | dcTranslate }}</span>
          </div>
        }
      </mat-card-content>
    </mat-card>

    <mat-card appearance="outlined">
      <mat-card-header
        ><mat-card-title>{{
          'branding.footerLinks' | dcTranslate
        }}</mat-card-title></mat-card-header
      >
      <mat-card-content>
        @for (link of links.controls; track link; let i = $index) {
          <div class="dc-link-row" [formGroup]="link" [attr.data-link]="i">
            <div class="dc-field">
              <label class="dc-field-label" [for]="'link-ar-' + i">{{
                'branding.labelAr' | dcTranslate
              }}</label>
              <mat-form-field
                ><input matInput [id]="'link-ar-' + i" formControlName="labelAr"
              /></mat-form-field>
            </div>
            <div class="dc-field">
              <label class="dc-field-label" [for]="'link-en-' + i">{{
                'branding.labelEn' | dcTranslate
              }}</label>
              <mat-form-field
                ><input matInput [id]="'link-en-' + i" formControlName="labelEn"
              /></mat-form-field>
            </div>
            <div class="dc-field dc-field--url">
              <label class="dc-field-label" [for]="'link-url-' + i">{{
                'branding.url' | dcTranslate
              }}</label>
              <mat-form-field
                ><input
                  matInput
                  dir="ltr"
                  [id]="'link-url-' + i"
                  formControlName="url"
                  placeholder="https://"
              /></mat-form-field>
            </div>
            @if (canEdit()) {
              <button
                matIconButton
                type="button"
                (click)="links.removeAt(i)"
                [attr.aria-label]="'config.delete' | dcTranslate"
              >
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
      </mat-card-content>
    </mat-card>

    @if (canEdit()) {
      <div class="dc-actions">
        <button
          matButton="filled"
          type="button"
          (click)="save()"
          [disabled]="busy()"
          data-testid="save-branding"
        >
          {{ drafts.saveLabel('apps.entity') | dcTranslate }}
        </button>
      </div>
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    mat-card-content {
      padding-top: 1rem;
    }
    .dc-logo {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 1.5rem;
    }
    .dc-logo__preview {
      display: flex;
      align-items: center;
      justify-content: center;
      min-width: 12rem;
      height: 6rem;
      padding: 1rem;
      border: 1px dashed var(--dga-border);
      border-radius: 4px;
      background: #ffffff;
    }
    .dc-logo__preview img {
      max-height: 4rem;
      max-width: 16rem;
    }
    .dc-logo__actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.75rem;
    }
    .dc-link-row {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      gap: 0.75rem;
      margin-bottom: 1rem;
    }
    .dc-link-row .dc-field {
      flex: 1 1 12rem;
    }
    .dc-link-row .dc-field--url {
      flex: 2 1 18rem;
    }
    .dc-actions {
      display: flex;
      justify-content: center;
    }
  `,
})
export class BrandingPage {
  private readonly api = inject(AppsControlApi);
  protected readonly drafts = inject(DraftsService);
  private readonly i18n = inject(I18nService);
  private readonly privileges = inject(PrivilegesService);

  protected readonly accept = ACCEPT;
  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly logoKey = signal<string | null>(null);
  protected readonly links = new FormArray<LinkForm>([]);
  protected readonly canEdit = computed(() => this.privileges.can(RESOURCE, 'EDIT'));
  protected readonly logoUrl = computed(() => {
    const key = this.logoKey();
    return key ? `/api/apps-control/v1/branding/assets/${key}` : null;
  });

  private readonly entity = rxResource({ stream: () => this.api.get('apps.entity', 'ENTITY') });

  constructor() {
    effect(() => {
      const row = this.entity.hasValue() ? this.entity.value() : null;
      if (!row) {
        return;
      }
      this.logoKey.set((row['logoFileKey'] as string | null) ?? null);
      this.links.clear();
      for (const link of (row['footerLinks'] as FooterLink[] | undefined) ?? []) {
        this.addLink(link);
      }
    });
  }

  protected addLink(link?: FooterLink): void {
    this.links.push(
      new FormGroup({
        labelAr: new FormControl(link?.labelAr ?? '', {
          nonNullable: true,
          validators: Validators.required,
        }),
        labelEn: new FormControl(link?.labelEn ?? '', {
          nonNullable: true,
          validators: Validators.required,
        }),
        url: new FormControl(link?.url ?? '', {
          nonNullable: true,
          validators: Validators.required,
        }),
      }),
    );
  }

  protected async upload(input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    this.busy.set(true);
    this.errors.set([]);
    try {
      this.logoKey.set((await firstValueFrom(this.api.uploadBrandingAsset(file))).key);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }

  protected async save(): Promise<void> {
    this.links.markAllAsTouched();
    const row = this.entity.hasValue() ? (this.entity.value() as ConfigRow) : null;
    if (!row) {
      return;
    }
    if (this.links.invalid) {
      this.errors.set([this.i18n.translate('branding.linksInvalid')]);
      return;
    }
    this.busy.set(true);
    this.errors.set([]);
    try {
      await this.drafts.addChange('apps.entity', 'ENTITY', 'UPDATE', {
        ...payloadOf(row),
        logoFileKey: this.logoKey(),
        footerLinks: this.links.getRawValue(),
      });
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }
}
