import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import {
  AppsControlApi,
  I18nService,
  LovItem,
  ReportFilterDef,
  TranslatePipe,
  SearchableSelectModule,
} from '@dynamic-cube/shared';
import { catchError, forkJoin, map, of } from 'rxjs';
import { joinValues, splitValues } from './report-model';
import { REPORT_TRANSLATIONS } from './report-translations';

const NONE: string[] = [];

/**
 * The fields of filters users fill (a dashboard's): text, number, date, yes/no, or a list of values (one or several,
 * offered from the list). Emits the whole set of values on each change.
 */
@Component({
  selector: 'dc-report-filter-fields',
  imports: [FormsModule, MatFormFieldModule, MatInputModule, SearchableSelectModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (f of filters(); track f.name) {
      <mat-form-field subscriptSizing="dynamic" [attr.data-filter]="f.name">
        <mat-label>{{ label(f) }}{{ f.required ? ' *' : '' }}</mat-label>
        @switch (f.type) {
          @case ('LIST') {
            @if (f.multiple) {
              <mat-select multiple [ngModel]="multi(f)" (ngModelChange)="setMulti(f.name, $event)">
                @for (o of options.value()?.[f.name] ?? []; track o.value) {
                  <mat-option [value]="o.value">{{ itemLabel(o) }}</mat-option>
                }
              </mat-select>
            } @else {
              <mat-select [ngModel]="value(f)" (ngModelChange)="set(f.name, $event)">
                <mat-option value="">{{ 'rp.any' | dcTranslate }}</mat-option>
                @for (o of options.value()?.[f.name] ?? []; track o.value) {
                  <mat-option [value]="o.value">{{ itemLabel(o) }}</mat-option>
                }
              </mat-select>
            }
          }
          @case ('YES_NO') {
            <mat-select [ngModel]="value(f)" (ngModelChange)="set(f.name, $event)">
              <mat-option value="">{{ 'rp.any' | dcTranslate }}</mat-option>
              <mat-option value="true">{{ 'rp.yes' | dcTranslate }}</mat-option>
              <mat-option value="false">{{ 'rp.no' | dcTranslate }}</mat-option>
            </mat-select>
          }
          @case ('DATE') {
            <input
              matInput
              type="date"
              [ngModel]="value(f)"
              (ngModelChange)="set(f.name, $event)"
            />
          }
          @case ('NUMBER') {
            <input
              matInput
              type="number"
              [ngModel]="value(f)"
              (ngModelChange)="set(f.name, $event === null ? null : '' + $event)"
            />
          }
          @default {
            <input matInput [ngModel]="value(f)" (ngModelChange)="set(f.name, $event)" />
          }
        }
        @if (errors()[f.name]; as e) {
          <mat-hint class="rff-error">{{ e }}</mat-hint>
        }
      </mat-form-field>
    }
  `,
  styles: `
    :host {
      display: contents;
    }
    .rff-error {
      color: var(--dga-error-600, #d92d20);
    }
  `,
})
export class ReportFilterFields {
  private readonly config = inject(AppsControlApi);
  private readonly i18n = inject(I18nService);
  readonly filters = input<ReportFilterDef[]>([]);
  readonly values = input<Record<string, string | null>>({});
  readonly errors = input<Record<string, string>>({});
  readonly changed = output<Record<string, string | null>>();

  /** The values of each list filter's list (up to 200). */
  protected readonly options = rxResource({
    params: () => this.filters().filter((f) => f.type === 'LIST' && f.lov),
    stream: ({ params }) =>
      params.length
        ? forkJoin(
            params.map((f) =>
              this.config.lovValues(f.lov!, { size: 200 }).pipe(
                map((page) => [f.name, page.items] as const),
                catchError(() => of([f.name, [] as LovItem[]] as const)),
              ),
            ),
          ).pipe(map((pairs) => Object.fromEntries(pairs) as Record<string, LovItem[]>))
        : of({} as Record<string, LovItem[]>),
  });

  /** One array per change of the values (a new array each check would loop). */
  private readonly multiValues = computed(() => {
    const values = this.values();
    return Object.fromEntries(
      Object.keys(values).map((k) => [k, splitValues(values[k])]),
    ) as Record<string, string[]>;
  });

  constructor() {
    this.i18n.register(REPORT_TRANSLATIONS);
  }

  protected label(f: ReportFilterDef): string {
    return this.i18n.language() === 'ar' ? f.labelAr : f.labelEn;
  }

  protected itemLabel(item: LovItem): string {
    return this.i18n.language() === 'ar' ? item.labelAr : item.labelEn;
  }

  protected value(f: ReportFilterDef): string {
    return this.values()[f.name] ?? '';
  }

  protected multi(f: ReportFilterDef): string[] {
    return this.multiValues()[f.name] ?? NONE;
  }

  protected set(name: string, value: string | null): void {
    this.changed.emit({ ...this.values(), [name]: value || null });
  }

  protected setMulti(name: string, values: string[]): void {
    this.set(name, joinValues(values));
  }
}
