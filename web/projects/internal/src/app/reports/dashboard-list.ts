import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import {
  DashboardSummary,
  DashboardView,
  I18nService,
  ReportsApi,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { catchError, map, of } from 'rxjs';
import { DashboardEditor, DashboardEditorData } from './dashboard-editor';
import { DASHBOARD_TRANSLATIONS } from './dashboard-translations';

/**
 * The dashboards the user may open (ADR-0047): their own (pinned from reports or built here) and the shared ones granted
 * to them.
 */
@Component({
  selector: 'dc-dashboard-list',
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, RouterLink, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header dc-page__header--actions">
      <div>
        <h1>{{ 'db.title' | dcTranslate }}</h1>
        <p class="dc-muted">{{ 'db.subtitle' | dcTranslate }}</p>
      </div>
      <button matButton="filled" type="button" (click)="create()" data-testid="dashboard-new">
        <mat-icon fontSet="material-symbols-outlined">add</mat-icon>{{ 'db.new' | dcTranslate }}
      </button>
    </header>
    @if (list.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @if (list.value()?.error; as e) {
      <p class="dc-errors" role="alert">{{ e }}</p>
    }
    <section class="dl-section">
      <h2>{{ 'db.mine' | dcTranslate }}</h2>
      <div class="dl-cards" data-testid="dashboards-mine">
        @for (d of mine(); track d.id) {
          <a
            class="dl-card"
            [routerLink]="['/reports/my-dashboards', d.id]"
            [attr.data-dashboard]="d.id"
          >
            <mat-icon fontSet="material-symbols-outlined">{{ d.icon || 'dashboard' }}</mat-icon>
            <span class="dl-name">{{ name(d) }}</span>
            @if (d.isDefault) {
              <span class="dl-badge">{{ 'db.default' | dcTranslate }}</span>
            }
          </a>
        } @empty {
          <p class="dc-muted">{{ 'db.mineNone' | dcTranslate }}</p>
        }
      </div>
    </section>
    <section class="dl-section">
      <h2>{{ 'db.shared' | dcTranslate }}</h2>
      <div class="dl-cards" data-testid="dashboards-shared">
        @for (d of shared(); track d.code) {
          <a
            class="dl-card"
            [routerLink]="['/reports/dashboards', d.code]"
            [attr.data-dashboard]="d.code"
          >
            <mat-icon fontSet="material-symbols-outlined">{{
              d.icon || 'space_dashboard'
            }}</mat-icon>
            <span class="dl-name">{{ name(d) }}</span>
            @if (description(d); as text) {
              <span class="dc-muted dl-desc">{{ text }}</span>
            }
          </a>
        } @empty {
          <p class="dc-muted">{{ 'db.none' | dcTranslate }}</p>
        }
      </div>
    </section>
  `,
  styles: `
    :host {
      display: grid;
      gap: 16px;
    }
    .dl-section h2 {
      margin: 0 0 8px;
      font-size: 16px;
    }
    .dl-cards {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr));
      gap: 10px;
    }
    .dl-card {
      display: grid;
      grid-template-columns: auto 1fr;
      align-items: center;
      gap: 4px 10px;
      padding: 14px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 10px;
      background: #fff;
      color: inherit;
      text-decoration: none;
    }
    .dl-card:hover {
      border-color: var(--dga-gray-400, #9da4ae);
    }
    .dl-name {
      font-weight: 600;
    }
    .dl-desc,
    .dl-badge {
      grid-column: 2;
      font-size: 12px;
    }
    .dl-badge {
      justify-self: start;
      padding: 1px 8px;
      border-radius: 999px;
      background: var(--dga-gray-100, #f3f4f6);
    }
  `,
})
export class DashboardList {
  private readonly api = inject(ReportsApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly i18n = inject(I18nService);

  protected readonly list = rxResource({
    stream: () =>
      this.api.dashboards().pipe(
        map((items) => ({ items, error: null as string | null })),
        catchError((error) =>
          of({ items: [] as DashboardSummary[], error: problemMessages(error).join(' ') }),
        ),
      ),
  });
  protected readonly mine = computed(
    () => this.list.value()?.items.filter((d) => d.personal) ?? [],
  );
  protected readonly shared = computed(
    () => this.list.value()?.items.filter((d) => !d.personal) ?? [],
  );

  constructor() {
    this.i18n.register(DASHBOARD_TRANSLATIONS);
  }

  protected name(d: DashboardSummary): string {
    return this.i18n.language() === 'ar' ? d.nameAr : d.nameEn;
  }

  protected description(d: DashboardSummary): string | null {
    return this.i18n.language() === 'ar' ? d.descriptionAr : d.descriptionEn;
  }

  /** A new dashboard of the user's own, built in the editor. */
  protected create(): void {
    this.dialog
      .open<DashboardEditor, DashboardEditorData, boolean | DashboardView>(DashboardEditor, {
        data: { personal: null },
        width: '80rem',
        maxWidth: '96vw',
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved && typeof saved === 'object' && saved.id)
          void this.router.navigate(['/reports/my-dashboards', saved.id]);
        else if (saved) this.list.reload();
      });
  }
}
