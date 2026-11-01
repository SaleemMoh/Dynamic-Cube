import {
  AuthType,
  ImportedField,
  ImportedOperation,
  OpenApiReadResult,
  OperationDefinition,
} from '@dynamic-cube/shared';
import { outputName } from './operation-designer-model';

/** How many response fields are chosen as outputs at first (the rest can be ticked). */
export const FIRST_OUTPUTS = 15;

/** An operation of the description as reviewed: ticked or not, its final code and name, and the fields it reads. */
export interface OperationChoice {
  source: ImportedOperation;
  selected: boolean;
  code: string;
  name: string;
  outputs: Set<string>;
}

export interface ProviderChoice {
  mode: 'new' | 'existing';
  code: string;
  nameAr: string;
  nameEn: string;
}

export interface ConnectionChoice {
  mode: 'new' | 'existing';
  code: string;
  nameAr: string;
  nameEn: string;
  baseUrl: string;
  authType: AuthType;
  secretName: string;
  tokenUrl: string;
  apiKeyHeader: string;
}

export interface ImportChoices {
  provider: ProviderChoice;
  connection: ConnectionChoice;
  envelopeProfileCode: string | null;
  /** Where the chosen envelope finds the data (e.g. data.result): outputs are read from there. */
  resultPath: string | null;
  operations: OperationChoice[];
}

/**
 * The fields an operation's outputs can read: with an envelope that finds the data at a path, the fields under that path
 * with the path taken off (outputs are read from the data); otherwise all of them.
 */
export function fieldsFor(
  operation: ImportedOperation,
  resultPath: string | null,
): ImportedField[] {
  if (!resultPath) return operation.fields;
  const prefix = `${resultPath}.`;
  const names: string[] = [];
  return operation.fields
    .filter((f) => f.path.startsWith(prefix))
    .map((f) => {
      const path = f.path.slice(prefix.length);
      // Named from the path under the result (nameAr, not resultNameAr).
      const name = outputName(path, names);
      names.push(name);
      return { ...f, path, name };
    });
}

/** The envelope whose result path holds the response fields of every operation that has some. */
export function matchingEnvelope(
  read: OpenApiReadResult,
  envelopes: { code: string; resultPath: string | null }[],
): { code: string; resultPath: string } | null {
  const withFields = read.operations.filter((o) => o.fields.length);
  if (!withFields.length) return null;
  for (const e of envelopes) {
    const path = e.resultPath;
    if (path && withFields.every((o) => fieldsFor(o, path).length > 0))
      return { code: e.code, resultPath: path };
  }
  return null;
}

/** Chooses the envelope; each operation's outputs start again from the first fields under its result path. */
export function withEnvelope(
  choices: ImportChoices,
  code: string | null,
  resultPath: string | null,
): ImportChoices {
  const path = code ? resultPath : null;
  return {
    ...choices,
    envelopeProfileCode: code,
    resultPath: path,
    operations: choices.operations.map((o) => ({ ...o, outputs: firstOutputs(o.source, path) })),
  };
}

function firstOutputs(operation: ImportedOperation, resultPath: string | null): Set<string> {
  return new Set(
    fieldsFor(operation, resultPath)
      .slice(0, FIRST_OUTPUTS)
      .map((f) => f.path),
  );
}

export interface PackageItem {
  entityType: string;
  entityCode: string;
  operation: 'CREATE';
  payload: Record<string, unknown>;
}

const CODE = /^[A-Z][A-Z0-9_-]{0,50}$/;

