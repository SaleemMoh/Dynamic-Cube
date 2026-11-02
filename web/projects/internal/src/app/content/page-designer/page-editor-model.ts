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

/** Whether text holds HTML markup (pages written before the designer, or bodies imported as HTML). */
export function looksLikeHtml(text: string | null | undefined): boolean {
  return /<\/?(p|h[1-6]|ul|ol|li|br|b|strong|i|em|a|div|span|hr|blockquote)\b[^>]*>/i.test(
    text ?? '',
  );
}

/**
 * HTML turned into the Markdown text blocks show (headings, paragraphs, lists, quotes, rules, bold, italic, links), so a
 * page written as HTML opens in the designer as editable text rather than as markup. Text that is not HTML is kept.
 */
export function htmlToMarkdown(html: string | null | undefined): string {
  const source = html ?? '';
  if (!looksLikeHtml(source)) return source;
  const doc = new DOMParser().parseFromString(`<body>${source}</body>`, 'text/html');
  const inline = (node: Node): string => {
    if (node.nodeType === Node.TEXT_NODE) return (node.textContent ?? '').replace(/\s+/g, ' ');
    if (!(node instanceof Element)) return '';
    const inner = [...node.childNodes].map(inline).join('');
    switch (node.tagName.toLowerCase()) {
      case 'b':
      case 'strong':
        return inner.trim() ? `**${inner.trim()}**` : '';
      case 'i':
      case 'em':
        return inner.trim() ? `*${inner.trim()}*` : '';
      case 'a': {
        const href = node.getAttribute('href');
        return href ? `[${inner.trim()}](${href})` : inner;
      }
      case 'br':
        return '\n';
      default:
        return inner;
    }
  };
  const blocks: string[] = [];
  const block = (node: Node): void => {
    if (node.nodeType === Node.TEXT_NODE) {
      const text = (node.textContent ?? '').trim();
      if (text) blocks.push(text.replace(/\s+/g, ' '));
      return;
    }
    if (!(node instanceof Element)) return;
    const tag = node.tagName.toLowerCase();
    const heading = /^h([1-6])$/.exec(tag);
    if (heading) {
      // The page title is the h1: headings inside the body start at ##.
      const level = Math.min(4, Math.max(2, Number(heading[1])));
      blocks.push(`${'#'.repeat(level)} ${inline(node).trim()}`);
    } else if (tag === 'p') {
      const text = inline(node).trim();
      if (text) blocks.push(text);
    } else if (tag === 'ul' || tag === 'ol') {
      const items = [...node.children]
        .filter((li) => li.tagName.toLowerCase() === 'li')
        .map((li, i) => `${tag === 'ol' ? `${i + 1}.` : '-'} ${inline(li).trim()}`);
      if (items.length) blocks.push(items.join('\n'));
    } else if (tag === 'blockquote') {
      blocks.push(`> ${inline(node).trim()}`);
    } else if (tag === 'hr') {
      blocks.push('---');
    } else if (['div', 'section', 'article', 'body', 'main'].includes(tag)) {
      node.childNodes.forEach(block);
    } else {
      const text = inline(node).trim();
      if (text) blocks.push(text);
    }
  };
  doc.body.childNodes.forEach(block);
  return blocks.join('\n\n');
}

/** A design whose text blocks hold HTML, with that HTML turned into Markdown. */
export function withMarkdownText(design: PageDesign): PageDesign {
  let changed = false;
  const sections = design.sections.map((s) => ({
    ...s,
    columns: s.columns.map((column) =>
      column.map((b) => {
        if (b.kind !== 'text' || (!looksLikeHtml(b.textAr) && !looksLikeHtml(b.textEn))) return b;
        changed = true;
        return { ...b, textAr: htmlToMarkdown(b.textAr), textEn: htmlToMarkdown(b.textEn) };
      }),
    ),
  }));
  return changed ? { ...design, sections } : design;
}
