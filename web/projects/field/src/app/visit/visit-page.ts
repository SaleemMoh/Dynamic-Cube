import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { I18nService, LocalizePipe, TranslatePipe, VisitDetail, VisitField, VisitFields, localize, missingVisitFields } from '@dynamic-cube/shared';
import { FieldStore, ReportDraft } from '../field-store';
import { CapturedPhoto, PhotoCapture } from './photo-capture';
import { SignaturePad } from './signature-pad';

interface Position {
  latitude: number;
  longitude: number;
  accuracy: number;
}

/**
 * One visit in the field: where and what to check, then the report: the outcome, the type's fields, photos, the
 * inspector's position and the site representative's signature. The report is kept on the device as it is filled in
 * and sent when there is a connection.
 */
@Component({
  selector: 'dc-field-visit',
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    RouterLink,
    LocalizePipe,
    TranslatePipe,
    VisitFields,
    PhotoCapture,
    SignaturePad,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a matButton routerLink="/" class="dc-fv__back">
      <mat-icon fontSet="material-symbols-outlined">{{ i18n.direction() === 'rtl' ? 'arrow_forward' : 'arrow_back' }}</mat-icon>
      {{ 'field.myVisits' | dcTranslate }}
    </a>
    @if (visit(); as v) {
      <section class="dc-fv__card">
        <div class="dc-fv__head">
          <h1>{{ { ar: v.type.nameAr, en: v.type.nameEn } | dcLocalize }}</h1>
          <span class="dc-fv__number" dir="ltr">{{ v.visit.number }}</span>
        </div>
        <dl class="dc-fv__facts">
          <dt>{{ 'field.party' | dcTranslate }}</dt>
          <dd>{{ v.visit.partyName ?? '—' }}</dd>
          <dt>{{ 'field.when' | dcTranslate }}</dt>
          <dd dir="ltr">{{ v.visit.scheduledAt | date: 'EEE yyyy-MM-dd HH:mm' }}</dd>
          <dt>{{ 'field.site' | dcTranslate }}</dt>
          <dd>{{ v.visit.siteAddress ?? '—' }}</dd>
          @if (v.visit.subjectNumber) {
            <dt>{{ 'field.request' | dcTranslate }}</dt>
            <dd dir="ltr">{{ v.visit.subjectNumber }}</dd>
          }
        </dl>
        @if (v.instructions) {
          <p class="dc-fv__instructions" dir="auto">{{ v.instructions }}</p>
        }
        <div class="dc-fv__actions">
          @if (v.latitude !== null && v.longitude !== null) {
            <a matButton="outlined" [href]="directions(v)" target="_blank" rel="noopener">
              <mat-icon fontSet="material-symbols-outlined">directions</mat-icon>
              {{ 'field.directions' | dcTranslate }}
            </a>
          }
          @if (v.visit.status === 'SCHEDULED' && !pending()) {
            <button matButton="filled" type="button" (click)="begin(v)" data-testid="field-start">
              <mat-icon fontSet="material-symbols-outlined">play_arrow</mat-icon>
              {{ 'field.start' | dcTranslate }}
            </button>
          }
        </div>
      </section>

      @if (pending(); as p) {
        <section class="dc-fv__card" [class.dc-fv__card--refused]="p.error" role="status" data-testid="field-pending">
          @if (p.error) {
            <strong>{{ 'field.refused' | dcTranslate }}</strong>
            <ul class="dc-errors">
              @for (e of p.error; track $index) {
                <li>{{ e }}</li>
              }
            </ul>
            <button matButton="outlined" type="button" (click)="edit(p.report)" data-testid="field-edit">{{ 'field.editReport' | dcTranslate }}</button>
          } @else {
            <strong>{{ 'field.queued' | dcTranslate }}</strong>
            <span class="dc-fv__muted">{{ (store.online() ? 'field.sending' : 'field.willSend') | dcTranslate }}</span>
          }
        </section>
      } @else if (v.visit.status === 'COMPLETED') {
        <section class="dc-fv__card" data-testid="field-completed">
          <strong>{{ 'field.completed' | dcTranslate }}</strong>
          <span
            >{{ outcomeName(v, v.visit.outcome) }} · <bdi dir="ltr">{{ v.visit.completedAt | date: 'yyyy-MM-dd HH:mm' }}</bdi></span
          >
        </section>
      } @else if (v.visit.status === 'SCHEDULED' || v.visit.status === 'IN_PROGRESS') {
        <section class="dc-fv__card dc-fv__form">
          <h2>{{ 'field.report' | dcTranslate }}</h2>
          <span class="dc-fv__label">{{ 'field.outcome' | dcTranslate }} *</span>
          <mat-button-toggle-group class="dc-fv__outcomes" [ngModel]="outcome()" (ngModelChange)="outcome.set($event)" data-testid="field-outcome">
            @for (o of v.type.outcomes; track o.code) {
              <mat-button-toggle [value]="o.code" [attr.data-kind]="o.kind" [attr.data-testid]="'field-outcome-' + o.code">{{
                { ar: o.nameAr, en: o.nameEn } | dcLocalize
              }}</mat-button-toggle>
            }
          </mat-button-toggle-group>

          <dc-visit-fields [fields]="v.type.fields" [value]="data()" (valueChange)="data.set($event)" />

          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'field.notes' | dcTranslate }}</mat-label>
            <textarea matInput rows="3" dir="auto" [ngModel]="report()" (ngModelChange)="report.set($event)" data-testid="field-report"></textarea>
          </mat-form-field>

          <span class="dc-fv__label">
            {{ 'field.photos' | dcTranslate }}
            @if (v.type.photosRequired) {
              ({{ 'field.atLeast' | dcTranslate }} {{ v.type.photosRequired }}) *
            }
          </span>
          <dc-photo-capture [photos]="photos()" (photosChange)="photos.set($event)" />

          <span class="dc-fv__label"
            >{{ 'field.location' | dcTranslate }}
            @if (v.type.requireLocation) {
              *
            }
          </span>
          <div class="dc-fv__location" data-testid="field-location">
            @if (position(); as p) {
              <mat-icon fontSet="material-symbols-outlined">my_location</mat-icon>
              <span dir="ltr">{{ p.latitude | number: '1.5-5' }}, {{ p.longitude | number: '1.5-5' }} (±{{ p.accuracy | number: '1.0-0' }} m)</span>
            } @else if (locating()) {
              <span>{{ 'field.locating' | dcTranslate }}</span>
            } @else {
              <span class="dc-fv__muted">{{ locationError() ?? 'field.noLocation' | dcTranslate }}</span>
            }
            <button matButton type="button" (click)="locate()" [disabled]="locating()">{{ 'field.locate' | dcTranslate }}</button>
          </div>

          @if (v.type.requireSignature) {
            <span class="dc-fv__label">{{ 'field.signature' | dcTranslate }} *</span>
            <dc-signature-pad [value]="signature()" (valueChange)="signature.set($event)" />
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'field.signedBy' | dcTranslate }}</mat-label>
              <input matInput dir="auto" [ngModel]="signedBy()" (ngModelChange)="signedBy.set($event)" data-testid="field-signed-by" />
            </mat-form-field>
          }

          @if (missing().length) {
            <ul class="dc-fv__missing" data-testid="field-missing">
              @for (m of missing(); track m) {
                <li>{{ m }}</li>
              }
            </ul>
          }
          <button
            matButton="filled"
            type="button"
            class="dc-fv__submit"
            [disabled]="missing().length > 0 || saving()"
            (click)="submit(v)"
            data-testid="field-submit"
          >
            <mat-icon fontSet="material-symbols-outlined">send</mat-icon>
            {{ 'field.submit' | dcTranslate }}
          </button>
        </section>
      }
    } @else if (store.loaded()) {
      <p class="dc-errors" role="alert">{{ 'field.notFound' | dcTranslate }}</p>
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 12px;
      max-width: 40rem;
      margin: 0 auto;
      padding: 8px 16px 48px;
    }
    .dc-fv__back {
      align-self: flex-start;
    }
    .dc-fv__card {
      display: grid;
      gap: 10px;
      padding: 16px;
      border: 1px solid var(--dga-border, #d2d6db);
      border-radius: 12px;
      background: #fff;
    }
    .dc-fv__card--refused {
      border-color: #fda29b;
      background: #fef3f2;
    }
    .dc-fv__head {
      display: flex;
      align-items: baseline;
      gap: 8px;
    }
    .dc-fv__head h1 {
      flex: 1;
      margin: 0;
      font-size: 20px;
    }
    .dc-fv__number {
      color: var(--dga-text-3, #384250);
      font-size: 13px;
    }
    .dc-fv__facts {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 6px 12px;
      margin: 0;
    }
    .dc-fv__facts dt {
      color: var(--dga-text-3, #384250);
    }
    .dc-fv__facts dd {
      margin: 0;
      font-weight: 600;
    }
    .dc-fv__instructions {
      margin: 0;
      padding: 8px 12px;
      border-radius: 8px;
      background: #fffaeb;
    }
    .dc-fv__actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .dc-fv__actions > * {
      flex: 1;
      min-height: 48px;
    }
    .dc-fv__form h2 {
      margin: 0;
      font-size: 18px;
    }
    .dc-fv__label {
      font-weight: 600;
      font-size: 14px;
    }
    .dc-fv__outcomes {
      display: flex;
      flex-wrap: wrap;
    }
    .dc-fv__outcomes mat-button-toggle {
      flex: 1;
      min-height: 48px;
    }
    .dc-fv__location {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-wrap: wrap;
    }
    .dc-fv__muted {
      color: var(--dga-text-3, #384250);
    }
    .dc-fv__missing {
      margin: 0;
      padding-inline-start: 20px;
      color: #b54708;
      font-size: 14px;
    }
    .dc-fv__submit {
      min-height: 52px;
      font-size: 16px;
    }
  `,
})
export class FieldVisitPage {
  readonly id = input.required<string>();
  protected readonly store = inject(FieldStore);
  protected readonly i18n = inject(I18nService);
  private readonly router = inject(Router);

  protected readonly visit = computed(() => this.store.visit(this.id()));
  protected readonly pending = computed(() => this.store.pending(this.id()));

  protected readonly outcome = signal<string | null>(null);
  protected readonly data = signal<Record<string, unknown>>({});
  protected readonly report = signal('');
  protected readonly photos = signal<CapturedPhoto[]>([]);
  protected readonly signature = signal<string | null>(null);
  protected readonly signedBy = signal('');
  protected readonly position = signal<Position | null>(null);
  protected readonly locating = signal(false);
  protected readonly locationError = signal<string | null>(null);
  protected readonly saving = signal(false);
  private restoring = false;
  /** The saved draft is back in the form (until then nothing is saved over it). */
  private readonly restored = signal(false);

  /** What still stops the report from being sent, in words. */
  protected readonly missing = computed(() => {
    const v = this.visit();
    if (!v) return [];
    const t = (key: string) => this.i18n.translate(key);
    const list: string[] = [];
    if (!this.outcome()) list.push(t('field.missing.outcome'));
    for (const code of missingVisitFields(v.type.fields, this.data())) {
      const f = v.type.fields.find((x) => x.code === code) as VisitField;
      list.push(localize({ ar: f.labelAr, en: f.labelEn }, this.i18n.language()));
    }
    if (this.photos().length < v.type.photosRequired) list.push(t('field.missing.photos').replace('{count}', String(v.type.photosRequired)));
    if (v.type.requireLocation && !this.position()) list.push(t('field.missing.location'));
    if (v.type.requireSignature && (!this.signature() || !this.signedBy().trim())) list.push(t('field.missing.signature'));
    return list;
  });

  constructor() {
    void this.store.start();
    // Restore what was filled in before, once the visit is known; then keep the draft as it changes.
    effect(() => {
      const v = this.visit();
      if (!v || this.restoring) return;
      this.restoring = true;
      untracked(() => void this.restore(v));
    });
    effect(() => {
      const draft: ReportDraft = {
        outcome: this.outcome(),
        data: this.data(),
        report: this.report(),
        photos: this.photos(),
        signature: this.signature(),
        signedBy: this.signedBy(),
      };
      if (this.restored() && untracked(() => this.visit())) void this.store.saveDraft(this.id(), draft);
    });
  }

  protected directions(v: VisitDetail): string {
    return `https://www.google.com/maps/dir/?api=1&destination=${v.latitude},${v.longitude}`;
  }

  protected outcomeName(v: VisitDetail, code: string | null): string {
    const o = v.type.outcomes.find((x) => x.code === code);
    return o ? localize({ ar: o.nameAr, en: o.nameEn }, this.i18n.language()) : (code ?? '');
  }

  protected async begin(v: VisitDetail): Promise<void> {
    await this.store.begin(v.visit.id);
    this.locate();
  }

  protected locate(): void {
    if (!('geolocation' in navigator)) {
      this.locationError.set('field.noGeolocation');
      return;
    }
    this.locating.set(true);
    this.locationError.set(null);
    navigator.geolocation.getCurrentPosition(
      (p) => {
        this.position.set({ latitude: p.coords.latitude, longitude: p.coords.longitude, accuracy: p.coords.accuracy });
        this.locating.set(false);
      },
      (e) => {
        this.locationError.set(e.code === e.PERMISSION_DENIED ? 'field.locationDenied' : 'field.locationFailed');
        this.locating.set(false);
      },
      { enableHighAccuracy: true, timeout: 20_000, maximumAge: 60_000 },
    );
  }

  /** A refused report back in the form, to correct and send again. */
  protected edit(report: {
    outcome: string;
    data: Record<string, unknown>;
    report: string | null;
    photos: CapturedPhoto[];
    signature: string | null;
    signedBy: string | null;
  }): void {
    const item = this.pending();
    this.outcome.set(report.outcome);
    this.data.set(report.data);
    this.report.set(report.report ?? '');
    this.photos.set(report.photos);
    this.signature.set(report.signature);
    this.signedBy.set(report.signedBy ?? '');
    if (item) void this.store.discard(item);
  }

  protected async submit(v: VisitDetail): Promise<void> {
    this.saving.set(true);
    try {
      const p = this.position();
      await this.store.submit(v.visit.id, {
        submissionId: crypto.randomUUID(),
        outcome: this.outcome()!,
        data: this.data(),
        report: this.report().trim() || null,
        photos: this.photos(),
        signature: v.type.requireSignature ? this.signature() : null,
        signedBy: v.type.requireSignature ? this.signedBy().trim() : null,
        latitude: p?.latitude ?? null,
        longitude: p?.longitude ?? null,
        accuracy: p?.accuracy ?? null,
        completedAt: new Date().toISOString(),
      });
      await this.router.navigateByUrl('/');
    } finally {
      this.saving.set(false);
    }
  }

  private async restore(v: VisitDetail): Promise<void> {
    const draft = await this.store.draft(v.visit.id);
    if (draft) {
      this.outcome.set(draft.outcome);
      this.data.set(draft.data);
      this.report.set(draft.report);
      this.photos.set(draft.photos as CapturedPhoto[]);
      this.signature.set(draft.signature);
      this.signedBy.set(draft.signedBy);
    }
    this.restored.set(true);
    if (v.visit.status === 'IN_PROGRESS' && v.type.requireLocation) this.locate();
  }
}
