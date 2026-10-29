import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { rxResource, takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ChannelPreview, CommunicationApi, LocalizePipe, MessageTemplateWording, RichTextEditor, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { catchError, debounceTime, filter, of, switchMap } from 'rxjs';
import { ConfigEditorData } from '../apps-control/config/config-editor';
import { DraftsService } from '../apps-control/drafts.service';

type WordingKey = keyof MessageTemplateWording;

const KEYS: WordingKey[] = [
  'emailSubjectAr',
  'emailSubjectEn',
  'emailBodyAr',
  'emailBodyEn',
  'smsAr',
  'smsEn',
  'inAppTitleAr',
  'inAppTitleEn',
  'inAppBodyAr',
  'inAppBodyEn',
  'link',
];

/** Dotted paths of a sample's values (`request.number`, `party.nameAr`…), the fields a template may use. */
export function samplePaths(value: unknown, prefix = ''): string[] {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return prefix ? [prefix] : [];
  }
  return Object.entries(value as Record<string, unknown>).flatMap(([key, child]) => samplePaths(child, prefix ? `${prefix}.${key}` : key));
}

/**
 * Message template editor: the wording of each channel in Arabic and English (Liquid), previewed on the sample facts of an
 * event type, with the fields that event offers. A channel is offered to rules once all of its fields are filled in.
 */
