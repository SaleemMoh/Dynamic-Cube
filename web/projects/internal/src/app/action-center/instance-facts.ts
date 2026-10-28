import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { I18nService, InstanceFacts, TranslatePipe, fieldText } from '@dynamic-cube/shared';

interface DecisionView {
  name: string;
  values: { key: string; value: string }[];
  columns: { key: string; label: string }[];
  rows: Record<string, unknown>[];
}

/** Keys of a result that are bookkeeping rather than findings. */
const HIDDEN = new Set(['evaluationId', 'version']);

/**
 * Of a bilingual pair (`sectorNameAr` / `sectorNameEn`) keep the one in the reader's language, labelled without the
 * suffix; other keys stay as they are.
 */
export function visibleKeys(keys: string[], language: 'ar' | 'en'): { key: string; label: string }[] {
  const mine = language === 'ar' ? 'Ar' : 'En';
  const other = language === 'ar' ? 'En' : 'Ar';
  return keys
    .filter((k) => !(k.endsWith(other) && keys.includes(k.slice(0, -2) + mine)))
    .map((k) => ({ key: k, label: k.endsWith(mine) && keys.includes(k.slice(0, -2) + other) ? k.slice(0, -2) : k }));
}

/** What the workflow's automated steps found: rule-set results (e.g. grades per sector) and the documents issued. */
@Component({
  selector: 'dc-instance-facts',
  imports: [MatTableModule, RouterLink, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (decisions().length || documents().length) {
      <section class="dc-card dc-facts" data-testid="instance-facts">
        <h2>{{ 'facts.title' | dcTranslate }}</h2>
        @for (d of decisions(); track d.name) {
          <h3 dir="ltr" class="dc-facts__name">{{ d.name }}</h3>
          <dl class="dc-facts__values">
            @for (v of d.values; track v.key) {
              <div>
                <dt dir="ltr">{{ v.key }}</dt>
                <dd>{{ v.value }}</dd>
              </div>
            }
          </dl>
          @if (d.rows.length) {
            <table mat-table [dataSource]="d.rows" class="dc-table" [attr.data-testid]="'facts-' + d.name">
              @for (c of d.columns; track c.key) {
                <ng-container [matColumnDef]="c.key">
                  <th mat-header-cell *matHeaderCellDef dir="ltr">{{ c.label }}</th>
                  <td mat-cell *matCellDef="let row">{{ text(row[c.key]) }}</td>
                </ng-container>
              }
              <tr mat-header-row *matHeaderRowDef="codes(d)"></tr>
              <tr mat-row *matRowDef="let row; columns: codes(d)"></tr>
            </table>
          }
        }
        @if (documents().length) {
          <h3>{{ 'facts.documents' | dcTranslate }}</h3>
          <ul class="dc-facts__docs">
            @for (doc of documents(); track doc.id) {
              <li>
                <a class="dc-link" dir="ltr" [routerLink]="['/documents', doc.id]">{{ doc.number }}</a>
                <span class="dc-muted">· {{ 'documents.version' | dcTranslate }} {{ doc.version }}</span>
              </li>
            }
          </ul>
        }
      </section>
    }
  `,
  styles: `
    .dc-facts {
      margin-bottom: 1rem;
    }
    .dc-facts h2 {
      margin: 0;
      font-size: 18px;
    }
    .dc-facts h3 {
      margin: 1rem 0 0.5rem;
      font-size: 15px;
    }
    .dc-facts__name {
      font-family: ui-monospace, monospace;
      text-align: start;
    }
    .dc-facts__values {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(10rem, 1fr));
      gap: 0.5rem 1rem;
      margin: 0 0 0.75rem;
    }
    .dc-facts__values dt {
      color: var(--dga-gray-500, #6c737f);
      font-size: 12px;
      font-family: ui-monospace, monospace;
      text-align: start;
    }
    .dc-facts__values dd {
      margin: 0;
      font-weight: 600;
    }
    .dc-facts__docs {
      margin: 0;
      padding-inline-start: 1.25rem;
    }
  `,
})
export class InstanceFactsCard {
  readonly facts = input<InstanceFacts | null | undefined>();
  private readonly i18n = inject(I18nService);

  protected readonly decisions = computed<DecisionView[]>(() =>
    Object.entries(this.facts()?.decisions ?? {}).map(([name, output]) => {
      const values = Object.entries(output)
        .filter(([k, v]) => !HIDDEN.has(k) && (v === null || typeof v !== 'object'))
        .map(([key, value]) => ({ key, value: factText(value) }));
      const rows = Array.isArray(output['items']) ? (output['items'] as Record<string, unknown>[]) : [];
      const keys = [...new Set(rows.flatMap((r) => Object.keys(r)))];
      const columns = visibleKeys(keys, this.i18n.language());
      // Names first (the sector), then the figures.
      columns.sort((a, b) => Number(/name/i.test(b.label)) - Number(/name/i.test(a.label)));
      return {
        name,
        values: visibleKeys(
          values.map((v) => v.key),
          this.i18n.language(),
        ).map((k) => values.find((v) => v.key === k.key)!),
        columns,
        rows,
      };
    }),
  );

  protected readonly documents = computed(() => Object.values(this.facts()?.documents ?? {}));

  protected codes(d: DecisionView): string[] {
    return d.columns.map((c) => c.key);
  }

  protected text(value: unknown): string {
    return factText(value);
  }
}

/** A fact as text, with fractions rounded to two places. */
export function factText(value: unknown): string {
  return typeof value === 'number' && !Number.isInteger(value) ? String(Math.round(value * 100) / 100) : fieldText(value);
}
