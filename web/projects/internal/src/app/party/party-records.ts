import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';
import {
  I18nService,
  LocalizePipe,
  RecordColumn,
  RecordList,
  RecordsApi,
  TranslatePipe,
  problemMessages,
  recordTableColumns,
} from '@dynamic-cube/shared';

/** A party's record lists for staff (read only); imported lists can be fetched again from their source. */
@Component({
  selector: 'dc-party-records',
  imports: [DatePipe, MatButtonModule, MatIconModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (list of lists.value() ?? []; track list.code) {
      <section class="dc-party-records" [attr.data-testid]="'party-record-list-' + list.code">
        <header class="dc-party-records__head">
          <h3>
            {{ { ar: list.nameAr, en: list.nameEn } | dcLocalize }}
            <span class="dc-muted">({{ list.records.length }})</span>
          </h3>
          @if (list.canImport && canImport()) {
            <button matButton="outlined" type="button" (click)="import(list)" [disabled]="busy()">
              <mat-icon>cloud_download</mat-icon>
              {{ 'partyRecords.import' | dcTranslate }}
            </button>
          }
        </header>
        @if (list.records.length) {
          <div class="dc-party-records__scroll">
            <table class="dc-table">
              <thead>
                <tr>
                  @for (c of list.columns; track c.code) {
                    <th>{{ { ar: c.labelAr, en: c.labelEn } | dcLocalize }}</th>
                  }
                  <th>{{ 'partyRecords.source' | dcTranslate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (r of list.records; track r.id) {
                  <tr>
                    @for (c of list.columns; track c.code) {
                      <td>{{ cell(c, r.data[c.code]) }}</td>
                    }
                    <td>
                      {{
                        (r.source === 'INTEGRATION'
                          ? 'partyRecords.imported'
                          : 'partyRecords.typed'
                        ) | dcTranslate
                      }}
                      @if (r.importedAt) {
                        <small class="dc-muted" dir="ltr">{{
                          r.importedAt | date: 'dd/MM/yyyy'
                        }}</small>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else {
          <p class="dc-muted">{{ 'partyRecords.none' | dcTranslate }}</p>
        }
      </section>
    } @empty {
      @if (!lists.isLoading()) {
        <p class="dc-muted">{{ 'partyRecords.noLists' | dcTranslate }}</p>
      }
    }
  `,
  styles: `
    .dc-party-records {
      margin-block: 12px 24px;
    }
    .dc-party-records__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }
    .dc-party-records__head h3 {
      margin: 0;
      font-size: 16px;
    }
    .dc-party-records__scroll {
      overflow-x: auto;
    }
  `,
})
export class PartyRecords {
  readonly partyId = input.required<string>();
  readonly canImport = input(false);
  private readonly api = inject(RecordsApi);
  private readonly snack = inject(MatSnackBar);
  private readonly i18n = inject(I18nService);
  protected readonly busy = signal(false);
  protected readonly lists = rxResource({
    params: () => this.partyId(),
    stream: ({ params }) => this.api.ofParty(params),
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

  protected async import(list: RecordList): Promise<void> {
    this.busy.set(true);
    try {
      const result = await firstValueFrom(this.api.importForParty(this.partyId(), list.code));
      this.snack.open(
        `${this.i18n.translate('partyRecords.done')} +${result.added} ~${result.updated} −${result.removed}`,
        undefined,
        { duration: 4000 },
      );
      this.lists.reload();
    } catch (error) {
      this.snack.open(problemMessages(error).join(' '), undefined, { duration: 6000 });
    } finally {
      this.busy.set(false);
    }
  }
}
