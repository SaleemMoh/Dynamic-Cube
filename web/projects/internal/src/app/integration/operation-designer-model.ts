import {
  OperationDefinition,
  OperationOutputDef,
  OperationParameterDef,
  OperationProtocol,
  SoapSettings,
} from '@dynamic-cube/shared';

export const METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'] as const;

const CODE = /^[A-Z][A-Z0-9_-]{0,50}$/;
const PARAMETER_NAME = /^[A-Za-z][A-Za-z0-9_-]{0,63}$/;
const READ_PATH = /^[A-Za-z_$][\w$-]*(\[(\d+|\*)\])*(\.[A-Za-z_$][\w$-]*(\[(\d+|\*)\])*)*$/;
const WRITE_PATH = /^[A-Za-z_$][\w$-]*(\.[A-Za-z_$][\w$-]*)*$/;
/** An XML element name without prefix (the parts of a SOAP body input's path). */
const XML_NAME = /^[A-Za-z_][\w-]*$/;
const XML_OPERATION = /^[A-Za-z_][\w.-]*$/;
const SOAP_11 = 'http://schemas.xmlsoap.org/soap/envelope/';
const SOAP_12 = 'http://www.w3.org/2003/05/soap-envelope';

/** A new SOAP call: version 1.1, child elements in the operation's namespace. */
export function emptySoap(): SoapSettings {
  return {
    version: '1.1',
    action: null,
    operationName: '',
    namespace: null,
    qualifiedElements: true,
  };
}

/** Whether the operation is a SOAP one (ADR-0044). */
export function isSoap(definition: OperationDefinition): boolean {
  return definition.protocol === 'SOAP';
}

/** Switches between REST and SOAP: SOAP is always a POST and carries its call; REST drops it. */
export function withProtocol(
  definition: OperationDefinition,
  protocol: OperationProtocol,
): OperationDefinition {
  if (protocol === 'SOAP')
    return {
      ...definition,
      protocol,
      method: 'POST',
      soap: definition.soap ?? emptySoap(),
      isIdempotent: definition.isIdempotent ?? null,
    };
  return { ...definition, protocol: 'REST', soap: null };
}

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
    protocol: value.protocol === 'SOAP' ? 'SOAP' : 'REST',
    soap: value.protocol === 'SOAP' ? { ...emptySoap(), ...(value.soap ?? {}) } : null,
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

/**
 * The SOAP envelope the provider receives (ADR-0044): the operation element in its namespace with each body input as an
 * element at its dot path (its value, or its name as a placeholder). The WS-Security header is added when it is sent.
 */
export function previewEnvelope(
  definition: OperationDefinition,
  inputs: Record<string, string | null>,
): string {
  const soap = definition.soap ?? emptySoap();
  interface XmlNode {
    name: string;
    value?: string;
    children: XmlNode[];
  }
  const root: XmlNode = { name: soap.operationName || 'Operation', children: [] };
  for (const p of definition.parameters.filter((x) => x.location === 'BODY')) {
    const parts = (p.target || p.name).split('.');
    if (!parts.every((part) => XML_NAME.test(part))) continue;
    let node = root;
    for (const part of parts.slice(0, -1)) {
      let child = [...node.children].reverse().find((c) => c.name === part && !c.value);
      if (!child) {
        child = { name: part, children: [] };
        node.children.push(child);
      }
      node = child;
    }
    node.children.push({
      name: parts[parts.length - 1],
      value: inputs[p.name] || p.sampleValue || `<${p.name}>`,
      children: [],
    });
  }
  const prefix = soap.namespace ? 'ns:' : '';
  const child = soap.qualifiedElements ? prefix : '';
  const escape = (text: string) =>
    text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
  const write = (node: XmlNode, indent: string, name: string): string =>
    node.value !== undefined
      ? `${indent}<${name}${node.name}>${escape(node.value)}</${name}${node.name}>`
      : node.children.length === 0
        ? `${indent}<${name}${node.name}/>`
        : [
            `${indent}<${name}${node.name}>`,
            ...node.children.map((c) => write(c, `${indent}  `, child)),
            `${indent}</${name}${node.name}>`,
          ].join('\n');
  const ns = soap.namespace ? ` xmlns:ns="${escape(soap.namespace)}"` : '';
  const operation = write(root, '    ', prefix).replace(
    `<${prefix}${root.name}`,
    `<${prefix}${root.name}${ns}`,
  );
  return [
    `<soapenv:Envelope xmlns:soapenv="${soap.version === '1.2' ? SOAP_12 : SOAP_11}">`,
    '  <soapenv:Body>',
    operation,
    '  </soapenv:Body>',
    '</soapenv:Envelope>',
  ].join('\n');
}

/**
 * A pasted SOAP response read as the platform reads it (ADR-0044): the content of the body's element as JSON — text
 * elements as strings, empty or nil ones as null, repeated elements as lists, attributes as properties, text beside
 * attributes as `$text`. A fault is read the same way. Null when the text is not a SOAP envelope.
 */
export function soapResponseToJson(text: string): unknown {
  const doc = new DOMParser().parseFromString(text, 'application/xml');
  const root = doc.documentElement;
  if (
    !root ||
    doc.getElementsByTagName('parsererror').length > 0 ||
    root.localName !== 'Envelope' ||
    (root.namespaceURI !== SOAP_11 && root.namespaceURI !== SOAP_12)
  )
    return null;
  const body = [...root.children].find(
    (c) => c.localName === 'Body' && c.namespaceURI === root.namespaceURI,
  );
  if (!body) return null;
  const first = body.firstElementChild;
  return first ? xmlToJson(first) : null;
}

const XSI = 'http://www.w3.org/2001/XMLSchema-instance';

function xmlToJson(element: Element): unknown {
  if (element.getAttributeNS(XSI, 'nil') === 'true') return null;
  const attributes = [...element.attributes].filter(
    (a) => a.prefix !== 'xmlns' && a.name !== 'xmlns' && a.namespaceURI !== XSI,
  );
  const children = [...element.children];
  const text = element.textContent ?? '';
  if (children.length === 0 && attributes.length === 0) return text.length ? text : null;
  const result: Record<string, unknown> = {};
  for (const a of attributes) result[a.localName] = a.value;
  const groups = new Map<string, unknown[]>();
  for (const c of children) {
    const list = groups.get(c.localName) ?? [];
    list.push(xmlToJson(c));
    groups.set(c.localName, list);
  }
  for (const [key, items] of groups) result[key] = items.length === 1 ? items[0] : items;
  if (children.length === 0 && text.length) result['$text'] = text;
  return result;
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
  const soap = isSoap(definition);
  const template = definition.pathTemplate?.trim() ?? '';
  if ((!template.startsWith('/') && !(soap && template === '')) || template.includes('://'))
    p('request', soap ? 'od.problem.soapPath' : 'od.problem.path');
  if (soap) {
    const call = definition.soap;
    if (!call || !XML_OPERATION.test(call.operationName?.trim() ?? ''))
      p('request', 'od.problem.soapOperation');
    if (call && call.version !== '1.1' && call.version !== '1.2')
      p('request', 'od.problem.soapVersion');
    if (call?.namespace?.trim() && !/^[A-Za-z][A-Za-z0-9+.-]*:\S+$/.test(call.namespace.trim()))
      p('request', 'od.problem.soapNamespace');
  }
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
    else if (
      soap &&
      param.location === 'BODY' &&
      !(param.target || param.name).split('.').every((part) => XML_NAME.test(part))
    )
      p('request', 'od.problem.soapElement', { name: param.name });
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
