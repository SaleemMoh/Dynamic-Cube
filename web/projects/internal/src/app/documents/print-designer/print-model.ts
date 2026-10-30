/** What a placed element is. */
export type PrintKind = 'text' | 'field' | 'image' | 'qr' | 'table' | 'line' | 'box';

/** A column of the lines table: a field of each line and its heading. */
export interface PrintColumn {
  field: string;
  title: string;
}

/** An element placed on the page; positions and sizes are in millimetres from the page's top-left corner. */
export interface PrintElement {
  id: string;
  kind: PrintKind;
  x: number;
  y: number;
  w: number;
  h: number;
  /** Text: what it says. Field: an optional label printed before the value. */
  text?: string;
  /** Field: the value's path in the document data (e.g. `document.number`). */
  field?: string;
  /** Image: an https address or an embedded image (data URL). */
  src?: string;
  /** Table: the line fields shown, in order. */
  columns?: PrintColumn[];
  fontSize?: number;
  bold?: boolean;
  align?: 'start' | 'center' | 'end';
  color?: string;
  background?: string;
  /** Border width in points (0 = none). */
  border?: number;
  borderColor?: string;
}

export type PageSize = 'A4' | 'A5' | 'LETTER';

/** A print layout as the designer keeps it; the template's HTML is generated from it. */
export interface PrintDesign {
  version: 1;
  pageSize: PageSize;
  landscape: boolean;
  direction: 'rtl' | 'ltr';
  /** Page colour behind everything. */
  background?: string;
  elements: PrintElement[];
}

/** Values of the document data a field can print, with what to call them in the palette. */
export const PRINT_FIELDS: { path: string; ar: string; en: string }[] = [
  { path: 'document.typeNameAr', ar: 'نوع المستند (عربي)', en: 'Document type (Arabic)' },
  { path: 'document.typeNameEn', ar: 'نوع المستند (إنجليزي)', en: 'Document type (English)' },
  { path: 'document.number', ar: 'رقم المستند', en: 'Document number' },
  { path: 'document.issuedOn', ar: 'تاريخ الإصدار', en: 'Issued on' },
  { path: 'document.expiresOn', ar: 'تاريخ الانتهاء', en: 'Expires on' },
  { path: 'document.version', ar: 'الإصدار', en: 'Version' },
  { path: 'document.status', ar: 'الحالة', en: 'Status' },
  { path: 'holder.nameAr', ar: 'اسم صاحب المستند (عربي)', en: 'Holder name (Arabic)' },
  { path: 'holder.nameEn', ar: 'اسم صاحب المستند (إنجليزي)', en: 'Holder name (English)' },
  { path: 'holder.identifiers.CR_NUMBER', ar: 'السجل التجاري', en: 'CR number' },
  { path: 'holder.identifiers.UNIFIED_NUMBER', ar: 'الرقم الموحد', en: 'Unified number' },
  { path: 'verifyUrl', ar: 'رابط التحقق', en: 'Verification address' },
];

const PAGES: Record<PageSize, { w: number; h: number }> = {
  A4: { w: 210, h: 297 },
  A5: { w: 148, h: 210 },
  LETTER: { w: 215.9, h: 279.4 },
};

/** The page's width and height in millimetres. */
export function pageMm(size: PageSize, landscape: boolean): { w: number; h: number } {
  const page = PAGES[size] ?? PAGES.A4;
  return landscape ? { w: page.h, h: page.w } : page;
}

