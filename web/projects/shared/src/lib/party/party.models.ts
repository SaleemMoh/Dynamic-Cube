import { IntegrationOutcome } from '../integration/integration.api';

export type PartyKind = 'INDIVIDUAL' | 'ESTABLISHMENT';
export type RecordSource = 'MANUAL' | 'INTEGRATION' | 'SIGN_IN';
export type CredentialStatus = 'ACTIVE' | 'SUSPENDED' | 'EXPIRED' | 'CANCELLED';
export type ProjectStatus = 'ONGOING' | 'COMPLETED' | 'CANCELLED';

export const INDIVIDUAL_IDENTIFIER_TYPES = ['NATIONAL_ID', 'IQAMA', 'GCC_ID', 'PASSPORT'];
export const ESTABLISHMENT_IDENTIFIER_TYPES = ['CR_NUMBER', 'UNIFIED_NUMBER'];

export interface PartySummary {
  id: string;
  kind: PartyKind;
  nameAr: string;
  nameEn: string;
  status: 'ACTIVE' | 'INACTIVE';
  identifierType: string;
  identifier: string;
  source: RecordSource;
}

export interface ProfileEntry {
  code: string;
  nameAr: string;
  nameEn: string;
  dataType: string;
  value: unknown;
  source: RecordSource | null;
  refreshedAt: string | null;
  isStale: boolean;
}

export interface RelationshipInfo {
  id: string;
  individualId: string;
  individualName: string;
  individualIdentifier: string;
  principalId: string;
  principalName: string;
  principalIdentifier: string;
  relationshipCode: string;
  relationshipNameAr: string;
  relationshipNameEn: string;
  canAct: boolean;
  services: string[];
  validFrom: string | null;
  validTo: string | null;
  source: RecordSource;
  sourceReference: string | null;
  endedAt: string | null;
}

export interface CredentialInfo {
  id: string;
  typeCode: string;
  typeNameAr: string;
  typeNameEn: string;
  number: string;
  issuedOn: string | null;
  expiresOn: string | null;
  status: CredentialStatus;
  data: unknown;
  source: RecordSource;
}

export interface ProjectInfo {
  id: string;
  number: string | null;
  nameAr: string;
  nameEn: string | null;
  clientName: string | null;
  contractValue: number | null;
  startOn: string | null;
  endOn: string | null;
  status: ProjectStatus;
  source: RecordSource;
}

export interface SanctionInfo {
  id: string;
  typeCode: string;
  typeNameAr: string;
  typeNameEn: string;
  blocksApplications: boolean;
  reason: string;
  startsOn: string;
  endsOn: string | null;
  isActive: boolean;
  liftedAt: string | null;
  liftedBy: string | null;
  liftReason: string | null;
  createdBy: string;
}

export interface PartyDetail {
  party: PartySummary;
  identifiers: { type: string; value: string; isPrimary: boolean }[];
  profile: ProfileEntry[];
  relationships: RelationshipInfo[];
  credentials: CredentialInfo[];
  projects: ProjectInfo[];
  sanctions: SanctionInfo[];
  contextsRefreshedAt: string | null;
}

export interface ActingContext {
  partyId: string;
  kind: PartyKind;
  nameAr: string;
  nameEn: string;
  identifierType: string;
  identifier: string;
  relationshipCode: string | null;
  relationshipNameAr: string | null;
  relationshipNameEn: string | null;
  services: string[];
  validTo: string | null;
}

export interface ContextWarning {
  source: string;
  outcome: IntegrationOutcome;
  messageAr: string | null;
  messageEn: string | null;
}

export interface ContextsResult {
  individual: ActingContext;
  contexts: ActingContext[];
  refreshedAt: string | null;
  warnings: ContextWarning[];
}

export interface ProfileRefreshResult {
  operationCode: string;
  outcome: IntegrationOutcome;
  messageAr: string | null;
  messageEn: string | null;
  fields: string[];
}
