import {
  PageBlock,
  PageDesign,
  PageLayout,
  PageSection,
  newSection,
  relayout,
} from '@dynamic-cube/shared';

/** Where a block sits: section, column and position. */
export interface BlockPlace {
  section: number;
  column: number;
  index: number;
}

/** The place of a block, or null. */
export function findBlock(design: PageDesign, id: string): BlockPlace | null {
  for (let s = 0; s < design.sections.length; s++) {
    const columns = design.sections[s].columns;
    for (let c = 0; c < columns.length; c++) {
      const index = columns[c].findIndex((b) => b.id === id);
      if (index >= 0) return { section: s, column: c, index };
    }
  }
  return null;
}

function withColumns(
  design: PageDesign,
  section: number,
  change: (columns: PageBlock[][]) => PageBlock[][],
): PageDesign {
  return {
    ...design,
    sections: design.sections.map((s, i) =>
      i === section ? { ...s, columns: change(s.columns.map((c) => [...c])) } : s,
    ),
  };
}

/** Puts a block at a place (its index is clamped to the column). */
export function insertBlock(design: PageDesign, block: PageBlock, place: BlockPlace): PageDesign {
  return withColumns(design, place.section, (columns) => {
    const column = columns[place.column] ?? [];
    column.splice(Math.max(0, Math.min(place.index, column.length)), 0, block);
    columns[place.column] = column;
    return columns;
  });
}

export function removeBlock(design: PageDesign, id: string): PageDesign {
  const place = findBlock(design, id);
  if (!place) return design;
  return withColumns(design, place.section, (columns) => {
    columns[place.column].splice(place.index, 1);
    return columns;
  });
}

/**
 * Moves a block to a place given as it is before the move (dropping a block just after itself, or onto its own spot,
 * leaves it where it is).
 */
export function moveBlock(design: PageDesign, id: string, to: BlockPlace): PageDesign {
  const from = findBlock(design, id);
  if (!from) return design;
  const block = design.sections[from.section].columns[from.column][from.index];
  let index = to.index;
  if (from.section === to.section && from.column === to.column && from.index < to.index) index--;
  return insertBlock(removeBlock(design, id), block, { ...to, index });
}

export function updateBlock(
  design: PageDesign,
  id: string,
  change: Partial<PageBlock>,
): PageDesign {
  const place = findBlock(design, id);
  if (!place) return design;
  return withColumns(design, place.section, (columns) => {
    columns[place.column][place.index] = {
      ...columns[place.column][place.index],
      ...change,
      id,
      kind: columns[place.column][place.index].kind,
    };
    return columns;
  });
}

export function addSection(
  design: PageDesign,
  at: number,
  id: string,
  layout: PageLayout = '1',
): PageDesign {
  const sections = [...design.sections];
  sections.splice(Math.max(0, Math.min(at, sections.length)), 0, newSection(id, layout));
  return { ...design, sections };
}

export function moveSection(design: PageDesign, index: number, step: -1 | 1): PageDesign {
  const target = index + step;
  if (target < 0 || target >= design.sections.length) return design;
  const sections = [...design.sections];
  [sections[index], sections[target]] = [sections[target], sections[index]];
  return { ...design, sections };
}

export function removeSection(design: PageDesign, index: number): PageDesign {
  return { ...design, sections: design.sections.filter((_, i) => i !== index) };
}

export function updateSection(
  design: PageDesign,
  index: number,
  change: Partial<Pick<PageSection, 'layout' | 'background'>>,
): PageDesign {
  return {
    ...design,
    sections: design.sections.map((s, i) => {
      if (i !== index) return s;
      const laid = change.layout && change.layout !== s.layout ? relayout(s, change.layout) : s;
      return { ...laid, background: change.background ?? laid.background };
    }),
  };
}

/** An id not used by any section or block yet. */
export function nextId(design: PageDesign, prefix: string): string {
  const used = new Set<string>();
  for (const s of design.sections) {
    used.add(s.id);
    for (const c of s.columns) for (const b of c) used.add(b.id);
  }
  let n = 1;
  while (used.has(`${prefix}${n}`)) n++;
  return `${prefix}${n}`;
}
