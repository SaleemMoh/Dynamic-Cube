import { PageSize, PrintColumn, PrintDesign, PrintElement, pageMm } from './print-model';

/** CSS pixels in a millimetre (96 dpi). */
const PX_PER_MM = 96 / 25.4;

/** The most elements an imported template gets. */
const MAX_ELEMENTS = 150;

const PATH = /^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z0-9_]+)*$/;
const PLACEHOLDER = /\{\{\s*([^}]+?)\s*\}\}/g;

/**
 * The template's HTML ready to be laid out as it is written: Liquid tags (`{% if %}`, `{% for %}`, …) removed so their
 * content shows once, and `{{ … }}` placeholders kept as text where they stand.
 */
export function stripLiquidTags(html: string): string {
  return html.replace(/\{%-?[\s\S]*?-?%\}/g, '');
}

/** The data path a placeholder prints (`document.issuedOn | date: …` → `document.issuedOn`), or null when it is not one. */
export function placeholderPath(expression: string): string | null {
  const path = expression.split('|')[0].trim();
  return PATH.test(path) ? path : null;
}

/**
 * What a piece of text becomes: plain text, or a field — the first placeholder it holds, with the text before it as the
 * field's label (what follows the placeholder is left out; the designer shows it to be adjusted).
 */
export function textPiece(text: string): { kind: 'text' | 'field'; text: string; field?: string } {
  const clean = text.replace(/\s+/g, ' ').trim();
  const first = [...clean.matchAll(PLACEHOLDER)].find((m) => placeholderPath(m[1]));
  if (!first) return { kind: 'text', text: clean };
  return {
    kind: 'field',
    field: placeholderPath(first[1])!,
    text: clean.slice(0, first.index).trim(),
  };
}

/**
 * A table's columns: the heading cells (of its `thead`, or of its first row of `th` cells) as titles, and the
 * `{{ line.x }}` of its first row printing line values as fields.
 */
