import { ConfigRow } from '@dynamic-cube/shared';

export type AttributeDataType = 'TEXT' | 'NUMBER' | 'DATE' | 'BOOL' | 'DOMAIN_REF' | 'JSON';

export interface AttributeDefinition {
  code: string;
  nameAr: string;
  nameEn: string;
  dataType: AttributeDataType;
  refClassCode: string | null;
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
  attributes: AttributeDefinition[];
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
}

export const DATA_TYPES: AttributeDataType[] = ['TEXT', 'NUMBER', 'DATE', 'BOOL', 'DOMAIN_REF', 'JSON'];
