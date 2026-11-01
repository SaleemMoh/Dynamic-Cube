import {
  addOutput,
  checkOperation,
  emptyOperation,
  outputName,
  placeholders,
  previewBody,
  previewPath,
  responseTree,
  syncPathParameters,
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
});
