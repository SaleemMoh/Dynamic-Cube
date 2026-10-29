import { JsonValue, jsonLogic, isTruthy } from '@dynamic-cube/shared';
import { NodeDef, WorkflowDraft } from './designer-model';

/** Where processing is: a node, and the splits whose branches it is on (innermost last). */
export interface TestToken {
  id: number;
  node: string;
  splits: { id: number; branches: number }[];
}

export interface TestLogEntry {
  node: string;
  /** Translation key. */
  key: string;
  params?: Record<string, string | number>;
  error?: boolean;
}

export interface TestState {
  tokens: TestToken[];
  facts: Record<string, JsonValue>;
  log: TestLogEntry[];
  /** Arrivals at a join, per join and split. */
  arrivals: Record<string, number>;
  outcome: string | null;
  failed: boolean;
  nextId: number;
}

/** Steps that wait for the person trying the workflow: tasks (an action), payments (paid or not), visits (an outcome). */
export function waiting(def: WorkflowDraft, state: TestState): { token: TestToken; node: NodeDef }[] {
  const byCode = new Map(def.nodes.map((n) => [n.code, n]));
  return state.tokens
    .map((token) => ({ token, node: byCode.get(token.node)! }))
    .filter((t) => t.node && (t.node.type === 'HUMAN_TASK' || t.node.type === 'PAYMENT' || t.node.type === 'VISIT'));
}

/** Starts a run at START with sample facts and lets it go as far as it can on its own. */
export function startRun(def: WorkflowDraft, data: Record<string, JsonValue>, level: string | null): TestState {
  const start = def.nodes.find((n) => n.type === 'START');
  const feeTotal = typeof data['__feeTotal'] === 'number' ? (data['__feeTotal'] as number) : 0;
  const clean = { ...data };
  delete clean['__feeTotal'];
  const state: TestState = {
    tokens: start ? [{ id: 1, node: start.code, splits: [] }] : [],
    facts: {
      data: clean,
      level,
      party: { id: 'TEST' },
      actions: {},
      decisions: {},
      documents: {},
      payments: {},
      visits: {},
      feeTotal,
    },
    log: [],
    arrivals: {},
    outcome: null,
    failed: !start,
    nextId: 2,
  };
  if (!start) state.log.push({ node: '', key: 'designer.test.noStart', error: true });
  return advance(def, state);
}

/** The person takes an action on a task, settles a bill (`PAID` / `UNPAID`), or ends a visit (an outcome / `CANCELLED`). */
export function choose(def: WorkflowDraft, state: TestState, tokenId: number, choice: string): TestState {
  const next = structuredClone(state);
  const token = next.tokens.find((t) => t.id === tokenId);
  const node = def.nodes.find((n) => n.code === token?.node);
  if (!token || !node) return state;
  if (node.type === 'PAYMENT') {
    const paid = choice === 'PAID';
    (next.facts['payments'] as Record<string, JsonValue>)[node.code] = {
      status: paid ? 'PAID' : 'EXPIRED',
    };
    next.log.push({ node: node.code, key: paid ? 'designer.test.paid' : 'designer.test.unpaid' });
    return move(def, next, token, paid ? node.next : node.expired);
  }
  if (node.type === 'VISIT') {
    const cancelled = choice === 'CANCELLED';
    (next.facts['visits'] as Record<string, JsonValue>)[node.code] = cancelled ? { status: 'CANCELLED' } : { status: 'COMPLETED', outcome: choice, data: {} };
    next.log.push({ node: node.code, key: cancelled ? 'designer.test.visitCancelled' : 'designer.test.visited', params: { outcome: choice } });
    return move(def, next, token, cancelled ? node.cancelled : (node.outcomes?.[choice] ?? node.next));
  }
  const action = (node.actions ?? []).find((a) => a.code === choice);
  if (!action) return state;
  (next.facts['actions'] as Record<string, JsonValue>)[node.code] = action.code;
  if (action.kind === 'RETURN') {
    const resume = action.resumeAt ?? node.code;
    next.log.push({
      node: node.code,
      key: 'designer.test.returned',
      params: { action: action.nameEn || action.code, resume },
    });
    return move(def, next, token, resume);
  }
  next.log.push({
    node: node.code,
    key: 'designer.test.acted',
    params: { action: action.nameEn || action.code },
  });
  return move(def, next, token, action.to);
}

function move(def: WorkflowDraft, state: TestState, token: TestToken, to: string | null | undefined): TestState {
  if (!to || !def.nodes.some((n) => n.code === to)) {
    state.log.push({ node: token.node, key: 'designer.test.dangling', error: true });
    state.failed = true;
    state.tokens = state.tokens.filter((t) => t.id !== token.id);
    return state;
  }
  token.node = to;
  return advance(def, state);
}

const LIMIT = 500;

