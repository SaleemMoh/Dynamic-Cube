import { JsonPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { Clipboard } from '@angular/cdk/clipboard';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AppsControlApi, I18nService, LocalizePipe, PackageCheck, PackageItemCheck, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { ALL_CONFIG_SECTIONS } from '../config/module-sections';
import { problemLines } from './json-package';
import { PayloadDiffView } from './payload-diff-view';
import { templateEntities, withoutHelp } from './package-template';

type ItemStatus = 'new' | 'changed' | 'deleted' | 'unchanged' | 'problems';
const STATUSES: ItemStatus[] = ['new', 'changed', 'deleted', 'unchanged', 'problems'];

export interface PackageReviewData {
  fileName: string;
  text: string;
}

/**
 * Shows an uploaded package before anything is imported: its title and items, what each would add or change (field by
 * field against the row as it is) and every problem found by a dry run on the server. Import is offered only when
 * there are none; items that change nothing are skipped. Closes with the number of drafts created.
 */
@Component({
  selector: 'dc-package-review-dialog',
  imports: [JsonPipe, MatButtonModule, MatDialogModule, MatIconModule, MatProgressBarModule, MatSlideToggleModule, LocalizePipe, PayloadDiffView, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'packages.reviewTitle' | dcTranslate }}</h2>
    <mat-dialog-content class="dc-review">
      <p class="dc-muted mono">{{ data.fileName }}</p>
      @if (busy()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (problems().length) {
        <ul class="dc-errors" role="alert" data-testid="package-errors">
          @for (problem of problems(); track $index) {
            <li dir="auto">{{ problem }}</li>
          }
        </ul>
      }
      @if (check(); as check) {
        <div class="dc-review__summary">
          <div>
            <span class="dc-muted">{{ 'packages.packageTitle' | dcTranslate }}</span>
            <strong>{{ check.title || '—' }}</strong>
          </div>
          <div>
            <span class="dc-muted">{{ 'changeSets.items' | dcTranslate }}</span>
            <strong>{{ check.items.length }}</strong>
          </div>
          @for (tally of tallies(); track tally.key) {
            <div [attr.data-testid]="'package-count-' + tally.key">
              <span class="dc-muted">{{ 'packages.status.' + tally.key | dcTranslate }}</span>
              <strong>{{ tally.count }}</strong>
            </div>
          }
          <div>
            <span class="dc-muted">{{ 'packages.areas' | dcTranslate }}</span>
            <strong>{{ areas() }}</strong>
          </div>
        </div>
        <p [class]="check.isValid ? 'dc-review__ok' : 'dc-review__bad'" data-testid="package-verdict">
          <mat-icon fontSet="material-symbols-outlined">{{ check.isValid ? 'check_circle' : 'error' }}</mat-icon>
          {{
            check.isValid
              ? ('packages.valid' | dcTranslate)
              : ('packages.invalid' | dcTranslate).replace('{count}', '' + failing())
          }}
        </p>
        @if (failing() || problems().length) {
          <button matButton="outlined" type="button" class="dc-review__copy" (click)="copyProblems()" data-testid="package-copy-problems">
            <mat-icon fontSet="material-symbols-outlined">content_copy</mat-icon>
            {{ 'packages.copyProblems' | dcTranslate }} ({{ allProblems().length }})
          </button>
        }
        @if (check.isValid && !changes()) {
          <p class="dc-review__ok" data-testid="package-up-to-date">
            <mat-icon fontSet="material-symbols-outlined">task_alt</mat-icon>
            {{ 'packages.upToDate' | dcTranslate }}
          </p>
        }
        <div class="dc-review__filters">
          @if (failing()) {
            <mat-slide-toggle [checked]="onlyProblems()" (change)="onlyProblems.set($event.checked)">{{ 'packages.onlyProblems' | dcTranslate }}</mat-slide-toggle>
          }
          @if (unchangedCount()) {
            <mat-slide-toggle [checked]="hideUnchanged()" (change)="hideUnchanged.set($event.checked)" data-testid="package-hide-unchanged">
              {{ ('packages.hideUnchanged' | dcTranslate).replace('{count}', '' + unchangedCount()) }}
            </mat-slide-toggle>
          }
        </div>
        <table class="dc-review__items">
          <thead>
            <tr>
              <th>#</th>
              <th>{{ 'packages.type' | dcTranslate }}</th>
              <th>{{ 'packages.code' | dcTranslate }}</th>
              <th>{{ 'packages.operation' | dcTranslate }}</th>
              <th>{{ 'packages.name' | dcTranslate }}</th>
              <th>{{ 'changeSets.status' | dcTranslate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (item of shown(); track item.index) {
              <tr [class.dc-review__row--bad]="hasErrors(item)" [attr.data-item]="item.index">
                <td>{{ item.index + 1 }}</td>
                <td>
                  {{ typeTitle(item.entityType) | dcLocalize }}
                  <div class="mono dc-muted">{{ item.entityType }}</div>
                </td>
                <td class="mono">{{ item.entityCode }}</td>
                <td><span class="dc-review__op">{{ item.operation }}</span></td>
                <td>{{ name(item) | dcLocalize }}</td>
                <td [attr.data-status]="status(item)">
                  @switch (status(item)) {
                    @case ('problems') {
                      <span class="dc-badge dc-badge--danger">{{ 'packages.problems' | dcTranslate }}</span>
                    }
                    @case ('unchanged') {
                      <span class="dc-badge">{{ 'packages.status.unchanged' | dcTranslate }}</span>
                    }
                    @case ('new') {
                      <span class="dc-badge dc-badge--success">{{ 'packages.status.new' | dcTranslate }}</span>
                    }
                    @case ('deleted') {
                      <span class="dc-badge dc-badge--danger">{{ 'packages.status.deleted' | dcTranslate }}</span>
                    }
                    @default {
                      <span class="dc-badge dc-badge--warning">{{ 'packages.status.changed' | dcTranslate }}</span>
                    }
                  }
                </td>
              </tr>
              <tr class="dc-review__detail">
                <td></td>
                <td colspan="5">
                  @if (hasErrors(item)) {
                    <ul class="dc-review__errors">
                      @for (error of errorsOf(item); track $index) {
                        <li dir="auto"><span class="mono">{{ error.field }}</span>: {{ error.message }}</li>
                      }
                    </ul>
                  }
                  @if (status(item) === 'changed' && item.current) {
                    <dc-payload-diff [before]="item.current" [after]="item.incoming ?? item.payload" />
                  } @else if (item.payload && status(item) !== 'unchanged') {
                    <details>
                      <summary>{{ 'packages.data' | dcTranslate }}</summary>
                      <pre dir="ltr">{{ item.payload | json }}</pre>
                    </details>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      <button matButton mat-dialog-close>{{ 'form.cancel' | dcTranslate }}</button>
      <button matButton="filled" [disabled]="!check()?.isValid || !changes() || busy()" (click)="import()" data-testid="package-import">
        <mat-icon fontSet="material-symbols-outlined">upload</mat-icon>
        {{ 'packages.confirm' | dcTranslate }} ({{ changes() }})
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-review {
      min-width: min(56rem, 90vw);
    }
    .dc-review__summary {
      display: flex;
      flex-wrap: wrap;
      gap: 8px 24px;
      margin-block: 8px;
    }
    .dc-review__summary div {
      display: flex;
      flex-direction: column;
    }
    .dc-review__copy {
      margin-inline-end: 12px;
    }
    .dc-review__filters {
      display: flex;
      flex-wrap: wrap;
      gap: 8px 24px;
    }
    .dc-review__ok,
    .dc-review__bad {
      display: flex;
      align-items: center;
      gap: 6px;
      font-weight: 600;
    }
    .dc-review__ok {
      color: #067647;
    }
    .dc-review__bad {
      color: #b42318;
    }
    .dc-review__items {
      width: 100%;
      margin-top: 8px;
      border-collapse: collapse;
      font-size: 14px;
    }
    .dc-review__items th {
      padding: 8px;
      background: var(--dga-gray-50);
      color: var(--dga-gray-600);
      font-size: 13px;
      font-weight: 600;
      text-align: start;
    }
    .dc-review__items td {
      padding: 8px;
      vertical-align: top;
    }
    .dc-review__detail td {
      padding-top: 0;
      border-bottom: 1px solid var(--dga-gray-100);
    }
    .dc-review__row--bad td {
      background: #fef3f2;
    }
    .dc-review__op {
      font-size: 12px;
      font-weight: 600;
    }
    .dc-review__errors {
      margin: 0;
      padding-inline-start: 18px;
      color: #b42318;
    }
    details pre {
      max-height: 16rem;
      overflow: auto;
      margin: 4px 0;
      padding: 8px;
      border-radius: var(--shell-radius, 8px);
      background: var(--dga-gray-50);
      font-size: 12px;
    }
  `,
})
export class PackageReviewDialog {
  protected readonly data = inject<PackageReviewData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<PackageReviewDialog, number>);
  private readonly api = inject(AppsControlApi);
  private readonly i18n = inject(I18nService);
  private readonly clipboard = inject(Clipboard);
  private readonly snackBar = inject(MatSnackBar);
  private readonly titles = new Map(templateEntities(ALL_CONFIG_SECTIONS).map((e) => [e.entityType, e.title]));
  private readonly pkg = signal<unknown>(null);

  protected readonly busy = signal(true);
  protected readonly problems = signal<string[]>([]);
  protected readonly check = signal<PackageCheck | null>(null);
  protected readonly onlyProblems = signal(false);
  protected readonly hideUnchanged = signal(true);
  protected readonly failing = computed(() => this.check()?.items.filter((i) => this.hasErrors(i)).length ?? 0);
  protected readonly unchangedCount = computed(() => this.check()?.items.filter((i) => this.status(i) === 'unchanged').length ?? 0);
  /** Items the import drafts: unchanged updates are skipped. */
  protected readonly changes = computed(() => (this.check()?.items.length ?? 0) - this.unchangedCount());
  protected readonly shown = computed(() =>
    (this.check()?.items ?? []).filter((i) => (!this.onlyProblems() || this.hasErrors(i)) && (!this.hideUnchanged() || this.onlyProblems() || this.status(i) !== 'unchanged')),
  );
  protected readonly tallies = computed(() => {
    const counts = new Map<ItemStatus, number>();
    for (const item of this.check()?.items ?? []) counts.set(this.status(item), (counts.get(this.status(item)) ?? 0) + 1);
    return STATUSES.filter((key) => counts.has(key)).map((key) => ({ key, count: counts.get(key)! }));
  });
  protected readonly areas = computed(() =>
    [...new Set((this.check()?.items ?? []).map((i) => i.area).filter((a): a is string => !!a))].map((a) => this.i18n.translate('area.' + a)).join('، ') || '—',
  );

  constructor() {
    void this.run();
  }

  /** Every problem of the package and its items, one per line. */
  protected readonly allProblems = computed(() => problemLines(this.problems(), this.check()?.items ?? []));

  protected copyProblems(): void {
    const text = [this.data.fileName, ...this.allProblems()].join('\n');
    if (this.clipboard.copy(text)) {
      this.snackBar.open(this.i18n.translate('packages.problemsCopied'), undefined, { duration: 3000 });
    }
  }

  /** What importing the item does: adds, changes, deletes or leaves the row as it is; or it cannot be imported. */
  protected status(item: PackageItemCheck): ItemStatus {
    if (this.hasErrors(item)) return 'problems';
    if (item.unchanged) return 'unchanged';
    switch ((item.operation ?? '').toUpperCase()) {
      case 'CREATE':
        return 'new';
      case 'DELETE':
        return 'deleted';
      default:
        return 'changed';
    }
  }

  protected hasErrors(item: PackageItemCheck): boolean {
    return Object.keys(item.errors).length > 0;
  }

  protected errorsOf(item: PackageItemCheck) {
    return Object.entries(item.errors).flatMap(([field, messages]) => messages.map((message) => ({ field, message })));
  }

  protected typeTitle(type: string | null) {
    return (type ? this.titles.get(type) : undefined) ?? { ar: type || '—', en: type || '—' };
  }

  protected name(item: PackageItemCheck) {
    const payload = item.payload ?? {};
    const ar = typeof payload['nameAr'] === 'string' ? payload['nameAr'] : '';
    const en = typeof payload['nameEn'] === 'string' ? payload['nameEn'] : '';
    return { ar: ar || en, en: en || ar };
  }

  protected async import(): Promise<void> {
    this.busy.set(true);
    try {
      const created = await firstValueFrom(this.api.importPackage(this.pkg() as { title: string; items: unknown[] }));
      this.ref.close(created.length);
    } catch (error) {
      this.problems.set(problemMessages(error));
      this.busy.set(false);
    }
  }

  private async run(): Promise<void> {
    let parsed: unknown;
    try {
      parsed = withoutHelp(JSON.parse(this.data.text));
    } catch (error) {
      this.problems.set([`${this.i18n.translate('form.invalidJson')} ${(error as Error).message}`]);
      this.busy.set(false);
      return;
    }

    this.pkg.set(parsed);
    try {
      const check = await firstValueFrom(this.api.checkPackage(parsed));
      this.check.set(check);
      this.problems.set(Object.entries(check.errors).flatMap(([field, messages]) => messages.map((m) => `${field}: ${m}`)));
    } catch (error) {
      this.problems.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }
}
