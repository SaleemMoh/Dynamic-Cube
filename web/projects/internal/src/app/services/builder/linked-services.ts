import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  AppsControlApi,
  ConfigRow,
  I18nService,
  LocalizePipe,
  TranslatePipe,
  TranslationDictionary,
} from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';
import { BundleDef, PrerequisiteDef, ServiceDraft, freeCode } from './builder-model';
import { BuilderStore } from './builder-store';

const REQUEST_STATUSES = [
  'APPROVED',
  'SUBMITTED',
  'IN_PROGRESS',
  'PENDING_CUSTOMER',
  'PENDING_EXTERNAL',
  'PENDING_PAYMENT',
  'REJECTED',
  'CLOSED',
];
const DOCUMENT_STATUSES = ['ACTIVE', 'EXPIRED', 'SUSPENDED'];

const LINKED_TRANSLATIONS: TranslationDictionary = {
  ar: {
    'ls.prerequisites': 'المتطلبات السابقة',
    'ls.prerequisitesHint':
      'ما يجب أن يكون لدى مقدم الطلب قبل أن يبدأ أو يقدم: طلب لخدمة أخرى بحالة معينة، أو وثيقة سارية. إن لم يتحقق رأى الرسالة ورابطًا للخدمة التي تحققه.',
    'ls.addPrerequisite': 'متطلب سابق',
    'ls.code': 'الرمز',
    'ls.kind': 'يتطلب',
    'ls.kind.REQUEST': 'طلبًا لخدمة',
    'ls.kind.DOCUMENT': 'وثيقة',
    'ls.service': 'الخدمة',
    'ls.documentType': 'نوع الوثيقة',
    'ls.statuses': 'بإحدى الحالات',
    'ls.statusesDefaultRequest': 'فارغ: معتمد',
    'ls.statusesDefaultDocument': 'فارغ: سارية',
    'ls.withinDays': 'خلال آخر (أيام)',
    'ls.validForDays': 'سارية لمدة لا تقل عن (أيام)',
    'ls.startService': 'خدمة يبدأها لتحقيقه',
    'ls.startServiceHint': 'فارغ: الخدمة المطلوبة نفسها',
    'ls.none': '—',
    'ls.messageAr': 'الرسالة بالعربية',
    'ls.messageEn': 'الرسالة بالإنجليزية',
    'ls.remove': 'حذف',
    'ls.bundle': 'الخدمات المرافقة',
    'ls.bundleHint':
      'خدمات تُطلب مع هذه الخدمة عند تقديمها، لمقدم الطلب نفسه وبالإجابات المشتركة: الإلزامية دائمًا، والاختيارية إذا اختارها. تُقدَّم تلقائيًا إذا اكتملت إجاباتها، وإلا بقيت مسودة ليكملها.',
    'ls.addBundle': 'خدمة مرافقة',
    'ls.required': 'إلزامية',
    'ls.noteAr': 'ملاحظة بالعربية',
    'ls.noteEn': 'ملاحظة بالإنجليزية',
    'ls.reqStatus.APPROVED': 'معتمد',
    'ls.reqStatus.SUBMITTED': 'مقدم',
    'ls.reqStatus.IN_PROGRESS': 'قيد المعالجة',
    'ls.reqStatus.PENDING_CUSTOMER': 'بانتظار مقدم الطلب',
    'ls.reqStatus.PENDING_EXTERNAL': 'بانتظار جهة أخرى',
    'ls.reqStatus.PENDING_PAYMENT': 'بانتظار السداد',
    'ls.reqStatus.REJECTED': 'مرفوض',
    'ls.reqStatus.CLOSED': 'مغلق',
    'ls.docStatus.ACTIVE': 'سارية',
    'ls.docStatus.EXPIRED': 'منتهية',
    'ls.docStatus.SUSPENDED': 'موقوفة',
  },
  en: {
    'ls.prerequisites': 'Prerequisites',
    'ls.prerequisitesHint':
      'What the applicant must already have before starting or submitting: a request of another service in some status, or a valid document. When one is missing they see its message and a link to the service that meets it.',
    'ls.addPrerequisite': 'Prerequisite',
    'ls.code': 'Code',
    'ls.kind': 'Needs',
    'ls.kind.REQUEST': 'A request of a service',
    'ls.kind.DOCUMENT': 'A document',
    'ls.service': 'Service',
    'ls.documentType': 'Document type',
    'ls.statuses': 'In one of these statuses',
    'ls.statusesDefaultRequest': 'Empty: approved',
    'ls.statusesDefaultDocument': 'Empty: active',
    'ls.withinDays': 'Within the last (days)',
    'ls.validForDays': 'Valid for at least (days)',
    'ls.startService': 'Service to start to meet it',
    'ls.startServiceHint': 'Empty: the service needed itself',
    'ls.none': '—',
    'ls.messageAr': 'Arabic message',
    'ls.messageEn': 'English message',
    'ls.remove': 'Remove',
    'ls.bundle': 'Bundled services',
    'ls.bundleHint':
      'Services requested together with this one when it is submitted, for the same applicant and with the answers they share: the required ones always, the optional ones when the applicant ticks them. Each is submitted at once when its answers are complete, otherwise it waits as a draft for the applicant.',
    'ls.addBundle': 'Bundled service',
    'ls.required': 'Required',
    'ls.noteAr': 'Arabic note',
    'ls.noteEn': 'English note',
    'ls.reqStatus.APPROVED': 'Approved',
    'ls.reqStatus.SUBMITTED': 'Submitted',
    'ls.reqStatus.IN_PROGRESS': 'In progress',
    'ls.reqStatus.PENDING_CUSTOMER': 'With the applicant',
    'ls.reqStatus.PENDING_EXTERNAL': 'Waiting for another party',
    'ls.reqStatus.PENDING_PAYMENT': 'Awaiting payment',
    'ls.reqStatus.REJECTED': 'Rejected',
    'ls.reqStatus.CLOSED': 'Closed',
    'ls.docStatus.ACTIVE': 'Active',
    'ls.docStatus.EXPIRED': 'Expired',
    'ls.docStatus.SUSPENDED': 'Suspended',
  },
};

