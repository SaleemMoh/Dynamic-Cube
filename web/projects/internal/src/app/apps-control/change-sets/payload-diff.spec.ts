import { diffPayload, sameValue } from './payload-diff';

describe('diffPayload', () => {
  it('ignores the key, ownership, property order and missing versus null', () => {
    expect(diffPayload({ code: 'A', owner: 'SYSTEM', nameEn: 'X', note: null, a: { x: 1, y: 2 } }, { nameEn: 'X', a: { y: 2, x: 1 } })).toEqual([]);
    expect(sameValue([1, { a: 1 }], [1, { a: 1 }])).toBe(true);
  });

  it('follows nested objects and reports added, removed and changed fields', () => {
    expect(
      diffPayload({ nameEn: 'Old', card: { icon: 'a', tags: ['x'] }, gone: 1 }, { nameEn: 'New', card: { icon: 'a', tags: ['x', 'y'] }, extra: true }),
    ).toEqual([
      { path: 'nameEn', kind: 'changed', before: 'Old', after: 'New' },
      { path: 'card.tags', kind: 'changed', before: ['x'], after: ['x', 'y'] },
      { path: 'gone', kind: 'removed', before: 1 },
      { path: 'extra', kind: 'added', after: true },
    ]);
  });

  it('matches lists of coded rows by code', () => {
    const before = {
      attributes: [
        { code: 'A', nameEn: 'One' },
        { code: 'B', nameEn: 'Two' },
      ],
    };
    const after = {
      attributes: [
        { code: 'B', nameEn: 'Second' },
        { code: 'C', nameEn: 'Three' },
      ],
    };
    expect(diffPayload(before, after)).toEqual([
      { path: 'attributes[A]', kind: 'removed', before: { code: 'A', nameEn: 'One' } },
      { path: 'attributes[B].nameEn', kind: 'changed', before: 'Two', after: 'Second' },
      { path: 'attributes[C]', kind: 'added', after: { code: 'C', nameEn: 'Three' } },
    ]);
  });

  it('compares deeper values whole past the depth', () => {
    expect(diffPayload({ definition: { steps: { a: 1 } } }, { definition: { steps: { a: 2 } } }, { depth: 1 })).toEqual([
      { path: 'definition', kind: 'changed', before: { steps: { a: 1 } }, after: { steps: { a: 2 } } },
    ]);
  });
});
