import { PageDesign, newBlock, starterPage } from '@dynamic-cube/shared';
import {
  addSection,
  findBlock,
  insertBlock,
  moveBlock,
  moveSection,
  nextId,
  removeBlock,
  removeSection,
  updateBlock,
  updateSection,
} from './page-editor-model';

const ids = (design: PageDesign, s: number, c: number) =>
  design.sections[s].columns[c].map((b) => b.id);

describe('page editor', () => {
  it('finds, inserts and removes blocks', () => {
    let page = starterPage();
    expect(findBlock(page, 'b4')).toEqual({ section: 1, column: 1, index: 0 });
    page = insertBlock(page, newBlock('divider', 'x'), { section: 1, column: 1, index: 0 });
    expect(ids(page, 1, 1)).toEqual(['x', 'b4']);
    page = removeBlock(page, 'x');
    expect(ids(page, 1, 1)).toEqual(['b4']);
  });

  it('moves a block within a column and to another column', () => {
    let page = starterPage();
    page = moveBlock(page, 'b1', { section: 0, column: 0, index: 2 });
    expect(ids(page, 0, 0)).toEqual(['b2', 'b1']);
    page = moveBlock(page, 'b2', { section: 0, column: 0, index: 1 });
    // Dropped just after itself: it stays.
    expect(ids(page, 0, 0)).toEqual(['b2', 'b1']);
    page = moveBlock(page, 'b3', { section: 1, column: 1, index: 1 });
    expect(ids(page, 1, 0)).toEqual([]);
    expect(ids(page, 1, 1)).toEqual(['b4', 'b3']);
  });

  it('edits a block but never its id or kind', () => {
    const page = updateBlock(starterPage(), 'b1', { textAr: 'جديد', id: 'other', kind: 'image' });
    const block = page.sections[0].columns[0][0];
    expect(block).toMatchObject({ id: 'b1', kind: 'heading', textAr: 'جديد' });
  });

  it('adds, moves and removes sections; fewer columns keep their blocks', () => {
    let page = addSection(starterPage(), 1, 's3', '1-1-1');
    expect(page.sections.map((s) => s.id)).toEqual(['s1', 's3', 's2']);
    page = moveSection(page, 2, -1);
    expect(page.sections.map((s) => s.id)).toEqual(['s1', 's2', 's3']);
    page = updateSection(page, 1, { layout: '1', background: 'brand' });
    expect(page.sections[1].columns).toHaveLength(1);
    expect(ids(page, 1, 0)).toEqual(['b3', 'b4']);
    expect(page.sections[1].background).toBe('brand');
    page = removeSection(page, 2);
    expect(page.sections).toHaveLength(2);
  });

  it('gives new ids that are not used yet', () => {
    expect(nextId(starterPage(), 'b')).toBe('b5');
    expect(nextId(starterPage(), 's')).toBe('s3');
  });
});