/** Moves every token on until it waits for a person, ends, or something is missing. */
function advance(def: WorkflowDraft, state: TestState): TestState {
  const byCode = new Map(def.nodes.map((n) => [n.code, n]));
  let steps = 0;
  let moved = true;
  while (moved && !state.failed) {
    moved = false;
    for (const token of [...state.tokens]) {
      if (!state.tokens.includes(token)) continue;
      const node = byCode.get(token.node);
      if (!node) {
        state.log.push({
          node: token.node,
          key: 'designer.test.missing',
          params: { code: token.node },
          error: true,
        });
        state.failed = true;
        break;
      }
      if (++steps > LIMIT) {
        state.log.push({ node: node.code, key: 'designer.test.loop', error: true });
        state.failed = true;
        break;
      }
      const go = (to: string | null | undefined) => {
        if (!to || !byCode.has(to)) {
          state.log.push({ node: node.code, key: 'designer.test.dangling', error: true });
          state.failed = true;
          return;
        }
        token.node = to;
        moved = true;
      };
      switch (node.type) {
        case 'START':
          state.log.push({ node: node.code, key: 'designer.test.started' });
          go(node.next);
          break;
        case 'HUMAN_TASK':
        case 'VISIT':
          break;
        case 'PAYMENT': {
          const fees = Number(state.facts['feeTotal'] ?? 0);
          const amount = node.amount === null || node.amount === undefined ? fees : Number(evaluate(node, node.amount, state) ?? 0);
          if (!(amount > 0)) {
            (state.facts['payments'] as Record<string, JsonValue>)[node.code] = {
              status: 'NOT_NEEDED',
            };
            state.log.push({ node: node.code, key: 'designer.test.nothingToPay' });
            go(node.next);
          }
          break;
        }
        case 'DECISION': {
          const name = node.result || node.code;
          (state.facts['decisions'] as Record<string, JsonValue>)[name] = {};
          state.log.push({
            node: node.code,
            key: 'designer.test.decision',
            params: { ruleSet: node.ruleSet ?? '?', name },
          });
          go(node.next);
          break;
        }
        case 'DOCUMENT':
          (state.facts['documents'] as Record<string, JsonValue>)[node.code] = {
            number: 'TEST-0001',
          };
          state.log.push({
            node: node.code,
            key: node.target === 'SOURCE' ? 'designer.test.amended' : 'designer.test.issued',
            params: { type: node.documentType ?? '?' },
          });
          go(node.next);
          break;
        case 'GATEWAY': {
          const routes = node.routes ?? [];
          let to: string | null | undefined = undefined;
          for (let i = 0; i < routes.length && to === undefined; i++) {
            const value = evaluate(node, routes[i].when, state);
            if (state.failed) break;
            if (isTruthy(value)) {
              to = routes[i].to;
              state.log.push({
                node: node.code,
                key: 'designer.test.route',
                params: { route: i + 1, to: routes[i].to },
              });
            }
          }
          if (state.failed) break;
          if (to === undefined) {
            to = node.default;
            state.log.push({
              node: node.code,
              key: 'designer.test.default',
              params: { to: node.default ?? '?' },
            });
          }
          go(to);
          break;
        }
        case 'PARALLEL_SPLIT': {
          const branches = node.branches ?? [];
          const split = { id: state.nextId++, branches: branches.length };
          state.log.push({
            node: node.code,
            key: 'designer.test.split',
            params: { count: branches.length },
          });
          state.tokens = state.tokens.filter((t) => t !== token);
          for (const b of branches) state.tokens.push({ id: state.nextId++, node: b, splits: [...token.splits, split] });
          moved = true;
          break;
        }
        case 'JOIN': {
          const split = token.splits[token.splits.length - 1];
          const key = `${node.code}:${split?.id ?? 0}`;
          const arrived = (state.arrivals[key] ?? 0) + 1;
          state.arrivals[key] = arrived;
          const needed = node.mode === 'ANY' ? 1 : node.mode === 'COUNT' ? (node.count ?? 1) : (split?.branches ?? 1);
          state.tokens = state.tokens.filter((t) => t !== token);
          if (arrived === needed) {
            // The branches still open on this split are not waited for.
            if (split) state.tokens = state.tokens.filter((t) => !t.splits.some((x) => x.id === split.id));
            state.log.push({ node: node.code, key: 'designer.test.joined', params: { arrived } });
            if (!node.next || !byCode.has(node.next)) {
              state.log.push({ node: node.code, key: 'designer.test.dangling', error: true });
              state.failed = true;
            } else {
              state.tokens.push({
                id: state.nextId++,
                node: node.next,
                splits: token.splits.slice(0, -1),
              });
            }
          } else if (arrived < needed) {
            state.log.push({
              node: node.code,
              key: 'designer.test.waitingJoin',
              params: { arrived, needed },
            });
          }
          moved = true;
          break;
        }
        case 'END':
          state.tokens = state.tokens.filter((t) => t !== token);
          if (state.tokens.length === 0) {
            state.outcome = node.outcome ?? 'CLOSED';
            state.log.push({
              node: node.code,
              key: 'designer.test.ended',
              params: { outcome: state.outcome },
            });
          }
          moved = true;
          break;
      }
    }
  }
  return state;
}

function evaluate(node: NodeDef, rule: unknown, state: TestState): JsonValue {
  try {
    return jsonLogic.evaluate((rule ?? null) as JsonValue, state.facts as JsonValue);
  } catch (e) {
    state.log.push({
      node: node.code,
      key: 'designer.test.ruleError',
      params: { message: (e as Error).message },
      error: true,
    });
    state.failed = true;
    return null;
  }
}
