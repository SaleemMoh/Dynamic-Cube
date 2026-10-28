import { docStatusBadge } from './document-view';
import { fieldText } from './documents.models';

describe('document helpers', () => {
  it('shows field values as text', () => {
    expect(fieldText(null)).toBe('');
    expect(fieldText(3)).toBe('3');
    expect(fieldText(true)).toBe('✓');
    expect(fieldText({ a: 1 })).toBe('{"a":1}');
    expect(fieldText('الرياض')).toBe('الرياض');
  });

  it('colours statuses', () => {
    expect(docStatusBadge('ACTIVE')).toBe('dc-badge--success');
    expect(docStatusBadge('SUSPENDED')).toBe('dc-badge--warning');
    expect(docStatusBadge('CANCELLED')).toBe('dc-badge--danger');
    expect(docStatusBadge('EXPIRED')).toBe('');
  });
});
