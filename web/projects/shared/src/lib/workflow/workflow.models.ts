/** Action Center and workflow types (see the Workflow module's API). */
export type InboxBox = 'MINE' | 'QUEUE' | 'DISTRIBUTE' | 'ESCALATED';
export type SlaState = 'ON_TRACK' | 'WARNING' | 'BREACHED';
export type WorkTaskStatus = 'OPEN' | 'COMPLETED' | 'CANCELLED';
export type AssignmentMode = 'QUEUE' | 'DISTRIBUTE' | 'AUTO';
export type ActionKind = 'COMPLETE' | 'APPROVE' | 'REJECT' | 'RETURN';
export type InstanceStatus = 'RUNNING' | 'WAITING_CUSTOMER' | 'COMPLETED' | 'CANCELLED';

export interface TaskSummary {
  id: string;
  instanceId: string;
  nodeCode: string;
  nameAr: string;
  nameEn: string;
  status: WorkTaskStatus;
  subjectId: string;
  subjectType: string;
  subjectNumber: string;
  titleAr: string;
  titleEn: string;
  partyNameAr: string;
  partyNameEn: string;
  serviceCode: string | null;
  levelCode: string | null;
  assignee: string | null;
  assigneeNameAr: string | null;
  assigneeNameEn: string | null;
  mode: AssignmentMode;
  createdAt: string;
  dueAt: string | null;
  sla: SlaState;
  completedBy: string | null;
  actionCode: string | null;
  /** The branch the task belongs to (ADR-0036 step 6). */
  branchCode?: string | null;
}

export interface TaskPage {
  items: TaskSummary[];
  total: number;
}

export interface InboxCounts {
  mine: number;
  queue: number;
  distribute: number;
  escalated: number;
}

export interface ActionView {
  code: string;
  kind: ActionKind;
  nameAr: string;
  nameEn: string;
  commentRequired: boolean;
  visibleToApplicant: boolean;
}

export interface TaskPermissions {
  canClaim: boolean;
  canRelease: boolean;
  canAssign: boolean;
  canAct: boolean;
  canNote: boolean;
}

export interface TimelineItem {
  at: string;
  type: string;
  actor: string;
  /** A user name, or `system` / `applicant`. */
  actorNameAr: string;
  actorNameEn: string;
  onBehalfOf: string | null;
  nodeCode: string | null;
  nodeNameAr: string | null;
  nodeNameEn: string | null;
  actionCode: string | null;
  actionNameAr: string | null;
  actionNameEn: string | null;
  target: string | null;
  targetNameAr: string | null;
  targetNameEn: string | null;
  comment: string | null;
  visibleToApplicant: boolean;
}

export interface InstanceView {
  id: string;
  workflowCode: string;
  versionNumber: number;
  status: InstanceStatus;
  outcome: string | null;
  startedAt: string;
  completedAt: string | null;
  dueAt: string | null;
  sla: SlaState;
  tasks: TaskSummary[];
  timeline: TimelineItem[];
  /** What automated steps recorded: `decisions.<result>` (rule-set outputs) and `documents.<step>` (issued documents). */
  facts: InstanceFacts;
  /** The workflow version's definition (nodes and routes), to draw where the instance is. */
  definition?: { nodes: { code: string; type: string }[] } | null;
  /** Steps the instance is at now. */
  currentNodes?: string[];
  /** Steps it has been through. */
  visitedNodes?: string[];
}

export interface InstanceFacts {
  decisions?: Record<string, Record<string, unknown>>;
  /** Items decided one by one at tasks: `items.<task>` (list, approved, rejected, counts). */
  items?: Record<string, Record<string, unknown>>;
  /** What employees filled in on tasks: `inputs.<task>.<field>`. */
  inputs?: Record<string, Record<string, unknown>>;
  documents?: Record<string, { id: string; number: string; version: number; amended: boolean }>;
}

export interface TaskDetail {
  task: TaskSummary;
  candidateRole: string | null;
  candidateOrgUnit: string | null;
  candidatePosition: string | null;
  candidateUser: string | null;
  distributorRole: string | null;
  comment: string | null;
  completedAt: string | null;
  onBehalfOf: string | null;
  permissions: TaskPermissions;
  actions: ActionView[];
  instance: InstanceView;
  /** What the employee fills in on the task. */
  fields?: TaskField[];
  /** What the fields hold now (saved, given last time, or the starting values). */
  inputs?: Record<string, unknown>;
  /** The task works decision steps out again with its inputs (shown live). */
  recalculates?: boolean;
  /** The items the task decides one by one, with the decisions so far. */
  items?: TaskItemView[];
}

export type ItemVerdict = 'APPROVED' | 'REJECTED';

/** One item of a task as the employee sees it: its key, label and the decision so far. */
export interface TaskItemView {
  key: string;
  label: string;
  decision: ItemVerdict | null;
  reason: string | null;
}

/** The decision on one item of a task (a rejection needs a reason). */
export interface ItemDecision {
  key: string;
  decision: ItemVerdict | null;
  reason: string | null;
}

export type TaskFieldType = 'TEXT' | 'LONG_TEXT' | 'NUMBER' | 'DATE' | 'CHECKBOX' | 'SELECT';

export interface TaskFieldOption {
  value: string;
  labelAr: string;
  labelEn: string;
}

/** A field an employee fills in on a task; kept as `inputs.<task>.<field>` for later rules and documents. */
export interface TaskField {
  code: string;
  labelAr: string;
  labelEn: string;
  hintAr?: string | null;
  hintEn?: string | null;
  type: TaskFieldType;
  /** Needed for actions that move on (complete, approve). */
  required?: boolean;
  options?: TaskFieldOption[];
  min?: number | null;
  max?: number | null;
  /** JSONLogic over the request's facts giving the starting value. */
  initial?: unknown;
}

/** A decision step worked out again with a task's inputs: the result recorded now and the one the inputs give. */
export interface RecalculatedDecision {
  step: string;
  nameAr: string;
  nameEn: string;
  result: string;
  before: Record<string, unknown> | null;
  after: Record<string, unknown> | null;
  problem: string | null;
}

export interface StaffOption {
  userName: string;
  nameAr: string;
  nameEn: string;
  openTasks: number;
}

export interface DelegationView {
  id: string;
  delegator: string;
  delegatorNameAr: string;
  delegatorNameEn: string;
  delegate: string;
  delegateNameAr: string;
  delegateNameEn: string;
  fromDate: string;
  toDate: string;
  reason: string | null;
  isCurrent: boolean;
}
