import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { JsonLogicRule } from './jsonlogic';
import { LogicBuilder } from './logic-builder';
import { LogicField } from './logic-fields';

/** Something a rule can read, e.g. `data.CLS_SECTORS` or `level` (a Logic Builder field). */
export type RuleVariable = LogicField;

/**
 * The rule editor of the builders and designers: the Logic Builder with the place's fields. Kept under its old name
 * so every place that edits a rule uses the Logic Builder.
 */
@Component({
  selector: 'dc-rule-editor',
  imports: [LogicBuilder],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dc-logic-builder
      [rule]="rule()"
      [fields]="variables()"
      [mode]="mode()"
      [label]="label()"
      [testId]="testId()"
      (ruleChange)="ruleChange.emit($event)"
    />
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
  `,
})
export class RuleEditor {
  readonly rule = input<JsonLogicRule | null | undefined>(null);
  readonly variables = input<RuleVariable[]>([]);
  /** `condition` (true or false) or `value` (what the rule gives). */
  readonly mode = input<'condition' | 'value'>('condition');
  readonly label = input<string>('');
  readonly testId = input<string>('rule-editor');
  readonly ruleChange = output<JsonLogicRule | null>();
}
