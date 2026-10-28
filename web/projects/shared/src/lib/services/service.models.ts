import { JsonLogicRule } from '../expressions/jsonlogic';
import { PartyKind } from '../party/party.models';

export type QuestionType = 'TEXT' | 'TEXTAREA' | 'NUMBER' | 'DATE' | 'SELECT' | 'CHECKBOX';
export type StepType = 'FORM' | 'LEVEL' | 'FEES' | 'REVIEW' | 'DECLARATION';
export type StatusCategory = 'DRAFT' | 'SUBMITTED' | 'IN_PROGRESS' | 'PENDING_CUSTOMER' | 'PENDING_EXTERNAL' | 'APPROVED' | 'REJECTED' | 'CANCELLED' | 'CLOSED';

export interface QuestionSnapshot {
  code: string;
  labelAr: string;
  labelEn: string;
  hintAr: string | null;
  hintEn: string | null;
  type: QuestionType;
  lov: string | null;
  lovParentQuestion: string | null;
  options: { value: string; labelAr: string; labelEn: string }[];
  multiple: boolean;
  validation: { min: number | null; max: number | null; maxLength: number | null; pattern: string | null };
  prefill: string | null;
}

export interface QuestionUse {
  code: string;
  required: boolean;
  requiredWhen?: JsonLogicRule | null;
  visibleWhen?: JsonLogicRule | null;
  readonly: boolean;
}

export interface SectionDefinition {
  code: string;
  titleAr: string;
  titleEn: string;
  visibleWhen?: JsonLogicRule | null;
  questions: QuestionUse[];
}

export interface StepDefinition {
  code: string;
  type: StepType;
  titleAr: string;
  titleEn: string;
  sections: SectionDefinition[];
  textAr?: string | null;
  textEn?: string | null;
}

export interface ServiceLevelDefinition {
  code: string;
  nameAr: string;
  nameEn: string;
  descriptionAr?: string | null;
  descriptionEn?: string | null;
  slaWorkingDays?: number | null;
}

export interface VersionDefinition {
  levels: ServiceLevelDefinition[];
  steps: StepDefinition[];
  eligibility: unknown[];
  validations: unknown[];
  fees: unknown[];
  card?: ServiceCardDefinition;
}

export type ServiceChannel = 'PORTAL' | 'MOBILE_APP' | 'BRANCH' | 'CALL_CENTER';

/** What the portal's service card says before the service is started. */
export interface ServiceCardDefinition {
  audienceAr: string | null;
  audienceEn: string | null;
  steps: { textAr: string; textEn: string }[];
  termsAr: string | null;
  termsEn: string | null;
  requiredDocuments: { nameAr: string; nameEn: string; noteAr: string | null; noteEn: string | null }[];
  channels: ServiceChannel[];
  faqUrl: string | null;
  phone: string | null;
  email: string | null;
}

export interface PortalCategory {
  code: string;
  nameAr: string;
  nameEn: string;
  descriptionAr: string | null;
  descriptionEn: string | null;
  icon: string | null;
}

/** SAR with VAT; `max` is null when a fee is a formula. */
export interface CostRange {
  min: number;
  max: number | null;
  dependsOnRequest: boolean;
}

export interface PortalServiceSummary {
  code: string;
  nameAr: string;
  nameEn: string;
  descriptionAr: string | null;
  descriptionEn: string | null;
  categoryCode: string | null;
  icon: string | null;
  partyKinds: PartyKind[];
  audienceAr: string | null;
  audienceEn: string | null;
  duration: { min: number; max: number } | null;
  cost: CostRange;
}

export interface PortalCatalog {
  categories: PortalCategory[];
  services: PortalServiceSummary[];
  lastUpdated: string | null;
}

export interface PortalLevel {
  code: string;
  nameAr: string;
  nameEn: string;
  descriptionAr: string | null;
  descriptionEn: string | null;
  slaWorkingDays: number | null;
  cost: CostRange;
}

export interface ServiceCard {
  service: PortalServiceSummary;
  category: PortalCategory | null;
  levels: PortalLevel[];
  card: ServiceCardDefinition;
  versionNumber: number;
  publishedAt: string | null;
}

export interface PortalStatistics {
  services: number;
  requests: number;
  inProgress: number;
  completed: number;
}

export interface ResolvedVersion {
  serviceCode: string;
  number: number;
  definition: VersionDefinition;
  questions: Record<string, QuestionSnapshot>;
}

export interface FeeLine {
  code: string;
  nameAr: string;
  nameEn: string;
  net: number;
  vat: number;
  total: number;
  isEstimate: boolean;
}

export interface EligibilityFailure {
  code: string;
  messageAr: string;
  messageEn: string;
}

export interface CatalogEntry {
  code: string;
  nameAr: string;
  nameEn: string;
  descriptionAr: string | null;
  descriptionEn: string | null;
  archetype: string;
  categoryCode: string | null;
  icon: string | null;
  partyKinds: PartyKind[];
  versionNumber: number;
  isEligible: boolean;
  reasons: EligibilityFailure[];
}

export interface ServiceView {
  service: CatalogEntry;
  version: ResolvedVersion;
  feesByLevel: Record<string, FeeLine[]>;
}

export interface RequestSummary {
  id: string;
  number: string | null;
  serviceCode: string;
  serviceNameAr: string;
  serviceNameEn: string;
  status: StatusCategory;
  displayStatus: string;
  beneficiaryNameAr: string;
  beneficiaryNameEn: string;
  feeTotal: number;
  createdAt: string;
  submittedAt: string | null;
  draftExpiresAt: string | null;
}

export interface FieldError {
  field: string | null;
  code: string;
  messageAr: string;
  messageEn: string;
  step: string | null;
}

export interface RequestView {
  request: RequestSummary;
  versionNumber: number;
  levelCode: string | null;
  currentStep: string | null;
  data: Record<string, unknown>;
  fees: FeeLine[];
  feesByLevel: Record<string, FeeLine[]>;
  version: ResolvedVersion;
  stepErrors: FieldError[];
  /** What happened to the request, for the applicant (returns and decisions carry their reason). */
  timeline: TimelineEntry[];
}

export interface TimelineEntry {
  at: string;
  userId: string;
  type: string;
  fromStatus: StatusCategory | null;
  toStatus: StatusCategory | null;
  comment: string | null;
}

export interface RequestDetail {
  view: RequestView;
  submitterPartyId: string;
  submitterName: string;
  beneficiaryPartyId: string;
  timeline: TimelineEntry[];
}
