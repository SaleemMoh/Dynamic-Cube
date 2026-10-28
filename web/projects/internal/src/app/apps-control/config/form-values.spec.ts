import {
  emptyToNull,
  fromDate,
  fromJsonText,
  toDate,
  toJsonText,
  withCurrentValues,
} from './form-values';

describe('form values', () => {
  it('round-trips ISO dates through local Date objects without shifting the day', () => {
    const date = toDate('2026-03-01');
    expect(date?.getDate()).toBe(1);
    expect(fromDate(date)).toBe('2026-03-01');
    expect(toDate('not a date')).toBeNull();
    expect(fromDate(null)).toBeNull();
  });

  it('formats and parses JSON, treating empty text as null', () => {
    expect(toJsonText({ a: 1 })).toBe('{\n  "a": 1\n}');
    expect(toJsonText(null)).toBe('');
    expect(fromJsonText('[1, 2]')).toEqual([1, 2]);
    expect(fromJsonText('  ')).toBeNull();
    expect(() => fromJsonText('{')).toThrow();
  });

  it('turns blank text into null', () => {
    expect(emptyToNull('  ')).toBeNull();
    expect(emptyToNull('x')).toBe('x');
    expect(emptyToNull(0)).toBe(0);
  });

  it('adds stored values missing from fixed options so they stay selected', () => {
    const options = [{ value: 'VIEW', label: { ar: 'VIEW', en: 'VIEW' } }];
    expect(withCurrentValues(options, ['VIEW', 'SOURCES_REVIEW'])?.map((o) => o.value)).toEqual([
      'VIEW',
      'SOURCES_REVIEW',
    ]);
    expect(withCurrentValues(options, 'VIEW')).toBe(options);
    expect(withCurrentValues(options, null)).toBe(options);
    expect(withCurrentValues(undefined, ['X'])).toBeUndefined();
  });
});
