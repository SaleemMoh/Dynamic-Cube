import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import {
  ActingPartyService,
  DynamicForm,
  FormSchema,
  FormValue,
  I18nService,
  LocalizePipe,
  PARTY_API,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { PageHero } from '../portal/page-hero';

interface AuthorityType {
  code: string;
  nameAr: string;
  nameEn: string;
  descriptionAr: string | null;
  descriptionEn: string | null;
  numberLabelAr: string;
  numberLabelEn: string;
  principalIdentifierType: string;
  requiresExpiry: boolean;
  checked: boolean;
}

interface Authority {
  id: string;
  type: string;
  typeNameAr: string;
  typeNameEn: string;
  principalId: string;
  principalNameAr: string;
  principalNameEn: string;
  principalIdentifier: string;
  number: string;
  validTo: string | null;
  active: boolean;
}

/**
 * Acting for another firm on an authority (a municipal authorization, a notarised agency…): the applicant gives the
 * firm's number and the authority's number, it is checked with the provider, and the firm becomes one of the firms they
 * can act for (the selector in the header) until the authority ends.
 */
@Component({
  selector: 'dc-my-authorities',
  imports: [
    DatePipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    DynamicForm,
    LocalizePipe,
    TranslatePipe,
    PageHero,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dc-page-hero
      [title]="'authorities.title' | dcTranslate"
      [text]="'authorities.lead' | dcTranslate"
    />
    <div class="dc-portal-block dc-page-body dc-auth">
      <section class="dc-card" data-testid="authorities-list">
        <h2>{{ 'authorities.mine' | dcTranslate }}</h2>
        @for (a of mine.value() ?? []; track a.id) {
          <div class="dc-auth__row" [attr.data-testid]="'authority-' + a.number">
            <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{
              a.active ? 'verified' : 'event_busy'
            }}</mat-icon>
            <div class="dc-auth__text">
              <strong>{{ { ar: a.principalNameAr, en: a.principalNameEn } | dcLocalize }}</strong>
              <span class="dc-muted">
                {{ { ar: a.typeNameAr, en: a.typeNameEn } | dcLocalize }} ·
                <span dir="ltr">{{ a.number }}</span> ·
                <span dir="ltr">{{ a.principalIdentifier }}</span>
                @if (a.validTo) {
                  · {{ 'authorities.until' | dcTranslate }}
                  <span dir="ltr">{{ a.validTo | date: 'yyyy-MM-dd' }}</span>
                }
              </span>
            </div>
            <button matButton="outlined" type="button" (click)="end(a)" [disabled]="busy()">
              {{ 'authorities.end' | dcTranslate }}
            </button>
          </div>
        } @empty {
          @if (!mine.isLoading()) {
            <p class="dc-empty">{{ 'authorities.none' | dcTranslate }}</p>
          }
        }
      </section>

      <section class="dc-card" data-testid="authority-form">
        <h2>{{ 'authorities.add' | dcTranslate }}</h2>
        @if (types.value()?.length) {
          @if (errors().length) {
            <ul class="dc-errors" role="alert">
              @for (e of errors(); track $index) {
                <li>{{ e }}</li>
              }
            </ul>
          }
          <mat-button-toggle-group
            class="dc-auth__types"
            [value]="chosen()?.code"
            (change)="typeCode.set($event.value)"
            [attr.aria-label]="'authorities.type' | dcTranslate"
            data-testid="authority-type"
          >
            @for (t of types.value() ?? []; track t.code) {
              <mat-button-toggle [value]="t.code">{{
                { ar: t.nameAr, en: t.nameEn } | dcLocalize
              }}</mat-button-toggle>
            }
          </mat-button-toggle-group>
          @if (chosen(); as t) {
            @if (t.descriptionAr || t.descriptionEn) {
              <p class="dc-muted">
                {{ { ar: t.descriptionAr ?? '', en: t.descriptionEn ?? '' } | dcLocalize }}
              </p>
            }
          }
          <dc-dynamic-form
            [schema]="schema()"
            [submitLabel]="'authorities.save' | dcTranslate"
            submitIcon="verified_user"
            (submitted)="add($event)"
          />
        } @else if (!types.isLoading()) {
          <p class="dc-empty">{{ 'authorities.noTypes' | dcTranslate }}</p>
        }
      </section>
    </div>
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }
    .dc-auth {
      display: grid;
      gap: 16px;
    }
    .dc-auth h2 {
      margin: 0 0 12px;
      font-size: 18px;
    }
    .dc-auth__types {
      flex-wrap: wrap;
      margin-bottom: 12px;
    }
    .dc-auth__row {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 10px 0;
      border-bottom: 1px solid var(--dga-gray-200, #e5e7eb);
    }
    .dc-auth__row mat-icon {
      color: var(--dga-primary-600, #1b8354);
    }
    .dc-auth__text {
      display: grid;
      gap: 2px;
      flex: 1;
    }
  `,
})
export class MyAuthorities {
  private readonly http = inject(HttpClient);
  private readonly acting = inject(ActingPartyService);
  private readonly snack = inject(MatSnackBar);
  private readonly i18n = inject(I18nService);
  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly typeCode = signal<string | null>(null);

  protected readonly types = rxResource({
    stream: () => this.http.get<AuthorityType[]>(`${PARTY_API}/me/authority-types`),
  });
  protected readonly mine = rxResource({
    stream: () => this.http.get<Authority[]>(`${PARTY_API}/me/authorities`),
  });
  protected readonly chosen = computed(() => {
    const types = this.types.value() ?? [];
    return types.find((t) => t.code === this.typeCode()) ?? types[0] ?? null;
  });

  protected readonly schema = computed<FormSchema>(() => {
    const t = this.chosen();
    return {
      code: `authority-${t?.code ?? ''}`,
      sections: [
        {
          code: 'authority',
          title: { ar: '', en: '' },
          columns: 2,
          fields: [
            {
              code: 'principalIdentifier',
              type: 'text',
              label: { ar: 'رقم السجل التجاري للمنشأة', en: "The firm's CR number" },
              required: true,
              pattern: '^[0-9]{10}$',
            },
            {
              code: 'number',
              type: 'text',
              label: { ar: t?.numberLabelAr ?? 'الرقم', en: t?.numberLabelEn ?? 'Number' },
              required: true,
            },
            {
              code: 'expiresOn',
              type: 'date',
              label: { ar: 'تاريخ الانتهاء', en: 'Expiry date' },
              required: !!t?.requiresExpiry,
              hint: t?.checked
                ? { ar: 'نتحقق منه لدى الجهة المصدرة', en: 'Checked with the issuing provider' }
                : undefined,
            },
          ],
        },
      ],
    };
  });

  protected async add(value: FormValue): Promise<void> {
    this.errors.set([]);
    this.busy.set(true);
    try {
      const added = await firstValueFrom(
        this.http.post<Authority>(`${PARTY_API}/me/authorities`, {
          ...value,
          type: this.chosen()?.code,
        }),
      );
      const name = this.i18n.language() === 'ar' ? added.principalNameAr : added.principalNameEn;
      this.snack.open(`${this.i18n.translate('authorities.added')} ${name}`, undefined, {
        duration: 5000,
      });
      this.mine.reload();
      await this.acting.load(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }

  protected async end(authority: Authority): Promise<void> {
    this.busy.set(true);
    try {
      await firstValueFrom(this.http.delete(`${PARTY_API}/me/authorities/${authority.id}`));
      this.mine.reload();
      await this.acting.load(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }
}