/**
 * The Rules tab's sub-requests (3f): prerequisites the applicant must meet (a request of a service in some status, or a
 * document of a type), and the services bundled with this one when it is submitted.
 */
@Component({
  selector: 'dc-linked-services',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
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
    <section class="dc-card" data-testid="builder-prerequisites">
      <header class="ls-head">
        <h2>{{ 'ls.prerequisites' | dcTranslate }}</h2>
        <button matButton type="button" (click)="addPrerequisite()" data-testid="add-prerequisite">
          <mat-icon fontSet="material-symbols-outlined">add</mat-icon
          >{{ 'ls.addPrerequisite' | dcTranslate }}
        </button>
      </header>
      <p class="ls-muted">{{ 'ls.prerequisitesHint' | dcTranslate }}</p>
      @for (x of prerequisites(); track $index; let i = $index) {
        <div class="ls-item" [attr.data-prerequisite]="i">
          <div class="ls-row">
            <mat-form-field subscriptSizing="dynamic" class="ls-code">
              <mat-label>{{ 'ls.code' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="x.code"
                (ngModelChange)="setPrerequisite(i, { code: $event.toUpperCase() })"
                [ngModelOptions]="{ updateOn: 'blur' }"
              />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'ls.kind' | dcTranslate }}</mat-label>
              <mat-select
                [ngModel]="x.kind"
                (ngModelChange)="setPrerequisite(i, { kind: $event, statuses: [] })"
              >
                <mat-option value="REQUEST">{{ 'ls.kind.REQUEST' | dcTranslate }}</mat-option>
                <mat-option value="DOCUMENT">{{ 'ls.kind.DOCUMENT' | dcTranslate }}</mat-option>
              </mat-select>
            </mat-form-field>
            @if (x.kind === 'REQUEST') {
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'ls.service' | dcTranslate }}</mat-label>
                <mat-select
                  [ngModel]="x.service ?? null"
                  (ngModelChange)="setPrerequisite(i, { service: $event })"
                  data-testid="prerequisite-service"
                >
                  @for (s of others(); track s.code) {
                    <mat-option [value]="s.code"
                      >{{ { ar: s.nameAr, en: s.nameEn } | dcLocalize }} ({{ s.code }})</mat-option
                    >
                  }
                </mat-select>
              </mat-form-field>
            } @else {
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'ls.documentType' | dcTranslate }}</mat-label>
                <mat-select
                  [ngModel]="x.documentType ?? null"
                  (ngModelChange)="setPrerequisite(i, { documentType: $event })"
                >
                  @for (d of documentTypes(); track d.code) {
                    <mat-option [value]="d.code"
                      >{{ { ar: d.nameAr, en: d.nameEn } | dcLocalize }} ({{ d.code }})</mat-option
                    >
                  }
                </mat-select>
              </mat-form-field>
            }
            <button
              matIconButton
              type="button"
              (click)="removePrerequisite(i)"
              [matTooltip]="'ls.remove' | dcTranslate"
            >
              <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
            </button>
          </div>
          <div class="ls-row">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'ls.statuses' | dcTranslate }}</mat-label>
              <mat-select
                multiple
                [ngModel]="x.statuses"
                (ngModelChange)="setPrerequisite(i, { statuses: $event })"
              >
                @for (s of x.kind === 'REQUEST' ? requestStatuses : documentStatuses; track s) {
                  <mat-option [value]="s">{{
                    (x.kind === 'REQUEST' ? 'ls.reqStatus.' : 'ls.docStatus.') + s | dcTranslate
                  }}</mat-option>
                }
              </mat-select>
              <mat-hint>{{
                (x.kind === 'REQUEST' ? 'ls.statusesDefaultRequest' : 'ls.statusesDefaultDocument')
                  | dcTranslate
              }}</mat-hint>
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic" class="ls-num">
              <mat-label>{{
                (x.kind === 'REQUEST' ? 'ls.withinDays' : 'ls.validForDays') | dcTranslate
              }}</mat-label>
              <input
                matInput
                type="number"
                min="0"
                [ngModel]="x.kind === 'REQUEST' ? x.withinDays : x.validForDays"
                (ngModelChange)="setDays(i, x.kind, $event)"
                [ngModelOptions]="{ updateOn: 'blur' }"
              />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'ls.startService' | dcTranslate }}</mat-label>
              <mat-select
                [ngModel]="x.startService ?? null"
                (ngModelChange)="setPrerequisite(i, { startService: $event })"
              >
                <mat-option [value]="null">{{ 'ls.none' | dcTranslate }}</mat-option>
                @for (s of others(); track s.code) {
                  <mat-option [value]="s.code"
                    >{{ { ar: s.nameAr, en: s.nameEn } | dcLocalize }} ({{ s.code }})</mat-option
                  >
                }
              </mat-select>
              @if (x.kind === 'REQUEST') {
                <mat-hint>{{ 'ls.startServiceHint' | dcTranslate }}</mat-hint>
              }
            </mat-form-field>
          </div>
          <div class="ls-row">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'ls.messageAr' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="rtl"
                [ngModel]="x.messageAr"
                (ngModelChange)="setPrerequisite(i, { messageAr: $event })"
                [ngModelOptions]="{ updateOn: 'blur' }"
              />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'ls.messageEn' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="x.messageEn"
                (ngModelChange)="setPrerequisite(i, { messageEn: $event })"
                [ngModelOptions]="{ updateOn: 'blur' }"
              />
            </mat-form-field>
          </div>
        </div>
      }
    </section>

    <section class="dc-card" data-testid="builder-bundle">
      <header class="ls-head">
        <h2>{{ 'ls.bundle' | dcTranslate }}</h2>
        <button matButton type="button" (click)="addBundle()" data-testid="add-bundle">
          <mat-icon fontSet="material-symbols-outlined">add</mat-icon
          >{{ 'ls.addBundle' | dcTranslate }}
        </button>
      </header>
      <p class="ls-muted">{{ 'ls.bundleHint' | dcTranslate }}</p>
      @for (b of bundle(); track $index; let i = $index) {
        <div class="ls-row" [attr.data-bundle]="i">
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'ls.service' | dcTranslate }}</mat-label>
            <mat-select
              [ngModel]="b.service || null"
              (ngModelChange)="setBundle(i, { service: $event })"
            >
              @for (s of bundleable(); track s.code) {
                <mat-option [value]="s.code"
                  >{{ { ar: s.nameAr, en: s.nameEn } | dcLocalize }} ({{ s.code }})</mat-option
                >
              }
            </mat-select>
          </mat-form-field>
          <mat-checkbox
            [ngModel]="b.required"
            (ngModelChange)="setBundle(i, { required: $event })"
            >{{ 'ls.required' | dcTranslate }}</mat-checkbox
          >
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'ls.noteAr' | dcTranslate }}</mat-label>
            <input
              matInput
              dir="rtl"
              [ngModel]="b.noteAr ?? ''"
              (ngModelChange)="setBundle(i, { noteAr: $event || null })"
              [ngModelOptions]="{ updateOn: 'blur' }"
            />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'ls.noteEn' | dcTranslate }}</mat-label>
            <input
              matInput
              dir="ltr"
              [ngModel]="b.noteEn ?? ''"
              (ngModelChange)="setBundle(i, { noteEn: $event || null })"
              [ngModelOptions]="{ updateOn: 'blur' }"
            />
          </mat-form-field>
          <button
            matIconButton
            type="button"
            (click)="removeBundle(i)"
            [matTooltip]="'ls.remove' | dcTranslate"
          >
            <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
          </button>
        </div>
      }
    </section>
  `,
  styles: `
    :host {
      display: contents;
    }
    .ls-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .ls-head h2 {
      margin: 0;
      font-size: 16px;
    }
    .ls-muted {
      margin: 0.25rem 0 0.5rem;
      color: var(--dga-text-3, #6c737f);
      font-size: 12px;
    }
    .ls-item {
      padding: 0.75rem 0;
      border-top: 1px solid var(--dga-gray-200, #e5e7eb);
    }
    .ls-row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 0.5rem;
    }
    .ls-row mat-form-field {
      flex: 1 1 12rem;
    }
    .ls-code {
      flex: 0 1 10rem !important;
    }
    .ls-num {
      flex: 0 1 12rem !important;
    }
  `,
})
export class LinkedServices {
  private readonly store = inject(BuilderStore);
  private readonly api = inject(AppsControlApi);
  protected readonly requestStatuses = REQUEST_STATUSES;
  protected readonly documentStatuses = DOCUMENT_STATUSES;

  private readonly services = toSignal(
    this.api.list('svc.service').pipe(catchError(() => of([] as ConfigRow[]))),
    { initialValue: [] as ConfigRow[] },
  );
  protected readonly documentTypes = toSignal(
    this.api.list('doc.document-type').pipe(catchError(() => of([] as ConfigRow[]))),
    { initialValue: [] as ConfigRow[] },
  );
  protected readonly prerequisites = computed(() => this.store.definition().prerequisites ?? []);
  protected readonly bundle = computed(() => this.store.definition().bundle ?? []);
  /** Every other active service. */
  protected readonly others = computed(() =>
    this.services().filter((s) => s.code !== this.store.service() && s.isActive !== false),
  );
  /** Services that can be bundled: not requested for a document. */
  protected readonly bundleable = computed(() => this.others().filter((s) => !s['documentType']));

  constructor() {
    inject(I18nService).register(LINKED_TRANSLATIONS);
  }

  private set(change: (def: ServiceDraft) => Partial<ServiceDraft>): void {
    this.store.apply((def) => ({ ...def, ...change(def) }));
  }

  protected addPrerequisite(): void {
    this.set((d) => {
      const list = d.prerequisites ?? [];
      const code = freeCode(
        list.map((x) => x.code),
        'PREREQUISITE',
      );
      return {
        prerequisites: [
          ...list,
          { code, kind: 'REQUEST', service: null, statuses: [], messageAr: '', messageEn: '' },
        ],
      };
    });
  }

  protected setPrerequisite(index: number, change: Partial<PrerequisiteDef>): void {
    this.set((d) => ({
      prerequisites: (d.prerequisites ?? []).map((x, i) => (i === index ? { ...x, ...change } : x)),
    }));
  }

  protected setDays(index: number, kind: PrerequisiteDef['kind'], value: unknown): void {
    const days = value === '' || value === null || value === undefined ? null : Number(value);
    this.setPrerequisite(index, kind === 'REQUEST' ? { withinDays: days } : { validForDays: days });
  }

  protected removePrerequisite(index: number): void {
    this.set((d) => ({ prerequisites: (d.prerequisites ?? []).filter((_, i) => i !== index) }));
  }

  protected addBundle(): void {
    this.set((d) => ({ bundle: [...(d.bundle ?? []), { service: '', required: false }] }));
  }

  protected setBundle(index: number, change: Partial<BundleDef>): void {
    this.set((d) => ({
      bundle: (d.bundle ?? []).map((b, i) => (i === index ? { ...b, ...change } : b)),
    }));
  }

  protected removeBundle(index: number): void {
    this.set((d) => ({ bundle: (d.bundle ?? []).filter((_, i) => i !== index) }));
  }
}