/** The first choices for a description: a new provider and connection from what it says, every operation ticked. */
export function initialChoices(read: OpenApiReadResult): ImportChoices {
  const provider = read.suggestedProviderCode;
  const auth = read.authentication.find((a) => a.authType !== 'NONE') ?? null;
  return {
    provider: { mode: 'new', code: provider, nameAr: read.title, nameEn: read.title },
    connection: {
      mode: 'new',
      code: `${provider}_API`.slice(0, 51),
      nameAr: read.title,
      nameEn: read.title,
      baseUrl: read.servers.find((s) => /^https?:\/\//.test(s)) ?? '',
      authType: auth?.authType ?? 'NONE',
      secretName: auth ? provider : '',
      tokenUrl: auth?.tokenUrl ?? '',
      apiKeyHeader: auth?.apiKeyHeader ?? '',
    },
    envelopeProfileCode: null,
    resultPath: null,
    operations: read.operations.map((o) => ({
      source: o,
      selected: true,
      code: withPrefix(provider, o.code),
      name: o.name,
      outputs: firstOutputs(o, null),
    })),
  };
}

/** A code with the prefix (once), trimmed to the code length. */
export function withPrefix(prefix: string, code: string): string {
  const p = prefix ? `${prefix}_` : '';
  return (code.startsWith(p) ? code : p + code).slice(0, 51).replace(/_+$/, '');
}

/** Problems that stop the import (translation keys), given the codes already used. */
export function importProblems(
  choices: ImportChoices,
  usedOperationCodes: ReadonlySet<string>,
): string[] {
  const problems: string[] = [];
  const p = choices.provider;
  const c = choices.connection;
  if (!CODE.test(p.code) || (p.mode === 'new' && (!p.nameAr.trim() || !p.nameEn.trim())))
    problems.push('oi.needConnection');
  else if (
    !CODE.test(c.code) ||
    (c.mode === 'new' &&
      (!c.nameAr.trim() ||
        !c.nameEn.trim() ||
        !/^https?:\/\/\S+$/.test(c.baseUrl.trim()) ||
        (c.authType !== 'NONE' && !c.secretName.trim())))
  )
    problems.push('oi.needConnection');
  const selected = choices.operations.filter((o) => o.selected);
  if (!selected.length) problems.push('oi.nothing');
  const codes = new Set<string>();
  for (const o of selected) {
    if (
      !CODE.test(o.code) ||
      codes.has(o.code) ||
      usedOperationCodes.has(o.code) ||
      !o.name.trim()
    ) {
      problems.push('oi.badOperation');
      break;
    }
    codes.add(o.code);
  }
  return problems;
}

/** The operation definition an imported operation becomes, with the chosen outputs. */
export function operationPayload(
  choice: OperationChoice,
  connectionCode: string,
  envelopeProfileCode: string | null,
  resultPath: string | null = null,
): OperationDefinition {
  const o = choice.source;
  return {
    nameAr: choice.name.trim(),
    nameEn: choice.name.trim(),
    connectionCode,
    envelopeProfileCode,
    descriptionAr: null,
    descriptionEn: o.description ? o.description.slice(0, 2000) : null,
    // A SOAP operation read from a WSDL keeps its SOAP call (ADR-0044).
    protocol: o.protocol === 'SOAP' ? 'SOAP' : 'REST',
    soap: o.protocol === 'SOAP' ? (o.soap ?? null) : null,
    method: o.method,
    pathTemplate: o.pathTemplate,
    parameters: o.parameters.map((p) => ({
      ...p,
      target: p.target ?? null,
      sampleValue: p.sampleValue ?? null,
    })),
    outputs: fieldsFor(o, envelopeProfileCode ? resultPath : null)
      .filter((f) => choice.outputs.has(f.path))
      .map((f) => ({ name: f.name, path: f.path })),
    responseCodes: [],
    maskFields: [],
    cacheSeconds: 0,
    isIdempotent: null,
    entityCode: '',
    businessApps: [],
  };
}

/** The configuration package of the import: the new provider and connection (when new), then the ticked operations. */
export function importItems(choices: ImportChoices): PackageItem[] {
  const items: PackageItem[] = [];
  const level = { entityCode: '', businessApps: [] as string[] };
  const p = choices.provider;
  const c = choices.connection;
  if (p.mode === 'new') {
    items.push({
      entityType: 'integ.provider',
      entityCode: p.code,
      operation: 'CREATE',
      payload: { nameAr: p.nameAr.trim(), nameEn: p.nameEn.trim(), ...level },
    });
  }
  if (c.mode === 'new') {
    items.push({
      entityType: 'integ.connection',
      entityCode: c.code,
      operation: 'CREATE',
      payload: {
        nameAr: c.nameAr.trim(),
        nameEn: c.nameEn.trim(),
        providerCode: p.code,
        baseUrl: c.baseUrl.trim().replace(/\/+$/, ''),
        authType: c.authType,
        secretName: c.authType === 'NONE' ? null : c.secretName.trim(),
        tokenUrl: c.authType === 'OAUTH2_CLIENT_CREDENTIALS' ? c.tokenUrl.trim() || null : null,
        apiKeyHeader: c.authType === 'API_KEY' ? c.apiKeyHeader.trim() || null : null,
        timeoutSeconds: 30,
        retryCount: 2,
        circuitBreakerFailures: 5,
        circuitBreakerSeconds: 60,
        headers: [],
        ...level,
      },
    });
  }
  for (const o of choices.operations.filter((x) => x.selected)) {
    items.push({
      entityType: 'integ.operation',
      entityCode: o.code,
      operation: 'CREATE',
      payload: operationPayload(o, c.code, choices.envelopeProfileCode, choices.resultPath),
    });
  }
  return items;
}
