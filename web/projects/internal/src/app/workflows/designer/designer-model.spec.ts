import { WorkflowDraft, addNode, check, connect, disconnect, edges, removeNode, renameNode, toDefinition } from './designer-model';

const base = (): WorkflowDraft => ({
  nodes: [
    { code: 'START', type: 'START', nameAr: 'بداية', nameEn: 'Start', next: 'REVIEW' },
    {
      code: 'REVIEW',
      type: 'HUMAN_TASK',
      nameAr: 'مراجعة',
      nameEn: 'Review',
      assignment: { target: 'ROLE', role: 'REVIEWER', mode: 'QUEUE' },
      actions: [{ code: 'APPROVE', kind: 'APPROVE', nameAr: 'اعتماد', nameEn: 'Approve', to: 'DONE' }],
    },
    { code: 'DONE', type: 'END', nameAr: 'تم', nameEn: 'Done', outcome: 'APPROVED' },
  ],
});

describe('designer model', () => {
  it('draws a valid graph without problems', () => {
    expect(check(base())).toEqual([]);
    expect(edges(base()).map((e) => `${e.from}>${e.to}`)).toEqual(['START>REVIEW', 'REVIEW>DONE']);
  });

  it('adds steps with free codes and connects them by kind', () => {
    let def = addNode(base(), 'GATEWAY', { x: 13, y: 21 }).def;
    expect(def.nodes.at(-1)!.code).toBe('GATEWAY_1');
    expect(def.layout!['GATEWAY_1']).toEqual({ x: 16, y: 24 });
    def = addNode(def, 'END', { x: 0, y: 0 }).def;
    def = connect(def, 'GATEWAY_1', 'route', 'DONE');
    def = connect(def, 'GATEWAY_1', 'default', 'END_1');
    def = connect(def, 'REVIEW', 'action', 'GATEWAY_1');
    const gateway = def.nodes.find((n) => n.code === 'GATEWAY_1')!;
    expect(gateway.routes).toEqual([{ when: null, to: 'DONE' }]);
    expect(gateway.default).toBe('END_1');
    expect(def.nodes.find((n) => n.code === 'REVIEW')!.actions!.at(-1)).toMatchObject({
      code: 'COMPLETE',
      kind: 'COMPLETE',
      to: 'GATEWAY_1',
    });
    // The new route needs its condition; nothing may lead back to START.
    expect(check(def).map((p) => p.key)).toContain('designer.problem.condition');
    expect(connect(def, 'REVIEW', 'action', 'START')).toBe(def);
  });

  it('an action to an approving or rejecting end is named after it', () => {
    const def = connect(addNode(base(), 'END', { x: 0, y: 0 }).def, 'REVIEW', 'action', 'DONE');
    expect(def.nodes[1].actions!.at(-1)).toMatchObject({ code: 'APPROVE_2', kind: 'APPROVE' });
  });

  it('removing or renaming a step updates everything that refers to it', () => {
    const renamed = renameNode(base(), 'REVIEW', 'CHECK');
    expect(renamed.nodes[0].next).toBe('CHECK');
    expect(renamed.nodes[1].code).toBe('CHECK');
    const removed = removeNode(base(), 'DONE');
    expect(removed.nodes[1].actions).toEqual([]);
    expect(check(removed).map((p) => p.key)).toEqual(expect.arrayContaining(['designer.problem.noEnd', 'designer.problem.noActions']));
    const unlinked = disconnect(base(), 'START', { kind: 'next' });
    expect(check(unlinked).map((p) => p.key)).toEqual(expect.arrayContaining(['designer.problem.noNext', 'designer.problem.unreachable']));
  });

  it('a visit step routes outcomes, has a cancelled way and is checked', () => {
    let def = addNode(base(), 'VISIT', { x: 0, y: 0 }).def;
    def = connect(def, 'START', 'next', 'VISIT_1');
    expect(check(def).map((p) => p.key)).toEqual(
      expect.arrayContaining(['designer.problem.visitType', 'designer.problem.noVisited', 'designer.problem.noVisitCancelled']),
    );
    def = connect(def, 'VISIT_1', 'next', 'REVIEW');
    def = connect(def, 'VISIT_1', 'outcome', 'DONE');
    def = connect(def, 'VISIT_1', 'cancelled', 'DONE');
    def = { ...def, nodes: def.nodes.map((n) => (n.code === 'VISIT_1' ? { ...n, visitType: 'SITE' } : n)) };
    expect(def.nodes.find((n) => n.code === 'VISIT_1')!.outcomes).toEqual({ OUTCOME: 'DONE' });
    expect(edges(def).filter((e) => e.from === 'VISIT_1').map((e) => e.outlet.kind)).toEqual(['next', 'outcome', 'cancelled']);
    expect(check(def)).toEqual([]);
    const renamed = renameNode(def, 'DONE', 'END_OK');
    expect(renamed.nodes.find((n) => n.code === 'VISIT_1')).toMatchObject({ outcomes: { OUTCOME: 'END_OK' }, cancelled: 'END_OK' });
    const removed = removeNode(def, 'DONE');
    expect(removed.nodes.find((n) => n.code === 'VISIT_1')).toMatchObject({ outcomes: {}, cancelled: null });
    expect(disconnect(def, 'VISIT_1', { kind: 'outcome', index: 0 }).nodes.find((n) => n.code === 'VISIT_1')!.outcomes).toEqual({});
  });

  it('saves a position for every step and only for steps', () => {
    const def = { ...base(), layout: { START: { x: 8, y: 8 }, GONE: { x: 1, y: 1 } } };
    const saved = toDefinition(def);
    expect(Object.keys(saved.layout!).sort()).toEqual(['DONE', 'REVIEW', 'START']);
    expect(saved.layout!['START']).toEqual({ x: 8, y: 8 });
  });
});
