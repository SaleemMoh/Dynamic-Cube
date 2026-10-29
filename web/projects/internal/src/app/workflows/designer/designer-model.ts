import { JsonValue, LocalizedText } from '@dynamic-cube/shared';
import { GraphDefinition, graphEdges, layout } from '../workflow-graph';

/** The node types the engine runs; the palette offers exactly these. */
export const NODE_TYPES = ['START', 'HUMAN_TASK', 'PARALLEL_SPLIT', 'JOIN', 'GATEWAY', 'DECISION', 'DOCUMENT', 'PAYMENT', 'VISIT', 'END'] as const;
export type NodeType = (typeof NODE_TYPES)[number];

export type JsonRule = JsonValue;

export interface ActionDef {
  code: string;
  kind: 'COMPLETE' | 'APPROVE' | 'REJECT' | 'RETURN';
  nameAr: string;
  nameEn: string;
  to?: string | null;
  commentRequired?: boolean;
  commentToApplicant?: boolean;
  displayStatus?: string | null;
  resumeAt?: string | null;
}

export interface AssignmentDef {
  target: 'ROLE' | 'USER' | 'PREVIOUS_ACTOR' | 'ORG_UNIT';
  role?: string | null;
  orgUnit?: string | null;
  position?: string | null;
  user?: string | null;
  step?: string | null;
  /** Org-structure routing: where the unit comes from. */
  unitFrom?: 'FIXED' | 'RULE' | 'ACTOR' | null;
  unitRule?: JsonRule;
  levelsUp?: number;
  includeSubUnits?: boolean;
  climbWhenEmpty?: boolean;
  mode: 'QUEUE' | 'DISTRIBUTE' | 'AUTO';
  distributorRole?: string | null;
}

export interface SlaDef {
  workingDays?: number | null;
  byLevel?: Record<string, number>;
  warnPercent?: number;
  escalateToRole?: string | null;
}

export interface LinesDef {
  source?: JsonRule;
  as?: string | null;
  fields?: Record<string, JsonRule>;
}

/** One node as the server reads it (unknown properties are kept as they are). */
export interface NodeDef {
  code: string;
  type: NodeType;
  nameAr: string;
  nameEn: string;
  next?: string | null;
  assignment?: AssignmentDef | null;
  actions?: ActionDef[];
  displayStatus?: string | null;
  sla?: SlaDef | null;
  excludeActorsOf?: string[];
  branches?: string[];
  mode?: 'ALL' | 'ANY' | 'COUNT';
  count?: number | null;
  routes?: { when?: JsonRule; to: string }[];
  default?: string | null;
  outcome?: 'APPROVED' | 'REJECTED' | 'CLOSED' | null;
  ruleSet?: string | null;
  input?: Record<string, JsonRule>;
  result?: string | null;
  documentType?: string | null;
  holder?: JsonRule;
  data?: Record<string, JsonRule>;
  lines?: LinesDef | null;
  reason?: string | null;
  target?: 'ISSUE' | 'SOURCE';
  validity?: 'KEEP' | 'RESET' | 'RENEW' | 'EXTEND' | null;
  validityMonths?: number | null;
  amount?: JsonRule;
  vatRate?: number | null;
  provider?: string | null;
  dueDays?: number | null;
  expired?: string | null;
  visitType?: string | null;
  site?: JsonRule;
  location?: JsonRule;
  instructions?: string | null;
  /** Visit: outcome code → the node it leads to. */
  outcomes?: Record<string, string>;
  cancelled?: string | null;
  [key: string]: unknown;
}

export interface Position {
  x: number;
  y: number;
}

export interface WorkflowDraft {
  slaWorkingDays?: number | null;
  nodes: NodeDef[];
  layout?: Record<string, Position>;
}

export const NODE_W = 200;
export const NODE_H = 64;
export const GRID = 8;
/** Largest coordinate the server accepts. */
export const MAX_CANVAS = 20000;

/** Where an arrow leaves a node: the property that holds its target. */
export type OutletKind = 'next' | 'branch' | 'route' | 'default' | 'action' | 'expired' | 'resume' | 'cancelled' | 'outcome';

export interface Outlet {
  kind: OutletKind;
  /** Position in the list (branches, routes, actions). */
  index?: number;
}

