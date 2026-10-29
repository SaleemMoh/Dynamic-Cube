/** One difference between two payloads: where it is and the values on each side (undefined when absent). */
export interface DiffEntry {
  path: string;
  kind: 'added' | 'removed' | 'changed';
  before?: unknown;
  after?: unknown;
}

export interface DiffOptions {
  /** Top-level properties that are not configuration: the key and the ownership by default. */
  ignore?: readonly string[];
  /** How deep nested objects are compared field by field; deeper values are compared whole. */
  depth?: number;
}

const isObject = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value);

/** Missing and null mean the same: a payload may leave out what the stored row holds as null. */
const absent = (value: unknown) => value === undefined || value === null;

/** Deep equality of JSON values, ignoring property order. */
export function sameValue(a: unknown, b: unknown): boolean {
  if (absent(a) || absent(b)) return absent(a) && absent(b);
  if (Array.isArray(a) || Array.isArray(b)) {
    return Array.isArray(a) && Array.isArray(b) && a.length === b.length && a.every((item, i) => sameValue(item, b[i]));
  }
  if (isObject(a) || isObject(b)) {
    if (!isObject(a) || !isObject(b)) return false;
    const keys = new Set([...Object.keys(a), ...Object.keys(b)]);
    return [...keys].every((key) => sameValue(a[key], b[key]));
  }
  return a === b;
}

/** Lists of rows with a code (attributes, steps, nodes) are matched by code, so a reorder or insert reads clearly. */
function keyedByCode(list: unknown[]): Map<string, unknown> | null {
  const map = new Map<string, unknown>();
  for (const item of list) {
    if (!isObject(item) || typeof item['code'] !== 'string' || map.has(item['code'])) return null;
    map.set(item['code'], item);
  }
  return map;
}

function walk(before: unknown, after: unknown, path: string, depth: number, out: DiffEntry[]): void {
  if (sameValue(before, after)) return;
  if (absent(before)) {
    out.push({ path, kind: 'added', after });
    return;
  }
  if (absent(after)) {
    out.push({ path, kind: 'removed', before });
    return;
  }
  if (depth > 0 && isObject(before) && isObject(after)) {
    for (const key of new Set([...Object.keys(before), ...Object.keys(after)])) {
      walk(before[key], after[key], path ? `${path}.${key}` : key, depth - 1, out);
    }
    return;
  }
  if (depth > 0 && Array.isArray(before) && Array.isArray(after)) {
    const left = keyedByCode(before);
    const right = keyedByCode(after);
    if (left && right) {
      for (const code of new Set([...left.keys(), ...right.keys()])) {
        walk(left.get(code), right.get(code), `${path}[${code}]`, depth - 1, out);
      }
      return;
    }
    if (before.length === after.length && before.every(isObject) && after.every(isObject)) {
      before.forEach((item, i) => walk(item, after[i], `${path}[${i}]`, depth - 1, out));
      return;
    }
  }
  out.push({ path, kind: 'changed', before, after });
}

/**
 * Field-by-field differences between a row as it is and as it would become. Nested objects are followed (`a.b`),
 * lists of coded rows are matched by code (`steps[MAIN].titleEn`); other lists are compared whole.
 */
export function diffPayload(
  before: Record<string, unknown> | null | undefined,
  after: Record<string, unknown> | null | undefined,
  options: DiffOptions = {},
): DiffEntry[] {
  const ignore = new Set(options.ignore ?? ['code', 'owner']);
  const strip = (row: Record<string, unknown> | null | undefined) => Object.fromEntries(Object.entries(row ?? {}).filter(([key]) => !ignore.has(key)));
  const out: DiffEntry[] = [];
  walk(strip(before), strip(after), '', options.depth ?? 6, out);
  return out;
}