/** A starting layout: the document type as a title, the number and dates, the holder, the lines and the QR code. */
export function starterDesign(): PrintDesign {
  return {
    version: 1,
    pageSize: 'A4',
    landscape: false,
    direction: 'rtl',
    elements: [
      {
        id: 'title',
        kind: 'field',
        field: 'document.typeNameAr',
        x: 20,
        y: 25,
        w: 170,
        h: 14,
        fontSize: 22,
        bold: true,
        align: 'center',
        color: '#1b8354',
      },
      {
        id: 'number',
        kind: 'field',
        field: 'document.number',
        text: 'رقم المستند: ',
        x: 110,
        y: 50,
        w: 80,
        h: 8,
        fontSize: 11,
      },
      {
        id: 'issued',
        kind: 'field',
        field: 'document.issuedOn',
        text: 'تاريخ الإصدار: ',
        x: 20,
        y: 50,
        w: 80,
        h: 8,
        fontSize: 11,
        align: 'end',
      },
      {
        id: 'holder',
        kind: 'field',
        field: 'holder.nameAr',
        x: 20,
        y: 66,
        w: 170,
        h: 10,
        fontSize: 15,
        bold: true,
        align: 'center',
      },
      { id: 'line1', kind: 'line', x: 20, y: 82, w: 170, h: 1, border: 1, borderColor: '#d2d6db' },
      {
        id: 'lines',
        kind: 'table',
        x: 20,
        y: 90,
        w: 170,
        h: 60,
        fontSize: 11,
        columns: [
          { field: 'sector', title: 'المجال' },
          { field: 'grade', title: 'الدرجة' },
        ],
      },
      { id: 'qr', kind: 'qr', x: 20, y: 240, w: 32, h: 32 },
      {
        id: 'verify',
        kind: 'field',
        field: 'verifyUrl',
        x: 56,
        y: 262,
        w: 134,
        h: 8,
        fontSize: 9,
        color: '#4d5761',
      },
    ],
  };
}

/** A new element of a kind dropped at a point, with a sensible size. */
export function newElement(kind: PrintKind, x: number, y: number, id: string): PrintElement {
  const sizes: Record<PrintKind, [number, number]> = {
    text: [70, 10],
    field: [70, 9],
    image: [40, 30],
    qr: [30, 30],
    table: [170, 50],
    line: [120, 1],
    box: [80, 40],
  };
  const [w, h] = sizes[kind];
  const element: PrintElement = { id, kind, x: round(x), y: round(y), w, h };
  if (kind === 'text') Object.assign(element, { text: 'نص', fontSize: 12 });
  if (kind === 'field') Object.assign(element, { field: 'document.number', fontSize: 12 });
  if (kind === 'table')
    Object.assign(element, { fontSize: 11, columns: [{ field: 'sector', title: 'المجال' }] });
  if (kind === 'line') Object.assign(element, { border: 1, borderColor: '#d2d6db' });
  if (kind === 'box') Object.assign(element, { border: 1, borderColor: '#9da4ae' });
  return element;
}

/** Keeps an element on the page (at least 1 mm of it) and snaps it to a millimetre grid. */
export function clampToPage(
  element: PrintElement,
  page: { w: number; h: number },
  grid = 1,
): PrintElement {
  const snap = (v: number) => Math.round(v / grid) * grid;
  const w = Math.max(1, Math.min(snap(element.w), page.w));
  const h = Math.max(1, Math.min(snap(element.h), page.h));
  return {
    ...element,
    w,
    h,
    x: Math.max(0, Math.min(snap(element.x), page.w - w)),
    y: Math.max(0, Math.min(snap(element.y), page.h - h)),
  };
}

function round(v: number): number {
  return Math.round(v * 10) / 10;
}

