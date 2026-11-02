import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import {
  ItemDecision,
  ItemVerdict,
  TaskItemView,
  TranslatePipe,
  WorkflowApi,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';

/**
 * The items a task decides one by one (e.g. each sector or project of the request): each approved or rejected, a
 * rejection with its reason, which the applicant reads.
 */
@Component({
  selector: 'dc-task-items',
  imports: [
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="dc-card dc-task-items" data-testid="task-items">
      <header class="dc-task-items__head">
        <div>
          <h2>{{ 'taskItems.title' | dcTranslate }}</h2>
          <p class="dc-muted">
            {{ 'taskItems.decided' | dcTranslate }} {{ decidedCount() }} / {{ items().length }} ·
            {{ 'taskItems.approved' | dcTranslate }} {{ approvedCount() }}
          </p>
        </div>
        @if (editable()) {
          <div class="dc-task-items__tools">
            <button
              matButton
              type="button"
              (click)="all('APPROVED')"
              data-testid="task-items-approve-all"
            >
              <mat-icon>done_all</mat-icon>
              {{ 'taskItems.approveAll' | dcTranslate }}
            </button>
            <button
              matButton="outlined"
              type="button"
              (click)="save()"
              [disabled]="saving()"
              data-testid="task-items-save"
            >
              <mat-icon fontSet="material-symbols-outlined">save</mat-icon>
              {{ 'taskInputs.save' | dcTranslate }}
            </button>
          </div>
        }
      </header>
      <ul class="dc-task-items__list">
        @for (item of items(); track item.key) {
          <li
            [class.dc-task-items--yes]="verdict(item.key) === 'APPROVED'"
            [class.dc-task-items--no]="verdict(item.key) === 'REJECTED'"
            [attr.data-testid]="'task-item-' + item.key"
          >
            <div class="dc-task-items__row">
              <strong dir="auto">{{ item.label }}</strong>
              <mat-button-toggle-group
                [value]="verdict(item.key)"
                (change)="decide(item.key, $event.value)"
                [disabled]="!editable()"
                [attr.aria-label]="item.label"
              >
                <mat-button-toggle value="APPROVED" data-testid="item-approve">
                  <mat-icon>check</mat-icon>
                  {{ 'taskItems.approve' | dcTranslate }}
                </mat-button-toggle>
                <mat-button-toggle value="REJECTED" data-testid="item-reject">
                  <mat-icon>close</mat-icon>
                  {{ 'taskItems.reject' | dcTranslate }}
                </mat-button-toggle>
              </mat-button-toggle-group>
            </div>
            @if (verdict(item.key) === 'REJECTED') {
              <mat-form-field subscriptSizing="dynamic" class="dc-task-items__reason">
                <mat-label>{{ 'taskItems.reason' | dcTranslate }}</mat-label>
                <input
                  matInput
                  dir="auto"
                  required
                  [ngModel]="reason(item.key)"
                  (ngModelChange)="explain(item.key, $event)"
                  [disabled]="!editable()"
                  data-testid="item-reason"
                />
                <mat-hint>{{ 'taskItems.reasonHint' | dcTranslate }}</mat-hint>
              </mat-form-field>
            }
          </li>
        }
      </ul>
    </section>
  `,
  styles: `
    .dc-task-items {
      margin-bottom: 1rem;
    }
    .dc-task-items__head {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 8px;
    }
    .dc-task-items__head h2 {
      margin: 0 0 2px;
      font-size: 16px;
    }
    .dc-task-items__head p {
      margin: 0;
    }
    .dc-task-items__tools {
      display: flex;
      gap: 8px;
    }
    .dc-task-items__list {
      display: grid;
      gap: 8px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .dc-task-items__list li {
      display: grid;
      gap: 8px;
      padding: 10px 12px;
      border: 1px solid var(--dga-gray-200);
      border-inline-start-width: 4px;
      border-radius: var(--shell-radius);
    }
    .dc-task-items__list li.dc-task-items--yes {
      border-inline-start-color: var(--dga-success-600, #079455);
    }
    .dc-task-items__list li.dc-task-items--no {
      border-inline-start-color: var(--mat-sys-error);
    }
    .dc-task-items__row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }
    .dc-task-items__reason {
      width: 100%;
    }
  `,
})
export class TaskItemsCard {
  readonly taskId = input.required<string>();
  readonly items = input.required<TaskItemView[]>();
  readonly editable = input(false);
  readonly saved = output<void>();

  private readonly api = inject(WorkflowApi);
  private readonly decisions = signal<
    Record<string, { decision: ItemVerdict | null; reason: string | null }>
  >({});
  protected readonly saving = signal(false);

  protected readonly decidedCount = computed(
    () => Object.values(this.decisions()).filter((d) => d.decision).length,
  );
  protected readonly approvedCount = computed(
    () => Object.values(this.decisions()).filter((d) => d.decision === 'APPROVED').length,
  );

  /** Items still to decide, and rejections without a reason: moving on needs neither. */
  readonly missing = computed(() =>
    this.items()
      .filter((i) => {
        const d = this.decisions()[i.key];
        return !d?.decision || (d.decision === 'REJECTED' && !d.reason?.trim());
      })
      .map((i) => i.label),
  );

  constructor() {
    effect(() => {
      const items = this.items();
      untracked(() =>
        this.decisions.set(
          Object.fromEntries(items.map((i) => [i.key, { decision: i.decision, reason: i.reason }])),
        ),
      );
    });
  }

  /** The decisions, for the action. */
  value(): ItemDecision[] {
    return Object.entries(this.decisions())
      .filter(([, d]) => d.decision)
      .map(([key, d]) => ({ key, decision: d.decision, reason: d.reason?.trim() || null }));
  }

  protected verdict(key: string): ItemVerdict | null {
    return this.decisions()[key]?.decision ?? null;
  }

  protected reason(key: string): string {
    return this.decisions()[key]?.reason ?? '';
  }

  protected decide(key: string, decision: ItemVerdict): void {
    this.decisions.update((all) => ({
      ...all,
      [key]: { decision, reason: all[key]?.reason ?? null },
    }));
  }

  protected explain(key: string, reason: string): void {
    this.decisions.update((all) => ({
      ...all,
      [key]: { decision: all[key]?.decision ?? null, reason },
    }));
  }

  protected all(decision: ItemVerdict): void {
    this.decisions.update((all) =>
      Object.fromEntries(
        Object.entries(all).map(([key, d]) => [
          key,
          d.decision ? d : { decision, reason: d.reason },
        ]),
      ),
    );
  }

  protected async save(): Promise<void> {
    this.saving.set(true);
    try {
      await firstValueFrom(this.api.saveInputs(this.taskId(), null, this.value()));
      this.saved.emit();
    } finally {
      this.saving.set(false);
    }
  }
}