/** A connection drawn on the canvas. */
export interface Edge {
  from: string;
  to: string;
  outlet: Outlet;
  /** Text on the arrow (an action's name, a route number…); a translation key when it starts with `designer.`. */
  label: LocalizedText | string;
}

/** A handle at the bottom of a node to draw a new connection from. */
export interface Handle {
  kind: OutletKind;
  /** Translation key of its tooltip. */
  hint: string;
  /** Short mark drawn on it. */
  mark: string;
}

/** The handles a node offers: what dragging from each creates. */
export function handles(node: NodeDef): Handle[] {
  switch (node.type) {
    case 'START':
    case 'JOIN':
    case 'DECISION':
    case 'DOCUMENT':
      return [{ kind: 'next', hint: 'designer.handle.next', mark: '' }];
    case 'HUMAN_TASK':
      return [{ kind: 'action', hint: 'designer.handle.action', mark: '+' }];
    case 'PARALLEL_SPLIT':
      return [{ kind: 'branch', hint: 'designer.handle.branch', mark: '+' }];
    case 'GATEWAY':
      return [
        { kind: 'route', hint: 'designer.handle.route', mark: '?' },
        { kind: 'default', hint: 'designer.handle.default', mark: '*' },
      ];
    case 'PAYMENT':
      return [
        { kind: 'next', hint: 'designer.handle.paid', mark: '✓' },
        { kind: 'expired', hint: 'designer.handle.unpaid', mark: '✗' },
      ];
    case 'VISIT':
      return [
        { kind: 'next', hint: 'designer.handle.visited', mark: '✓' },
        { kind: 'outcome', hint: 'designer.handle.outcome', mark: '?' },
        { kind: 'cancelled', hint: 'designer.handle.visitCancelled', mark: '✗' },
      ];
    case 'END':
      return [];
  }
}

/** Every connection of the graph, in reading order. */
export function edges(def: WorkflowDraft): Edge[] {
  const out: Edge[] = [];
  for (const n of def.nodes) {
    if (n.next)
      out.push({
        from: n.code,
        to: n.next,
        outlet: { kind: 'next' },
        label: n.type === 'PAYMENT' ? 'designer.edge.paid' : n.type === 'VISIT' ? 'designer.edge.visited' : '',
      });
    Object.entries(n.outcomes ?? {}).forEach(([outcome, to], index) => out.push({ from: n.code, to, outlet: { kind: 'outcome', index }, label: outcome }));
    if (n.cancelled)
      out.push({
        from: n.code,
        to: n.cancelled,
        outlet: { kind: 'cancelled' },
        label: 'designer.edge.visitCancelled',
      });
    (n.branches ?? []).forEach((to, index) => out.push({ from: n.code, to, outlet: { kind: 'branch', index }, label: '' }));
    (n.routes ?? []).forEach((r, index) =>
      out.push({
        from: n.code,
        to: r.to,
        outlet: { kind: 'route', index },
        label: `#${index + 1}`,
      }),
    );
    if (n.default)
      out.push({
        from: n.code,
        to: n.default,
        outlet: { kind: 'default' },
        label: 'designer.edge.default',
      });
    if (n.expired)
      out.push({
        from: n.code,
        to: n.expired,
        outlet: { kind: 'expired' },
        label: 'designer.edge.unpaid',
      });
    (n.actions ?? []).forEach((a, index) => {
      if (a.kind === 'RETURN') {
        if (a.resumeAt)
          out.push({
            from: n.code,
            to: a.resumeAt,
            outlet: { kind: 'resume', index },
            label: { ar: a.nameAr || a.code, en: a.nameEn || a.code },
          });
      } else if (a.to) {
        out.push({
          from: n.code,
          to: a.to,
          outlet: { kind: 'action', index },
          label: { ar: a.nameAr || a.code, en: a.nameEn || a.code },
        });
      }
    });
  }
  return out;
}

export function sameOutlet(a: Outlet, b: Outlet): boolean {
  return a.kind === b.kind && (a.index ?? -1) === (b.index ?? -1);
}

