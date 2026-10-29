import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { TranslatePipe } from '@dynamic-cube/shared';

export interface FilterOption {
  value: string;
  label: string;
}

/** The DGA filter panel above a table: a status list, a search field and the dark search button. */
@Component({
  selector: 'dc-list-filter',
  imports: [FormsModule, MatFormFieldModule, MatIconModule, MatSelectModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dc-filter" role="search" (ngSubmit)="term.set(draft)">
      <div class="dc-filter__status">
        <span class="dc-field-label" id="dc-filter-status">{{ 'list.status' | dcTranslate }}</span>
        <mat-form-field subscriptSizing="dynamic">
          <mat-select aria-labelledby="dc-filter-status" [ngModel]="status()" (ngModelChange)="status.set($event)" name="status" data-testid="filter-status">
            <mat-option value="">{{ 'list.allStatuses' | dcTranslate }}</mat-option>
            @for (o of statuses(); track o.value) {
              <mat-option [value]="o.value">{{ o.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>
      <div class="dc-filter__search">
        <label class="dc-dga-search__field">
          <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">search</mat-icon>
          <input name="q" type="search" [(ngModel)]="draft" [placeholder]="placeholder()" [attr.aria-label]="placeholder()" data-testid="filter-search" />
        </label>
        <button type="submit" class="dc-dark-button">
          {{ 'portal.search' | dcTranslate }}
        </button>
      </div>
    </form>
  `,
  styles: `
    .dc-filter {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      gap: 16px 32px;
      padding: 24px;
      border-radius: 16px;
      background: var(--dga-gray-50);
    }
    .dc-filter__status {
      display: flex;
      flex: 1 1 18rem;
      flex-direction: column;
      gap: 8px;
    }
    .dc-filter__search {
      display: flex;
      flex: 1 1 24rem;
      gap: 16px;
    }
    .dc-filter__search .dc-dga-search__field {
      flex: 1;
    }
  `,
})
export class ListFilter {
  readonly statuses = input<FilterOption[]>([]);
  readonly placeholder = input('');
  readonly status = model('');
  readonly term = model('');
  protected draft = '';
}
