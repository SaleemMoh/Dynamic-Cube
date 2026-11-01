import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  AppsControlApi,
  ConfigRow,
  I18nService,
  LocalizePipe,
  PublicApiAdmin,
  TranslatePipe,
} from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';
import { PUBLIC_API_TRANSLATIONS } from './public-api-translations';

/** Every public API call and designer try (masked inputs written ***), newest first (ADR-0042). */
@Component({
  selector: 'dc-api-calls',
  imports: [
    DatePipe,
    FormsModule,
    LocalizePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <div>
        <h1>{{ 'pa.calls.title' | dcTranslate }}</h1>
        <p class="ac-muted">{{ 'pa.calls.subtitle' | dcTranslate }}</p>
      </div>
      <button matButton type="button" (click)="page.reload()">
        <mat-icon fontSet="material-symbols-outlined">refresh</mat-icon
        >{{ 'pa.calls.refresh' | dcTranslate }}
      </button>
    </header>
    <div class="dc-toolbar">
      <mat-form-field subscriptSizing="dynamic">
        <mat-select
          [placeholder]="'pa.calls.client' | dcTranslate"
          [ngModel]="client()"
          (ngModelChange)="filter(client, $event)"
          [attr.aria-label]="'pa.calls.client' | dcTranslate"
          data-testid="pa-calls-client"
        >
          <mat-option [value]="null">{{ 'pa.calls.all' | dcTranslate }}</mat-option>
          @for (c of clients(); track c.code) {
            <mat-option [value]="c.code">{{
              { ar: c.nameAr, en: c.nameEn } | dcLocalize
            }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-select
          [placeholder]="'pa.calls.endpoint' | dcTranslate"
          [ngModel]="endpoint()"
          (ngModelChange)="filter(endpoint, $event)"
          [attr.aria-label]="'pa.calls.endpoint' | dcTranslate"
        >
          <mat-option [value]="null">{{ 'pa.calls.all' | dcTranslate }}</mat-option>
          @for (e of endpoints(); track e.code) {
            <mat-option [value]="e.code">{{
              { ar: e.nameAr, en: e.nameEn } | dcLocalize
            }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-select
          [placeholder]="'pa.calls.status' | dcTranslate"
          [ngModel]="status()"
          (ngModelChange)="filter(status, $event)"
          [attr.aria-label]="'pa.calls.status' | dcTranslate"
        >
          <mat-option [value]="null">{{ 'pa.calls.all' | dcTranslate }}</mat-option>
          @for (s of statuses; track s) {
            <mat-option [value]="s">{{ 'pa.calls.status.' + s | dcTranslate }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>
    <table
      mat-table
      [dataSource]="page.value()?.items ?? []"
      class="dc-table"
      data-testid="pa-calls"
    >
      <ng-container matColumnDef="startedAt">
        <th mat-header-cell *matHeaderCellDef>{{ 'pa.calls.at' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr">
          {{ row.startedAt | date: 'yyyy-MM-dd HH:mm:ss' }}
        </td>
      </ng-container>
      <ng-container matColumnDef="client">
        <th mat-header-cell *matHeaderCellDef>{{ 'pa.calls.client' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr">
          {{ row.clientCode || row.clientId }}
          @if (row.isTry) {
            <span class="dc-pill">{{ 'pa.calls.try' | dcTranslate }}</span>
          }
        </td>
      </ng-container>
      <ng-container matColumnDef="path">
        <th mat-header-cell *matHeaderCellDef>{{ 'pa.calls.path' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr" class="ac-path">
          <b>{{ row.method }}</b> {{ row.path }}
        </td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef>{{ 'pa.calls.status' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row">
          <span
            class="dc-pill"
            [class.dc-pill--green]="row.status < 300"
            [class.dc-pill--gold]="row.status >= 400 && row.status < 500"
            [matTooltip]="row.error ?? ''"
            >{{ row.status }}</span
          >
        </td>
      </ng-container>
      <ng-container matColumnDef="rows">
        <th mat-header-cell *matHeaderCellDef>{{ 'pa.calls.rows' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr">{{ row.rows ?? '—' }}</td>
      </ng-container>
      <ng-container matColumnDef="duration">
        <th mat-header-cell *matHeaderCellDef>{{ 'pa.calls.duration' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr">{{ row.durationMs }} ms</td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
    @if ((page.value()?.items ?? []).length === 0 && !page.isLoading()) {
      <p class="ac-muted">{{ 'pa.calls.empty' | dcTranslate }}</p>
    }
    <mat-paginator
      [length]="page.value()?.total ?? 0"
      [pageSize]="take()"
      [pageSizeOptions]="[25, 50, 100]"
      (page)="paginate($event)"
    />
  `,
  styles: `
    .ac-muted {
      margin: 2px 0 0;
      color: var(--dga-text-3, #6c737f);
      font-size: 13px;
    }
    .ac-path {
      max-width: 32rem;
      font-family: ui-monospace, monospace;
      font-size: 12px;
      overflow-wrap: anywhere;
    }
  `,
})
export class ApiCalls {
  private readonly api = inject(PublicApiAdmin);
  private readonly config = inject(AppsControlApi);
  protected readonly statuses = [2, 4, 5];
  protected readonly columns = ['startedAt', 'client', 'path', 'status', 'rows', 'duration'];
  protected readonly client = signal<string | null>(null);
  protected readonly endpoint = signal<string | null>(null);
  protected readonly status = signal<number | null>(null);
  protected readonly skip = signal(0);
  protected readonly take = signal(50);

  private readonly list = (type: string) =>
    toSignal(this.config.list(type).pipe(catchError(() => of([] as ConfigRow[]))), {
      initialValue: [] as ConfigRow[],
    });
  protected readonly clients = this.list('integ.api-client');
  protected readonly endpoints = this.list('integ.api-endpoint');

  protected readonly page = rxResource({
    params: () => ({
      client: this.client(),
      endpoint: this.endpoint(),
      status: this.status(),
      skip: this.skip(),
      take: this.take(),
    }),
    stream: ({ params }) => this.api.calls(params),
  });

  constructor() {
    inject(I18nService).register(PUBLIC_API_TRANSLATIONS);
  }

  protected filter<T>(target: { set(value: T): void }, value: T): void {
    this.skip.set(0);
    target.set(value);
  }

  protected paginate(event: PageEvent): void {
    this.take.set(event.pageSize);
    this.skip.set(event.pageIndex * event.pageSize);
  }
}
