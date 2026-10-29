// Builds the Arabic user manual from manual-content.js: ar/README.md (Markdown) and ar/user-manual-ar.docx (Word, RTL).
// Run from the repository root: node docs/manual/build-manual.js   (needs the `docx` npm package).
// Screenshots come from ar/images (captured from the running internal system in Arabic).

const fs = require('fs');
const path = require('path');
const {
  AlignmentType,
  BorderStyle,
  Document,
  Footer,
  HeadingLevel,
  ImageRun,
  LevelFormat,
  Packer,
  PageNumber,
  Paragraph,
  ShadingType,
  Table,
  TableCell,
  TableRow,
  TextRun,
  WidthType,
} = require('docx');
const manual = require('./manual-content');

const dir = path.join(__dirname, 'ar');
/** Inline JSON (one level of nesting), e.g. {"var": "task.assignee"}. */
const JSON_PART = /(\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\})/;
/** Inline JSON as code in Markdown. */
const md = (text) => text.split(JSON_PART).map((part) => (part.startsWith('{') ? `\`${part}\`` : part)).join('');
const FONT = 'IBM Plex Sans Arabic';
const GOLD = '9D7A3D';
const GRAY = 'EAECF0';

// ---------- Markdown ----------
function markdown() {
  const lines = [`# ${manual.title}`, '', `**${manual.subtitle}** — ${manual.version}`, ''];
  lines.push('> هذا الملف مولّد من `docs/manual/manual-content.js` بالأمر `node docs/manual/build-manual.js`، ومعه نسخة Word: [user-manual-ar.docx](user-manual-ar.docx).', '');
  lines.push('## المحتويات', '');
  for (const b of manual.blocks.filter((b) => b.h1)) lines.push(`- [${b.h1}](#${anchor(b.h1)})`);
  lines.push('');
  for (const b of manual.blocks) {
    if (b.h1) lines.push(`## ${b.h1}`, '');
    else if (b.h2) lines.push(`### ${b.h2}`, '');
    else if (b.h3) lines.push(`#### ${b.h3}`, '');
    else if (b.p) lines.push(md(b.p), '');
    else if (b.ul) lines.push(...b.ul.map((i) => `- ${md(i)}`), '');
    else if (b.ol) lines.push(...b.ol.map((i, n) => `${n + 1}. ${md(i)}`), '');
    else if (b.note) lines.push(`> **ملاحظة:** ${md(b.note)}`, '');
    else if (b.img) lines.push(`![${b.caption}](images/${b.img})`, '', `*${b.caption}*`, '');
    else if (b.table) {
      lines.push(`| ${b.table.head.join(' | ')} |`, `| ${b.table.head.map(() => '---').join(' | ')} |`);
      lines.push(...b.table.rows.map((r) => `| ${r.map((c) => md(c).replace(/\|/g, '\\|')).join(' | ')} |`), '');
    }
  }
  return lines.join('\n');
}

/** GitHub-style heading anchor (Arabic letters kept, spaces to dashes, punctuation dropped). */
function anchor(text) {
  return `${text}`
    .trim()
    .toLowerCase()
    .replace(/[^\p{L}\p{N}\s-]/gu, '')
    .replace(/\s/g, '-');
}

// ---------- Word ----------
const run = (text, extra = {}) => new TextRun({ text, rightToLeft: true, font: { ascii: FONT, hAnsi: FONT, cs: FONT }, ...extra });

/** Text with inline `{…}` JSON kept left-to-right in its own run. */
function runs(text, extra = {}) {
  // LRE … PDF keeps JSON left-to-right inside right-to-left text.
  return text
    .split(JSON_PART)
    .filter(Boolean)
    .map((part) => (part.startsWith('{') ? new TextRun({ text: `\u202A${part}\u202C`, font: 'Consolas', size: 18, ...extra }) : run(part, extra)));
}

