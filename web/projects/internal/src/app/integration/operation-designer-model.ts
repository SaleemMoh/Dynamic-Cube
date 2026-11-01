import {
  OperationDefinition,
  OperationOutputDef,
  OperationParameterDef,
} from '@dynamic-cube/shared';

export const METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'] as const;

const CODE = /^[A-Z][A-Z0-9_-]{0,50}$/;
const PARAMETER_NAME = /^[A-Za-z][A-Za-z0-9_-]{0,63}$/;
const READ_PATH = /^[A-Za-z_$][\w$-]*(\[(\d+|\*)\])*(\.[A-Za-z_$][\w$-]*(\[(\d+|\*)\])*)*$/;
const WRITE_PATH = /^[A-Za-z_$][\w$-]*(\.[A-Za-z_$][\w$-]*)*$/;

/** A new operation: a GET on the chosen connection, nothing else yet. */
export function emptyOperation(connectionCode: string | null = null): OperationDefinition {
  return {
    nameAr: '',
    nameEn: '',
    connectionCode,
    envelopeProfileCode: null,
    descriptionAr: null,
    descriptionEn: null,
    method: 'GET',
    pathTemplate: '/',
    parameters: [],
    outputs: [],
    responseCodes: [],
    maskFields: [],
    cacheSeconds: 0,
    isIdempotent: null,
  };
}

/** A stored operation with every list present, so each editor has something to work on. */
export function normalizeOperation(
  row: Record<string, unknown> | null | undefined,
): OperationDefinition {
  const value = { ...emptyOperation(), ...(row ?? {}) } as OperationDefinition;
  return {
    ...value,
    parameters: (value.parameters ?? []).map((p) => ({
      ...p,
      target: p.target ?? null,
      sampleValue: p.sampleValue ?? null,
    })),
    outputs: value.outputs ?? [],
    responseCodes: value.responseCodes ?? [],
    maskFields: value.maskFields ?? [],
  };
}

/** The placeholders of a path template, e.g. /cr/{crNumber} → ['crNumber']. */
export function placeholders(template: string): string[] {
  return [...template.matchAll(/\{([^}]*)\}/g)].map((m) => m[1]);
}

/**
 * Keeps the path parameters in line with the template: a placeholder typed in the path gets its parameter (required),
 * and a path parameter whose placeholder was removed goes.
 */
export function syncPathParameters(
  parameters: OperationParameterDef[],
  template: string,
): OperationParameterDef[] {
  const names = placeholders(template).filter((n) => PARAMETER_NAME.test(n));
  const kept = parameters.filter((p) => p.location !== 'PATH' || names.includes(p.name));
  const added = names
    .filter((n) => !kept.some((p) => p.location === 'PATH' && p.name === n))
    .map<OperationParameterDef>((name) => ({
      name,
      location: 'PATH',
      target: null,
      isRequired: true,
      isMasked: false,
      sampleValue: null,
    }));
  return [...kept, ...added];
}

/** The URL the provider receives for these inputs (the path filled, the query added), without the connection's base. */
export function previewPath(
  definition: OperationDefinition,
  inputs: Record<string, string | null>,
): string {
  let path = definition.pathTemplate;
  const query: string[] = [];
  for (const p of definition.parameters) {
    const value = inputs[p.name] ?? p.sampleValue ?? '';
    if (p.location === 'PATH')
      path = path.replaceAll(`{${p.name}}`, value ? encodeURIComponent(value) : `{${p.name}}`);
    if (p.location === 'QUERY' && value)
      query.push(`${encodeURIComponent(p.target || p.name)}=${encodeURIComponent(value)}`);
  }
  return path + (query.length ? `?${query.join('&')}` : '');
}

/** The JSON body the provider receives: each body input written at its dot path (its value, or its name as a placeholder). */
export function previewBody(
  definition: OperationDefinition,
  inputs: Record<string, string | null>,
): Record<string, unknown> | null {
  const body: Record<string, unknown> = {};
  let any = false;
  for (const p of definition.parameters.filter((x) => x.location === 'BODY')) {
    const path = p.target || p.name;
    if (!WRITE_PATH.test(path)) continue;
    const parts = path.split('.');
    let node = body;
    for (const part of parts.slice(0, -1)) {
      if (typeof node[part] !== 'object' || node[part] === null) node[part] = {};
      node = node[part] as Record<string, unknown>;
    }
    node[parts[parts.length - 1]] = inputs[p.name] || p.sampleValue || `<${p.name}>`;
    any = true;
  }
  return any ? body : null;
}

/** A node of a response as a tree: its key, the output path that reads it, a short preview and its children. */
export interface ResponseNode {
  key: string;
  path: string;
  kind: 'object' | 'array' | 'value';
  preview: string;
  children: ResponseNode[];
}

/**
 * A response as a tree to pick outputs from. A list shows its first element's fields under `name[*]`, so an output reads
 * the field of every element (e.g. Licenses[*].Commercial_NO).
 */
