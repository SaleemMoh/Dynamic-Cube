import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  AppsControlApi,
  ConfigRow,
  I18nService,
  LocalizePipe,
  TranslatePipe,
  problemMessages,
  SearchableSelectModule,
} from '@dynamic-cube/shared';
import { ConfigEditorData } from '../config/config-editor';
import { payloadOf } from '../config/form-values';
import { DraftsService } from '../drafts.service';
import { SubmitButton } from '../submit-button';
import { CONNECTED_APP_TRANSLATIONS } from './connected-app-translations';

/** The standard actions a screen may offer (others can be added by code). */
export const STANDARD_ACTIONS = ['VIEW', 'CREATE', 'EDIT', 'DELETE', 'APPROVE', 'EXPORT', 'PRINT'];

/** A connected app's screen as edited here. */
export interface ScreenRow {
  /** The code it was loaded with; null for a new screen. */
  original: string | null;
  code: string;
  nameAr: string;
  nameEn: string;
  parentCode: string | null;
  actions: string[];
  other: string;
  removed: boolean;
  row: ConfigRow | null;
}

export function toScreenRow(row: ConfigRow): ScreenRow {
  const actions = (row['availableActions'] as string[] | undefined) ?? ['VIEW'];
  return {
    original: row.code,
    code: row.code,
    nameAr: row.nameAr,
    nameEn: row.nameEn,
    parentCode: (row['parentCode'] as string | null) ?? null,
    actions: actions.filter((a) => STANDARD_ACTIONS.includes(a)),
    other: actions.filter((a) => !STANDARD_ACTIONS.includes(a)).join(', '),
    removed: false,
    row,
  };
}

/** The actions of a row: the standard ones ticked and the others typed, upper case, without repeats. */
export function actionsOf(screen: ScreenRow): string[] {
  const other = screen.other
    .split(',')
    .map((a) => a.trim().toUpperCase())
    .filter((a) => a.length > 0);
  return [...new Set([...screen.actions, ...other])];
}

/** Whether an existing screen was changed. */
export function isChanged(screen: ScreenRow): boolean {
  if (!screen.row) return false;
  const before = toScreenRow(screen.row);
  return (
    before.nameAr !== screen.nameAr.trim() ||
    before.nameEn !== screen.nameEn.trim() ||
    before.parentCode !== screen.parentCode ||
    actionsOf(before).join(',') !== actionsOf(screen).join(',')
  );
}

/**
 * A connected app's screens and the actions each offers (ADR-0036 step 7): staff register them here; roles and personal
 * privileges grant them and the app's menu tree opens them. Saved as screen (resource) changes of the app in the Users &
 * privileges area, through its approval.
 */
