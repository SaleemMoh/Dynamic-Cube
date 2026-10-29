import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AppsControlApi, I18nService, PartyApi, PartySummary, RelationshipInfo, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { Observable, catchError, debounceTime, distinctUntilChanged, firstValueFrom, map, of, switchMap } from 'rxjs';
import { PrivilegesService } from '../privileges.service';

const ESTABLISHMENT_NUMBER = /^[0-9]{10}$/;

/**
 * The establishments a person may act for on the portal, by national ID or iqama number: links reported by the
 * commercial registry integration at sign-in, and links staff add here (also before the person's first sign-in). An
 * establishment not in the registry yet is registered with the names given.
 */
@Component({
  selector: 'dc-person-links',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule, MatSelectModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="dc-plinks" data-testid="person-links">
      <header class="dc-plinks__head">
        <h3>{{ 'personLinks.title' | dcTranslate }}</h3>
        @if (canEdit() && !adding()) {
          <button matButton="outlined" type="button" (click)="startAdding()" data-testid="person-links-add">
            <mat-icon fontSet="material-symbols-outlined">add_business</mat-icon>{{ 'personLinks.add' | dcTranslate }}
          </button>
        }
      </header>
      <p class="dc-muted">{{ 'personLinks.hint' | dcTranslate }}</p>
      @if (links.isLoading() || busy()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li dir="auto">{{ e }}</li>
          }
        </ul>
      }

      @for (l of current(); track l.id) {
        <div class="dc-plinks__row" [attr.data-link]="l.principalIdentifier">
          <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">storefront</mat-icon>
          <div class="dc-plinks__who">
            <strong>{{ l.principalName }}</strong>
            <span class="dc-muted mono" dir="ltr">{{ l.principalIdentifier }}</span>
          </div>
          <span class="dc-pill">{{ i18n.localize({ ar: l.relationshipNameAr, en: l.relationshipNameEn }) }}</span>
          <span class="dc-pill" [class.dc-pill--gold]="l.source === 'MANUAL'">{{ 'personLinks.source.' + l.source | dcTranslate }}</span>
          @if (l.validTo) {
            <span class="dc-muted"
              >{{ 'personLinks.until' | dcTranslate }} <span dir="ltr">{{ l.validTo }}</span></span
            >
          }
          @if (l.services.length) {
            <span class="dc-muted mono" dir="ltr">{{ l.services.join(', ') }}</span>
          }
          @if (canEdit() && l.source !== 'INTEGRATION') {
            <button
              matButton
              type="button"
              class="dc-plinks__end"
              (click)="end(l)"
              [disabled]="busy()"
              [attr.data-testid]="'person-links-end-' + l.principalIdentifier"
            >
              {{ 'personLinks.end' | dcTranslate }}
            </button>
          }
        </div>
      } @empty {
        @if (!links.isLoading()) {
          <p class="dc-plinks__empty" data-testid="person-links-empty">{{ 'personLinks.none' | dcTranslate }}</p>
        }
      }

      @if (adding()) {
        <div class="dc-plinks__form" data-testid="person-links-form">
          <div class="dc-plinks__grid">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'personLinks.number' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                inputmode="numeric"
                maxlength="10"
                [ngModel]="number()"
                (ngModelChange)="number.set($event.trim())"
                data-testid="person-links-number"
              />
              <mat-hint>{{ 'personLinks.numberHint' | dcTranslate }}</mat-hint>
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'personLinks.relationship' | dcTranslate }}</mat-label>
              <mat-select [ngModel]="relationship()" (ngModelChange)="relationship.set($event)" data-testid="person-links-relationship">
                @for (t of types.value() ?? []; track t.code) {
                  <mat-option [value]="t.code">{{ i18n.localize({ ar: t.nameAr, en: t.nameEn }) }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>
          @if (found(); as f) {
            <p class="dc-plinks__found" data-testid="person-links-found">
              <mat-icon fontSet="material-symbols-outlined">check_circle</mat-icon>{{ i18n.localize({ ar: f.nameAr, en: f.nameEn }) }}
            </p>
          } @else if (looked()) {
            <p class="dc-muted">{{ 'personLinks.newEstablishment' | dcTranslate }}</p>
            <div class="dc-plinks__grid">
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'personLinks.nameAr' | dcTranslate }}</mat-label>
                <input matInput [ngModel]="nameAr()" (ngModelChange)="nameAr.set($event)" data-testid="person-links-name-ar" />
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'personLinks.nameEn' | dcTranslate }}</mat-label>
                <input matInput dir="ltr" [ngModel]="nameEn()" (ngModelChange)="nameEn.set($event)" data-testid="person-links-name-en" />
              </mat-form-field>
            </div>
          }
          <div class="dc-plinks__grid">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'personLinks.validTo' | dcTranslate }}</mat-label>
              <input matInput type="date" dir="ltr" [ngModel]="validTo()" (ngModelChange)="validTo.set($event)" />
              <mat-hint>{{ 'personLinks.validToHint' | dcTranslate }}</mat-hint>
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'personLinks.services' | dcTranslate }}</mat-label>
              <input matInput dir="ltr" [ngModel]="services()" (ngModelChange)="services.set($event)" />
              <mat-hint>{{ 'personLinks.servicesHint' | dcTranslate }}</mat-hint>
            </mat-form-field>
          </div>
          <div class="dc-plinks__actions">
            <button matButton="filled" type="button" (click)="link()" [disabled]="busy() || !canLink()" data-testid="person-links-save">
              {{ 'personLinks.save' | dcTranslate }}
            </button>
            <button matButton type="button" (click)="adding.set(false)">{{ 'form.cancel' | dcTranslate }}</button>
          </div>
        </div>
      }
    </section>
  `,
  styles: `
    .dc-plinks {
      display: grid;
      gap: 8px;
      padding-top: 12px;
      border-top: 1px solid var(--dga-gray-200);
    }
    .dc-plinks__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }
    .dc-plinks__head h3 {
      margin: 0;
      font-size: 14px;
    }
    .dc-plinks > p {
      margin: 0;
      font-size: 12px;
    }
    .dc-plinks__row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
      padding: 8px 10px;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius);
      font-size: 13px;
    }
    .dc-plinks__row > mat-icon {
      color: var(--dynamic-cube-gold-700);
    }
    .dc-plinks__who {
      display: grid;
      flex: 1;
      min-width: 10rem;
    }
    .dc-plinks__end {
      --mat-button-text-label-text-color: #b42318;
    }
    .dc-plinks__empty {
      padding: 10px 12px;
      border-radius: var(--shell-radius);
      background: var(--dga-gray-50);
      color: var(--dga-gray-700);
    }
    .dc-plinks__form {
      display: grid;
      gap: 10px;
      padding: 12px;
      border: 1px dashed var(--dga-gray-300);
      border-radius: var(--shell-radius);
    }
    .dc-plinks__grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr));
      gap: 12px;
    }
    .dc-plinks__found {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0;
      color: #067647;
      font-weight: 600;
    }
    .dc-plinks__actions {
      display: flex;
      gap: 8px;
    }
  `,
})
export class PersonLinks {
  /** The person's national ID or iqama number. */
  readonly nationalId = input.required<string>();
  /** Names to register the person with when they have not signed in yet. */
  readonly personName = input<string | null>(null);

  protected readonly i18n = inject(I18nService);
  private readonly parties = inject(PartyApi);
  private readonly appsControl = inject(AppsControlApi);
  private readonly privileges = inject(PrivilegesService);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly canEdit = computed(() => this.privileges.can('party.parties', 'EDIT'));
  protected readonly links = rxResource({ params: () => this.nationalId(), stream: ({ params }) => this.parties.personLinks(params) });
  protected readonly current = computed(() => (this.links.value()?.links ?? []).filter((l) => !l.endedAt));
  protected readonly types = rxResource({
    stream: () => this.appsControl.list('party.relationship-type').pipe(map((rows) => rows.filter((r) => r.isActive))),
  });

  protected readonly adding = signal(false);
  protected readonly number = signal('');
  protected readonly relationship = signal('MANAGER');
  protected readonly nameAr = signal('');
  protected readonly nameEn = signal('');
  protected readonly validTo = signal('');
  protected readonly services = signal('');
  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);

  /** The establishment the number finds in the registry (undefined while not looked up). */
  private readonly lookup = toSignal(
    toObservable(this.number).pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap((n): Observable<PartySummary | null | undefined> =>
        ESTABLISHMENT_NUMBER.test(n)
          ? this.parties.search(n, 'ESTABLISHMENT', 0, 1).pipe(
              map((page) => page.items[0] ?? null),
              catchError(() => of(null)),
            )
          : of(undefined),
      ),
    ),
  );
  protected readonly found = computed(() => this.lookup() ?? null);
  protected readonly looked = computed(() => this.lookup() === null);
  protected readonly canLink = computed(
    () => ESTABLISHMENT_NUMBER.test(this.number()) && !!this.relationship() && (!!this.found() || (!!this.nameAr().trim() && !!this.nameEn().trim())),
  );

  protected startAdding(): void {
    this.number.set('');
    this.nameAr.set('');
    this.nameEn.set('');
    this.validTo.set('');
    this.services.set('');
    this.errors.set([]);
    this.adding.set(true);
  }

  protected async link(): Promise<void> {
    const name = this.personName()?.trim() || null;
    const done = await this.run(
      this.parties.linkEstablishment(this.nationalId(), {
        establishmentIdentifier: this.number(),
        establishmentNameAr: this.found() ? null : this.nameAr().trim(),
        establishmentNameEn: this.found() ? null : this.nameEn().trim(),
        relationshipCode: this.relationship(),
        services: this.services()
          .split(/[\s,]+/)
          .filter(Boolean),
        validFrom: null,
        validTo: this.validTo() || null,
        personNameAr: name,
        personNameEn: name,
      }),
      'personLinks.linked',
    );
    if (done) this.adding.set(false);
  }

  protected end(link: RelationshipInfo): Promise<boolean> {
    return this.run(this.parties.endPersonLink(this.nationalId(), link.id), 'personLinks.ended');
  }

  private async run(change: Observable<unknown>, done: string): Promise<boolean> {
    this.busy.set(true);
    this.errors.set([]);
    try {
      await firstValueFrom(change);
      this.snackBar.open(this.i18n.translate(done), undefined, { duration: 4000 });
      this.links.reload();
      return true;
    } catch (error) {
      this.errors.set(problemMessages(error));
      return false;
    } finally {
      this.busy.set(false);
    }
  }
}
