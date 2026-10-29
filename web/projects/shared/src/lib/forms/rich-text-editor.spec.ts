import { cleanHtml, isBlankHtml } from './rich-text-editor';

describe('rich text editor', () => {
  it('keeps formatting and fields but drops scripts, handlers and script links', () => {
    const html = cleanHtml(
      '<p style="color:red"><b>{{ request.number }}</b></p><script>alert(1)</script><img src="x" onerror="alert(1)"><a href="javascript:alert(1)">x</a>',
    );
    expect(html).toContain('<p style="color:red"><b>{{ request.number }}</b></p>');
    expect(html).not.toContain('script');
    expect(html).not.toContain('onerror');
    expect(html).not.toContain('javascript');
  });

  it('treats an emptied editor as blank', () => {
    expect(isBlankHtml('<p><br></p>')).toBe(true);
    expect(isBlankHtml('<p>مرحبا</p>')).toBe(false);
    expect(isBlankHtml(null)).toBe(true);
  });
});
