import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { SlaState, TranslatePipe, WorkflowApi } from '@dynamic-cube/shared';
import { debounceTime } from 'rxjs';
import { TaskTable } from './task-table';

/** Every open task, for supervisors: find late or unassigned work and reassign it from the task page. */
@Component({
  selector: 'dc-supervision',
  imports: [
    FormsModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    TaskTable,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'supervision.title' | dcTranslate }}</h1>
    </header>
    <p class="dc-muted">{{ 'supervision.hint' | dcTranslate }}</p>
    <div class="dc-toolbar">
      <mat-form-field class="dc-toolbar__search" subscriptSizing="dynamic">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input
          matInput
          [placeholder]="'requests.searchHint' | dcTranslate"
          [ngModel]="search()"
          (ngModelChange)="search.set($event); skip.set(0)"
          data-testid="supervision-search"
        />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-select
          [ngModel]="sla()"
          (ngModelChange)="sla.set($event); skip.set(0)"
          [attr.aria-label]="'tasks.due' | dcTranslate"
          data-testid="supervision-sla"
        >
          <mat-option [value]="null">{{ 'supervision.allSla' | dcTranslate }}</mat-option>
          @for (s of slaStates; track s) {
            <mat-option [value]="s">{{ 'sla.' + s | dcTranslate }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-checkbox [ngModel]="unassigned()" (ngModelChange)="unassigned.set($event); skip.set(0)" data-testid="supervision-unassigned">
        {{ 'supervision.unassignedOnly' | dcTranslate }}
      </mat-checkbox>
    </div>
    @if (page.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    <dc-task-table [tasks]="page.value()?.items ?? []" />
    <mat-paginator [length]="page.value()?.total ?? 0" [pageSize]="take()" [pageSizeOptions]="[25, 50, 100]" (page)="paginate($event)" />
  `,
})
export class Supervision {
  private readonly api = inject(WorkflowApi);
  protected readonly slaStates: SlaState[] = ['ON_TRACK', 'WARNING', 'BREACHED'];
  protected readonly search = signal('');
  private readonly debounced = toSignal(toObservable(this.search).pipe(debounceTime(300)), {
    initialValue: '',
  });
  protected readonly sla = signal<SlaState | null>(null);
  protected readonly unassigned = signal(false);
  protected readonly skip = signal(0);
  protected readonly take = signal(50);
  protected readonly page = rxResource({
    params: () => ({
      search: this.debounced().trim() || null,
      sla: this.sla(),
      unassigned: this.unassigned() || null,
      skip: this.skip(),
      take: this.take(),
    }),
    stream: ({ params }) => this.api.supervision(params, params.skip, params.take),
  });

  protected paginate(event: PageEvent): void {
    this.take.set(event.pageSize);
    this.skip.set(event.pageIndex * event.pageSize);
  }
}
