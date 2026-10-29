import { markdownText, renderMarkdown } from './markdown';

describe('renderMarkdown', () => {
  it('escapes markup written by authors', () => {
    expect(renderMarkdown('<script>alert(1)</script> & "x"')).toBe('<p>&lt;script&gt;alert(1)&lt;/script&gt; &amp; &quot;x&quot;</p>');
  });

  it('renders headings below the page title, paragraphs and line breaks', () => {
    expect(renderMarkdown('# الشروط\n\nسطر أول\nسطر ثان\n\n## Scope\n### Details')).toBe('<h2>الشروط</h2>\n<p>سطر أول<br>سطر ثان</p>\n<h2>Scope</h2>\n<h3>Details</h3>');
  });

  it('renders lists, quotes and rules', () => {
    expect(renderMarkdown('- one\n- two\n  continued\n\n1. first\n2) second\n\n> note\n\n---')).toBe(
      '<ul><li>one</li><li>two continued</li></ul>\n<ol><li>first</li><li>second</li></ol>\n<blockquote>note</blockquote>\n<hr>',
    );
  });

  it('renders bold, italic and code, leaving code untouched', () => {
    expect(renderMarkdown('**bold**, *italic*, _also_ and `a*b*c` but snake_case_name')).toBe(
      '<p><strong>bold</strong>, <em>italic</em>, <em>also</em> and <code>a*b*c</code> but snake_case_name</p>',
    );
  });

  it('keeps safe links only, opening other sites in a new tab', () => {
    expect(renderMarkdown('[portal](/services?category=A&b=1) [mail](mailto:a@b.sa) [site](https://x.sa) [bad](javascript:void) [bad2](//evil.com)')).toBe(
      '<p><a href="/services?category=A&amp;b=1">portal</a> <a href="mailto:a@b.sa">mail</a> <a href="https://x.sa" target="_blank" rel="noopener">site</a> bad bad2</p>',
    );
  });

  it('gives the plain text', () => {
    expect(markdownText('## Fees\n- **100** SAR, see [table](/fees)')).toBe('Fees 100 SAR, see table');
  });
});
