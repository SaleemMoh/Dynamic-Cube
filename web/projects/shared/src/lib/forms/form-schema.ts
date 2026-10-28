import { JsonLogicRule } from '../expressions/jsonlogic';
import { LocalizedText } from '../i18n/language';

/**
 * Schema-driven form definition produced by the Service Builder and rendered by {@link DynamicForm}.
 * Fields are identified by stable codes (question bank codes) so shared questions render once.
 */
export interface FormSchema {
  code: string;
  sections: FormSection[];
}

export interface FormSection {
  code: string;
  title: LocalizedText;
  /** JSONLogic rule evaluated against the form value; the section is shown when truthy. */
  visibleWhen?: JsonLogicRule;
  fields: FormField[];
}

export type FormFieldType = 'text' | 'textarea' | 'number' | 'date' | 'select' | 'checkbox';

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
  /** Options for `select` fields (later supplied by an LOV). */
  options?: FormOption[];
}

export interface FormOption {
  value: string;
  label: LocalizedText;
}
