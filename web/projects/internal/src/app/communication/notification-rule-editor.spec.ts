import { fromRows, toRows } from './notification-rule-editor';

describe('notification rule recipients', () => {
  it('round-trips the party, named roles and employees named by the event', () => {
    const stored = [
      { kind: 'PARTY', value: null },
      { kind: 'ROLE', value: ['A', 'B'] },
      { kind: 'ROLE', value: 'C' },
      { kind: 'USER', value: { var: 'task.assignee' } },
    ];
    const rows = toRows(stored);
    expect(rows).toEqual([
      { kind: 'PARTY', codes: [], fromEvent: null },
      { kind: 'ROLE', codes: ['A', 'B'], fromEvent: null },
      { kind: 'ROLE', codes: ['C'], fromEvent: null },
      { kind: 'USER', codes: [], fromEvent: 'task.assignee' },
    ]);
    expect(fromRows(rows)).toEqual([
      { kind: 'PARTY' },
      { kind: 'ROLE', value: ['A', 'B'] },
      { kind: 'ROLE', value: 'C' },
      { kind: 'USER', value: { var: 'task.assignee' } },
    ]);
  });

  it('keeps a role named by the event', () => {
    expect(fromRows(toRows([{ kind: 'ROLE', value: { var: 'task.escalatedToRole' } }]))).toEqual([{ kind: 'ROLE', value: { var: 'task.escalatedToRole' } }]);
  });
});
