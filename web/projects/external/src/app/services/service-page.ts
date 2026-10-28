import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { ActingPartyService, FeeTable, FieldError, LocalizePipe, ServicesApi, TranslatePipe, requestErrors } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';

/** A service before starting it: what it is, its levels with duration and fees, and whether the acting party may apply. */
@Component({
  selector: 'dc-service-page',
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, RouterLink, FeeTable, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a routerLink="/services" class="dc-link">{{ 'catalog.title' | dcTranslate }}</a>
    @if (view.value(); as v) {
      <h1 data-testid="service-title">{{ { ar: v.service.nameAr, en: v.service.nameEn } | dcLocalize }}</h1>
      @if (v.service.descriptionAr || v.service.descriptionEn) {
        <p>{{ { ar: v.service.descriptionAr ?? '', en: v.service.descriptionEn ?? '' } | dcLocalize }}</p>
      }
      @for (level of v.version.definition.levels; track level.code) {
        <section class="dc-card dc-service__level">
          <h2>{{ { ar: level.nameAr, en: level.nameEn } | dcLocalize }}</h2>
          @if (level.slaWorkingDays) {
            <p>{{ 'wizard.duration' | dcTranslate }} {{ level.slaWorkingDays }} {{ 'wizard.workingDays' | dcTranslate }}</p>
          }
          <dc-fee-table [lines]="v.feesByLevel[level.code] ?? []" />
        </section>
      }
      @if (!v.version.definition.levels.length) {
        <dc-fee-table [lines]="v.feesByLevel[''] ?? []" />
      }
      @if (v.service.isEligible) {
        <button matButton="filled" (click)="start(v.service.code)" [disabled]="starting()" data-testid="start-request">
          <mat-icon fontSet="material-symbols-outlined">edit_document</mat-icon>
          {{ 'catalog.start' | dcTranslate }}
        </button>
      } @else {
        <ul class="dc-errors" role="alert">
          @for (r of v.service.reasons; track r.code) {
            <li>{{ { ar: r.messageAr, en: r.messageEn } | dcLocalize }}</li>
          }
        </ul>
      }
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li>{{ { ar: e.messageAr, en: e.messageEn } | dcLocalize }}</li>
          }
        </ul>
      }
    } @else if (view.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (view.error()) {
      <p class="dc-errors" role="alert">{{ 'catalog.notFound' | dcTranslate }}</p>
    }
  `,
  styles: `
    .dc-service__level {
      margin-block: 1rem;
    }
    .dc-service__level h2 {
      margin-top: 0;
      font-size: 18px;
    }
  `,
})
export class ServicePage {
  readonly code = input.required<string>();
  private readonly api = inject(ServicesApi);
  private readonly router = inject(Router);
  private readonly acting = inject(ActingPartyService);
  protected readonly starting = signal(false);
  protected readonly errors = signal<FieldError[]>([]);
  protected readonly view = rxResource({ params: () => ({ code: this.code(), party: this.acting.current()?.partyId }), stream: ({ params }) => this.api.service(params.code) });

  protected async start(code: string): Promise<void> {
    this.starting.set(true);
    this.errors.set([]);
    try {
      const draft = await firstValueFrom(this.api.create(code));
      await this.router.navigate(['/requests', draft.request.id]);
    } catch (error) {
      this.errors.set(requestErrors(error));
    } finally {
      this.starting.set(false);
    }
  }
}