const PREFIX: Record<NodeType, string> = {
  START: 'START',
  HUMAN_TASK: 'TASK',
  PARALLEL_SPLIT: 'SPLIT',
  JOIN: 'JOIN',
  GATEWAY: 'GATEWAY',
  DECISION: 'DECISION',
  DOCUMENT: 'DOCUMENT',
  PAYMENT: 'PAYMENT',
  VISIT: 'VISIT',
  END: 'END',
};

const DEFAULT_NAMES: Record<NodeType, LocalizedText> = {
  START: { ar: 'البداية', en: 'Start' },
  HUMAN_TASK: { ar: 'مهمة جديدة', en: 'New task' },
  PARALLEL_SPLIT: { ar: 'تفرع متوازٍ', en: 'Parallel split' },
  JOIN: { ar: 'دمج الفروع', en: 'Join' },
  GATEWAY: { ar: 'توجيه بشرط', en: 'Gateway' },
  DECISION: { ar: 'تقييم القواعد', en: 'Decision' },
  DOCUMENT: { ar: 'إصدار مستند', en: 'Issue document' },
  PAYMENT: { ar: 'سداد الرسوم', en: 'Payment' },
  VISIT: { ar: 'زيارة ميدانية', en: 'Site visit' },
  END: { ar: 'النهاية', en: 'End' },
};

/** A code not used by any node: the type's prefix and the first free number (START has no number). */
export function freeCode(def: WorkflowDraft, type: NodeType): string {
  const used = new Set(def.nodes.map((n) => n.code));
  if (type === 'START' && !used.has('START')) return 'START';
  for (let i = 1; ; i++) {
    const code = `${PREFIX[type]}_${i}`;
    if (!used.has(code)) return code;
  }
}

/** A new node of a type with what the server needs filled in where there is an obvious default. */
export function newNode(def: WorkflowDraft, type: NodeType): NodeDef {
  const names = DEFAULT_NAMES[type];
  const node: NodeDef = { code: freeCode(def, type), type, nameAr: names.ar, nameEn: names.en };
  switch (type) {
    case 'HUMAN_TASK':
      return { ...node, assignment: { target: 'ROLE', role: null, mode: 'QUEUE' }, actions: [] };
    case 'PARALLEL_SPLIT':
      return { ...node, branches: [] };
    case 'JOIN':
      return { ...node, mode: 'ALL', next: null };
    case 'GATEWAY':
      return { ...node, routes: [], default: null };
    case 'DOCUMENT':
      return { ...node, documentType: null, target: 'ISSUE', next: null };
    case 'END':
      return { ...node, outcome: 'APPROVED' };
    case 'VISIT':
      return { ...node, visitType: null, outcomes: {}, next: null, cancelled: null };
    default:
      return { ...node, next: null };
  }
}

function clone(def: WorkflowDraft): WorkflowDraft {
  return structuredClone(def);
}

function snap(value: number): number {
  return Math.min(MAX_CANVAS, Math.max(0, Math.round(value / GRID) * GRID));
}

export function addNode(def: WorkflowDraft, type: NodeType, at: Position): { def: WorkflowDraft; code: string } {
  const next = clone(def);
  const node = newNode(next, type);
  next.nodes.push(node);
  next.layout = { ...(next.layout ?? {}), [node.code]: { x: snap(at.x), y: snap(at.y) } };
  return { def: next, code: node.code };
}

export function moveNode(def: WorkflowDraft, code: string, at: Position): WorkflowDraft {
  return { ...def, layout: { ...(def.layout ?? {}), [code]: { x: snap(at.x), y: snap(at.y) } } };
}

const ACTION_NAMES: Record<ActionDef['kind'], LocalizedText> = {
  APPROVE: { ar: 'اعتماد', en: 'Approve' },
  REJECT: { ar: 'رفض', en: 'Reject' },
  COMPLETE: { ar: 'إحالة', en: 'Send on' },
  RETURN: { ar: 'إعادة للتعديل', en: 'Return for correction' },
};

/** A task action not used by the task yet, named after its kind. */
export function newAction(node: NodeDef, kind: ActionDef['kind'], to: string | null): ActionDef {
  const used = new Set((node.actions ?? []).map((a) => a.code));
  let code: string = kind;
  for (let i = 2; used.has(code); i++) code = `${kind}_${i}`;
  return { code, kind, nameAr: ACTION_NAMES[kind].ar, nameEn: ACTION_NAMES[kind].en, to };
}

