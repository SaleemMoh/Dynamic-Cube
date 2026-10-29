import { JsonLogicRule } from '../expressions/jsonlogic';
import { LocalizedText } from '../i18n/language';
import type { StructuredShape } from './structured-input';

/**
 * Schema-driven form definition produced by the Service Builder and rendered by {@link DynamicForm}.
 * Fields are identified by stable codes (question bank codes) so shared questions render once.
 */
export interface FormSchema {
  code: string;
  /** How the sections are shown: one below the other (default), as tabs, or as an accordion. */
  display?: FormDisplay;
  sections: FormSection[];
}

export type FormDisplay = 'stacked' | 'tabs' | 'accordion';

/** Share of a section's row a field takes when the section has columns. */
export type FieldWidth = 'full' | 'half' | 'third';

export interface FormSection {
  code: string;
  title: LocalizedText;
  /** JSONLogic rule evaluated against the form value; the section is shown when truthy. */
  visibleWhen?: JsonLogicRule;
  /** 1 to 3 columns; without, as many as fit. Phones always show one. */
  columns?: number;
  fields: FormField[];
}

/**
 * `icon` is a Material Symbols name chosen from the icon picker (or typed). `dualList` chooses several options by moving
 * them between an available and a chosen list. `structured` edits nested data (rows, lists, objects) through a form made
 * for its {@link FormField.structure}, with a JSON view.
 */
export type FormFieldType = 'text' | 'textarea' | 'number' | 'date' | 'select' | 'checkbox' | 'icon' | 'dualList' | 'structured' | 'file';

export interface FormField {
  code: string;
  type: FormFieldType;
  label: LocalizedText;
  hint?: LocalizedText;
  required?: boolean;
  /** JSONLogic rule; when truthy the field becomes required. */
  requiredWhen?: JsonLogicRule;
  /** JSONLogic rule; hidden fields are disabled and excluded from the submitted value. */
  visibleWhen?: JsonLogicRule;
  /** Fixed options for `select` fields. */
  options?: FormOption[];
  /** Apps Control LOV supplying the options of a `select` field (grant-filtered on the server). */
  lov?: string;
  /** Code of the field whose value is the LOV's parent (cascading lists, e.g. city by region). */
  lovParentField?: string;
  /** `select` allows several values. */
  multiple?: boolean;
  /** Shown but not editable (still submitted). */
  readonly?: boolean;
  /** In a section with columns: full row, half or a third (default one column); `full` also spans a section without columns. */
  width?: FieldWidth;
  /** For `file` fields: accepted extensions (e.g. pdf), the largest file in MB and how many files (with `multiple`). */
  accept?: string[];
  maxSizeMb?: number;
  maxFiles?: number;
  /** For `structured` fields: what the value holds. */
  structure?: StructuredShape;
  /** Limits checked in the browser and again on the server. */
  min?: number;
  max?: number;
  maxLength?: number;
  pattern?: string;
}

export interface FormOption {
  value: string;
  label: LocalizedText;
}
