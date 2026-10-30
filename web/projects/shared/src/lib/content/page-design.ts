/** How a section splits into columns (relative widths). */
export type PageLayout = '1' | '1-1' | '1-1-1' | '2-1' | '1-2';

export type PageBackground = 'none' | 'light' | 'brand' | 'dark';

export type PageBlockKind =
  'heading' | 'text' | 'image' | 'button' | 'cards' | 'callout' | 'divider' | 'spacer';

/** A card of a cards block. */
export interface PageCard {
  icon?: string;
  titleAr: string;
  titleEn: string;
  textAr?: string;
  textEn?: string;
  url?: string;
}

/** A block of content in a column; texts come in both languages. */
export interface PageBlock {
  id: string;
  kind: PageBlockKind;
  /** Heading, text (Markdown) and callout. */
  textAr?: string;
  textEn?: string;
  /** Heading: 1–3. */
  level?: number;
  align?: 'start' | 'center' | 'end';
  /** Image: an https address or an embedded image. */
  src?: string;
  altAr?: string;
  altEn?: string;
  /** Button. */
  labelAr?: string;
  labelEn?: string;
  url?: string;
  style?: 'primary' | 'secondary';
  /** Callout. */
  tone?: 'info' | 'success' | 'warning';
  /** Cards. */
  items?: PageCard[];
  /** Spacer: height in pixels. */
  size?: number;
}

/** A band of the page: its columns, each a list of blocks. */
export interface PageSection {
  id: string;
  layout: PageLayout;
  background: PageBackground;
  columns: PageBlock[][];
}

/** A page's designed layout (what a page version stores). */
export interface PageDesign {
  version: 1;
  sections: PageSection[];
}

/** Columns of each layout and their relative widths. */
export const PAGE_LAYOUTS: Record<PageLayout, number[]> = {
  '1': [1],
  '1-1': [1, 1],
  '1-1-1': [1, 1, 1],
  '2-1': [2, 1],
  '1-2': [1, 2],
};

/** A layout with the given number of columns kept from the old one (blocks of dropped columns join the last kept one). */
export function relayout(section: PageSection, layout: PageLayout): PageSection {
  const count = PAGE_LAYOUTS[layout].length;
  const columns = Array.from({ length: count }, (_, i) => [...(section.columns[i] ?? [])]);
  for (let i = count; i < section.columns.length; i++)
    columns[count - 1].push(...section.columns[i]);
  return { ...section, layout, columns };
}

export function newBlock(kind: PageBlockKind, id: string): PageBlock {
  switch (kind) {
    case 'heading':
      return { id, kind, level: 2, textAr: 'عنوان', textEn: 'Heading' };
    case 'text':
      return { id, kind, textAr: 'نص الفقرة.', textEn: 'Paragraph text.' };
    case 'image':
      return { id, kind, src: '', altAr: '', altEn: '' };
    case 'button':
      return {
        id,
        kind,
        labelAr: 'المزيد',
        labelEn: 'Learn more',
        url: '/services',
        style: 'primary',
      };
    case 'cards':
      return {
        id,
        kind,
        items: [
          {
            icon: 'verified',
            titleAr: 'عنوان البطاقة',
            titleEn: 'Card title',
            textAr: 'وصف قصير.',
            textEn: 'A short description.',
          },
          {
            icon: 'speed',
            titleAr: 'عنوان البطاقة',
            titleEn: 'Card title',
            textAr: 'وصف قصير.',
            textEn: 'A short description.',
          },
        ],
      };
    case 'callout':
      return { id, kind, tone: 'info', textAr: 'ملاحظة مهمة.', textEn: 'An important note.' };
    case 'spacer':
      return { id, kind, size: 32 };
    default:
      return { id, kind };
  }
}

export function newSection(id: string, layout: PageLayout = '1'): PageSection {
  return { id, layout, background: 'none', columns: PAGE_LAYOUTS[layout].map(() => []) };
}

/** A first layout: a title band and a two-column band. */
export function starterPage(): PageDesign {
  return {
    version: 1,
    sections: [
      {
        id: 's1',
        layout: '1',
        background: 'light',
        columns: [
          [
            {
              id: 'b1',
              kind: 'heading',
              level: 1,
              align: 'center',
              textAr: 'عنوان الصفحة',
              textEn: 'Page title',
            },
            {
              id: 'b2',
              kind: 'text',
              align: 'center',
              textAr: 'مقدمة قصيرة عن الصفحة.',
              textEn: 'A short introduction.',
            },
          ],
        ],
      },
      {
        id: 's2',
        layout: '1-1',
        background: 'none',
        columns: [
          [{ id: 'b3', kind: 'text', textAr: 'نص العمود الأول.', textEn: 'First column.' }],
          [
            {
              id: 'b4',
              kind: 'button',
              labelAr: 'الخدمات',
              labelEn: 'Services',
              url: '/services',
              style: 'primary',
            },
          ],
        ],
      },
    ],
  };
}

/** A stored layout, or null when the value is not one. */
export function readPageDesign(value: unknown): PageDesign | null {
  if (!value || typeof value !== 'object') return null;
  const design = value as Partial<PageDesign>;
  if (!Array.isArray(design.sections)) return null;
  return {
    version: 1,
    sections: design.sections
      .filter(
        (s): s is PageSection =>
          !!s && typeof s === 'object' && Array.isArray((s as PageSection).columns),
      )
      .map((s) => ({
        ...s,
        layout: s.layout in PAGE_LAYOUTS ? s.layout : '1',
        background: s.background ?? 'none',
      })),
  };
}

/** Whether a link may be followed: a site path, https or mailto. */
export function safeLink(url: string | undefined): string | null {
  if (!url) return null;
  const u = url.trim();
  return (u.startsWith('/') && !u.startsWith('//')) || /^https:\/\//i.test(u) || /^mailto:/i.test(u)
    ? u
    : null;
}

/** Whether an image may be shown: https or an embedded png/jpeg/webp. */
export function safeImage(src: string | undefined): string | null {
  if (!src) return null;
  return /^https:\/\//i.test(src) ||
    /^data:image\/(png|jpeg|webp);base64,[A-Za-z0-9+/=]+$/.test(src)
    ? src
    : null;
}
