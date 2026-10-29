/**
 * A small, safe Markdown subset for portal pages and answers: headings, paragraphs, lists, quotes, rules, bold, italic,
 * code and links. All text is HTML-escaped first, so authors cannot inject markup; links must be http(s), mailto, tel or
 * site-relative. `#` and `##` are the page's sections (`h2`: the page title is the `h1`), `###` and `####` below them.
 */
export function renderMarkdown(source: string | null | undefined): string {
  const lines = (source ?? '').replace(/\r\n?/g, '\n').split('\n');
  const html: string[] = [];
  let paragraph: string[] = [];
  let list: { tag: 'ul' | 'ol'; items: string[] } | null = null;
  let quote: string[] = [];

  const flush = () => {
    if (paragraph.length) html.push(`<p>${paragraph.map(inline).join('<br>')}</p>`);
    if (list) html.push(`<${list.tag}>${list.items.map((i) => `<li>${inline(i)}</li>`).join('')}</${list.tag}>`);
    if (quote.length) html.push(`<blockquote>${quote.map(inline).join('<br>')}</blockquote>`);
    paragraph = [];
    list = null;
    quote = [];
  };

  for (const raw of lines) {
    const line = raw.trimEnd();
    const heading = /^(#{1,4})\s+(.+)$/.exec(line);
    const bullet = /^\s*[-*]\s+(.+)$/.exec(line);
    const numbered = /^\s*\d+[.)]\s+(.+)$/.exec(line);
    const quoted = /^>\s?(.*)$/.exec(line);
    if (!line.trim()) {
      flush();
    } else if (heading) {
      flush();
      const level = Math.max(2, heading[1].length);
      html.push(`<h${level}>${inline(heading[2])}</h${level}>`);
    } else if (/^(-{3,}|\*{3,})$/.test(line.trim())) {
      flush();
      html.push('<hr>');
    } else if (bullet || numbered) {
      const tag = bullet ? 'ul' : 'ol';
      if (paragraph.length || quote.length || (list && list.tag !== tag)) flush();
      list ??= { tag, items: [] };
      list.items.push((bullet ?? numbered)![1]);
    } else if (quoted) {
      if (paragraph.length || list) flush();
      quote.push(quoted[1]);
    } else if (list && /^\s{2,}\S/.test(raw)) {
      // An indented line continues the list item above.
      list.items[list.items.length - 1] += ' ' + line.trim();
    } else {
      if (list || quote.length) flush();
      paragraph.push(line.trim());
    }
  }
  flush();
  return html.join('\n');
}

const SAFE_URL = /^(https?:\/\/|mailto:|tel:|\/(?!\/)|#)/i;

function escape(text: string): string {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

function inline(text: string): string {
  const codes: string[] = [];
  let out = escape(text).replace(/`([^`]+)`/g, (_, code: string) => {
    codes.push(`<code>${code}</code>`);
    return `\uE000${codes.length - 1}\uE000`;
  });
  out = out.replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, (match, label: string, url: string) => {
    const href = url.replace(/&amp;/g, '&');
    if (!SAFE_URL.test(href)) return label;
    const external = /^https?:/i.test(href);
    return `<a href="${url}"${external ? ' target="_blank" rel="noopener"' : ''}>${label}</a>`;
  });
  out = out
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
    .replace(/(^|[^*\w])\*([^*\s][^*]*)\*/g, '$1<em>$2</em>')
    .replace(/(^|[^\w])_([^_\s][^_]*)_(?!\w)/g, '$1<em>$2</em>');
  return out.replace(/\uE000(\d+)\uE000/g, (_, i: string) => codes[Number(i)]);
}

/** The text of Markdown without its markup (search, previews). */
export function markdownText(source: string | null | undefined): string {
  return (source ?? '')
    .replace(/\[([^\]]+)\]\([^)]*\)/g, '$1')
    .replace(/^#{1,4}\s+|^\s*[-*]\s+|^\s*\d+[.)]\s+|^>\s?/gm, '')
    .replace(/[*_`]/g, '')
    .replace(/\s+/g, ' ')
    .trim();
}
