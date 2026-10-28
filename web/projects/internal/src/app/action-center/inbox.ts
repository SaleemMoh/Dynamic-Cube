import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { Router } from '@angular/router';
import { I18nService, InboxBox, InboxCounts, TaskSummary, TranslatePipe, WorkflowApi, problemMessages } from '@dynamic-cube/shared';
import { debounceTime, firstValueFrom } from 'rxjs';
import { DelegationsDialog } from './delegations-dialog';
import { TaskTable } from './task-table';

const BOXES: { box: InboxBox; count: keyof InboxCounts }[] = [
  { box: 'MINE', count: 'mine' },
  { box: 'QUEUE', count: 'queue' },
  { box: 'DISTRIBUTE', count: 'distribute' },
  { box: 'ESCALATED', count: 'escalated' },
];

/** The employee's Action Center: my tasks, the queue to claim from, tasks to distribute and escalations. */
@Component({
  selector: 'dc-inbox',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatTabsModule,
    TaskTable,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'inbox.title' | dcTranslate }}</h1>
      <button matButton="outlined" (click)="openDelegations()" data-testid="delegations">
        <mat-icon fontSet="material-symbols-outlined">event_busy</mat-icon>
        {{ 'delegations.title' | dcTranslate }}
      </button>
    </header>
    <mat-tab-group [selectedIndex]="index()" (selectedIndexChange)="choose($event)" mat-stretch-tabs="false" animationDuration="0">
      @for (b of boxes; track b.box) {
        <mat-tab>
          <ng-template mat-tab-label>
            <span [attr.data-testid]="'box-' + b.box">{{ 'inbox.' + b.box | dcTranslate }}</span>
            <span class="dc-count" [class.dc-count--alert]="b.box === 'ESCALATED' && countOf(b) > 0" [attr.data-testid]="'count-' + b.box">{{
              countOf(b)
            }}</span>
          </ng-template>
        </mat-tab>
      }
    </mat-tab-group>
    <p class="dc-muted dc-inbox__hint">{{ 'inbox.hint.' + box() | dcTranslate }}</p>
    <div class="dc-toolbar">
      <mat-form-field class="dc-toolbar__search" subscriptSizing="dynamic">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input
          matInput
          [placeholder]="'requests.searchHint' | dcTranslate"
          [ngModel]="search()"
          (ngModelChange)="search.set($event); skip.set(0)"
          data-testid="inbox-search"
        />
      </mat-form-field>
    </div>
    @if (page.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    <dc-task-table [tasks]="page.value()?.items ?? []" [claimable]="box() === 'QUEUE'" (claim)="claim($event)" />
    <mat-paginator [length]="page.value()?.total ?? 0" [pageSize]="take()" [pageSizeOptions]="[25, 50, 100]" (page)="paginate($event)" />
  `,
  styles: `
    .dc-count {
      display: inline-grid;
      place-items: center;
      min-width: 22px;
      height: 22px;
      margin-inline-start: 8px;
      padding: 0 6px;
      border-radius: 9999px;
      background: var(--dga-gray-100);
      font-size: 12px;
      font-weight: 600;
    }
    .dc-count--alert {
      background: #fef3f2;
      color: #b42318;
    }
    .dc-inbox__hint {
      margin: 0.75rem 0 0;
    }
  `,
})
export class Inbox {
  private readonly api = inject(WorkflowApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(I18nService);
  protected readonly boxes = BOXES;
  protected readonly box = signal<InboxBox>(sessionBox());
  protected readonly index = computed(() => BOXES.findIndex((b) => b.box === this.box()));
  protected readonly search = signal('');
  private readonly debounced = toSignal(toObservable(this.search).pipe(debounceTime(300)), {
    initialValue: '',
  });
  protected readonly skip = signal(0);
  protected readonly take = signal(50);
  protected readonly counts = rxResource({ stream: () => this.api.counts() });
  protected readonly page = rxResource({
    params: () => ({
      box: this.box(),
      search: this.debounced().trim() || null,
      skip: this.skip(),
      take: this.take(),
    }),
    stream: ({ params }) => this.api.inbox(params.box, params.search, params.skip, params.take),
  });

  protected countOf(b: (typeof BOXES)[number]): number {
    return this.counts.value()?.[b.count] ?? 0;
  }

  protected choose(index: number): void {
    const box = BOXES[index].box;
    this.box.set(box);
    this.skip.set(0);
    try {
      sessionStorage.setItem(BOX_KEY, box);
    } catch {
      // Remembering the tab is a convenience only.
    }
  }

  protected paginate(event: PageEvent): void {
    this.take.set(event.pageSize);
    this.skip.set(event.pageIndex * event.pageSize);
  }

  protected async claim(task: TaskSummary): Promise<void> {
    try {
      await firstValueFrom(this.api.claim(task.id));
      await this.router.navigate(['/action-center/tasks', task.id]);
    } catch (error) {
      this.snackBar.open(problemMessages(error).join(' '), this.i18n.translate('common.close'), {
        duration: 6000,
      });
      this.page.reload();
      this.counts.reload();
    }
  }

  protected openDelegations(): void {
    this.dialog.open(DelegationsDialog, { width: '44rem', maxWidth: '95vw' });
  }
}

const BOX_KEY = 'dc.internal.inbox.box';

function sessionBox(): InboxBox {
  try {
    const stored = sessionStorage.getItem(BOX_KEY) as InboxBox | null;
    return stored && BOXES.some((b) => b.box === stored) ? stored : 'MINE';
  } catch {
    return 'MINE';
  }
}