/**
 * Draws a connection from a handle of `from` to `to`: sets the single target (next, default, expired) or adds a
 * branch, a route (without its condition yet) or an action (approve or reject when it leads to such an end).
 * Returns the definition unchanged when the connection is not possible.
 */
export function connect(def: WorkflowDraft, from: string, kind: OutletKind, to: string): WorkflowDraft {
  const target = def.nodes.find((n) => n.code === to);
  if (!target || target.type === 'START' || from === to) return def;
  const next = clone(def);
  const node = next.nodes.find((n) => n.code === from);
  if (!node) return def;
  switch (kind) {
    case 'next':
      node.next = to;
      break;
    case 'default':
      node.default = to;
      break;
    case 'expired':
      node.expired = to;
      break;
    case 'cancelled':
      node.cancelled = to;
      break;
    case 'outcome': {
      // A placeholder outcome code: the panel picks the visit type's outcome it stands for.
      const used = node.outcomes ?? {};
      let code = 'OUTCOME';
      for (let i = 2; code in used; i++) code = `OUTCOME_${i}`;
      node.outcomes = { ...used, [code]: to };
      break;
    }
    case 'branch':
      if ((node.branches ?? []).includes(to)) return def;
      node.branches = [...(node.branches ?? []), to];
      break;
    case 'route':
      node.routes = [...(node.routes ?? []), { when: null, to }];
      break;
    case 'action': {
      const kindOf: ActionDef['kind'] =
        target.type === 'END' && target.outcome === 'APPROVED' ? 'APPROVE' : target.type === 'END' && target.outcome === 'REJECTED' ? 'REJECT' : 'COMPLETE';
      node.actions = [...(node.actions ?? []), newAction(node, kindOf, to)];
      break;
    }
    case 'resume':
      return def;
  }
  return next;
}

/** Removes a connection: a list entry (branch, route, action) goes with it; a single target becomes empty. */
export function disconnect(def: WorkflowDraft, from: string, outlet: Outlet): WorkflowDraft {
  const next = clone(def);
  const node = next.nodes.find((n) => n.code === from);
  if (!node) return def;
  const i = outlet.index ?? -1;
  switch (outlet.kind) {
    case 'next':
      node.next = null;
      break;
    case 'default':
      node.default = null;
      break;
    case 'expired':
      node.expired = null;
      break;
    case 'cancelled':
      node.cancelled = null;
      break;
    case 'outcome':
      node.outcomes = Object.fromEntries(Object.entries(node.outcomes ?? {}).filter((_, k) => k !== i));
      break;
    case 'branch':
      node.branches = (node.branches ?? []).filter((_, k) => k !== i);
      break;
    case 'route':
      node.routes = (node.routes ?? []).filter((_, k) => k !== i);
      break;
    case 'action':
      node.actions = (node.actions ?? []).filter((_, k) => k !== i);
      break;
    case 'resume':
      if (node.actions?.[i]) node.actions[i].resumeAt = null;
      break;
  }
  return next;
}

/** Removes a node and every reference to it. */
export function removeNode(def: WorkflowDraft, code: string): WorkflowDraft {
  const next = clone(def);
  next.nodes = next.nodes.filter((n) => n.code !== code);
  for (const n of next.nodes) {
    if (n.next === code) n.next = null;
    if (n.default === code) n.default = null;
    if (n.expired === code) n.expired = null;
    if (n.cancelled === code) n.cancelled = null;
    if (n.outcomes) n.outcomes = Object.fromEntries(Object.entries(n.outcomes).filter(([, to]) => to !== code));
    if (n.branches) n.branches = n.branches.filter((b) => b !== code);
    if (n.routes) n.routes = n.routes.filter((r) => r.to !== code);
    if (n.actions) {
      n.actions = n.actions.filter((a) => a.to !== code);
      for (const a of n.actions) if (a.resumeAt === code) a.resumeAt = null;
    }
    if (n.excludeActorsOf) n.excludeActorsOf = n.excludeActorsOf.filter((s) => s !== code);
    if (n.assignment?.step === code) n.assignment.step = null;
  }
  if (next.layout) {
    const rest = { ...next.layout };
    delete rest[code];
    next.layout = rest;
  }
  return next;
}

