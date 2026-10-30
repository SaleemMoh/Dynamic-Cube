import {
  clampToPage,
  escapeText,
  newElement,
  pageMm,
  readDesign,
  starterDesign,
  toHtml,
} from './print-model';

describe('print layout', () => {
  it('measures pages in millimetres, turned for landscape', () => {
    expect(pageMm('A4', false)).toEqual({ w: 210, h: 297 });
    expect(pageMm('A5', true)).toEqual({ w: 210, h: 148 });
  });

  it('prints static text as it is, never as Liquid or markup', () => {
    expect(escapeText('<b>{{ secret }}</b> 50%')).toBe(
      '&lt;b&gt;&#123;&#123; secret &#125;&#125;&lt;/b&gt; 50&#37;',
    );
  });

  it('generates one absolutely placed page with fields, the lines table and the QR code', () => {
    const html = toHtml(starterDesign());
    expect(html).toContain('@page { size: A4 portrait; margin: 0; }');
    expect(html).toContain('left:20mm;top:25mm;width:170mm;height:14mm');
    expect(html).toContain('{{ document.typeNameAr }}');
    expect(html).toContain('رقم المستند: {{ document.number }}');
    expect(html).toContain(
      '{% for line in lines %}<tr><td>{{ line.sector }}</td><td>{{ line.grade }}</td></tr>{% endfor %}',
    );
    expect(html).toContain('<img src="{{ qr }}" alt="QR">');
  });

  it('drops unsafe field paths, colours and image addresses', () => {
    const design = starterDesign();
    design.elements = [
      {
        id: 'a',
        kind: 'field',
        field: 'x }}{% raw',
        x: 0,
        y: 0,
        w: 10,
        h: 10,
        color: 'red;background:url(x)',
      },
      { id: 'b', kind: 'image', src: 'javascript:alert(1)', x: 0, y: 0, w: 10, h: 10 },
    ];
    const html = toHtml(design);
    expect(html).not.toContain('{% raw');
    expect(html).not.toContain('url(x)');
    expect(html).not.toContain('javascript:');
  });

  it('keeps elements on the page and snaps them to the grid', () => {
    const e = clampToPage(
      { ...newElement('box', 0, 0, 'b'), x: 205.4, y: -3, w: 80.4, h: 40 },
      pageMm('A4', false),
      1,
    );
    expect(e).toMatchObject({ x: 130, y: 0, w: 80, h: 40 });
  });

  it('reads stored designs and rejects anything else', () => {
    expect(
      readDesign({ version: 1, pageSize: 'A5', elements: [{ id: 'x', kind: 'text' }] })?.pageSize,
    ).toBe('A5');
    expect(readDesign('<html>')).toBeNull();
    expect(readDesign({ version: 2, elements: [] })).toBeNull();
  });
});
