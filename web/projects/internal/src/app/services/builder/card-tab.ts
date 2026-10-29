import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { I18nService, LocalizePipe, ServiceCardDefinition, ServiceChannel, TranslatePipe } from '@dynamic-cube/shared';
import { BuilderStore } from './builder-store';
import { move } from './builder-model';

/** What the portal's service page says before the service is started, with a preview of it. */
@Component({
  selector: 'dc-builder-card-tab',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule, MatTooltipModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-ct">
      <div class="dc-ct__edit">
        <section class="dc-card">
          <h2>{{ 'builder.card.about' | dcTranslate }}</h2>
          <div class="dc-ct__pair">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'builder.card.audienceAr' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="rtl"
                [ngModel]="card().audienceAr ?? ''"
                (ngModelChange)="set({ audienceAr: $event || null })"
                [ngModelOptions]="{ updateOn: 'blur' }"
                data-testid="card-audience-ar"
              />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'builder.card.audienceEn' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="card().audienceEn ?? ''"
                (ngModelChange)="set({ audienceEn: $event || null })"
                [ngModelOptions]="{ updateOn: 'blur' }"
              />
            </mat-form-field>
          </div>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'builder.card.channels' | dcTranslate }}</mat-label>
            <mat-select multiple [ngModel]="card().channels" (ngModelChange)="set({ channels: $event })">
              @for (c of channels; track c) {
                <mat-option [value]="c">{{ 'builder.channel.' + c | dcTranslate }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <div class="dc-ct__pair">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'builder.card.phone' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="card().phone ?? ''"
                (ngModelChange)="set({ phone: $event || null })"
                [ngModelOptions]="{ updateOn: 'blur' }"
              />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'builder.card.email' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="card().email ?? ''"
                (ngModelChange)="set({ email: $event || null })"
                [ngModelOptions]="{ updateOn: 'blur' }"
              />
            </mat-form-field>
          </div>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'builder.card.faq' | dcTranslate }}</mat-label>
            <input
              matInput
              dir="ltr"
              [ngModel]="card().faqUrl ?? ''"
              (ngModelChange)="set({ faqUrl: $event || null })"
              [ngModelOptions]="{ updateOn: 'blur' }"
            />
          </mat-form-field>
        </section>

        <section class="dc-card">
          <header class="dc-ct__head">
            <h2>{{ 'builder.card.steps' | dcTranslate }}</h2>
            <button matButton type="button" (click)="set({ steps: [...card().steps, { textAr: '', textEn: '' }] })" data-testid="card-add-step">
              <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
              {{ 'builder.add' | dcTranslate }}
            </button>
          </header>
          @for (s of card().steps; track $index; let i = $index; let first = $first; let last = $last) {
            <div class="dc-ct__row">
              <strong>{{ i + 1 }}</strong>
              <mat-form-field subscriptSizing="dynamic">
                <input
                  matInput
                  dir="rtl"
                  [placeholder]="'builder.textAr' | dcTranslate"
                  [ngModel]="s.textAr"
                  (ngModelChange)="step(i, { textAr: $event })"
                  [ngModelOptions]="{ updateOn: 'blur' }"
                />
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <input
                  matInput
                  dir="ltr"
                  [placeholder]="'builder.textEn' | dcTranslate"
                  [ngModel]="s.textEn"
                  (ngModelChange)="step(i, { textEn: $event })"
                  [ngModelOptions]="{ updateOn: 'blur' }"
                />
              </mat-form-field>
              <button matIconButton type="button" [disabled]="first" (click)="set({ steps: reorder(card().steps, i, -1) })">
                <mat-icon fontSet="material-symbols-outlined">arrow_upward</mat-icon>
              </button>
              <button matIconButton type="button" [disabled]="last" (click)="set({ steps: reorder(card().steps, i, 1) })">
                <mat-icon fontSet="material-symbols-outlined">arrow_downward</mat-icon>
              </button>
              <button matIconButton type="button" (click)="set({ steps: without(card().steps, i) })" [matTooltip]="'rule.remove' | dcTranslate">
                <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
              </button>
            </div>
          }
        </section>

        <section class="dc-card">
          <header class="dc-ct__head">
            <h2>{{ 'builder.card.documents' | dcTranslate }}</h2>
            <button
              matButton
              type="button"
              (click)="set({ requiredDocuments: [...card().requiredDocuments, { nameAr: '', nameEn: '', noteAr: null, noteEn: null }] })"
              data-testid="card-add-document"
            >
              <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
              {{ 'builder.add' | dcTranslate }}
            </button>
          </header>
          <p class="dc-ct__muted">{{ 'builder.card.documentsHint' | dcTranslate }}</p>
          @for (d of card().requiredDocuments; track $index; let i = $index) {
            <div class="dc-ct__doc">
              <div class="dc-ct__pair">
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ 'builder.nameAr' | dcTranslate }}</mat-label>
                  <input matInput dir="rtl" [ngModel]="d.nameAr" (ngModelChange)="document(i, { nameAr: $event })" [ngModelOptions]="{ updateOn: 'blur' }" />
                </mat-form-field>
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ 'builder.nameEn' | dcTranslate }}</mat-label>
                  <input matInput dir="ltr" [ngModel]="d.nameEn" (ngModelChange)="document(i, { nameEn: $event })" [ngModelOptions]="{ updateOn: 'blur' }" />
                </mat-form-field>
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ 'builder.card.noteAr' | dcTranslate }}</mat-label>
                  <input
                    matInput
                    dir="rtl"
                    [ngModel]="d.noteAr ?? ''"
                    (ngModelChange)="document(i, { noteAr: $event || null })"
                    [ngModelOptions]="{ updateOn: 'blur' }"
                  />
                </mat-form-field>
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ 'builder.card.noteEn' | dcTranslate }}</mat-label>
                  <input
                    matInput
                    dir="ltr"
                    [ngModel]="d.noteEn ?? ''"
                    (ngModelChange)="document(i, { noteEn: $event || null })"
                    [ngModelOptions]="{ updateOn: 'blur' }"
                  />
                </mat-form-field>
              </div>
              <button matButton type="button" class="dc-ct__danger" (click)="set({ requiredDocuments: without(card().requiredDocuments, i) })">
                {{ 'rule.remove' | dcTranslate }}
              </button>
            </div>
          }
        </section>

        <section class="dc-card">
          <h2>{{ 'builder.card.terms' | dcTranslate }}</h2>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'builder.textAr' | dcTranslate }}</mat-label>
            <textarea
              matInput
              dir="rtl"
              rows="4"
              [ngModel]="card().termsAr ?? ''"
              (ngModelChange)="set({ termsAr: $event || null })"
              [ngModelOptions]="{ updateOn: 'blur' }"
            ></textarea>
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'builder.textEn' | dcTranslate }}</mat-label>
            <textarea
              matInput
              dir="ltr"
              rows="4"
              [ngModel]="card().termsEn ?? ''"
              (ngModelChange)="set({ termsEn: $event || null })"
              [ngModelOptions]="{ updateOn: 'blur' }"
            ></textarea>
          </mat-form-field>
        </section>
      </div>

      <aside class="dc-ct__preview" [attr.aria-label]="'builder.card.preview' | dcTranslate" data-testid="card-preview">
        <p class="dc-ct__muted">{{ 'builder.card.preview' | dcTranslate }}</p>
        <div class="dc-ct__page">
          <div class="dc-ct__facts">
            <div>
              <span>{{ 'builder.card.duration' | dcTranslate }}</span>
              <strong>{{ duration() }}</strong>
            </div>
            <div>
              <span>{{ 'builder.card.audience' | dcTranslate }}</span>
              <strong>{{ { ar: card().audienceAr ?? '—', en: card().audienceEn ?? '—' } | dcLocalize }}</strong>
            </div>
            <div>
              <span>{{ 'builder.card.channels' | dcTranslate }}</span>
              <strong>{{ channelNames() }}</strong>
            </div>
          </div>
          @if (card().steps.length) {
            <h3>{{ 'builder.card.steps' | dcTranslate }}</h3>
            <ol>
              @for (s of card().steps; track $index) {
                <li>{{ { ar: s.textAr, en: s.textEn } | dcLocalize }}</li>
              }
            </ol>
          }
          @if (card().requiredDocuments.length) {
            <h3>{{ 'builder.card.documents' | dcTranslate }}</h3>
            <ul>
              @for (d of card().requiredDocuments; track $index) {
                <li>
                  {{ { ar: d.nameAr, en: d.nameEn } | dcLocalize }}
                  @if (d.noteAr || d.noteEn) {
                    <small> — {{ { ar: d.noteAr ?? '', en: d.noteEn ?? '' } | dcLocalize }}</small>
                  }
                </li>
              }
            </ul>
          }
          @if (card().termsAr || card().termsEn) {
            <h3>{{ 'builder.card.terms' | dcTranslate }}</h3>
            <p class="dc-ct__terms">{{ { ar: card().termsAr ?? '', en: card().termsEn ?? '' } | dcLocalize }}</p>
          }
          @if (card().phone || card().email) {
            <p class="dc-ct__muted" dir="ltr">{{ card().phone }} {{ card().email }}</p>
          }
        </div>
      </aside>
    </div>
  `,
  styles: `
    :host {
      display: block;
      min-height: 0;
      height: 100%;
    }
    .dc-ct {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 24rem;
      height: 100%;
      min-height: 0;
    }
    .dc-ct__edit {
      display: grid;
      align-content: start;
      gap: 1rem;
      padding: 0.75rem;
      overflow: auto;
    }
    .dc-ct__edit mat-form-field {
      width: 100%;
    }
    .dc-ct h2 {
      margin: 0 0 0.5rem;
      font-size: 16px;
    }
    .dc-ct__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .dc-ct__pair {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
      gap: 0.5rem;
    }
    .dc-ct__row {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      margin-bottom: 0.25rem;
    }
    .dc-ct__row mat-form-field {
      flex: 1;
    }
    .dc-ct__doc {
      padding: 0.5rem;
      margin-bottom: 0.5rem;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 8px;
    }
    .dc-ct__danger {
      color: #b42318;
    }
    .dc-ct__muted {
      margin: 0 0 0.5rem;
      color: var(--dga-text-3, #6c737f);
      font-size: 12px;
    }
    .dc-ct__preview {
      padding: 0.75rem;
      overflow: auto;
      border-inline-start: 1px solid var(--dga-gray-200, #e5e7eb);
      background: var(--dga-gray-25, #fcfcfd);
    }
    .dc-ct__page {
      padding: 1rem;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 12px;
      background: #fff;
    }
    .dc-ct__page h3 {
      margin: 1rem 0 0.25rem;
      font-size: 15px;
      color: #136f40;
    }
    .dc-ct__facts {
      display: grid;
      gap: 0.5rem;
    }
    .dc-ct__facts div {
      display: grid;
    }
    .dc-ct__facts span {
      color: var(--dga-text-3, #6c737f);
      font-size: 12px;
    }
    .dc-ct__terms {
      white-space: pre-line;
    }
  `,
})
export class CardTab {
  private readonly store = inject(BuilderStore);
  private readonly i18n = inject(I18nService);
  protected readonly channels: ServiceChannel[] = ['PORTAL', 'MOBILE_APP', 'BRANCH', 'CALL_CENTER'];
  protected readonly card = computed(() => this.store.definition().card);

  /** As the portal shows it: the fastest and slowest service levels. */
  protected readonly duration = computed(() => {
    const days = this.store
      .definition()
      .levels.map((l) => l.slaWorkingDays)
      .filter((d): d is number => !!d);
    if (!days.length) return '—';
    const min = Math.min(...days);
    const max = Math.max(...days);
    const unit = this.i18n.translate('builder.card.workingDays');
    return min === max ? `${min} ${unit}` : `${min}–${max} ${unit}`;
  });

  protected readonly channelNames = computed(() =>
    this.card()
      .channels.map((c) => this.i18n.translate('builder.channel.' + c))
      .join('، '),
  );

  protected set(change: Partial<ServiceCardDefinition>): void {
    this.store.apply((def) => ({ ...def, card: { ...def.card, ...change } }));
  }

  protected step(index: number, change: Partial<{ textAr: string; textEn: string }>): void {
    this.set({ steps: this.card().steps.map((s, i) => (i === index ? { ...s, ...change } : s)) });
  }

  protected document(index: number, change: Partial<ServiceCardDefinition['requiredDocuments'][number]>): void {
    this.set({ requiredDocuments: this.card().requiredDocuments.map((d, i) => (i === index ? { ...d, ...change } : d)) });
  }

  protected reorder<T>(list: T[], index: number, by: number): T[] {
    return move(list, index, index + by);
  }

  protected without<T>(list: T[], index: number): T[] {
    return list.filter((_, i) => i !== index);
  }
}
