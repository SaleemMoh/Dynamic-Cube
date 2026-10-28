import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { AppsControlApi, LocalizePipe, TranslatePipe } from '@dynamic-cube/shared';
import { PrivilegesService } from '../privileges.service';
import { ClassRow } from './class-models';

/** Lookup types (classes). Opening one shows its attribute definitions and values. */
@Component({
  selector: 'dc-class-list',
  imports: [MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatTableModule, MatTooltipModule, RouterLink, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'classes.title' | dcTranslate }}</h1>
      @if (privileges.can('apps-control.classes', 'CREATE')) {
        <a matButton="filled" routerLink="new" data-testid="new-class">
          <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
          {{ 'classes.new' | dcTranslate }}
        </a>
      }
    </header>
    <p class="dc-muted">{{ 'classes.hint' | dcTranslate }}</p>
    <mat-form-field class="dc-search" subscriptSizing="dynamic">
      <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
      <input matInput [placeholder]="'config.search' | dcTranslate" (input)="search.set($any($event.target).value)" />
    </mat-form-field>
    <table mat-table [dataSource]="rows()" class="dc-table">
      <ng-container matColumnDef="code">
        <th mat-header-cell *matHeaderCellDef>{{ 'config.code' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr">
          <a [routerLink]="[row.code]" class="dc-link" [attr.data-testid]="'class-' + row.code">{{ row.code }}</a>
        </td>
      </ng-container>
      <ng-container matColumnDef="name">
        <th mat-header-cell *matHeaderCellDef>{{ 'config.name' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row">{{ { ar: row.nameAr, en: row.nameEn } | dcLocalize }}</td>
      </ng-container>
      <ng-container matColumnDef="attributes">
        <th mat-header-cell *matHeaderCellDef>{{ 'classes.attributes' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row">{{ row.attributes.length }}</td>
      </ng-container>
      <ng-container matColumnDef="parent">
        <th mat-header-cell *matHeaderCellDef>{{ 'classes.parentClass' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr">{{ row.parentClassCode }}</td>
      </ng-container>
      <ng-container matColumnDef="state">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let row">
          @if (row.owner === 'SYSTEM') {
            <mat-icon fontSet="material-symbols-outlined" [matTooltip]="'config.system' | dcTranslate">lock</mat-icon>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
  `,
  styles: `
    .dc-search {
      width: 24rem;
      max-width: 100%;
      margin-block: 0.5rem 1rem;
    }
  `,
})
export class ClassList {
  protected readonly privileges = inject(PrivilegesService);
  private readonly api = inject(AppsControlApi);
  protected readonly search = signal('');
  protected readonly columns = ['code', 'name', 'attributes', 'parent', 'state'];
  private readonly list = rxResource({ stream: () => this.api.list('apps.class') });

  protected readonly rows = computed(() => {
    const rows = (this.list.hasValue() ? this.list.value() : []) as ClassRow[];
    const term = this.search().trim().toLowerCase();
    return term ? rows.filter((r) => `${r.code} ${r.nameAr} ${r.nameEn}`.toLowerCase().includes(term)) : rows;
  });
}
