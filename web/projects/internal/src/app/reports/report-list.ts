import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { I18nService, ReportSummary, ReportsApi, TranslatePipe } from '@dynamic-cube/shared';
import { REPORT_TRANSLATIONS } from './report-translations';

/** The reports the user may open (each report is a screen roles grant), grouped by their category. */
@Component({
  selector: 'dc-report-list',
  imports: [
    FormsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    RouterLink,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'rp.list.title' | dcTranslate }}</h1>
      <p class="dc-muted">{{ 'rp.list.subtitle' | dcTranslate }}</p>
    </header>
    <div class="dc-toolbar">
      <mat-form-field class="dc-toolbar__search" subscriptSizing="dynamic">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input
          matInput
          [placeholder]="'rp.list.search' | dcTranslate"
          [ngModel]="search()"
          (ngModelChange)="search.set($event)"
          data-testid="report-search"
        />
      </mat-form-field>
    </div>
    @for (g of groups(); track g.name) {
      <section class="rl-group">
        <h2>{{ g.name }}</h2>
        <div class="rl-cards">
          @for (r of g.reports; track r.code) {
            <a
              class="rl-card"
              [routerLink]="['view', r.code]"
              [attr.data-testid]="'report-' + r.code"
            >
              <mat-icon fontSet="material-symbols-outlined" class="rl-card__icon">{{
                r.icon || 'analytics'
              }}</mat-icon>
              <span class="rl-card__text">
                <strong>{{ name(r) }}</strong>
                @if (description(r); as d) {
                  <span class="dc-muted">{{ d }}</span>
                }
              </span>
            </a>
          }
        </div>
      </section>
    }
    @if (!reports.isLoading() && !groups().length) {
      <p class="dc-empty">{{ 'rp.list.empty' | dcTranslate }}</p>
    }
  `,
  styles: `
    .rl-group h2 {
      margin: 16px 0 8px;
      font-size: 15px;
    }
    .rl-cards {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(18rem, 1fr));
      gap: 12px;
    }
    .rl-card {
      display: flex;
      gap: 12px;
      align-items: flex-start;
      padding: 14px 16px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 10px;
      background: #fff;
      color: inherit;
      text-decoration: none;
    }
    .rl-card:hover,
    .rl-card:focus-visible {
      border-color: var(--dynamic-cube-gold, #ca9f45);
    }
    .rl-card__icon {
      color: var(--dynamic-cube-gold, #ca9f45);
    }
    .rl-card__text {
      display: grid;
      gap: 4px;
      font-size: 13px;
    }
  `,
})
export class ReportList {
  private readonly api = inject(ReportsApi);
  private readonly i18n = inject(I18nService);
  protected readonly search = signal('');
  protected readonly reports = rxResource({ stream: () => this.api.mine() });

  constructor() {
    this.i18n.register(REPORT_TRANSLATIONS);
  }

  private ar(): boolean {
    return this.i18n.language() === 'ar';
  }

  protected name(r: ReportSummary): string {
    return this.ar() ? r.nameAr : r.nameEn;
  }

  protected description(r: ReportSummary): string | null {
    return this.ar() ? r.descriptionAr : r.descriptionEn;
  }

  protected readonly groups = computed(() => {
    const term = this.search().trim().toLowerCase();
    const other = this.i18n.translate('rp.list.other');
    const groups = new Map<string, ReportSummary[]>();
    for (const r of this.reports.value() ?? []) {
      const text =
        `${r.code} ${r.nameAr} ${r.nameEn} ${r.descriptionAr ?? ''} ${r.descriptionEn ?? ''}`.toLowerCase();
      if (term && !text.includes(term)) continue;
      const name = (this.ar() ? r.categoryAr : r.categoryEn) || other;
      groups.set(name, [...(groups.get(name) ?? []), r]);
    }
    return [...groups.entries()]
      .sort(([a], [b]) => (a === other ? 1 : b === other ? -1 : a.localeCompare(b)))
      .map(([name, reports]) => ({
        name,
        reports: reports.sort((a, b) => this.name(a).localeCompare(this.name(b))),
      }));
  });
}