const para = (children, extra = {}) => new Paragraph({ bidirectional: true, alignment: AlignmentType.START, spacing: { after: 120, line: 320 }, children, ...extra });

function imageParagraphs(file, caption) {
  const data = fs.readFileSync(path.join(dir, 'images', file));
  // Screenshots are 1440×900; 16 cm wide.
  const width = 600;
  return [
    new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { before: 120, after: 60 },
      children: [new ImageRun({ type: 'jpg', data, transformation: { width, height: Math.round((width * 900) / 1440) }, altText: { title: caption, description: caption, name: file } })],
    }),
    new Paragraph({ bidirectional: true, alignment: AlignmentType.CENTER, spacing: { after: 240 }, children: [run(caption, { italics: true, size: 18, color: '667085' })] }),
  ];
}

function tableOf(head, rows) {
  const total = 9638; // A4 text width at 2 cm margins (DXA)
  const widths = head.length === 2 ? [3000, total - 3000] : head.map(() => Math.floor(total / head.length));
  widths[widths.length - 1] = total - widths.slice(0, -1).reduce((a, b) => a + b, 0);
  const border = { style: BorderStyle.SINGLE, size: 4, color: 'D0D5DD' };
  const cell = (text, i, header) =>
    new TableCell({
      width: { size: widths[i], type: WidthType.DXA },
      borders: { top: border, bottom: border, left: border, right: border },
      shading: header ? { type: ShadingType.CLEAR, color: 'auto', fill: 'F5EFE3' } : undefined,
      margins: { top: 60, bottom: 60, left: 100, right: 100 },
      children: [para(runs(text, header ? { bold: true } : {}), { spacing: { after: 0 } })],
    });
  return new Table({
    visuallyRightToLeft: true,
    width: { size: total, type: WidthType.DXA },
    columnWidths: widths,
    rows: [new TableRow({ tableHeader: true, children: head.map((h, i) => cell(h, i, true)) }), ...rows.map((r) => new TableRow({ children: r.map((c, i) => cell(c, i, false)) }))],
  });
}

function wordBody() {
  const out = [];
  let first = true;
  for (const b of manual.blocks) {
    if (b.h1) {
      out.push(new Paragraph({ heading: HeadingLevel.HEADING_1, bidirectional: true, pageBreakBefore: !first, children: [run(b.h1)] }));
      first = false;
    } else if (b.h2) out.push(new Paragraph({ heading: HeadingLevel.HEADING_2, bidirectional: true, children: [run(b.h2)] }));
    else if (b.h3) out.push(new Paragraph({ heading: HeadingLevel.HEADING_3, bidirectional: true, children: [run(b.h3)] }));
    else if (b.p) out.push(para(runs(b.p)));
    else if (b.ul) out.push(...b.ul.map((i) => para(runs(i), { numbering: { reference: 'bullets', level: 0 }, spacing: { after: 80, line: 300 } })));
    else if (b.ol) {
      const reference = `steps-${out.length}`;
      numberedLists.push(reference);
      out.push(...b.ol.map((i) => para(runs(i), { numbering: { reference, level: 0 }, spacing: { after: 80, line: 300 } })));
    } else if (b.note)
      out.push(
        para([run('ملاحظة: ', { bold: true, color: GOLD }), ...runs(b.note)], {
          shading: { type: ShadingType.CLEAR, color: 'auto', fill: 'FBF8F2' },
          border: { right: { style: BorderStyle.SINGLE, size: 18, color: GOLD, space: 8 } },
          indent: { start: 200, end: 200 },
        }),
      );
    else if (b.img) out.push(...imageParagraphs(b.img, b.caption));
    else if (b.table) out.push(tableOf(b.table.head, b.table.rows), new Paragraph({ children: [] }));
  }
  return out;
}

const numberedLists = [];

