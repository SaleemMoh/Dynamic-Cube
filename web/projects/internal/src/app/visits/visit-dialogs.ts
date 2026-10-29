import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { I18nService, LocalizePipe, TranslatePipe, VisitDetail, VisitFields, VisitsApi, missingVisitFields, problemMessages } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';

/** `yyyy-MM-ddTHH:mm` in local time, as a datetime-local input wants it. */
function localInput(at: Date): string {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${at.getFullYear()}-${p(at.getMonth() + 1)}-${p(at.getDate())}T${p(at.getHours())}:${p(at.getMinutes())}`;
}

/** Assigns the inspector and the date (the inspectors holding the type's role, least busy first). */
@Component({
  selector: 'dc-visit-schedule-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ (data.visit.inspector ? 'visits.reschedule' : 'visits.schedule') | dcTranslate }}</h2>
    <mat-dialog-content class="dc-visit-dialog">
      <p class="dc-muted">{{ 'visits.scheduleHint' | dcTranslate }}</p>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'visits.inspector' | dcTranslate }}</mat-label>
        <mat-select [ngModel]="inspector()" (ngModelChange)="inspector.set($event)" data-testid="schedule-inspector">
          @for (i of inspectors.value() ?? []; track i.userName) {
            <mat-option [value]="i.userName">{{ { ar: i.nameAr, en: i.nameEn } | dcLocalize }} · {{ load(i.openVisits) }}</mat-option>
          }
        </mat-select>
        @if (inspectors.hasValue() && !inspectors.value().length) {
          <mat-hint>{{ 'visits.noInspectors' | dcTranslate }}</mat-hint>
        }
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'visits.at' | dcTranslate }}</mat-label>
        <input matInput type="datetime-local" dir="ltr" [ngModel]="at()" (ngModelChange)="at.set($event)" data-testid="schedule-at" />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'visits.comment' | dcTranslate }}</mat-label>
        <input matInput [ngModel]="comment()" (ngModelChange)="comment.set($event)" />
      </mat-form-field>
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li>{{ e }}</li>
          }
        </ul>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close>{{ 'form.cancel' | dcTranslate }}</button>
      <button matButton="filled" [disabled]="!inspector() || !at() || busy()" (click)="save()" data-testid="schedule-save">
        {{ (data.visit.inspector ? 'visits.reschedule' : 'visits.schedule') | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-visit-dialog {
      display: grid;
      gap: 12px;
      min-width: min(28rem, 80vw);
    }
  `,
})
export class VisitScheduleDialog {
  protected readonly data = inject<VisitDetail>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<VisitScheduleDialog, VisitDetail>);
  private readonly api = inject(VisitsApi);
  private readonly i18n = inject(I18nService);
  protected readonly inspectors = rxResource({ stream: () => this.api.inspectors(this.data.visit.typeCode) });
  protected readonly inspector = signal<string | null>(this.data.visit.inspector);
  protected readonly at = signal(localInput(this.data.visit.scheduledAt ? new Date(this.data.visit.scheduledAt) : this.tomorrow()));
  protected readonly comment = signal('');
  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);

  protected load(count: number): string {
    return this.i18n.translate('visits.load').replace('{count}', String(count));
  }

  protected async save(): Promise<void> {
    this.busy.set(true);
    this.errors.set([]);
    try {
      const detail = await firstValueFrom(
        this.api.schedule(this.data.visit.id, this.inspector()!, new Date(this.at()).toISOString(), this.comment().trim() || null),
      );
      this.ref.close(detail);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }

  private tomorrow(): Date {
    const d = new Date();
    d.setDate(d.getDate() + 1);
    d.setHours(9, 0, 0, 0);
    return d;
  }
}

/** Records a visit's result by hand: the outcome, the type's fields and the report. */
@Component({
  selector: 'dc-visit-outcome-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, LocalizePipe, TranslatePipe, VisitFields],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'visits.record' | dcTranslate }}</h2>
    <mat-dialog-content class="dc-visit-dialog">
      <p class="dc-muted">{{ 'visits.recordHint' | dcTranslate }}</p>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'visits.outcome' | dcTranslate }}</mat-label>
        <mat-select [ngModel]="outcome()" (ngModelChange)="outcome.set($event)" data-testid="record-outcome">
          @for (o of data.type.outcomes; track o.code) {
            <mat-option [value]="o.code">{{ { ar: o.nameAr, en: o.nameEn } | dcLocalize }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <dc-visit-fields [fields]="data.type.fields" [value]="values()" [errors]="fieldErrors()" (valueChange)="values.set($event)" />
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'visits.report' | dcTranslate }}</mat-label>
        <textarea matInput rows="3" dir="auto" [ngModel]="report()" (ngModelChange)="report.set($event)" data-testid="record-report"></textarea>
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'visits.comment' | dcTranslate }}</mat-label>
        <input matInput [ngModel]="comment()" (ngModelChange)="comment.set($event)" />
      </mat-form-field>
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li>{{ e }}</li>
          }
        </ul>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close>{{ 'form.cancel' | dcTranslate }}</button>
      <button matButton="filled" [disabled]="!ready() || busy()" (click)="save()" data-testid="record-save">{{ 'visits.record' | dcTranslate }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-visit-dialog {
      display: grid;
      gap: 12px;
      min-width: min(32rem, 80vw);
    }
  `,
})
export class VisitOutcomeDialog {
  protected readonly data = inject<VisitDetail>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<VisitOutcomeDialog, VisitDetail>);
  private readonly api = inject(VisitsApi);
  protected readonly outcome = signal<string | null>(null);
  protected readonly values = signal<Record<string, unknown>>({});
  protected readonly report = signal('');
  protected readonly comment = signal('');
  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly fieldErrors = signal<Record<string, string>>({});
  protected readonly ready = computed(() => !!this.outcome() && missingVisitFields(this.data.type.fields, this.values()).length === 0);

  protected async save(): Promise<void> {
    this.busy.set(true);
    this.errors.set([]);
    try {
      const detail = await firstValueFrom(
        this.api.record(this.data.visit.id, this.outcome()!, this.values(), this.report().trim() || null, this.comment().trim() || null),
      );
      this.ref.close(detail);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }
}
