import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { BusinessAppContext, ESTABLISHMENT_IDENTIFIER_TYPES, FormValue, I18nService, INDIVIDUAL_IDENTIFIER_TYPES, LocalizePipe, PartyApi, PartyKind, TranslatePipe } from '@dynamic-cube/shared';
import { debounceTime, firstValueFrom } from 'rxjs';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormDialog, FormDialogData } from '../common/form-dialog';
import { PrivilegesService } from '../apps-control/privileges.service';

/** Individuals and establishments: search by name or identifier, register manually. */
@Component({
  selector: 'dc-party-list',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
    RouterLink,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'party.parties' | dcTranslate }}</h1>
      @if (privileges.can('party.parties', 'CREATE')) {
        <button matButton="filled" (click)="register()" data-testid="register-party">
          <mat-icon fontSet="material-symbols-outlined">person_add</mat-icon>
          {{ 'party.register' | dcTranslate }}
        </button>
      }
    </header>
    <div class="dc-toolbar">
      <mat-form-field class="dc-toolbar__search" subscriptSizing="dynamic">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input matInput [placeholder]="'party.searchHint' | dcTranslate" [ngModel]="search()" (ngModelChange)="search.set($event); skip.set(0)" data-testid="party-search" />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-select [ngModel]="kind()" (ngModelChange)="kind.set($event); skip.set(0)" [attr.aria-label]="'party.kind' | dcTranslate">
          <mat-option [value]="null">{{ 'party.allKinds' | dcTranslate }}</mat-option>
          <mat-option value="INDIVIDUAL">{{ 'party.kind.INDIVIDUAL' | dcTranslate }}</mat-option>
          <mat-option value="ESTABLISHMENT">{{ 'party.kind.ESTABLISHMENT' | dcTranslate }}</mat-option>
        </mat-select>
      </mat-form-field>
    </div>
    <table mat-table [dataSource]="page.value()?.items ?? []" class="dc-table">
      <ng-container matColumnDef="name">
        <th mat-header-cell *matHeaderCellDef>{{ 'config.name' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row">
          <a [routerLink]="[row.id]" class="dc-link" [attr.data-testid]="'party-' + row.identifier">{{ { ar: row.nameAr, en: row.nameEn } | dcLocalize }}</a>
        </td>
      </ng-container>
      <ng-container matColumnDef="kind">
        <th mat-header-cell *matHeaderCellDef>{{ 'party.kind' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row">{{ 'party.kind.' + row.kind | dcTranslate }}</td>
      </ng-container>
      <ng-container matColumnDef="identifier">
        <th mat-header-cell *matHeaderCellDef>{{ 'party.identifier' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row"><span dir="ltr">{{ row.identifier }}</span> <span class="dc-muted">{{ 'identifier.' + row.identifierType | dcTranslate }}</span></td>
      </ng-container>
      <ng-container matColumnDef="source">
        <th mat-header-cell *matHeaderCellDef>{{ 'party.source' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row">{{ 'source.' + row.source | dcTranslate }}</td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
    <mat-paginator [length]="page.value()?.total ?? 0" [pageSize]="take()" [pageSizeOptions]="[25, 50, 100]" (page)="paginate($event)" />
  `,
})
export class PartyList {
  protected readonly privileges = inject(PrivilegesService);
  private readonly appContext = inject(BusinessAppContext);
  private readonly api = inject(PartyApi);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(I18nService);
  private readonly router = inject(Router);
  protected readonly columns = ['name', 'kind', 'identifier', 'source'];
  protected readonly search = signal('');
  private readonly debounced = toSignal(toObservable(this.search).pipe(debounceTime(300)), { initialValue: '' });
  protected readonly kind = signal<PartyKind | null>(null);
  protected readonly skip = signal(0);
  protected readonly take = signal(50);

  protected readonly page = rxResource({
    params: () => ({ search: this.debounced().trim() || null, kind: this.kind(), app: this.appContext.current(), skip: this.skip(), take: this.take() }),
    stream: ({ params }) => this.api.search(params.search, params.kind, params.skip, params.take, params.app),
  });

  protected paginate(event: PageEvent): void {
    this.take.set(event.pageSize);
    this.skip.set(event.pageIndex * event.pageSize);
  }

  protected register(): void {
    const tr = (key: string) => ({ ar: this.i18n.translate(key, 'ar'), en: this.i18n.translate(key, 'en') });
    const identifierOptions = (types: string[]) => types.map((t) => ({ value: t, label: tr('identifier.' + t) }));
    let created = '';
    const data: FormDialogData = {
      title: this.i18n.translate('party.register'),
      submitLabel: this.i18n.translate('party.register'),
      initialValue: { kind: 'ESTABLISHMENT', identifierType: 'CR_NUMBER' },
      schema: {
        code: 'register-party',
        sections: [
          {
            code: 'party',
            title: tr('party.party'),
            fields: [
              { code: 'kind', type: 'select', label: tr('party.kind'), required: true, options: [{ value: 'INDIVIDUAL', label: tr('party.kind.INDIVIDUAL') }, { value: 'ESTABLISHMENT', label: tr('party.kind.ESTABLISHMENT') }] },
              { code: 'identifierType', type: 'select', label: tr('party.identifierType'), required: true, options: identifierOptions([...ESTABLISHMENT_IDENTIFIER_TYPES, ...INDIVIDUAL_IDENTIFIER_TYPES]) },
              { code: 'identifier', type: 'text', label: tr('party.identifier'), required: true },
              { code: 'nameAr', type: 'text', label: tr('config.nameAr'), required: true },
              { code: 'nameEn', type: 'text', label: tr('config.nameEn'), required: true },
            ],
          },
        ],
      },
      submit: async (value: FormValue) => {
        created = (await firstValueFrom(this.api.register(value as never))).id;
      },
    };
    this.dialog
      .open(FormDialog, { data, width: '36rem', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((ok) => {
        if (ok && created) void this.router.navigate(['/party/parties', created]);
      });
  }
}