async function word() {
  const body = wordBody();
  const cover = [
    new Paragraph({ spacing: { before: 3000 }, children: [] }),
    new Paragraph({ bidirectional: true, alignment: AlignmentType.CENTER, children: [run(manual.title, { bold: true, size: 56, color: '101828' })] }),
    new Paragraph({ bidirectional: true, alignment: AlignmentType.CENTER, spacing: { before: 200 }, children: [run(manual.subtitle, { size: 32, color: GOLD })] }),
    new Paragraph({ bidirectional: true, alignment: AlignmentType.CENTER, spacing: { before: 600 }, children: [run(manual.version, { size: 22, color: '667085' })] }),
    new Paragraph({ bidirectional: true, pageBreakBefore: true, children: [run('المحتويات', { bold: true, size: 32, color: GOLD })] }),
    // A plain list of the chapters (a TOC field stays empty until Word updates it).
    ...manual.blocks.filter((b) => b.h1).map((b) => para([run(b.h1, { size: 24 })], { spacing: { after: 80 }, border: { bottom: { style: BorderStyle.DOTTED, size: 4, color: 'D0D5DD', space: 4 } } })),
    new Paragraph({ pageBreakBefore: true, children: [] }),
  ];
  const heading = (size, color) => ({ run: { font: { ascii: FONT, hAnsi: FONT, cs: FONT }, size, sizeComplexScript: size, bold: true, boldComplexScript: true, color }, paragraph: { spacing: { before: 240, after: 120 } } });
  const doc = new Document({
    creator: 'Dynamic Cube',
    title: manual.title,
    features: { updateFields: true },
    styles: {
      default: { document: { run: { font: { ascii: FONT, hAnsi: FONT, cs: FONT }, size: 22, sizeComplexScript: 22, rightToLeft: true } } },
      paragraphStyles: [
        { id: 'Heading1', name: 'Heading 1', basedOn: 'Normal', next: 'Normal', quickFormat: true, ...heading(36, GOLD), paragraph: { spacing: { before: 0, after: 200 }, outlineLevel: 0 } },
        { id: 'Heading2', name: 'Heading 2', basedOn: 'Normal', next: 'Normal', quickFormat: true, ...heading(28, '101828'), paragraph: { spacing: { before: 300, after: 120 }, outlineLevel: 1 } },
        { id: 'Heading3', name: 'Heading 3', basedOn: 'Normal', next: 'Normal', quickFormat: true, ...heading(24, '344054'), paragraph: { spacing: { before: 200, after: 100 }, outlineLevel: 2 } },
      ],
    },
    numbering: {
      config: [
        { reference: 'bullets', levels: [{ level: 0, format: LevelFormat.BULLET, text: '•', alignment: AlignmentType.START, style: { paragraph: { indent: { start: 540, hanging: 270 } } } }] },
        ...numberedLists.map((reference) => ({
          reference,
          levels: [{ level: 0, format: LevelFormat.DECIMAL, text: '%1.', alignment: AlignmentType.START, style: { paragraph: { indent: { start: 540, hanging: 300 } } } }],
        })),
      ],
    },
    sections: [
      {
        properties: { bidi: true, page: { margin: { top: 1134, bottom: 1134, left: 1134, right: 1134 } } },
        footers: {
          default: new Footer({
            children: [
              new Paragraph({
                alignment: AlignmentType.CENTER,
                children: [run(`${manual.title} — `, { size: 16, color: '98A2B3' }), new TextRun({ children: [PageNumber.CURRENT], size: 16, color: '98A2B3' })],
              }),
            ],
          }),
        },
        children: [...cover, ...body],
      },
    ],
  });
  return Packer.toBuffer(doc);
}

(async () => {
  fs.writeFileSync(path.join(dir, 'README.md'), markdown() + '\n');
  fs.writeFileSync(path.join(dir, 'user-manual-ar.docx'), await word());
  console.log('Wrote', path.join(dir, 'README.md'), 'and user-manual-ar.docx');
})();