/** Changes a node's code everywhere it is referred to. */
export function renameNode(def: WorkflowDraft, from: string, to: string): WorkflowDraft {
  if (from === to || def.nodes.some((n) => n.code === to)) return def;
  const next = clone(def);
  const swap = (c: string | null | undefined) => (c === from ? to : c);
  for (const n of next.nodes) {
    if (n.code === from) n.code = to;
    n.next = swap(n.next);
    n.default = swap(n.default);
    n.expired = swap(n.expired);
    n.cancelled = swap(n.cancelled);
    if (n.outcomes) n.outcomes = Object.fromEntries(Object.entries(n.outcomes).map(([o, t]) => [o, swap(t)!]));
    if (n.branches) n.branches = n.branches.map((b) => swap(b)!);
    if (n.routes) for (const r of n.routes) r.to = swap(r.to)!;
    if (n.actions) {
      for (const a of n.actions) {
        a.to = swap(a.to);
        a.resumeAt = swap(a.resumeAt);
      }
    }
    if (n.excludeActorsOf) n.excludeActorsOf = n.excludeActorsOf.map((s) => swap(s)!);
    if (n.assignment) n.assignment.step = swap(n.assignment.step);
  }
  if (next.layout?.[from]) {
    const rest = { ...next.layout, [to]: next.layout[from] };
    delete rest[from];
    next.layout = rest;
  }
  return next;
}

/** Replaces one node (edited in the side panel), keeping its place in the list. */
export function replaceNode(def: WorkflowDraft, code: string, node: NodeDef): WorkflowDraft {
  const renamed = node.code !== code ? renameNode(def, code, node.code) : def;
  return { ...renamed, nodes: renamed.nodes.map((n) => (n.code === node.code ? node : n)) };
}

/** Positions for every node: the saved ones, and the automatic layout for nodes without one. */
export function positions(def: WorkflowDraft): Record<string, Position> {
  const saved = def.layout ?? {};
  if (def.nodes.every((n) => saved[n.code])) return saved;
  const auto = arrange(def);
  return Object.fromEntries(def.nodes.map((n) => [n.code, saved[n.code] ?? auto[n.code]]));
}

/** Arranges all nodes top to bottom from the start (the read-only graph's layout). */
export function arrange(def: WorkflowDraft): Record<string, Position> {
  const graph = def as unknown as GraphDefinition;
  const placed = layout(graph, graphEdges(graph, 'en'));
  // Its rows are 120 apart, room for these taller boxes too.
  return Object.fromEntries(placed.nodes.map((p) => [p.node.code, { x: snap(p.x + 24), y: snap(p.y + 16) }]));
}

/** What the server will be sent: the layout reduced to the nodes that exist. */
export function toDefinition(def: WorkflowDraft): WorkflowDraft {
  const at = positions(def);
  return { ...def, layout: Object.fromEntries(def.nodes.map((n) => [n.code, at[n.code]])) };
}

export interface Problem {
  /** The node concerned, when there is one (the list selects it). */
  node?: string;
  /** Translation key. */
  key: string;
  params?: Record<string, string | number>;
  /** Warnings do not stop saving; the server decides. */
  warning?: boolean;
}

const CODE = /^[A-Z][A-Z0-9_-]{0,50}$/;

