import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import {
  DashboardView,
  I18nService,
  ReportsApi,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { catchError, firstValueFrom, of, tap } from 'rxjs';
import { DASHBOARD_TRANSLATIONS } from './dashboard-translations';
import { DashboardEditor, DashboardEditorData } from './dashboard-editor';
import { DashboardWidget, WidgetDrill } from './dashboard-widget';
import { ReportDrill, ReportDrillData } from './report-drill';
import { ReportFilterFields } from './report-filter-fields';
import { cleanFilters } from './report-model';
import { REPORT_TRANSLATIONS } from './report-translations';

/**
 * A dashboard (ADR-0047): a shared one (by code, a screen roles grant) or one of the user's own (by id). Its filters fill
 * its widgets' report filters; each widget runs on its own through its report, within the user's access, and opens the
 * rows behind a group. The user's own dashboards are edited and deleted here.
 */
@Component({
  selector: 'dc-dashboard-viewer',
  imports: [
    DashboardWidget,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    ReportFilterFields,
    RouterLink,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="dv-back" routerLink="/reports/dashboards">
      <mat-icon fontSet="material-symbols-outlined">arrow_back</mat-icon
      >{{ 'db.back' | dcTranslate }}
    </a>
    @if (view.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (e of errors(); track $index) {
          <li>{{ e }}</li>
        }
      </ul>
    }
    @if (view.value(); as v) {
      <header class="dc-page__header dc-page__header--actions">
        <div>
          <h1 data-testid="dashboard-name">{{ ar() ? v.nameAr : v.nameEn }}</h1>
          @if (ar() ? v.descriptionAr : v.descriptionEn; as d) {
            <p class="dc-muted">{{ d }}</p>
          }
        </div>
        @if (v.personal) {
          <div class="dv-actions">
            <button matButton type="button" (click)="edit(v)" data-testid="dashboard-edit">
              <mat-icon fontSet="material-symbols-outlined">edit</mat-icon
              >{{ 'db.edit' | dcTranslate }}
            </button>
            <button matButton type="button" (click)="remove(v)" data-testid="dashboard-delete">
              <mat-icon fontSet="material-symbols-outlined">delete</mat-icon
              >{{ 'db.delete' | dcTranslate }}
            </button>
          </div>
        }
      </header>
      @if (v.filters.length) {
        <div class="dv-filters" data-testid="dashboard-filters">
          <dc-report-filter-fields
            [filters]="v.filters"
            [values]="values()"
            (changed)="values.set($event)"
          />
          <div class="dv-buttons">
            <button
              matButton="filled"
              type="button"
              (click)="apply()"
              data-testid="dashboard-apply"
            >
              <mat-icon fontSet="material-symbols-outlined">play_arrow</mat-icon
              >{{ 'db.apply' | dcTranslate }}
            </button>
            <button matButton type="button" (click)="clear()">
              {{ 'db.clear' | dcTranslate }}
            </button>
          </div>
        </div>
      }
      @if (v.hidden) {
        <p class="dv-note" role="status">{{ hiddenText(v.hidden) }}</p>
      }
      <div class="dv-grid" data-testid="dashboard-grid">
        @for (w of v.widgets; track w.name) {
          <dc-dashboard-widget
            [style.grid-column]="'span ' + span(w.span)"
            [attr.data-widget]="w.name"
            [widget]="w"
            [dashboard]="{ code: v.code, id: v.id }"
            [filters]="applied()"
            (drill)="drill($event)"
          />
        } @empty {
          <p class="dc-empty">{{ 'db.empty' | dcTranslate }}</p>
        }
      </div>
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: 14px;
    }
    .dv-back {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      justify-self: start;
      color: var(--dga-text-3, #6c737f);
      font-size: 13px;
      text-decoration: none;
    }
    .dv-actions,
    .dv-buttons {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      align-items: center;
    }
    .dv-filters {
      display: flex;
      flex-wrap: wrap;
      gap: 10px;
      align-items: center;
    }
    .dv-note {
      margin: 0;
      color: var(--dga-text-3, #6c737f);
      font-size: 13px;
    }
    .dv-grid {
      display: grid;
      grid-template-columns: repeat(12, minmax(0, 1fr));
      gap: 12px;
    }
    @media (max-width: 900px) {
      .dv-grid > * {
        grid-column: 1 / -1 !important;
      }
    }
  `,
})
export class DashboardViewer {
  private readonly api = inject(ReportsApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly i18n = inject(I18nService);
  /** A shared dashboard's code, or (on /reports/my-dashboards/:id) the user's own dashboard's id. */
  readonly code = input<string | undefined>(undefined);
  readonly id = input<string | undefined>(undefined);

  protected readonly values = signal<Record<string, string | null>>({});
  /** The values the widgets last ran with; a new object runs them again. */
  protected readonly applied = signal<Record<string, string | null>>({});
  protected readonly errors = signal<string[]>([]);
  protected readonly ar = computed(() => this.i18n.language() === 'ar');

  protected readonly view = rxResource({
    params: () => ({ code: this.code(), id: this.id() }),
    stream: ({ params }) =>
      (params.id ? this.api.myDashboard(params.id) : this.api.dashboard(params.code ?? '')).pipe(
        tap(() => {
          this.values.set({});
          this.applied.set({});
        }),
        catchError((error) => {
          this.errors.set(problemMessages(error));
          return of(null);
        }),
      ),
  });

  constructor() {
    this.i18n.register(REPORT_TRANSLATIONS);
    this.i18n.register(DASHBOARD_TRANSLATIONS);
  }

  protected span(value: number): number {
    return Math.min(12, Math.max(1, value || 6));
  }

  protected hiddenText(n: number): string {
    return this.i18n.translate('db.hidden').replace('{n}', String(n));
  }

  protected apply(): void {
    this.applied.set(cleanFilters(this.values()));
  }

  protected clear(): void {
    this.values.set({});
    this.applied.set({});
  }

  protected drill(event: WidgetDrill): void {
    this.dialog.open<ReportDrill, ReportDrillData>(ReportDrill, {
      data: {
        report: event.widget.report,
        reportNameAr: event.widget.reportNameAr,
        reportNameEn: event.widget.reportNameEn,
        titleAr: event.target.titleAr,
        titleEn: event.target.titleEn,
        canExport: event.widget.canExport,
        request: {
          filters: cleanFilters(event.filters),
          source: event.target.source,
          chart: event.target.chart,
          values: event.target.values,
        },
      },
      maxWidth: '96vw',
    });
  }

  protected edit(view: DashboardView): void {
    this.dialog
      .open<DashboardEditor, DashboardEditorData, boolean>(DashboardEditor, {
        data: { personal: view },
        width: '80rem',
        maxWidth: '96vw',
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.view.reload();
      });
  }

  protected async remove(view: DashboardView): Promise<void> {
    if (!view.id || !confirm(this.i18n.translate('db.deleteConfirm'))) return;
    try {
      await firstValueFrom(this.api.deleteMyDashboard(view.id));
      void this.router.navigate(['/reports/dashboards']);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}
