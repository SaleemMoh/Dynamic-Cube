import { GraphDefinition, graphEdges, layout } from './workflow-graph';

const definition: GraphDefinition = {
  nodes: [
    { code: 'START', type: 'START', next: 'SPLIT' },
    { code: 'SPLIT', type: 'PARALLEL_SPLIT', branches: ['A', 'B'] },
    { code: 'A', type: 'HUMAN_TASK', actions: [{ code: 'DONE', kind: 'COMPLETE', nameEn: 'Done', nameAr: 'تم', to: 'JOIN' }] },
    { code: 'B', type: 'HUMAN_TASK', actions: [{ code: 'BACK', kind: 'RETURN' }, { code: 'DONE', kind: 'COMPLETE', to: 'JOIN' }] },
    { code: 'JOIN', type: 'JOIN', next: 'CHECK' },
    { code: 'CHECK', type: 'GATEWAY', routes: [{ to: 'SPLIT' }], default: 'END' },
    { code: 'END', type: 'END', outcome: 'APPROVED' },
    { code: 'ORPHAN', type: 'END' },
  ],
};

describe('workflow graph', () => {
  it('draws every link: next, branches, routes, the default and actions that move on (returns have no target)', () => {
    const edges = graphEdges(definition, 'en');
    expect(edges.map((e) => `${e.from}>${e.to}:${e.label}`)).toEqual([
      'START>SPLIT:',
      'SPLIT>A:',
      'SPLIT>B:',
      'A>JOIN:Done',
      'B>JOIN:DONE',
      'JOIN>CHECK:',
      'CHECK>SPLIT:#1',
      'CHECK>END:*',
    ]);
  });

  it('layers nodes by distance from the start, keeps loops from pushing nodes down and puts unreachable nodes last', () => {
    const placed = layout(definition, graphEdges(definition, 'en'));
    const layer = Object.fromEntries(placed.nodes.map((p) => [p.node.code, p.layer]));
    expect(layer).toEqual({ START: 0, SPLIT: 1, A: 2, B: 2, JOIN: 3, CHECK: 4, END: 5, ORPHAN: 6 });
    const [a, b] = placed.nodes.filter((p) => p.layer === 2);
    expect(a.y).toBe(b.y);
    expect(a.x).toBeLessThan(b.x);
  });
});