/** What is wrong with the graph so far, the same structural checks the server makes and a few hints. */
export function check(def: WorkflowDraft): Problem[] {
  const problems: Problem[] = [];
  const byCode = new Map<string, NodeDef>();
  for (const n of def.nodes) {
    if (byCode.has(n.code)) problems.push({ node: n.code, key: 'designer.problem.duplicate', params: { code: n.code } });
    byCode.set(n.code, n);
  }
  const starts = def.nodes.filter((n) => n.type === 'START');
  if (starts.length !== 1) problems.push({ key: 'designer.problem.oneStart' });
  if (!def.nodes.some((n) => n.type === 'END')) problems.push({ key: 'designer.problem.noEnd' });
  const p = (node: NodeDef, key: string, params?: Record<string, string | number>, warning = false) => problems.push({ node: node.code, key, params, warning });
  const target = (node: NodeDef, to: string | null | undefined, key: string) => {
    if (!to) p(node, key);
    else if (!byCode.has(to)) p(node, 'designer.problem.missingTarget', { to });
  };
  for (const n of def.nodes) {
    if (!CODE.test(n.code)) p(n, 'designer.problem.code');
    if (!n.nameAr?.trim() || !n.nameEn?.trim()) p(n, 'designer.problem.names');
    switch (n.type) {
      case 'START':
      case 'JOIN':
      case 'DECISION':
      case 'DOCUMENT':
        target(n, n.next, 'designer.problem.noNext');
        break;
      case 'PAYMENT':
        target(n, n.next, 'designer.problem.noPaid');
        target(n, n.expired, 'designer.problem.noUnpaid');
        break;
      case 'VISIT':
        if (!n.visitType) p(n, 'designer.problem.visitType');
        target(n, n.next, 'designer.problem.noVisited');
        target(n, n.cancelled, 'designer.problem.noVisitCancelled');
        for (const [outcome, to] of Object.entries(n.outcomes ?? {})) {
          if (!CODE.test(outcome)) p(n, 'designer.problem.outcomeCode', { outcome });
          target(n, to, 'designer.problem.noVisited');
        }
        break;
      case 'HUMAN_TASK': {
        const a = n.assignment;
        if (
          !a ||
          (a.target === 'ROLE' && !a.role) ||
          (a.target === 'USER' && !a.user) ||
          (a.target === 'PREVIOUS_ACTOR' && byCode.get(a.step ?? '')?.type !== 'HUMAN_TASK') ||
          (a.target === 'ORG_UNIT' &&
            (!a.unitFrom || (a.unitFrom === 'FIXED' && !a.orgUnit) || (a.unitFrom === 'RULE' && (a.unitRule === null || a.unitRule === undefined))))
        ) {
          p(n, 'designer.problem.assignment');
        }
        if (a?.mode === 'DISTRIBUTE' && !a.distributorRole) p(n, 'designer.problem.distributor');
        if (!(n.actions ?? []).length) p(n, 'designer.problem.noActions');
        for (const action of n.actions ?? []) {
          if (action.kind !== 'RETURN') target(n, action.to, 'designer.problem.actionTarget');
          if (!CODE.test(action.code) || !action.nameAr?.trim() || !action.nameEn?.trim()) p(n, 'designer.problem.action', { action: action.code });
        }
        break;
      }
      case 'PARALLEL_SPLIT':
        if ((n.branches ?? []).length < 2) p(n, 'designer.problem.branches');
        break;
      case 'GATEWAY':
        if (!(n.routes ?? []).length) p(n, 'designer.problem.routes');
        (n.routes ?? []).forEach((r, i) => {
          if (r.when === null || r.when === undefined) p(n, 'designer.problem.condition', { route: i + 1 });
        });
        target(n, n.default, 'designer.problem.noDefault');
        break;
      case 'END':
        if (!n.outcome) p(n, 'designer.problem.outcome');
        break;
    }
    if (n.type === 'DECISION' && !n.ruleSet) p(n, 'designer.problem.ruleSet');
    if (n.type === 'DOCUMENT' && !n.documentType) p(n, 'designer.problem.documentType');
  }
  // Reachable from START, as the server requires.
  const start = starts[0];
  if (start) {
    const seen = new Set([start.code]);
    const pending = [start.code];
    const all = edges(def);
    while (pending.length) {
      const code = pending.pop()!;
      for (const e of all.filter((x) => x.from === code)) {
        if (byCode.has(e.to) && !seen.has(e.to)) {
          seen.add(e.to);
          pending.push(e.to);
        }
      }
    }
    for (const n of def.nodes) if (!seen.has(n.code)) p(n, 'designer.problem.unreachable');
  }
  // A split's branches usually meet at a join.
  if (def.nodes.some((n) => n.type === 'PARALLEL_SPLIT') && !def.nodes.some((n) => n.type === 'JOIN')) {
    problems.push({ key: 'designer.problem.noJoin', warning: true });
  }
  return problems;
}
