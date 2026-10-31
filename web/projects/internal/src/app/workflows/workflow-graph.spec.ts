import { TestBed } from '@angular/core/testing';
import { GraphDefinition, WorkflowGraph, graphEdges, layout } from './workflow-graph';

const definition: GraphDefinition = {
  nodes: [
    { code: 'START', type: 'START', next: 'SPLIT' },
    { code: 'SPLIT', type: 'PARALLEL_SPLIT', branches: ['A', 'B'] },
    {
      code: 'A',
      type: 'HUMAN_TASK',
      actions: [
        { code: 'DONE', kind: 'COMPLETE', nameEn: 'Done', nameAr: 'تم', to: 'JOIN' },
        { code: 'STOP', kind: 'REJECT', nameEn: 'Reject', nameAr: 'رفض', to: 'REJECTED' },
      ],
    },
    {
      code: 'B',
      type: 'HUMAN_TASK',
      actions: [
        { code: 'BACK', kind: 'RETURN' },
        { code: 'DONE', kind: 'COMPLETE', to: 'JOIN' },
      ],
    },
    { code: 'JOIN', type: 'JOIN', next: 'CHECK' },
    { code: 'CHECK', type: 'GATEWAY', routes: [{ to: 'SPLIT' }], default: 'END' },
    { code: 'END', type: 'END', outcome: 'APPROVED' },
    { code: 'REJECTED', type: 'END', outcome: 'REJECTED' },
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
      'A>REJECTED:Reject',
      'B>JOIN:DONE',
      'JOIN>CHECK:',
      'CHECK>SPLIT:#1',
      'CHECK>END:*',
    ]);
  });

  it('layers nodes by distance from the start, keeps loops from pushing nodes down, puts every end on one row and unreachable nodes last', () => {
    const placed = layout(definition, graphEdges(definition, 'en'));
    const layer = Object.fromEntries(placed.nodes.map((p) => [p.node.code, p.layer]));
    expect(layer).toEqual({ START: 0, SPLIT: 1, A: 2, B: 2, JOIN: 3, CHECK: 4, END: 5, REJECTED: 5, ORPHAN: 6 });
    const [a, b] = placed.nodes.filter((p) => p.layer === 2);
    expect(a.y).toBe(b.y);
    expect(a.x).toBeLessThan(b.x);
  });

  it('draws the failed route of an integration call', () => {
    const call: GraphDefinition = { nodes: [{ code: 'CALL', type: 'INTEGRATION', next: 'OK', failed: 'KO' }, { code: 'OK', type: 'END' }, { code: 'KO', type: 'END' }] };
    expect(graphEdges(call, 'en').map((e) => `${e.from}>${e.to}:${e.label}`)).toEqual(['CALL>OK:', 'CALL>KO:✗']);
  });

  it('marks where a request is now, the steps it went through and the arrows it took, with a legend', () => {
    const fixture = TestBed.createComponent(WorkflowGraph);
    fixture.componentRef.setInput('definition', definition);
    fixture.componentRef.setInput('current', ['B']);
    fixture.componentRef.setInput('visited', ['START', 'SPLIT', 'A', 'B', 'JOIN']);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const state = (code: string) => element.querySelector(`[data-node="${code}"]`)?.getAttribute('data-state');

    expect(state('B')).toBe('current');
    expect(state('A')).toBe('visited');
    expect(state('END')).toBeNull();
    expect(element.querySelectorAll('.dc-graph__halo')).toHaveLength(1);
    // START>SPLIT, SPLIT>A, SPLIT>B, A>JOIN and B>JOIN lead between steps it has been at; JOIN>CHECK does not.
    expect(element.querySelectorAll('.dc-graph__edge--taken')).toHaveLength(5);
    expect(element.querySelector('.dc-graph__legend')).not.toBeNull();
  });
});
