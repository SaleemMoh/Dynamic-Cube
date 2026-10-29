import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { JsonLogicRule, RuleEditor, RuleVariable, TranslatePipe } from '@dynamic-cube/shared';

/** Named values, each given by a rule: a decision's inputs, a document's data fields, a line's fields. */
@Component({
  selector: 'dc-mapping-editor',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatTooltipModule, RuleEditor, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (entry of entries(); track entry[0]; let i = $index) {
      <div class="dc-mapping__entry">
        <div class="dc-mapping__key">
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'designer.field.fieldName' | dcTranslate }}</mat-label>
            <input matInput dir="ltr" [ngModel]="entry[0]" (ngModelChange)="rename(entry[0], $event)" [ngModelOptions]="{ updateOn: 'blur' }" />
          </mat-form-field>
          <button matIconButton type="button" (click)="remove(entry[0])" [matTooltip]="'rule.remove' | dcTranslate">
            <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
          </button>
        </div>
        <dc-rule-editor mode="value" [rule]="entry[1]" [variables]="variables()" (ruleChange)="set(entry[0], $event)" />
      </div>
    }
    <button matButton type="button" (click)="add()">
      <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
      {{ 'designer.addField' | dcTranslate }}
    </button>
  `,
  styles: `
    :host {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: 0.5rem;
      min-width: 0;
    }
    .dc-mapping__entry {
      display: grid;
      gap: 0.25rem;
      padding: 0.5rem;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 8px;
    }
    .dc-mapping__key {
      display: flex;
      align-items: center;
      gap: 0.25rem;
    }
    .dc-mapping__key mat-form-field {
      flex: 1;
    }
  `,
})
export class MappingEditor {
  readonly mapping = input<Record<string, JsonLogicRule>>({});
  readonly variables = input<RuleVariable[]>([]);
  readonly mappingChange = output<Record<string, JsonLogicRule>>();

  protected readonly entries = computed(() => Object.entries(this.mapping() ?? {}));

  protected set(key: string, rule: JsonLogicRule | null): void {
    this.mappingChange.emit({ ...this.mapping(), [key]: rule });
  }

  protected rename(from: string, to: string): void {
    const name = to.trim();
    if (!name || name === from || name in this.mapping()) return;
    // Keeps the order of the entries.
    this.mappingChange.emit(Object.fromEntries(Object.entries(this.mapping()).map(([k, v]) => [k === from ? name : k, v])));
  }

  protected remove(key: string): void {
    const next = { ...this.mapping() };
    delete next[key];
    this.mappingChange.emit(next);
  }

  protected add(): void {
    let i = Object.keys(this.mapping()).length + 1;
    while (`FIELD_${i}` in this.mapping()) i++;
    this.mappingChange.emit({ ...this.mapping(), [`FIELD_${i}`]: null });
  }
}