const PATH = /^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z0-9_]+)*$/;
const COLOR = /^#[0-9a-f]{3,8}$/i;
const IMAGE =
  /^(https:\/\/[^\s"'<>]+|data:image\/(png|jpeg|webp|svg\+xml);base64,[A-Za-z0-9+/=]+)$/;

/** Text printed as it is: HTML-escaped and never read as Liquid. */
export function escapeText(text: string): string {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/\{/g, '&#123;')
    .replace(/\}/g, '&#125;')
    .replace(/%/g, '&#37;')
    .replace(/\n/g, '<br>');
}

function color(value: string | undefined): string | null {
  return value && COLOR.test(value) ? value : null;
}

function styleOf(e: PrintElement, direction: 'rtl' | 'ltr'): string {
  const parts = [`left:${e.x}mm`, `top:${e.y}mm`, `width:${e.w}mm`, `height:${e.h}mm`];
  if (e.fontSize) parts.push(`font-size:${e.fontSize}pt`);
  if (e.bold) parts.push('font-weight:700');
  if (e.align)
    parts.push(
      `text-align:${e.align === 'center' ? 'center' : e.align === 'end' ? (direction === 'rtl' ? 'left' : 'right') : direction === 'rtl' ? 'right' : 'left'}`,
    );
  const fg = color(e.color);
  if (fg) parts.push(`color:${fg}`);
  const bg = color(e.background);
  if (bg) parts.push(`background:${bg}`);
  if (e.kind === 'line') {
    parts.push(
      `border-top:${e.border ?? 1}pt solid ${color(e.borderColor) ?? '#d2d6db'}`,
      'height:0',
    );
  } else if (e.border) {
    parts.push(`border:${e.border}pt solid ${color(e.borderColor) ?? '#9da4ae'}`);
  }
  return parts.join(';');
}

function contentOf(e: PrintElement): string {
  switch (e.kind) {
    case 'text':
      return escapeText(e.text ?? '');
    case 'field':
      return (
        (e.text ? escapeText(e.text) : '') +
        (e.field && PATH.test(e.field) ? `{{ ${e.field} }}` : '')
      );
    case 'image':
      return e.src && IMAGE.test(e.src) ? `<img src="${e.src}" alt="">` : '';
    case 'qr':
      return '<img src="{{ qr }}" alt="QR">';
    case 'table': {
      const columns = (e.columns ?? []).filter((c) => PATH.test(c.field));
      const head = columns.map((c) => `<th>${escapeText(c.title)}</th>`).join('');
      const cells = columns.map((c) => `<td>{{ line.${c.field} }}</td>`).join('');
      return `<table><thead><tr>${head}</tr></thead><tbody>{% for line in lines %}<tr>${cells}</tr>{% endfor %}</tbody></table>`;
    }
    default:
      return '';
  }
}

/** The template's HTML (with Liquid) for a layout: one page, every element placed absolutely in millimetres. */
export function toHtml(design: PrintDesign): string {
  const page = pageMm(design.pageSize, design.landscape);
  const orientation = design.landscape ? 'landscape' : 'portrait';
  const background = color(design.background);
  const elements = design.elements
    .map(
      (e) =>
        `    <div class="el el-${e.kind}" style="${styleOf(e, design.direction)}">${contentOf(e)}</div>`,
    )
    .join('\n');
  return `<html dir="${design.direction}" lang="${design.direction === 'rtl' ? 'ar' : 'en'}">
<head>
  <meta charset="utf-8">
  <style>
    @page { size: ${design.pageSize} ${orientation}; margin: 0; }
    body { margin: 0; font-family: 'IBM Plex Sans Arabic', sans-serif; color: #111927; }
    .page { position: relative; width: ${page.w}mm; height: ${page.h}mm; margin: 0 auto; overflow: hidden;${background ? ` background: ${background};` : ''} }
    .el { position: absolute; box-sizing: border-box; overflow: hidden; line-height: 1.35; }
    .el img { width: 100%; height: 100%; object-fit: contain; }
    .el table { width: 100%; border-collapse: collapse; }
    .el th, .el td { border: 1px solid #d2d6db; padding: 1.5mm 2.5mm; text-align: start; }
    .el th { background: #f3f4f6; font-weight: 700; }
  </style>
</head>
<body>
  <div class="page">
${elements}
  </div>
</body>
</html>
`;
}

/** A stored design, or null when the value is not one (e.g. a template written as HTML). */
export function readDesign(value: unknown): PrintDesign | null {
  if (!value || typeof value !== 'object') return null;
  const design = value as Partial<PrintDesign>;
  if (design.version !== 1 || !Array.isArray(design.elements)) return null;
  return {
    version: 1,
    pageSize: design.pageSize && design.pageSize in PAGES ? design.pageSize : 'A4',
    landscape: !!design.landscape,
    direction: design.direction === 'ltr' ? 'ltr' : 'rtl',
    background: design.background,
    elements: design.elements.filter(
      (e): e is PrintElement =>
        !!e && typeof e === 'object' && typeof (e as PrintElement).id === 'string',
    ),
  };
}
