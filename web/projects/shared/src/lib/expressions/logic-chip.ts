import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { I18nService } from '../i18n/i18n.service';
import { TranslatePipe } from '../i18n/translate.pipe';
import { JsonLogicRule } from './jsonlogic';
import { LogicBuilder } from './logic-builder';
import { LogicField, LogicValues } from './logic-fields';
import {
  LogicCondition,
  LogicValue,
  describeCondition,
  describeValue,
  readCondition,
  readValue,
} from './logic-model';
import { choiceName, logicLabel, logicWords, registerLogicTexts } from './logic-text';

interface LogicDialogData {
  title: string;
  rule: JsonLogicRule | null;
  mode: 'condition' | 'value';
  fields: LogicField[];
}

/** The Logic Builder in a dialog: Done gives back the rule, Cancel leaves it as it was. */
@Component({
  selector: 'dc-logic-dialog',
  imports: [MatButtonModule, MatDialogModule, TranslatePipe, LogicBuilder],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <dc-logic-builder
        [rule]="rule()"
        [mode]="data.mode"
        [fields]="data.fields"
        (ruleChange)="rule.set($event)"
        testId="logic-dialog-builder"
      />
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close type="button">{{ 'form.cancel' | dcTranslate }}</button>
      <button matButton="filled" type="button" (click)="done()" data-testid="logic-dialog-done">
        {{ 'logic.done' | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
})
export class LogicDialog {
  protected readonly data = inject<LogicDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<LogicDialog, { rule: JsonLogicRule | null }>);
  protected readonly rule = signal<JsonLogicRule | null>(this.data.rule);

  protected done(): void {
    this.ref.close({ rule: this.rule() });
  }
}

/** A rule shown as a sentence in a small space (a table cell); clicking it edits the rule in the Logic Builder. */
@Component({
  selector: 'dc-logic-chip',
  imports: [MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="lc"
      [class.lc--empty]="empty()"
      (click)="edit()"
      [disabled]="disabled()"
      [attr.aria-label]="'logic.edit' | dcTranslate"
      data-testid="logic-chip"
    >
      <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{
        mode() === 'condition' ? 'rule' : 'function'
      }}</mat-icon>
      <span class="lc__text">{{
        empty()
          ? ((mode() === 'condition' ? 'logic.always' : 'logic.setValue') | dcTranslate)
          : sentence()
      }}</span>
      <mat-icon fontSet="material-symbols-outlined" class="lc__edit" aria-hidden="true"
        >edit</mat-icon
      >
    </button>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .lc {
      display: flex;
      align-items: center;
      gap: 6px;
      width: 100%;
      min-height: 32px;
      padding: 4px 8px;
      border: 1px solid var(--dga-gray-300, #d2d6db);
      border-radius: 6px;
      background: #fff;
      color: inherit;
      font: inherit;
      font-size: 12px;
      text-align: start;
      cursor: pointer;
    }
    .lc:hover:not(:disabled) {
      border-color: var(--mat-sys-primary, #1b8354);
    }
    .lc--empty .lc__text {
      color: var(--dga-text-3, #6c737f);
    }
    .lc mat-icon {
      flex: none;
      font-size: 16px;
      width: 16px;
      height: 16px;
      color: var(--dga-text-3, #6c737f);
    }
    .lc__text {
      flex: 1;
      overflow: hidden;
      text-overflow: ellipsis;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
    }
  `,
})
export class LogicChip {
  private readonly i18n = inject(I18nService);
  private readonly dialog = inject(MatDialog);
  private readonly values = inject(LogicValues);
  readonly rule = input<JsonLogicRule | null | undefined>(null);
  readonly mode = input<'condition' | 'value'>('condition');
  readonly fields = input<LogicField[]>([]);
  readonly title = input<string>('');
  readonly disabled = input(false);
  readonly ruleChange = output<JsonLogicRule | null>();

  protected readonly empty = computed(
    () => this.rule() === null || this.rule() === undefined || this.rule() === '',
  );
  protected readonly sentence = computed(() => {
    const language = this.i18n.language();
    const words = {
      ...logicWords(this.i18n),
      valueName: (path: string, value: string) =>
        choiceName(this.fields(), this.values, path, value, language),
    };
    const label = (p: string) => logicLabel(this.fields(), p, language);
    return this.mode() === 'condition'
      ? describeCondition(readCondition(this.rule()) as LogicCondition, words, label)
      : describeValue(readValue(this.rule()) as LogicValue, words, label);
  });

  constructor() {
    registerLogicTexts(this.i18n);
  }

  protected edit(): void {
    this.dialog
      .open<LogicDialog, LogicDialogData, { rule: JsonLogicRule | null }>(LogicDialog, {
        data: {
          title: this.title() || this.i18n.translate('logic.edit'),
          rule: this.rule() ?? null,
          mode: this.mode(),
          fields: this.fields(),
        },
        width: '56rem',
        maxWidth: '96vw',
      })
      .afterClosed()
      .subscribe((result) => {
        if (result) this.ruleChange.emit(result.rule);
      });
  }
}