export function responseTree(value: unknown, path = '', key = '', depth = 0): ResponseNode {
  if (Array.isArray(value)) {
    const listPath = path ? `${path}[*]` : '';
    const first = value.find((v) => v !== null && v !== undefined);
    const children =
      depth < 8 && first !== undefined && typeof first === 'object' && !Array.isArray(first)
        ? responseTree(first, listPath, key, depth + 1).children
        : [];
    return { key, path: listPath, kind: 'array', preview: `[${value.length}]`, children };
  }
  if (value !== null && typeof value === 'object') {
    const children =
      depth < 8
        ? Object.entries(value as Record<string, unknown>)
            .filter(([k]) => /^[A-Za-z_$][\w$-]*$/.test(k))
            .map(([k, v]) => responseTree(v, path ? `${path}.${k}` : k, k, depth + 1))
        : [];
    return {
      key,
      path,
      kind: 'object',
      preview: `{${Object.keys(value as object).length}}`,
      children,
    };
  }
  const text =
    value === null || value === undefined
      ? 'null'
      : typeof value === 'string'
        ? `"${value}"`
        : String(value);
  return {
    key,
    path,
    kind: 'value',
    preview: text.length > 40 ? `${text.slice(0, 40)}…` : text,
    children: [],
  };
}

/** An output name from a path: its last two segments in camel case (owners[*].name → ownersName). */
export function outputName(path: string, taken: Iterable<string> = []): string {
  const parts = path
    .replaceAll('[*]', '')
    .replace(/\[\d+\]/g, '')
    .split('.')
    .filter(Boolean)
    .slice(-2)
    .map((p) => p.replace(/[^A-Za-z0-9]/g, ''))
    .filter(Boolean);
  let name =
    parts
      .map((p, i) => (i === 0 ? p[0].toLowerCase() + p.slice(1) : p[0].toUpperCase() + p.slice(1)))
      .join('') || 'value';
  if (!/^[A-Za-z]/.test(name)) name = `v${name}`;
  name = name.slice(0, 60);
  const used = new Set(taken);
  if (!used.has(name)) return name;
  for (let i = 2; ; i++) if (!used.has(`${name}${i}`)) return `${name}${i}`;
}

/** Adds an output reading `path` (once). */
export function addOutput(outputs: OperationOutputDef[], path: string): OperationOutputDef[] {
  if (!path || outputs.some((o) => o.path === path)) return outputs;
  return [
    ...outputs,
    {
      name: outputName(
        path,
        outputs.map((o) => o.name),
      ),
      path,
    },
  ];
}

/** A problem of the definition: the part of the designer it belongs to and a translation key with its values. */
export interface DesignProblem {
  part: 'request' | 'response' | 'codes' | 'details';
  key: string;
  params?: Record<string, string | number>;
}

/** The checks the server makes that can be made here (the server check covers the rest). */
export function checkOperation(
  code: string,
  definition: OperationDefinition,
  isNew: boolean,
): DesignProblem[] {
  const problems: DesignProblem[] = [];
  const p = (part: DesignProblem['part'], key: string, params?: DesignProblem['params']) =>
    problems.push({ part, key, params });
  if (isNew && !CODE.test(code)) p('details', 'od.problem.code');
  if (!definition.nameAr?.trim() || !definition.nameEn?.trim()) p('details', 'od.problem.names');
  if (!definition.connectionCode) p('request', 'od.problem.connection');
  const template = definition.pathTemplate?.trim() ?? '';
  if (!template.startsWith('/') || template.includes('://')) p('request', 'od.problem.path');
  const names = new Set<string>();
  definition.parameters.forEach((param, i) => {
    const n = i + 1;
    if (!PARAMETER_NAME.test(param.name)) p('request', 'od.problem.parameterName', { n });
    if (names.has(param.name)) p('request', 'od.problem.parameterTwice', { name: param.name });
    names.add(param.name);
    if (param.location === 'PATH' && !template.includes(`{${param.name}}`))
      p('request', 'od.problem.noPlaceholder', { name: param.name });
    if (param.location === 'BODY' && definition.method === 'GET')
      p('request', 'od.problem.bodyOnGet', { name: param.name });
    if (param.location === 'BODY' && !WRITE_PATH.test(param.target || param.name))
      p('request', 'od.problem.bodyPath', { name: param.name });
  });
  for (const name of placeholders(template)) {
    if (!definition.parameters.some((x) => x.location === 'PATH' && x.name === name))
      p('request', 'od.problem.placeholder', { name });
  }
  const outputs = new Set<string>();
  definition.outputs.forEach((o, i) => {
    if (!PARAMETER_NAME.test(o.name) || outputs.has(o.name))
      p('response', 'od.problem.outputName', { n: i + 1 });
    outputs.add(o.name);
    if (!READ_PATH.test(o.path)) p('response', 'od.problem.outputPath', { n: i + 1 });
  });
  const codes = new Set<string>();
  definition.responseCodes.forEach((c, i) => {
    if (!c.code?.trim() || codes.has(c.code)) p('codes', 'od.problem.code2', { n: i + 1 });
    codes.add(c.code);
  });
  if (definition.cacheSeconds < 0 || definition.cacheSeconds > 7 * 24 * 3600)
    p('details', 'od.problem.cache');
  return problems;
}