export function tableColumns(table: HTMLTableElement): PrintColumn[] {
  const rows = [...table.querySelectorAll('tr')];
  const headRow = table.querySelector('thead tr') ?? rows.find((r) => r.querySelector('th'));
  const heads = headRow
    ? [...headRow.querySelectorAll('th, td')].map((c) => (c.textContent ?? '').trim())
    : [];
  const row = rows.find((r) => /\{\{\s*line\./.test(r.textContent ?? ''));
  const cells = row ? [...row.querySelectorAll('td, th')] : [];
  const columns: PrintColumn[] = [];
  cells.forEach((cell, i) => {
    const match = /\{\{\s*line\.([^}|\s]+)/.exec(cell.textContent ?? '');
    if (match && PATH.test(match[1]))
      columns.push({ field: match[1], title: heads[i] ?? match[1] });
  });
  return columns;
}

function round(value: number): number {
  return Math.round(value * 2) / 2;
}

function isTransparent(color: string): boolean {
  return !color || color === 'transparent' || /rgba\([^)]*,\s*0\)$/.test(color);
}

function hex(color: string): string | undefined {
  const m = /rgba?\((\d+),\s*(\d+),\s*(\d+)/.exec(color);
  if (!m) return undefined;
  return '#' + [m[1], m[2], m[3]].map((n) => Number(n).toString(16).padStart(2, '0')).join('');
}

/**
 * A template written as HTML turned into a layout of the visual designer: the HTML is laid out off screen at the page's
 * width and each part becomes an element where it shows — text and fields (a `{{ path }}` with the text before it as its
 * label), the QR code, images, the lines table with its columns, and bordered or shaded areas as boxes. Approximate: the
 * designer shows it to be checked and adjusted.
 */
export async function importPrintHtml(
  html: string,
  pageSize: PageSize,
  landscape: boolean,
  doc: Document = document,
): Promise<PrintDesign> {
  const page = pageMm(pageSize, landscape);
  const direction = /<html[^>]*dir\s*=\s*["']?ltr/i.test(html) ? 'ltr' : 'rtl';
  const frame = doc.createElement('iframe');
  frame.setAttribute('aria-hidden', 'true');
  frame.setAttribute('sandbox', 'allow-same-origin');
  Object.assign(frame.style, {
    position: 'fixed',
    left: '-20000px',
    top: '0',
    width: `${page.w}mm`,
    height: `${page.h}mm`,
    border: '0',
    visibility: 'hidden',
  });
  doc.body.appendChild(frame);
  try {
    await new Promise<void>((resolve) => {
      frame.onload = () => resolve();
      frame.srcdoc = stripLiquidTags(html);
      setTimeout(resolve, 3000);
    });
    const inner = frame.contentDocument;
    const view = frame.contentWindow;
    if (!inner?.body || !view) return { version: 1, pageSize, landscape, direction, elements: [] };
    const origin = inner.body.getBoundingClientRect();
    const elements: PrintElement[] = [];
    const place = (rect: DOMRect): Pick<PrintElement, 'x' | 'y' | 'w' | 'h'> | null => {
      const x = round((rect.left - origin.left) / PX_PER_MM);
      const y = round((rect.top - origin.top) / PX_PER_MM);
      const w = round(rect.width / PX_PER_MM);
      const h = round(rect.height / PX_PER_MM);
      if (w < 1 || h < 1 || x >= page.w || y >= page.h) return null;
      return {
        x: Math.max(0, x),
        y: Math.max(0, y),
        w: Math.min(w, page.w - Math.max(0, x)),
        h: Math.min(h, page.h - Math.max(0, y)),
      };
    };
    const add = (element: Omit<PrintElement, 'id'>) => {
      if (elements.length < MAX_ELEMENTS)
        elements.push({ ...element, id: `e${elements.length + 1}` });
    };
    const blockLike = (el: Element) => {
      const display = view.getComputedStyle(el).display;
      return display !== 'none' && !display.startsWith('inline');
    };

    /** The QR code (an image of `{{ qr }}`, square, as wide as it is set) or an https / embedded image. */
    const image = (el: Element): void => {
      const rect = el.getBoundingClientRect();
      const src = el.getAttribute('src') ?? '';
      const qr = /\{\{\s*qr\s*\}\}/.test(src) || /qr/i.test(el.getAttribute('alt') ?? '');
      // A placeholder source does not load: the image takes the size it is given (square for a QR code).
      const width = Number(el.getAttribute('width')) || rect.width || (qr ? 30 * PX_PER_MM : 0);
      const height = qr ? width : Number(el.getAttribute('height')) || rect.height || width;
      const box = place(new DOMRect(rect.left, rect.top, width, height));
      if (!box) return;
      if (qr) add({ kind: 'qr', ...box });
      else if (/^(https:\/\/|data:image\/)/.test(src)) add({ kind: 'image', ...box, src });
    };

    const walk = (el: Element): void => {
      const style = view.getComputedStyle(el);
      if (style.display === 'none' || style.visibility === 'hidden') return;
      const tag = el.tagName.toLowerCase();
      if (['script', 'style', 'head', 'title', 'meta', 'link'].includes(tag)) return;
      const box = place(el.getBoundingClientRect());

      if (tag === 'img') {
        image(el);
        return;
      }
      if (tag === 'table') {
        if (box) add({ kind: 'table', ...box, columns: tableColumns(el as HTMLTableElement) });
        return;
      }
      if (tag === 'hr') {
        if (box)
          add({ kind: 'line', ...box, h: 0.5, border: 1, borderColor: hex(style.borderTopColor) });
        return;
      }

      // A bordered or shaded area: a box under what it holds.
      const borders = ['Top', 'Right', 'Bottom', 'Left'].map(
        (s) => parseFloat(style.getPropertyValue(`border-${s.toLowerCase()}-width`)) || 0,
      );
      const bordered = borders.every((b) => b > 0);
      const shaded = !isTransparent(style.backgroundColor) && el !== inner.body && tag !== 'html';
      if (box && (bordered || shaded) && box.w > 4 && box.h > 4) {
        add({
          kind: 'box',
          ...box,
          border: bordered ? round(borders[0] * 0.75) || 1 : 0,
          borderColor: bordered ? hex(style.borderTopColor) : undefined,
          background: shaded ? hex(style.backgroundColor) : undefined,
        });
      }

      // A block holding only inline content: one text or field element where it shows.
      const children = [...el.children];
      const text = (el.textContent ?? '').trim();
      if (
        text &&
        children.every((c) => !blockLike(c) || c.tagName.toLowerCase() === 'br') &&
        tag !== 'body' &&
        tag !== 'html'
      ) {
        if (!box) return;
        // Images inside the block (a QR code above its caption) are placed on their own.
        const images = [...el.querySelectorAll('img')];
        images.forEach(image);
        // The text starts below them.
        const below = Math.max(
          ...images.map((img) => {
            const r = img.getBoundingClientRect();
            return (
              r.top +
              (Number(img.getAttribute('height')) || Number(img.getAttribute('width')) || r.height)
            );
          }),
          0,
        );
        // At least as wide as the images above it (a caption under a QR code).
        const blockRect = el.getBoundingClientRect();
        const left = Math.min(
          blockRect.left,
          ...images.map((img) => img.getBoundingClientRect().left),
        );
        const right = Math.max(
          blockRect.right,
          ...images.map(
            (img) =>
              img.getBoundingClientRect().left +
              (Number(img.getAttribute('width')) || img.getBoundingClientRect().width),
          ),
        );
        const textBox =
          below &&
          place(
            new DOMRect(
              left,
              below,
              right - left,
              // At least a line of text: an image that does not load off screen leaves its block shorter.
              Math.max(el.getBoundingClientRect().bottom - below, 6 * PX_PER_MM),
            ),
          );
        const piece = textPiece(el.textContent ?? '');
        const weight = Number(style.fontWeight) || (style.fontWeight === 'bold' ? 700 : 400);
        const align =
          style.textAlign === 'center'
            ? 'center'
            : style.textAlign === (direction === 'rtl' ? 'left' : 'right')
              ? 'end'
              : 'start';
        add({
          kind: piece.kind,
          ...(textBox || box),
          text: piece.text || undefined,
          field: piece.field,
          fontSize: Math.round(parseFloat(style.fontSize) * 0.75) || undefined,
          bold: weight >= 600 || undefined,
          align,
          color: hex(style.color),
        });
        return;
      }
      children.forEach(walk);
    };
    walk(inner.body);
    const background = view.getComputedStyle(inner.body).backgroundColor;
    return {
      version: 1,
      pageSize,
      landscape,
      direction,
      background: isTransparent(background) ? undefined : hex(background),
      elements,
    };
  } finally {
    frame.remove();
  }
}
