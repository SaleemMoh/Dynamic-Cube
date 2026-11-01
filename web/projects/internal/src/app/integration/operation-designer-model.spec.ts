import {
  addOutput,
  checkOperation,
  emptyOperation,
  normalizeOperation,
  outputName,
  placeholders,
  previewBody,
  previewEnvelope,
  previewPath,
  responseTree,
  soapResponseToJson,
  syncPathParameters,
  withProtocol,
} from './operation-designer-model';

describe('operation designer model', () => {
  it('keeps the path parameters in line with the placeholders of the path', () => {
    expect(placeholders('/cr/{crNumber}/owners/{ownerId}')).toEqual(['crNumber', 'ownerId']);
    const first = syncPathParameters(
      [{ name: 'lang', location: 'QUERY', isRequired: false, isMasked: false }],
      '/cr/{crNumber}',
    );
    expect(first.map((p) => [p.name, p.location, p.isRequired])).toEqual([
      ['lang', 'QUERY', false],
      ['crNumber', 'PATH', true],
    ]);
    // The placeholder renamed: the old path parameter goes, the new one comes; the query parameter stays.
    expect(syncPathParameters(first, '/cr/{number}').map((p) => p.name)).toEqual([
      'lang',
      'number',
    ]);
  });

  it('previews the URL and the JSON body the provider receives', () => {
    const op = {
      ...emptyOperation('GW'),
      method: 'POST',
      pathTemplate: '/cr/{crNumber}',
      parameters: [
        {
          name: 'crNumber',
          location: 'PATH' as const,
          isRequired: true,
          isMasked: false,
          sampleValue: '1010',
        },
        {
          name: 'lang',
          location: 'QUERY' as const,
          target: 'language',
          isRequired: false,
          isMasked: false,
          sampleValue: 'ar',
        },
        {
          name: 'idNumber',
          location: 'BODY' as const,
          target: 'person.id',
          isRequired: true,
          isMasked: true,
        },
        {
          name: 'note',
          location: 'BODY' as const,
          isRequired: false,
          isMasked: false,
          sampleValue: 'hi',
        },
      ],
    };
    expect(previewPath(op, { crNumber: '7001' })).toBe('/cr/7001?language=ar');
    expect(previewBody(op, {})).toEqual({ person: { id: '<idNumber>' }, note: 'hi' });
    expect(previewBody(emptyOperation(), {})).toBeNull();
  });

  it('shows a response as a tree whose lists read every element', () => {
    const tree = responseTree({
      crNumber: '1',
      owners: [{ name: 'A', id: 2 }],
      tags: ['x'],
      'bad key': 1,
    });
    expect(tree.children.map((c) => [c.key, c.path, c.kind])).toEqual([
      ['crNumber', 'crNumber', 'value'],
      ['owners', 'owners[*]', 'array'],
      ['tags', 'tags[*]', 'array'],
    ]);
    expect(tree.children[1].children.map((c) => c.path)).toEqual([
      'owners[*].name',
      'owners[*].id',
    ]);
    expect(outputName('owners[*].name')).toBe('ownersName');
    expect(outputName('crNumber', ['crNumber'])).toBe('crNumber2');
    const outputs = addOutput(addOutput([], 'owners[*].name'), 'owners[*].name');
    expect(outputs).toEqual([{ name: 'ownersName', path: 'owners[*].name' }]);
  });

  it('reports the problems saving would report', () => {
    const op = {
      ...emptyOperation(null),
      pathTemplate: '/cr/{crNumber}',
      parameters: [
        { name: 'idNumber', location: 'PATH' as const, isRequired: true, isMasked: false },
        { name: 'x', location: 'BODY' as const, target: 'a..b', isRequired: true, isMasked: false },
      ],
      outputs: [{ name: 'bad name', path: 'a..b' }],
    };
    const keys = checkOperation('bad code', op, true).map((p) => p.key);
    expect(keys).toEqual([
      'od.problem.code',
      'od.problem.names',
      'od.problem.connection',
      'od.problem.noPlaceholder',
      'od.problem.bodyOnGet',
      'od.problem.bodyPath',
      'od.problem.placeholder',
      'od.problem.outputName',
      'od.problem.outputPath',
    ]);
    expect(
      checkOperation('OK', { ...emptyOperation('GW'), nameAr: 'أ', nameEn: 'A' }, true),
    ).toEqual([]);
  });

  it('switches to SOAP as a POST with its call, and back to REST without it', () => {
    const rest = { ...emptyOperation('GW'), method: 'GET' };
    const soap = withProtocol(rest, 'SOAP');
    expect(soap.protocol).toBe('SOAP');
    expect(soap.method).toBe('POST');
    expect(soap.soap).toEqual({
      version: '1.1',
      action: null,
      operationName: '',
      namespace: null,
      qualifiedElements: true,
    });
    const back = withProtocol(soap, 'REST');
    expect(back.protocol).toBe('REST');
    expect(back.soap).toBeNull();
    expect(normalizeOperation({ protocol: 'SOAP', soap: { operationName: 'Get' } }).soap).toEqual({
      version: '1.1',
      action: null,
      operationName: 'Get',
      namespace: null,
      qualifiedElements: true,
    });
    expect(normalizeOperation({ method: 'GET' }).protocol).toBe('REST');
  });

  it('previews the SOAP envelope: inputs as elements at their paths under the operation element', () => {
    const op = {
      ...withProtocol(emptyOperation('GW'), 'SOAP'),
      parameters: [
        {
          name: 'nin',
          location: 'BODY' as const,
          target: 'request.nin',
          isRequired: true,
          isMasked: true,
        },
        {
          name: 'dob',
          location: 'BODY' as const,
          target: 'request.dateOfBirth',
          isRequired: true,
          isMasked: false,
          sampleValue: '1410-01-15',
        },
        { name: 'code', location: 'BODY' as const, isRequired: false, isMasked: false },
      ],
    };
    op.soap = {
      ...op.soap!,
      operationName: 'GetPersonInfo',
      namespace: 'http://yakeen.test/person',
    };
    expect(previewEnvelope(op, { nin: '1000000001' })).toBe(
      [
        '<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">',
        '  <soapenv:Body>',
        '    <ns:GetPersonInfo xmlns:ns="http://yakeen.test/person">',
        '      <ns:request>',
        '        <ns:nin>1000000001</ns:nin>',
        '        <ns:dateOfBirth>1410-01-15</ns:dateOfBirth>',
        '      </ns:request>',
        '      <ns:code>&lt;code&gt;</ns:code>',
        '    </ns:GetPersonInfo>',
        '  </soapenv:Body>',
        '</soapenv:Envelope>',
      ].join('\n'),
    );
    op.soap = { ...op.soap, version: '1.2', qualifiedElements: false };
    const unqualified = previewEnvelope(op, {});
    expect(unqualified).toContain('http://www.w3.org/2003/05/soap-envelope');
    expect(unqualified).toContain('<request>');
  });

  it('reads a pasted SOAP response as the platform does', () => {
    const json = soapResponseToJson(`
      <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
        <s:Body><r:Resp xmlns:r="urn:r">
          <r:item id="1">a</r:item><r:item id="2">b</r:item><r:empty/><r:none xsi:nil="true"/><r:n>7</r:n>
        </r:Resp></s:Body>
      </s:Envelope>`);
    expect(json).toEqual({
      item: [
        { id: '1', $text: 'a' },
        { id: '2', $text: 'b' },
      ],
      empty: null,
      none: null,
      n: '7',
    });
    expect(soapResponseToJson('<x>not soap</x>')).toBeNull();
    expect(soapResponseToJson('<broken')).toBeNull();
  });

  it('checks the SOAP call and the element names of its inputs', () => {
    const op = {
      ...withProtocol(
        { ...emptyOperation('GW'), nameAr: 'أ', nameEn: 'A', pathTemplate: '' },
        'SOAP',
      ),
      parameters: [
        { name: 'x', location: 'BODY' as const, target: 'a.$b', isRequired: true, isMasked: false },
      ],
    };
    op.soap = { ...op.soap!, namespace: 'not a uri' };
    expect(checkOperation('OK', op, true).map((p) => p.key)).toEqual([
      'od.problem.soapOperation',
      'od.problem.soapNamespace',
      'od.problem.soapElement',
    ]);
    op.soap = { ...op.soap, operationName: 'GetPersonInfo', namespace: 'urn:yakeen' };
    op.parameters = [{ ...op.parameters[0], target: 'request.nin' }];
    expect(checkOperation('OK', op, true)).toEqual([]);
  });
});