@Component({
  selector: 'dc-message-template-editor',
  imports: [
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTabsModule,
    RichTextEditor,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ 'msgTemplates.editor' | dcTranslate }}
      @if (data.row) {
        <span dir="ltr">{{ data.row.code }}</span>
      }
    </h2>
    <mat-dialog-content class="dc-msg">
      <div class="dc-msg__form">
        <div class="dc-msg__row">
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'config.code' | dcTranslate }}</mat-label>
            <input matInput dir="ltr" [ngModel]="code()" (ngModelChange)="code.set($event)" [readonly]="!!data.row" required data-testid="msg-code" />
          </mat-form-field>
          <mat-checkbox [ngModel]="active()" (ngModelChange)="active.set($event)">{{ 'config.active' | dcTranslate }}</mat-checkbox>
        </div>
        <div class="dc-msg__row">
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'config.nameAr' | dcTranslate }}</mat-label>
            <input matInput [ngModel]="nameAr()" (ngModelChange)="nameAr.set($event)" required data-testid="msg-name-ar" />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'config.nameEn' | dcTranslate }}</mat-label>
            <input matInput dir="ltr" [ngModel]="nameEn()" (ngModelChange)="nameEn.set($event)" required data-testid="msg-name-en" />
          </mat-form-field>
        </div>
        <mat-tab-group mat-stretch-tabs="false" animationDuration="0ms">
          <mat-tab [label]="'channel.IN_APP' | dcTranslate">
            <div class="dc-msg__fields">
              @for (key of inAppKeys; track key) {
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ 'msgTemplates.' + key | dcTranslate }}</mat-label>
                  <textarea
                    matInput
                    [rows]="key.includes('Body') ? 3 : 1"
                    [attr.dir]="key.endsWith('En') || key === 'link' ? 'ltr' : 'rtl'"
                    [ngModel]="value(key)"
                    (ngModelChange)="set(key, $event)"
                    [attr.data-testid]="'msg-' + key"
                  ></textarea>
                </mat-form-field>
              }
            </div>
          </mat-tab>
          <mat-tab [label]="'channel.EMAIL' | dcTranslate">
            <div class="dc-msg__fields">
              @for (key of emailKeys; track key) {
                @if (key.includes('Body')) {
                  <!-- The body is written in a rich text editor; it is stored as HTML. -->
                  <dc-rich-text-editor
                    [label]="'msgTemplates.' + key | dcTranslate"
                    [dir]="key.endsWith('En') ? 'ltr' : 'rtl'"
                    [value]="value(key)"
                    (valueChange)="set(key, $event)"
                    [fields]="paths()"
                    [minRows]="7"
                    [testId]="'msg-' + key"
                  />
                } @else {
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'msgTemplates.' + key | dcTranslate }}</mat-label>
                    <input
                      matInput
                      [attr.dir]="key.endsWith('En') ? 'ltr' : 'rtl'"
                      [ngModel]="value(key)"
                      (ngModelChange)="set(key, $event)"
                      [attr.data-testid]="'msg-' + key"
                    />
                  </mat-form-field>
                }
              }
            </div>
          </mat-tab>
          <mat-tab [label]="'channel.SMS' | dcTranslate">
            <div class="dc-msg__fields">
              @for (key of smsKeys; track key) {
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ 'msgTemplates.' + key | dcTranslate }}</mat-label>
                  <textarea
                    matInput
                    rows="3"
                    [attr.dir]="key.endsWith('En') ? 'ltr' : 'rtl'"
                    [ngModel]="value(key)"
                    (ngModelChange)="set(key, $event)"
                    [attr.data-testid]="'msg-' + key"
                  ></textarea>
                  <mat-hint align="end">{{ (value(key) ?? '').length }}</mat-hint>
                </mat-form-field>
              }
            </div>
          </mat-tab>
        </mat-tab-group>
      </div>

      <div class="dc-msg__side">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'msgTemplates.sampleEvent' | dcTranslate }}</mat-label>
          <mat-select [ngModel]="event()" (ngModelChange)="event.set($event)" data-testid="msg-event">
            @for (e of events.value() ?? []; track e.code) {
              <mat-option [value]="e.code">{{ { ar: e.nameAr, en: e.nameEn } | dcLocalize }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <div class="dc-msg__preview-bar">
          <strong>{{ 'msgTemplates.preview' | dcTranslate }}</strong>
          <mat-button-toggle-group [ngModel]="language()" (ngModelChange)="language.set($event)" hideSingleSelectionIndicator>
            <mat-button-toggle value="ar">العربية</mat-button-toggle>
            <mat-button-toggle value="en">English</mat-button-toggle>
          </mat-button-toggle-group>
        </div>
        @if (previewError()) {
          <p class="dc-errors" role="alert" dir="ltr">{{ previewError() }}</p>
        }
        @for (p of previews(); track p.channel) {
          <section class="dc-msg__card" [attr.data-testid]="'preview-' + p.channel">
            <span class="dc-badge dc-badge--info">{{ 'channel.' + p.channel | dcTranslate }}</span>
            @if (p.channel === 'EMAIL') {
              <p class="dc-msg__subject" [attr.dir]="language() === 'ar' ? 'rtl' : 'ltr'">{{ language() === 'ar' ? p.titleAr : p.titleEn }}</p>
              <iframe class="dc-msg__frame" sandbox="" [srcdoc]="emailHtml(p)" [title]="'channel.EMAIL' | dcTranslate"></iframe>
            } @else {
              @if (p.titleAr || p.titleEn) {
                <p class="dc-msg__subject" [attr.dir]="language() === 'ar' ? 'rtl' : 'ltr'">{{ language() === 'ar' ? p.titleAr : p.titleEn }}</p>
              }
              <p class="dc-msg__body" [attr.dir]="language() === 'ar' ? 'rtl' : 'ltr'">{{ language() === 'ar' ? p.bodyAr : p.bodyEn }}</p>
            }
          </section>
        } @empty {
          <p class="dc-muted">{{ 'msgTemplates.noChannel' | dcTranslate }}</p>
        }
        <details class="dc-msg__vars">
          <summary>{{ 'msgTemplates.fields' | dcTranslate }}</summary>
          <ul dir="ltr">
            @for (path of paths(); track path) {
              <li>
                <code>{{ liquid(path) }}</code>
              </li>
            }
          </ul>
        </details>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions>
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li>{{ e }}</li>
          }
        </ul>
      }
      <button matButton type="button" mat-dialog-close>{{ 'form.cancel' | dcTranslate }}</button>
      <button matButton="filled" type="button" [disabled]="!valid()" (click)="save()" data-testid="msg-save">
        {{ drafts.saveLabel(data.type.entityType) | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-msg {
      display: grid;
      grid-template-columns: minmax(0, 3fr) minmax(0, 2fr);
      gap: 1.5rem;
      max-height: 78vh;
    }
    @media (max-width: 1100px) {
      .dc-msg {
        grid-template-columns: 1fr;
      }
    }
    .dc-msg__form,
    .dc-msg__side {
      display: grid;
      gap: 0.75rem;
      align-content: start;
    }
    .dc-msg__row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.75rem;
    }
    .dc-msg__row mat-form-field {
      flex: 1;
      min-width: 10rem;
    }
    .dc-msg__fields {
      display: grid;
      gap: 0.75rem;
      padding-top: 0.75rem;
    }
    .dc-msg__preview-bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .dc-msg__card {
      display: grid;
      gap: 0.5rem;
      justify-items: start;
      padding: 0.75rem 1rem;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 12px;
      background: #ffffff;
    }
    .dc-msg__subject {
      margin: 0;
      font-weight: 600;
      width: 100%;
    }
    .dc-msg__body {
      margin: 0;
      white-space: pre-wrap;
      width: 100%;
    }
    .dc-msg__frame {
      width: 100%;
      height: 14rem;
      border: 1px solid var(--dga-gray-100, #f3f4f6);
      border-radius: 8px;
    }
    .dc-msg__vars ul {
      max-height: 14rem;
      overflow: auto;
      margin: 0.5rem 0 0;
      padding-inline-start: 1.25rem;
      font-size: 13px;
    }
  `,
})
export class MessageTemplateEditor {
  protected readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<MessageTemplateEditor, boolean>);
  private readonly api = inject(CommunicationApi);
  private readonly sanitizer = inject(DomSanitizer);
  protected readonly drafts = inject(DraftsService);

  protected readonly inAppKeys: WordingKey[] = ['inAppTitleAr', 'inAppTitleEn', 'inAppBodyAr', 'inAppBodyEn', 'link'];
  protected readonly emailKeys: WordingKey[] = ['emailSubjectAr', 'emailSubjectEn', 'emailBodyAr', 'emailBodyEn'];
  protected readonly smsKeys: WordingKey[] = ['smsAr', 'smsEn'];
  protected readonly code = signal(this.data.row?.code ?? '');
  protected readonly nameAr = signal(this.data.row?.nameAr ?? '');
  protected readonly nameEn = signal(this.data.row?.nameEn ?? '');
  protected readonly active = signal(this.data.row?.isActive ?? true);
  protected readonly wording = signal<MessageTemplateWording>(Object.fromEntries(KEYS.map((k) => [k, (this.data.row?.[k] as string | null) ?? null])));
  protected readonly events = rxResource({ stream: () => this.api.events() });
  protected readonly event = signal<string | null>(null);
  protected readonly language = signal<'ar' | 'en'>('ar');
  protected readonly previews = signal<ChannelPreview[]>([]);
  protected readonly previewError = signal<string | null>(null);
  protected readonly errors = signal<string[]>([]);
  protected readonly valid = computed(() => !!this.code().trim() && !!this.nameAr().trim() && !!this.nameEn().trim());
  private readonly eventCode = computed(() => this.event() ?? this.events.value()?.[0]?.code ?? null);
  protected readonly paths = computed(() => samplePaths(this.events.value()?.find((e) => e.code === this.eventCode())?.sample ?? {}));

  constructor() {
    toObservable(computed(() => ({ event: this.eventCode(), wording: this.wording() })))
      .pipe(
        filter((r) => !!r.event),
        debounceTime(400),
        switchMap((r) =>
          this.api.preview(r.event!, r.wording).pipe(
            catchError((error: unknown) => {
              this.previewError.set(problemMessages(error).join(' '));
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((previews) => {
        if (previews) {
          this.previewError.set(null);
          this.previews.set(previews);
        }
      });
  }

  protected liquid(path: string): string {
    return `{{ ${path} }}`;
  }

  protected value(key: WordingKey): string | null {
    return this.wording()[key] ?? null;
  }

  protected set(key: WordingKey, value: string): void {
    this.wording.update((w) => ({ ...w, [key]: value }));
  }

  protected emailHtml(preview: ChannelPreview): SafeHtml {
    // Shown in a frame without scripts.
    return this.sanitizer.bypassSecurityTrustHtml(this.language() === 'ar' ? preview.bodyAr : preview.bodyEn);
  }

  protected async save(): Promise<void> {
    this.errors.set([]);
    const wording = Object.fromEntries(KEYS.map((k) => [k, this.wording()[k]?.trim() ? this.wording()[k] : null]));
    try {
      await this.drafts.addChange(this.data.type.entityType, this.code().trim(), this.data.row ? 'UPDATE' : 'CREATE', {
        nameAr: this.nameAr().trim(),
        nameEn: this.nameEn().trim(),
        isActive: this.active(),
        ...wording,
      });
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}
