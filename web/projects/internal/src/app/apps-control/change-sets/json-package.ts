import { ConfigType } from '../config/config-types';

/** What a JSON import may carry for one entity type: its type and how a row's key is formed. */
export interface ImportTarget {
  entityType: string;
  key: ConfigType['key'];
}

export interface PackageItem {
  entityType: string;
  entityCode: string;
  operation: 'CREATE' | 'UPDATE' | 'DELETE';
  payload: Record<string, unknown> | null;
}

export interface ConfigPackage {
  title: string;
  items: PackageItem[];
}

const isObject = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value);

/** The change-item key of a row: its code, `APP/CODE` for app-scoped rows, or the singleton's fixed code. */
export function rowKey(row: Record<string, unknown>, key: ConfigType['key']): string | null {
  if (typeof key === 'object') return key.singleton;
  const code = typeof row['code'] === 'string' ? row['code'].trim() : '';
  if (!code) return null;
  const app = typeof row['appCode'] === 'string' ? row['appCode'].trim() : '';
  return key === 'appScoped' && app && !code.includes('/') ? `${app}/${code}` : code;
}

/**
 * Turns pasted JSON into a configuration package. A package (`{ items: [...] }`) and package items pass through;
 * a row or a list of rows of the target type become items, updating the rows that exist (`existing` keys) and
 * creating the others. Throws an Error whose message says what is wrong.
 */
export function toPackage(json: unknown, target: ImportTarget | null, existing: ReadonlySet<string>, title: string): ConfigPackage {
  if (isObject(json) && Array.isArray(json['items'])) {
    return { title: typeof json['title'] === 'string' && json['title'].trim() ? json['title'] : title, items: json['items'] as PackageItem[] };
  }
  const list = Array.isArray(json) ? json : [json];
  if (!list.length) throw new Error('empty');
  const items = list.map((entry, index): PackageItem => {
    if (!isObject(entry)) throw new Error(`#${index + 1}: not an object`);
    if (typeof entry['entityType'] === 'string' && typeof entry['entityCode'] === 'string') {
      return entry as unknown as PackageItem;
    }
    if (!target) throw new Error(`#${index + 1}: entityType and entityCode are required`);
    const key = rowKey(entry, target.key);
    if (!key) throw new Error(`#${index + 1}: code is required`);
    const payload: Record<string, unknown> = { ...entry };
    delete payload['entityType'];
    return { entityType: target.entityType, entityCode: key, operation: existing.has(key) ? 'UPDATE' : 'CREATE', payload };
  });
  return { title, items };
}

/** Every problem of a package check as plain lines, to copy into a ticket or a message. */
export function problemLines(
  topLevel: string[],
  items: { index: number; entityType: string | null; entityCode: string | null; errors: Record<string, string[]> }[],
): string[] {
  const lines = [...topLevel];
  for (const item of items) {
    for (const [field, messages] of Object.entries(item.errors)) {
      for (const message of messages) lines.push(`#${item.index + 1} ${item.entityType ?? '?'} ${item.entityCode ?? '?'} — ${field}: ${message}`);
    }
  }
  return lines;
}
