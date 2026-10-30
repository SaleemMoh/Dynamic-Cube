import { ConfigRow } from '@dynamic-cube/shared';

export type AttributeDataType = 'TEXT' | 'TEXTAREA' | 'NUMBER' | 'DATE' | 'BOOL' | 'DOMAIN_REF' | 'LOV_REF' | 'JSON';

export interface AttributeDefinition {
  code: string;
  nameAr: string;
  nameEn: string;
  dataType: AttributeDataType;
  refClassCode: string | null;
  /** List of values the options come from (type `LOV_REF`). */
  refLovCode?: string | null;
  /** Several values (a list of codes) for `DOMAIN_REF` and `LOV_REF`. */
  isMultiple?: boolean;
  attributeGroup: 'ATTRIBUTE' | 'PARAMETER';
  isRequired: boolean;
  validation: Record<string, unknown> | null;
  displayOrder: number;
  isReportable: boolean;
}

export interface ClassRow extends ConfigRow {
  descriptionAr: string | null;
  descriptionEn: string | null;
  parentClassCode: string | null;
  isHierarchical: boolean;
  allowMultiAssign: boolean;
  /** Values may be limited to some companies. */
  valuesPerCompany?: boolean;
  attributes: AttributeDefinition[];
  /** Named values stored on the class itself. */
  settings?: ClassSetting[];
}

export interface ClassSetting {
  code: string;
  nameAr: string;
  nameEn: string;
  value: string | null;
}

export interface DomainRow extends ConfigRow {
  classCode: string;
  parentCode: string | null;
  detailsAr: string | null;
  detailsEn: string | null;
  displayOrder: number;
  isDefault: boolean;
  attributes: Record<string, unknown>;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  /** Companies the value serves (classes with values per company); empty: every company. */
  companies?: string[];
}

export const DATA_TYPES: AttributeDataType[] = ['TEXT', 'TEXTAREA', 'NUMBER', 'DATE', 'BOOL', 'DOMAIN_REF', 'LOV_REF', 'JSON'];

/** Types whose fields may take several values. */
export const MULTI_TYPES: AttributeDataType[] = ['DOMAIN_REF', 'LOV_REF'];
