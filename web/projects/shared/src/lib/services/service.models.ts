import { JsonLogicRule } from '../expressions/jsonlogic';
import { PartyKind } from '../party/party.models';

export type QuestionType = 'TEXT' | 'TEXTAREA' | 'NUMBER' | 'DATE' | 'SELECT' | 'CHECKBOX' | 'FILE';
export type StepType = 'FORM' | 'LEVEL' | 'FEES' | 'REVIEW' | 'DECLARATION';
export type StatusCategory =
  'DRAFT' | 'SUBMITTED' | 'IN_PROGRESS' | 'PENDING_CUSTOMER' | 'PENDING_EXTERNAL' | 'PENDING_PAYMENT' | 'APPROVED' | 'REJECTED' | 'CANCELLED' | 'CLOSED';

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
  validation: {
    min: number | null;
    max: number | null;
    maxLength: number | null;
    pattern: string | null;
    /** File questions: accepted extensions (empty: the usual documents and images), largest file and how many. */
    extensions?: string[];
    maxSizeMb?: number | null;
    maxFiles?: number | null;
  };
  prefill: string | null;
}

export interface QuestionUse {
  code: string;
  required: boolean;
  requiredWhen?: JsonLogicRule | null;
  visibleWhen?: JsonLogicRule | null;
  readonly: boolean;
  /** In a section with columns: FULL row, HALF or THIRD (default one column). */
  width?: 'FULL' | 'HALF' | 'THIRD' | null;
}

export interface SectionDefinition {
  code: string;
  titleAr: string;
  titleEn: string;
  visibleWhen?: JsonLogicRule | null;
  /** 1 to 3 columns (default: as many as fit). */
  columns?: number | null;
  questions: QuestionUse[];
}

export interface StepDefinition {
  code: string;
  type: StepType;
  titleAr: string;
  titleEn: string;
  sections: SectionDefinition[];
  /** Form steps: STACKED (default), TABS or ACCORDION. */
  display?: 'STACKED' | 'TABS' | 'ACCORDION' | null;
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
  /** For a follow-up service: the document it is requested for. */
  requiredDocument: { code: string; nameAr: string; nameEn: string } | null;
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
  /** Set for follow-up services: the document type they act on. */
  documentType: string | null;
}

export interface ServiceView {
  service: CatalogEntry;
  version: ResolvedVersion;
  feesByLevel: Record<string, FeeLine[]>;
  /** For a follow-up service: the acting party's documents it can be requested for. */
  documents: DocumentOption[];
}

/** One of the applicant's documents for a follow-up service: available now or not, the window and any open request. */
export interface DocumentOption {
  id: string;
  number: string;
  status: string;
  issuedOn: string;
  expiresOn: string | null;
  isAvailable: boolean;
  opensOn: string | null;
  closesOn: string | null;
  reasons: EligibilityFailure[];
  openRequestId: string | null;
  openRequestNumber: string | null;
}

export interface FollowUpService {
  service: CatalogEntry;
  document: DocumentOption;
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
  /** The document a follow-up request is for. */
  sourceDocumentNumber: string | null;
}

export interface FieldError {
  field: string | null;
  code: string;
  messageAr: string;
  messageEn: string;
  step: string | null;
}

/** A file uploaded with a request as the answer of a file question (the answer holds the ids). */
export interface RequestFileInfo {
  id: string;
  questionCode: string;
  fileName: string;
  contentType: string;
  length: number;
  uploadedAt: string;
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
  /** Files uploaded with the request. */
  files?: RequestFileInfo[] | null;
  /** Bills issued by the request's payment steps. */
  bills?: BillInfo[] | null;
  /** Visits requested by the request's visit steps. */
  visits?: VisitInfo[] | null;
}

export type VisitStatus = 'PENDING' | 'SCHEDULED' | 'IN_PROGRESS' | 'SENT' | 'COMPLETED' | 'CANCELLED';

/** A visit as the applicant sees it: its type, when it is due or scheduled, and its outcome. */
export interface VisitInfo {
  id: string;
  number: string;
  visitType: string;
  typeNameAr: string;
  typeNameEn: string;
  mode: 'FIELD' | 'EXTERNAL' | 'MANUAL';
  status: VisitStatus;
  requestedAt: string;
  scheduledAt: string | null;
  dueAt: string | null;
  completedAt: string | null;
  outcome: string | null;
  outcomeNameAr: string | null;
  outcomeNameEn: string | null;
  outcomeKind: 'PASS' | 'FAIL' | 'OTHER' | null;
  siteAddress: string | null;
}

export type BillStatus = 'PENDING' | 'PAID' | 'EXPIRED' | 'CANCELLED';

export interface BillLine {
  code: string;
  nameAr: string;
  nameEn: string;
  net: number;
  vat: number;
  total: number;
}

/** A bill: what to pay, the number to pay with, until when, and how it was paid. */
export interface BillInfo {
  id: string;
  number: string;
  providerCode: string;
  providerBillNumber: string | null;
  billerCode: string | null;
  status: BillStatus;
  lines: BillLine[];
  total: number;
  issuedAt: string;
  dueAt: string;
  paidAt: string | null;
  paymentReference: string | null;
  payUrl: string | null;
  instructionsAr: string | null;
  instructionsEn: string | null;
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
