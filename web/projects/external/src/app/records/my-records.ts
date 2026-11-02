import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';
import {
  ActingPartyService,
  I18nService,
  LocalizePipe,
  PartyRecord,
  RecordColumn,
  RecordList,
  RecordsApi,
  RowDialog,
  TranslatePipe,
  problemMessages,
  recordFields,
  recordTableColumns,
} from '@dynamic-cube/shared';
import { PageHero } from '../portal/page-hero';

/**
 * The record lists of the acting party (its projects, owners, branches…): records typed by hand are added, changed and
 * removed here; imported ones are fetched again from their source. Services let the applicant pick from them.
 */
@Component({
  selector: 'dc-my-records',
  imports: [DatePipe, MatButtonModule, MatIconModule, LocalizePipe, TranslatePipe, PageHero],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dc-page-hero [title]="'records.mine' | dcTranslate" [text]="'records.lead' | dcTranslate" />
    <div class="dc-portal-block dc-page-body">
      @for (list of lists.value() ?? []; track list.code) {
        <section class="dc-records" [attr.data-testid]="'record-list-' + list.code">
          <header class="dc-records__head">
            <div>
              <h2>{{ { ar: list.nameAr, en: list.nameEn } | dcLocalize }}</h2>
              @if (list.descriptionAr || list.descriptionEn) {
                <p class="dc-muted">
                  {{ { ar: list.descriptionAr ?? '', en: list.descriptionEn ?? '' } | dcLocalize }}
                </p>
              }
            </div>
            <div class="dc-records__actions">
              @if (list.canImport) {
                <button
                  matButton="outlined"
                  type="button"
                  (click)="import(list)"
                  [disabled]="busy()"
                  [attr.data-testid]="'record-import-' + list.code"
                >
                  <mat-icon>cloud_download</mat-icon>
                  {{ 'records.import' | dcTranslate }}
                </button>
              }
              @if (list.allowManual) {
                <button
                  matButton="filled"
                  type="button"
                  (click)="edit(list, null)"
                  [disabled]="busy()"
                  [attr.data-testid]="'record-add-' + list.code"
                >
                  <mat-icon>add</mat-icon>
                  {{ 'records.add' | dcTranslate }}
                </button>
              }
            </div>
          </header>
          @if (list.records.length) {
            <div class="dc-records__scroll">
              <table class="dc-table dc-table--portal dc-records__table">
                <thead>
                  <tr>
                    @for (c of shown(list); track c.code) {
                      <th scope="col">{{ { ar: c.labelAr, en: c.labelEn } | dcLocalize }}</th>
                    }
                    <th scope="col">{{ 'records.source' | dcTranslate }}</th>
                    <th scope="col">
                      <span class="cdk-visually-hidden">{{ 'rows.actions' | dcTranslate }}</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  @for (r of list.records; track r.id) {
                    <tr [attr.data-testid]="'record-' + r.id">
                      @for (c of shown(list); track c.code) {
                        <td>{{ cell(c, r.data[c.code]) }}</td>
                      }
                      <td>
                        @if (r.source === 'INTEGRATION') {
                          <span class="dc-badge dc-badge--info">{{
                            'records.imported' | dcTranslate
                          }}</span>
                          @if (r.importedAt) {
                            <small class="dc-muted" dir="ltr">
                              {{ r.importedAt | date: 'dd/MM/yyyy' }}</small
                            >
                          }
                        } @else {
                          <span class="dc-badge">{{ 'records.typed' | dcTranslate }}</span>
                        }
                      </td>
                      <td class="dc-table__actions">
                        @if (r.source === 'MANUAL' && list.allowManual) {
                          <button
                            matIconButton
                            type="button"
                            (click)="edit(list, r)"
                            [attr.aria-label]="'records.edit' | dcTranslate"
                          >
                            <mat-icon>edit</mat-icon>
                          </button>
                          <button
                            matIconButton
                            type="button"
                            (click)="remove(r)"
                            [attr.aria-label]="'records.remove' | dcTranslate"
                          >
                            <mat-icon>delete</mat-icon>
                          </button>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <p class="dc-empty">{{ 'rows.noRecords' | dcTranslate }}</p>
          }
        </section>
      } @empty {
        @if (!lists.isLoading()) {
          <p class="dc-empty">{{ 'records.none' | dcTranslate }}</p>
        }
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }
    .dc-records {
      margin-block-end: 32px;
    }
    .dc-records__head {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 16px;
      flex-wrap: wrap;
      margin-block-end: 12px;
    }
    .dc-records__head h2 {
      margin: 0 0 4px;
      font-size: 20px;
    }
    .dc-records__head p {
      margin: 0;
    }
    .dc-records__actions {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
    }
    .dc-records__scroll {
      overflow-x: auto;
    }
    .dc-records__table {
      width: 100%;
    }
  `,
})
export class MyRecords {
  private readonly api = inject(RecordsApi);
  private readonly acting = inject(ActingPartyService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly i18n = inject(I18nService);
  protected readonly busy = signal(false);
  protected readonly lists = rxResource({
    params: () => this.acting.current()?.partyId ?? 'none',
    stream: () => this.api.mine(),
  });

  protected shown(list: RecordList): RecordColumn[] {
    return recordTableColumns(list);
  }

  protected cell(column: RecordColumn, value: unknown): string {
    if (value === null || value === undefined || value === '') return '—';
    const ar = this.i18n.language() === 'ar';
    if (column.type === 'CHECKBOX') return value ? (ar ? 'نعم' : 'Yes') : ar ? 'لا' : 'No';
    const option = column.options.find((o) => o.value === value);
    return option ? (ar ? option.labelAr : option.labelEn) : String(value);
  }

  /** Adds a record (none given) or changes one typed by hand, through the list's form. */
  protected edit(list: RecordList, record: PartyRecord | null): void {
    const ref = this.dialog.open(RowDialog, {
      width: '720px',
      maxWidth: '95vw',
      disableClose: true,
      data: {
        title: { ar: list.nameAr, en: list.nameEn },
        columns: recordFields(list),
        row: record?.data ?? null,
        context: {},
        uploader: null,
      },
    });
    ref.afterClosed().subscribe(async (data?: Record<string, unknown>) => {
      if (!data) return;
      await this.run(
        () => (record ? this.api.update(record.id, data) : this.api.add(list.code, data)),
        'records.saved',
      );
    });
  }

  protected async remove(record: PartyRecord): Promise<void> {
    await this.run(() => this.api.remove(record.id), 'records.removed');
  }

  protected async import(list: RecordList): Promise<void> {
    this.busy.set(true);
    try {
      const result = await firstValueFrom(this.api.import(list.code));
      const ok = result.outcome === 'SUCCESS' || result.outcome === 'NOT_FOUND';
      const message = ok
        ? `${this.i18n.translate('records.importDone')} +${result.added} ~${result.updated} −${result.removed}`
        : ((this.i18n.language() === 'ar' ? result.messageAr : result.messageEn) ??
          this.i18n.translate('records.importFailed'));
      this.snack.open(message, undefined, { duration: 5000 });
      this.lists.reload();
    } catch (error) {
      this.snack.open(problemMessages(error).join(' '), undefined, { duration: 6000 });
    } finally {
      this.busy.set(false);
    }
  }

  private async run(action: () => import('rxjs').Observable<unknown>, done: string): Promise<void> {
    this.busy.set(true);
    try {
      await firstValueFrom(action());
      this.snack.open(this.i18n.translate(done), undefined, { duration: 3000 });
      this.lists.reload();
    } catch (error) {
      this.snack.open(problemMessages(error).join(' '), undefined, { duration: 6000 });
    } finally {
      this.busy.set(false);
    }
  }
}
