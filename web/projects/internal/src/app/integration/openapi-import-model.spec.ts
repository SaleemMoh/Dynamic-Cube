import { OpenApiReadResult } from '@dynamic-cube/shared';
import {
  fieldsFor,
  importItems,
  importProblems,
  initialChoices,
  matchingEnvelope,
  withEnvelope,
  withPrefix,
} from './openapi-import-model';

const read: OpenApiReadResult = {
  title: 'Ministry of Commerce',
  version: '1',
  description: null,
  suggestedProviderCode: 'MINISTRY_OF_COMMERCE',
  servers: ['/relative', 'https://api.commerce.example/v2'],
  authentication: [
    {
      name: 'gw',
      authType: 'OAUTH2_CLIENT_CREDENTIALS',
      tokenUrl: '/oauth/token',
      apiKeyHeader: null,
      note: null,
    },
  ],
  operations: [
    {
      code: 'GET_REGISTRATION',
      method: 'GET',
      pathTemplate: '/registrations/{crNumber}',
      name: 'Registration by CR number',
      description: 'Reads a registration.',
      tag: null,
      parameters: [
        {
          name: 'crNumber',
          location: 'PATH',
          target: null,
          isRequired: true,
          isMasked: false,
          sampleValue: '1010',
        },
      ],
      fields: Array.from({ length: 20 }, (_, i) => ({
        name: `f${i}`,
        path: `data.f${i}`,
        type: 'string',
      })),
      warnings: [],
    },
    {
      code: 'VERIFY_OWNER',
      method: 'POST',
      pathTemplate: '/owners/verify',
      name: 'Verify owner',
      description: null,
      tag: null,
      parameters: [],
      fields: [],
      warnings: [],
    },
  ],
  warnings: [],
};

describe('OpenAPI import model', () => {
  it('starts with a new provider and connection from the description and every operation ticked', () => {
    const choices = initialChoices(read);
    expect(choices.connection).toEqual(
      expect.objectContaining({
        code: 'MINISTRY_OF_COMMERCE_API',
        baseUrl: 'https://api.commerce.example/v2',
        authType: 'OAUTH2_CLIENT_CREDENTIALS',
        secretName: 'MINISTRY_OF_COMMERCE',
        tokenUrl: '/oauth/token',
      }),
    );
    expect(choices.operations.map((o) => [o.code, o.selected, o.outputs.size])).toEqual([
      ['MINISTRY_OF_COMMERCE_GET_REGISTRATION', true, 15],
      ['MINISTRY_OF_COMMERCE_VERIFY_OWNER', true, 0],
    ]);
    expect(withPrefix('MCI', 'MCI_CR')).toBe('MCI_CR');
  });

  it('builds the package: provider, connection, then the ticked operations with their chosen outputs', () => {
    const choices = initialChoices(read);
    choices.operations[1].selected = false;
    expect(importProblems(choices, new Set())).toEqual([]);
    const items = importItems(choices);
    expect(items.map((i) => [i.entityType, i.entityCode])).toEqual([
      ['integ.provider', 'MINISTRY_OF_COMMERCE'],
      ['integ.connection', 'MINISTRY_OF_COMMERCE_API'],
      ['integ.operation', 'MINISTRY_OF_COMMERCE_GET_REGISTRATION'],
    ]);
    const op = items[2].payload;
    expect(op['connectionCode']).toBe('MINISTRY_OF_COMMERCE_API');
    expect((op['outputs'] as unknown[]).length).toBe(15);
    expect(items[1].payload['secretName']).toBe('MINISTRY_OF_COMMERCE');
    expect(items[1].payload['apiKeyHeader']).toBeNull();
  });

  it('uses an existing provider and connection without creating them, and stops on codes already in use', () => {
    const choices = initialChoices(read);
    choices.provider = { mode: 'existing', code: 'MCI', nameAr: '', nameEn: '' };
    choices.connection = { ...choices.connection, mode: 'existing', code: 'MOMRAH' };
    expect(importItems(choices).map((i) => i.entityType)).toEqual([
      'integ.operation',
      'integ.operation',
    ]);
    expect(importProblems(choices, new Set(['MINISTRY_OF_COMMERCE_VERIFY_OWNER']))).toEqual([
      'oi.badOperation',
    ]);
    choices.operations.forEach((o) => (o.selected = false));
    expect(importProblems(choices, new Set())).toEqual(['oi.nothing']);
  });

  it('reads outputs from the data an envelope finds, suggesting the envelope whose result path holds every response', () => {
    const envelopes = [
      { code: 'OTHER', resultPath: 'payload' },
      { code: 'MOMRAH', resultPath: 'data' },
    ];
    expect(matchingEnvelope(read, envelopes)).toEqual({ code: 'MOMRAH', resultPath: 'data' });
    expect(fieldsFor(read.operations[0], 'data')[0]).toEqual({
      name: 'f0',
      path: 'f0',
      type: 'string',
    });
    const choices = withEnvelope(initialChoices(read), 'MOMRAH', 'data');
    expect([...choices.operations[0].outputs].slice(0, 2)).toEqual(['f0', 'f1']);
    const op = importItems(choices).find((i) => i.entityCode.endsWith('GET_REGISTRATION'))!.payload;
    expect(op['envelopeProfileCode']).toBe('MOMRAH');
    expect((op['outputs'] as { path: string }[])[0].path).toBe('f0');
  });
});