@Component({
  selector: 'dc-connected-screens-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    SearchableSelectModule,
    MatTooltipModule,
    LocalizePipe,
    TranslatePipe,
    SubmitButton,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ 'ca.screens.title' | dcTranslate }} — {{ { ar: app.nameAr, en: app.nameEn } | dcLocalize }}
    </h2>
    <mat-dialog-content class="dc-cs">
      @if (loading() || saving()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <p class="dc-cs__note">{{ 'ca.screens.intro' | dcTranslate }}</p>
      @if (errors().length) {
        <ul class="dc-errors" role="alert" data-testid="screens-errors">
          @for (e of errors(); track $index) {
            <li dir="auto">{{ e }}</li>
          }
        </ul>
      }
      @for (s of screens(); track $index; let i = $index) {
        <section
          class="dc-cs__row"
          [class.dc-cs__row--removed]="s.removed"
          [attr.data-testid]="'screen-row-' + i"
        >
          <div class="dc-cs__head">
            <mat-form-field subscriptSizing="dynamic" class="dc-cs__code">
              <mat-label>{{ 'ca.screens.code' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="s.code"
                (ngModelChange)="set(i, { code: $event })"
                [readonly]="!!s.original"
                [attr.data-testid]="'screen-code-' + i"
              />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic" class="dc-cs__grow">
              <mat-label>{{ 'ca.screens.nameAr' | dcTranslate }}</mat-label>
              <input
                matInput
                [ngModel]="s.nameAr"
                (ngModelChange)="set(i, { nameAr: $event })"
                [attr.data-testid]="'screen-name-ar-' + i"
              />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic" class="dc-cs__grow">
              <mat-label>{{ 'ca.screens.nameEn' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="s.nameEn"
                (ngModelChange)="set(i, { nameEn: $event })"
                [attr.data-testid]="'screen-name-en-' + i"
              />
            </mat-form-field>
            @if (s.removed) {
              <span class="dc-pill">{{ 'ca.screens.removed' | dcTranslate }}</span>
              <button matButton type="button" (click)="set(i, { removed: false })">
                {{ 'ca.screens.undo' | dcTranslate }}
              </button>
            } @else {
              @if (!s.original) {
                <span class="dc-pill dc-pill--green">{{ 'ca.screens.new' | dcTranslate }}</span>
              } @else if (changed(s)) {
                <span class="dc-pill dc-pill--gold">{{ 'ca.screens.changed' | dcTranslate }}</span>
              }
              <button
                matIconButton
                type="button"
                (click)="remove(i)"
                [matTooltip]="'ca.screens.remove' | dcTranslate"
                [attr.aria-label]="'ca.screens.remove' | dcTranslate"
                [attr.data-testid]="'screen-remove-' + i"
              >
                <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
              </button>
            }
          </div>
          <div class="dc-cs__body">
            <mat-form-field subscriptSizing="dynamic" class="dc-cs__parent">
              <mat-label>{{ 'ca.screens.parent' | dcTranslate }}</mat-label>
              <mat-select [ngModel]="s.parentCode" (ngModelChange)="set(i, { parentCode: $event })">
                <mat-option [value]="null">{{ 'ca.screens.none' | dcTranslate }}</mat-option>
                @for (p of parentsFor(i); track p) {
                  <mat-option [value]="p"
                    ><span dir="ltr">{{ p }}</span></mat-option
                  >
                }
              </mat-select>
            </mat-form-field>
            <div
              class="dc-cs__actions"
              role="group"
              [attr.aria-label]="'ca.screens.actions' | dcTranslate"
            >
              @for (a of standard; track a) {
                <button
                  type="button"
                  class="dc-cs__chip"
                  [class.dc-cs__chip--on]="s.actions.includes(a)"
                  [attr.aria-pressed]="s.actions.includes(a)"
                  (click)="toggle(i, a)"
                  [attr.data-testid]="'screen-action-' + i + '-' + a"
                >
                  {{ 'ca.action.' + a | dcTranslate }}
                </button>
              }
            </div>
            <mat-form-field subscriptSizing="dynamic" class="dc-cs__grow">
              <mat-label>{{ 'ca.screens.other' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="s.other"
                (ngModelChange)="set(i, { other: $event })"
              />
            </mat-form-field>
          </div>
        </section>
      } @empty {
        @if (!loading()) {
          <p class="dc-muted" data-testid="screens-empty">{{ 'ca.screens.empty' | dcTranslate }}</p>
        }
      }
      <button matButton="outlined" type="button" (click)="add()" data-testid="screen-add">
        <mat-icon fontSet="material-symbols-outlined">add</mat-icon
        >{{ 'ca.screens.add' | dcTranslate }}
      </button>
      <p class="dc-muted dc-cs__hint">{{ codeHint() }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" mat-dialog-close>{{ 'ca.cancel' | dcTranslate }}</button>
      <dc-submit-button
        entityType="apps.resource"
        [disabled]="saving()"
        (submitted)="save(true)"
        testId="screens-submit"
      />
      <button
        matButton="filled"
        type="button"
        (click)="save(false)"
        [disabled]="saving()"
        data-testid="screens-save"
      >
        {{ drafts.saveLabel('apps.resource') | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-cs {
      display: grid;
      gap: 12px;
    }
    .dc-cs__note {
      margin: 0;
      color: var(--dga-gray-600);
    }
    .dc-cs__row {
      display: grid;
      gap: 8px;
      padding: 12px;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius);
    }
    .dc-cs__row--removed {
      opacity: 0.6;
      background: var(--dga-gray-50);
    }
    .dc-cs__head,
    .dc-cs__body {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .dc-cs__code {
      width: 14rem;
    }
    .dc-cs__parent {
      width: 14rem;
    }
    .dc-cs__grow {
      flex: 1;
      min-width: 10rem;
    }
    .dc-cs__actions {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }
    .dc-cs__chip {
      padding: 4px 12px;
      border: 1px solid var(--dga-gray-300);
      border-radius: 9999px;
      background: #fff;
      color: var(--dga-gray-700);
      font: inherit;
      font-size: 13px;
      cursor: pointer;
    }
    .dc-cs__chip--on {
      border-color: var(--dynamic-cube-gold-600, #b8862f);
      background: var(--dynamic-cube-gold-50);
      color: var(--dynamic-cube-gold-700);
      font-weight: 600;
    }
    .dc-cs__hint {
      margin: 0;
    }
  `,
})
export class ConnectedScreensDialog {
  private readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  private readonly api = inject(AppsControlApi);
  private readonly i18n = inject(I18nService);
  private readonly ref = inject(MatDialogRef<ConnectedScreensDialog, boolean>);
  protected readonly drafts = inject(DraftsService);

  protected readonly app = this.data.row!;
  protected readonly standard = STANDARD_ACTIONS;
  protected readonly screens = signal<ScreenRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly codeHint = computed(() =>
    this.i18n.translate('ca.screens.codeHint').replace('{app}', this.app.code),
  );

  constructor() {
    this.i18n.register(CONNECTED_APP_TRANSLATIONS);
    this.api.list('apps.resource').subscribe({
      next: (rows) => {
        this.screens.set(
          rows
            .filter((r) => r['businessApp'] === this.app.code)
            .sort((a, b) => a.code.localeCompare(b.code))
            .map(toScreenRow),
        );
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.errors.set(problemMessages(error));
        this.loading.set(false);
      },
    });
  }

  protected changed(screen: ScreenRow): boolean {
    return isChanged(screen);
  }

  protected set(index: number, patch: Partial<ScreenRow>): void {
    this.screens.update((list) => list.map((s, i) => (i === index ? { ...s, ...patch } : s)));
  }

  protected toggle(index: number, action: string): void {
    const screen = this.screens()[index];
    this.set(index, {
      actions: screen.actions.includes(action)
        ? screen.actions.filter((a) => a !== action)
        : STANDARD_ACTIONS.filter((a) => a === action || screen.actions.includes(a)),
    });
  }

  protected add(): void {
    this.screens.update((list) => [
      ...list,
      {
        original: null,
        code: `${this.app.code}.`,
        nameAr: '',
        nameEn: '',
        parentCode: null,
        actions: ['VIEW'],
        other: '',
        removed: false,
        row: null,
      },
    ]);
  }

  protected remove(index: number): void {
    const screen = this.screens()[index];
    if (screen.original) {
      this.set(index, { removed: true });
    } else {
      this.screens.update((list) => list.filter((_, i) => i !== index));
    }
  }

  /** Screens a row may sit under: the app's other screens that stay. */
  protected parentsFor(index: number): string[] {
    return this.screens()
      .filter((s, i) => i !== index && !s.removed && s.code.trim().length > 0)
      .map((s) => s.code.trim());
  }

  protected async save(submit: boolean): Promise<void> {
    const list = this.screens();
    const kept = list.filter((s) => !s.removed);
    if (
      kept.some(
        (s) => !s.code.trim() || !s.nameAr.trim() || !s.nameEn.trim() || actionsOf(s).length === 0,
      )
    ) {
      this.errors.set([this.i18n.translate('ca.screens.required')]);
      return;
    }

    const codes = kept.map((s) => s.code.trim());
    const repeated = codes.find((c, i) => codes.indexOf(c) !== i);
    if (repeated) {
      this.errors.set([this.i18n.translate('ca.screens.duplicate').replace('{code}', repeated)]);
      return;
    }

    const work = [
      ...list
        .filter((s) => s.removed && s.original)
        .map((s) => ({ s, operation: 'DELETE' as const })),
      ...kept.filter((s) => !s.original).map((s) => ({ s, operation: 'CREATE' as const })),
      ...kept
        .filter((s) => s.original && isChanged(s))
        .map((s) => ({ s, operation: 'UPDATE' as const })),
    ];
    if (work.length === 0) {
      this.errors.set([this.i18n.translate('ca.screens.nothing')]);
      return;
    }

    this.errors.set([]);
    this.saving.set(true);
    try {
      // Parents first: a new screen may sit under another new one.
      const order = (s: ScreenRow) => (s.parentCode && codes.includes(s.parentCode) ? 1 : 0);
      for (const { s, operation } of work.sort((a, b) => order(a.s) - order(b.s))) {
        const payload =
          operation === 'DELETE'
            ? null
            : {
                ...payloadOf(s.row),
                nameAr: s.nameAr.trim(),
                nameEn: s.nameEn.trim(),
                businessApp: this.app.code,
                entityCode: (this.app['entityCode'] as string | null) ?? '',
                appCode: null,
                resourceType: (s.row?.['resourceType'] as string | undefined) ?? 'SCREEN',
                parentCode: s.parentCode,
                availableActions: actionsOf(s),
              };
        await this.drafts.addChange('apps.resource', s.code.trim(), operation, payload, { submit });
      }
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.saving.set(false);
    }
  }
}
