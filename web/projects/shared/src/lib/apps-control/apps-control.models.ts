/** Contracts of the Apps Control API (`/api/apps-control/v1`). Enum values are UPPER_SNAKE text. */

export interface MeInfo {
  userName: string;
  nameAr: string | null;
  nameEn: string | null;
  preferredLanguage: string | null;
  app: string;
  roles: string[];
  orgUnits: string[];
  orgUnit: string | null;
  positions: string[];
}

export type MenuItemType =
  'GROUP' | 'SCREEN' | 'SERVICE' | 'REPORT' | 'DASHBOARD' | 'QUEUE' | 'URL';

export interface MenuNode {
  code: string;
  nameAr: string;
  nameEn: string;
  itemType: MenuItemType;
  target: string | null;
  icon: string | null;
  descriptionAr: string | null;
  descriptionEn: string | null;
  children: MenuNode[];
}

export interface MenuGroupNode {
  code: string;
  nameAr: string;
  nameEn: string;
  icon: string | null;
  items: MenuNode[];
}

export interface LovItem {
  value: string;
  labelAr: string;
  labelEn: string;
  parent: string | null;
}

export interface LovPage {
  lov: string;
  items: LovItem[];
  defaultValue: string | null;
  hasMore: boolean;
}

export type ChangeSetStatus = 'DRAFT' | 'SUBMITTED' | 'APPROVED' | 'REJECTED' | 'PUBLISHED';
export type ChangeOperation = 'CREATE' | 'UPDATE' | 'DELETE';

export interface ChangeSetSummary {
  id: string;
  area: string;
  title: string;
  status: ChangeSetStatus;
  requiresApproval: boolean;
  createdBy: string;
  createdAt: string;
  submittedBy: string | null;
  submittedAt: string | null;
  reviewedBy: string | null;
  reviewedAt: string | null;
  reviewComment: string | null;
  publishedBy: string | null;
  publishedAt: string | null;
  itemCount: number;
  isMine: boolean;
}

export interface ChangeItemInfo {
  id: string;
  sequence: number;
  entityType: string;
  entityCode: string;
  operation: ChangeOperation;
  before: Record<string, unknown> | null;
  after: Record<string, unknown> | null;
}

export interface ChangeSetDetail {
  changeSet: ChangeSetSummary;
  items: ChangeItemInfo[];
}

export interface EntityTypeInfo {
  entityType: string;
  area: string;
  resourceCode: string;
  actions: string[];
  /** Changes of this type wait for a checker; otherwise saving publishes at once. */
  requiresApproval: boolean;
}

/** Snapshot of a configuration row as returned by the admin endpoints. */
export interface ConfigRow {
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
  owner: 'SYSTEM' | 'ENTITY';
  [field: string]: unknown;
}

export interface DomainItem {
  code: string;
  nameAr: string;
  nameEn: string;
  parentCode: string | null;
  displayOrder: number;
  isDefault: boolean;
  attributes: Record<string, unknown>;
}

export interface FooterLink {
  labelAr: string;
  labelEn: string;
  url: string;
}

/** Entity name, logo and footer links shown by every app's shell. */
export interface BrandingInfo {
  nameAr: string;
  nameEn: string;
  logoUrl: string | null;
  footerLinks: FooterLink[];
  /** Main colour of the internal system (`#RRGGBB`), when chosen. */
  internalThemeColor?: string | null;
  /** Main colour of the portal (`#RRGGBB`), when chosen. */
  portalThemeColor?: string | null;
  footerImageUrl?: string | null;
  loginImageUrl?: string | null;
  /** Behind the titles of the portal's pages. */
  portalHeroImageUrl?: string | null;
  contact?: EntityContact | null;
}

/** How the public reaches the entity. */
export interface EntityContact {
  phone?: string | null;
  email?: string | null;
  addressAr?: string | null;
  addressEn?: string | null;
  mapUrl?: string | null;
  social: SocialLink[];
}

/** X, LINKEDIN, INSTAGRAM, YOUTUBE, FACEBOOK, SNAPCHAT, TIKTOK or WHATSAPP. */
export interface SocialLink {
  network: string;
  url: string;
}

export interface UploadedAsset {
  key: string;
  url: string;
}

export interface UiTextItem {
  key: string;
  textAr: string;
  textEn: string;
}

/** RFC 7807 problem with optional field errors, as returned by the API. */
export interface ApiProblem {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

/** Field errors and messages of an API problem, ready to show. */
export function problemMessages(error: unknown): string[] {
  const problem = (error as { error?: ApiProblem } | null)?.error;
  if (problem?.errors) {
    return Object.entries(problem.errors).flatMap(([field, messages]) =>
      messages.map((m) => (field ? `${field}: ${m}` : m)),
    );
  }
  if (problem?.detail) {
    return [problem.detail];
  }
  if (problem?.title) {
    return [problem.title];
  }
  return [(error as { message?: string } | null)?.message ?? 'Request failed'];
}

/** One item of a checked package: whether its row exists and what is wrong with it (field → messages). */
export interface PackageItemCheck {
  index: number;
  entityType: string | null;
  entityCode: string | null;
  operation: string | null;
  area: string | null;
  exists: boolean;
  payload: Record<string, unknown> | null;
  errors: Record<string, string[]>;
  /** An update that would leave the row as it is; the import skips it. */
  unchanged?: boolean;
  /** The row as it is now, when it exists. */
  current?: Record<string, unknown> | null;
  /** The row as the package would leave it: the payload read as the server reads it, defaults filled in. */
  incoming?: Record<string, unknown> | null;
}

/** Rows of one type to export: all of them, or the given codes. */
export interface ExportSelection {
  entityType: string;
  codes?: string[] | null;
}

export interface ExportRequest {
  title?: string;
  /** In apply order: types before the types that refer to them. */
  types: ExportSelection[];
  /** Also export rows maintained by the product (they come with every installation). */
  includeSystem?: boolean;
  /** Skip codes that do not exist instead of failing (used when following references). */
  skipMissing?: boolean;
}

export interface ExportedItem {
  entityType: string;
  entityCode: string;
  operation: 'UPSERT';
  payload: Record<string, unknown>;
}

/** An exported configuration package, importable as is here or in another environment. */
export interface ExportedPackage {
  title: string;
  exportedAt: string;
  items: ExportedItem[];
}

/** A package checked without saving anything. */
export interface PackageCheck {
  title: string | null;
  isValid: boolean;
  errors: Record<string, string[]>;
  items: PackageItemCheck[];
}

/** What account management this installation offers (see `AppsControl:Identity`). */
export interface AccountCapabilities {
  provider: string;
  mode: 'Managed' | 'Federated';
  canCreate: boolean;
  canSetPasswords: boolean;
  canEnable: boolean;
  federations: string[];
}

/** The sign-in account behind a user. `source` is `LOCAL` or the directory / identity provider that owns it. */
export interface AccountView {
  userName: string;
  exists: boolean;
  enabled: boolean;
  source: string;
  canSetPassword: boolean;
  email: string | null;
  emailVerified: boolean;
  requiredActions: string[];
  createdAt: string | null;
}

export interface NewAccountRequest {
  userName: string;
  email: string | null;
  firstName: string | null;
  lastName: string | null;
  temporaryPassword: string | null;
  sendSetupEmail: boolean;
}
