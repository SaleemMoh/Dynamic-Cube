import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { LocalizePipe, TranslatePipe, WorkflowApi, problemMessages } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';

/** Out of office: who acts on my tasks between two dates. The delegate sees my tasks in their own inbox. */
@Component({
  selector: 'dc-delegations-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'delegations.title' | dcTranslate }}</h2>
    <mat-dialog-content>
      <p class="dc-muted">{{ 'delegations.hint' | dcTranslate }}</p>
      <ul class="dc-delegations" data-testid="delegation-list">
        @for (d of delegations.value() ?? []; track d.id) {
          <li>
            <span>
              <strong>{{ d.delegatorName }}</strong> → <strong>{{ d.delegateName }}</strong>
              <span class="dc-muted" dir="ltr">{{ d.fromDate }} – {{ d.toDate }}</span>
              @if (d.isCurrent) {
                <span class="dc-badge dc-badge--success">{{ 'delegations.current' | dcTranslate }}</span>
              }
              @if (d.reason) {
                <span class="dc-muted">· {{ d.reason }}</span>
              }
            </span>
            <button matIconButton (click)="cancel(d.id)" [attr.aria-label]="'delegations.cancel' | dcTranslate">
              <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
            </button>
          </li>
        } @empty {
          <li class="dc-muted">{{ 'delegations.none' | dcTranslate }}</li>
        }
      </ul>
      <h3>{{ 'delegations.new' | dcTranslate }}</h3>
      <div class="dc-delegation-form">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'delegations.delegate' | dcTranslate }}</mat-label>
          <mat-select [(ngModel)]="delegate" data-testid="delegate">
            @for (s of staff.value() ?? []; track s.userName) {
              <mat-option [value]="s.userName">{{ { ar: s.nameAr, en: s.nameEn } | dcLocalize }} ({{ s.userName }})</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'delegations.from' | dcTranslate }}</mat-label>
          <input matInput type="date" [(ngModel)]="fromDate" data-testid="delegation-from" />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'delegations.to' | dcTranslate }}</mat-label>
          <input matInput type="date" [(ngModel)]="toDate" data-testid="delegation-to" />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic" class="dc-delegation-form__wide">
          <mat-label>{{ 'delegations.reason' | dcTranslate }}</mat-label>
          <input matInput [(ngModel)]="reason" />
        </mat-form-field>
      </div>
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li dir="auto">{{ e }}</li>
          }
        </ul>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close>{{ 'common.close' | dcTranslate }}</button>
      <button matButton="filled" [disabled]="!delegate || !fromDate || !toDate" (click)="add()" data-testid="add-delegation">
        {{ 'delegations.add' | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-delegations {
      display: grid;
      gap: 0.5rem;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .dc-delegations li {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
    }
    .dc-delegation-form {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 0.75rem;
    }
    .dc-delegation-form__wide {
      grid-column: 1 / -1;
    }
    @media (max-width: 600px) {
      .dc-delegation-form {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class DelegationsDialog {
  private readonly api = inject(WorkflowApi);
  protected readonly delegations = rxResource({ stream: () => this.api.delegations() });
  protected readonly staff = rxResource({ stream: () => this.api.staff() });
  protected readonly errors = signal<string[]>([]);
  protected delegate = '';
  protected fromDate = today();
  protected toDate = '';
  protected reason = '';

  protected async add(): Promise<void> {
    this.errors.set([]);
    try {
      await firstValueFrom(
        this.api.delegate({
          delegate: this.delegate,
          fromDate: this.fromDate,
          toDate: this.toDate,
          reason: this.reason.trim() || null,
        }),
      );
      this.delegate = '';
      this.reason = '';
      this.delegations.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  protected async cancel(id: string): Promise<void> {
    this.errors.set([]);
    try {
      await firstValueFrom(this.api.cancelDelegation(id));
      this.delegations.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}

function today(): string {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
}
