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
}

export interface InstanceFacts {
  decisions?: Record<string, Record<string, unknown>>;
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
