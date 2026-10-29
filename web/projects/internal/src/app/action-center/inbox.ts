import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
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
import { ActivatedRoute, Router } from '@angular/router';
import { I18nService, InboxBox, InboxCounts, TaskSummary, TranslatePipe, WorkflowApi, problemMessages } from '@dynamic-cube/shared';
import { debounceTime, firstValueFrom } from 'rxjs';
import { DelegationsDialog } from './delegations-dialog';
import { TaskTable } from './task-table';

const BOXES: { box: InboxBox; count: keyof InboxCounts; icon: string }[] = [
  { box: 'MINE', count: 'mine', icon: 'assignment_ind' },
  { box: 'QUEUE', count: 'queue', icon: 'inbox' },
  { box: 'DISTRIBUTE', count: 'distribute', icon: 'call_split' },
  { box: 'ESCALATED', count: 'escalated', icon: 'running_with_errors' },
];

/**
 * The employee's Action Center: four boxes (my tasks, the queue to claim from, tasks to distribute, escalations) shown
 * as cards with their counts, then the chosen box's tasks.
 */
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
    TaskTable,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header dc-ac__head">
      <div>
        <h1>{{ 'ac.title' | dcTranslate }}</h1>
        <p>{{ 'ac.lead' | dcTranslate }}</p>
      </div>
      <div class="dc-ac__actions">
        <button matButton="outlined" (click)="refresh()" data-testid="inbox-refresh">
          <mat-icon fontSet="material-symbols-outlined">refresh</mat-icon>
          {{ 'ac.refresh' | dcTranslate }}
        </button>
        <button matButton="outlined" (click)="openDelegations()" data-testid="delegations">
          <mat-icon fontSet="material-symbols-outlined">event_busy</mat-icon>
          {{ 'delegations.title' | dcTranslate }}
        </button>
      </div>
    </header>

    <div class="dc-ac__boxes" role="tablist" [attr.aria-label]="'ac.title' | dcTranslate">
      @for (b of boxes; track b.box) {
        <button
          type="button"
          role="tab"
          class="dc-ac__box"
          [class.dc-ac__box--active]="box() === b.box"
          [class.dc-ac__box--alert]="b.box === 'ESCALATED' && countOf(b) > 0"
          [attr.aria-selected]="box() === b.box"
          (click)="choose($index)"
          [attr.data-testid]="'box-' + b.box"
        >
          <span class="dc-ac__icon" aria-hidden="true"
            ><mat-icon fontSet="material-symbols-outlined">{{ b.icon }}</mat-icon></span
          >
          <span class="dc-ac__boxtext">
            <strong [attr.data-testid]="'count-' + b.box">{{ countOf(b) }}</strong>
            <span>{{ 'inbox.' + b.box | dcTranslate }}</span>
          </span>
        </button>
      }
    </div>

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
      <span class="dc-toolbar__spacer"></span>
      <span class="dc-muted dc-ac__hint">
        <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">info</mat-icon>
        {{ 'inbox.hint.' + box() | dcTranslate }}
      </span>
    </div>
    @if (page.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    <dc-task-table [tasks]="page.value()?.items ?? []" [claimable]="box() === 'QUEUE'" [emptyKey]="'ac.empty.' + box()" (claim)="claim($event)" />
    @if ((page.value()?.total ?? 0) > take()) {
      <mat-paginator [length]="page.value()?.total ?? 0" [pageSize]="take()" [pageSizeOptions]="[25, 50, 100]" (page)="paginate($event)" />
    }
  `,
  styles: `
    .dc-ac__head {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      justify-content: space-between;
      gap: 12px;
    }
    .dc-ac__actions {
      display: flex;
      gap: 8px;
    }
    .dc-ac__boxes {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: 12px;
      margin-bottom: 12px;
    }
    .dc-ac__box {
      position: relative;
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 12px 14px;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius-lg);
      background: #fff;
      box-shadow: var(--shell-shadow-sm);
      font: inherit;
      text-align: start;
      cursor: pointer;
      transition:
        border-color 120ms ease,
        box-shadow 120ms ease;
    }
    .dc-ac__box:hover {
      border-color: var(--dga-gray-300);
      box-shadow: var(--shell-shadow-md);
    }
    .dc-ac__box--active {
      border-color: var(--dynamic-cube-gold);
      background: var(--dynamic-cube-gold-50);
    }
    .dc-ac__box--active::before {
      content: '';
      position: absolute;
      inset-block: 10px;
      inset-inline-start: -1px;
      width: 3px;
      border-radius: 9999px;
      background: var(--dynamic-cube-gold);
    }
    .dc-ac__icon {
      display: grid;
      place-items: center;
      flex: none;
      width: 40px;
      height: 40px;
      border-radius: var(--shell-radius);
      background: var(--dynamic-cube-gold-50);
      color: var(--dynamic-cube-gold-700);
    }
    .dc-ac__box--active .dc-ac__icon {
      background: #fff;
    }
    .dc-ac__box--alert .dc-ac__icon {
      background: #fef3f2;
      color: #b42318;
    }
    .dc-ac__box--alert strong {
      color: #b42318;
    }
    .dc-ac__boxtext {
      display: grid;
      gap: 2px;
      min-width: 0;
    }
    .dc-ac__boxtext strong {
      font-size: 22px;
      line-height: 1.1;
      color: var(--dga-gray-900);
    }
    .dc-ac__boxtext span {
      overflow: hidden;
      color: var(--dga-gray-600);
      font-size: 13px;
      font-weight: 600;
      white-space: nowrap;
      text-overflow: ellipsis;
    }
    .dc-ac__hint {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      font-size: 13px;
    }
    .dc-ac__hint mat-icon {
      width: 18px;
      height: 18px;
      font-size: 18px;
    }
    @media (max-width: 900px) {
      .dc-ac__boxes {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
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

  constructor() {
    // A link can open a box (from the home page: ?box=QUEUE).
    const wanted = BOXES.findIndex((b) => b.box === inject(ActivatedRoute).snapshot.queryParamMap.get('box'));
    if (wanted >= 0) this.choose(wanted);
  }

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

  protected refresh(): void {
    this.page.reload();
    this.counts.reload();
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
