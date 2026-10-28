/** Conversions between API payloads (ISO dates, JSON) and form controls (Date objects, JSON text). */

export function toDate(value: unknown): Date | null {
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return null;
  }
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

export function fromDate(value: unknown): string | null {
  if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
    return null;
  }
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
}

export function toJsonText(value: unknown): string {
  return value === undefined || value === null ? '' : JSON.stringify(value, null, 2);
}

/** Parses JSON text; empty text is null. Throws on invalid JSON. */
export function fromJsonText(value: unknown): unknown {
  if (typeof value !== 'string' || value.trim() === '') {
    return null;
  }
  return JSON.parse(value) as unknown;
}

export function emptyToNull(value: unknown): unknown {
  return typeof value === 'string' && value.trim() === '' ? null : value;
}
