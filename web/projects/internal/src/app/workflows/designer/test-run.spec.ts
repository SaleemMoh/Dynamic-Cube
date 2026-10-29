import { WorkflowDraft } from './designer-model';
import { choose, startRun, waiting } from './test-run';

const task = (code: string, actions: { code: string; to?: string; kind?: 'COMPLETE' | 'RETURN' }[]) => ({
  code,
  type: 'HUMAN_TASK' as const,
  nameAr: code,
  nameEn: code,
  assignment: { target: 'ROLE' as const, role: 'R', mode: 'QUEUE' as const },
  actions: actions.map((a) => ({
    code: a.code,
    kind: a.kind ?? 'COMPLETE',
    nameAr: a.code,
    nameEn: a.code,
    to: a.to ?? null,
  })),
});

const def: WorkflowDraft = {
  nodes: [
    { code: 'START', type: 'START', nameAr: 's', nameEn: 's', next: 'PAY' },
    { code: 'PAY', type: 'PAYMENT', nameAr: 'p', nameEn: 'p', next: 'ROUTE', expired: 'UNPAID' },
    {
      code: 'ROUTE',
      type: 'GATEWAY',
      nameAr: 'g',
      nameEn: 'g',
      routes: [{ when: { '==': [{ var: 'level' }, 'FAST'] }, to: 'DONE' }],
      default: 'SPLIT',
    },
    { code: 'SPLIT', type: 'PARALLEL_SPLIT', nameAr: 'x', nameEn: 'x', branches: ['A', 'B'] },
    task('A', [
      { code: 'OK', to: 'JOIN' },
      { code: 'BACK', kind: 'RETURN' },
    ]),
    task('B', [{ code: 'OK', to: 'JOIN' }]),
    { code: 'JOIN', type: 'JOIN', nameAr: 'j', nameEn: 'j', mode: 'ALL', next: 'DONE' },
    { code: 'DONE', type: 'END', nameAr: 'd', nameEn: 'd', outcome: 'APPROVED' },
    { code: 'UNPAID', type: 'END', nameAr: 'u', nameEn: 'u', outcome: 'CLOSED' },
  ],
};

describe('designer test run', () => {
  it('passes a payment with nothing to pay and follows the gateway', () => {
    const run = startRun(def, {}, 'FAST');
    expect(run.outcome).toBe('APPROVED');
    expect(run.log.map((e) => e.key)).toContain('designer.test.nothingToPay');
  });

  it('waits for the bill, splits, waits for both branches and joins', () => {
    let run = startRun(def, { __feeTotal: 100 }, 'NORMAL');
    expect(waiting(def, run).map((w) => w.node.code)).toEqual(['PAY']);
    run = choose(def, run, run.tokens[0].id, 'PAID');
    expect(waiting(def, run).map((w) => w.node.code)).toEqual(['A', 'B']);
    // A return comes back to the same task.
    run = choose(def, run, run.tokens.find((t) => t.node === 'A')!.id, 'BACK');
    expect(
      waiting(def, run)
        .map((w) => w.node.code)
        .sort(),
    ).toEqual(['A', 'B']);
    run = choose(def, run, run.tokens.find((t) => t.node === 'A')!.id, 'OK');
    expect(run.outcome).toBeNull();
    run = choose(def, run, run.tokens.find((t) => t.node === 'B')!.id, 'OK');
    expect(run.outcome).toBe('APPROVED');
  });

  it('an unpaid bill takes the expired way', () => {
    let run = startRun(def, { __feeTotal: 100 }, null);
    run = choose(def, run, run.tokens[0].id, 'UNPAID');
    expect(run.outcome).toBe('CLOSED');
  });
});
