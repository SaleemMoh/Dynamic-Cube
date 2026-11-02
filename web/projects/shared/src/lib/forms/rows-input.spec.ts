import { describe, expect, it } from 'vitest';
import { FormField } from './form-schema';
import { rowFiles, rowForForm, rowForStorage, tableColumns } from './rows-input';
import { stepSchema } from '../services/service-form';
import { ResolvedVersion } from '../services/service.models';

const text = (code: string): FormField => ({ code, type: 'text', label: { ar: code, en: code } });

describe('rows input', () => {
  const columns: FormField[] = [
    text('NAME'),
    { ...text('START'), type: 'date' },
    { ...text('LETTER'), type: 'file', multiple: true },
    text('NOTE'),
    text('EXTRA'),
  ];

  it('keeps dates as yyyy-MM-dd and leaves empty values out, and gives dates back to the form', () => {
    const stored = rowForStorage({
      NAME: 'School',
      START: new Date(2025, 0, 9),
      NOTE: '',
      LETTER: [],
    });
    expect(stored).toEqual({ NAME: 'School', START: '2025-01-09' });
    const back = rowForForm(columns, stored);
    expect(back['START']).toBeInstanceOf(Date);
    expect((back['START'] as Date).getDate()).toBe(9);
  });

  it('lists the files of a row by column', () => {
    expect(rowFiles(columns, { NAME: 'x', LETTER: ['a', 'b'] })).toEqual([
      { column: 'LETTER', id: 'a' },
      { column: 'LETTER', id: 'b' },
    ]);
    expect(rowFiles(columns, null)).toEqual([]);
  });

  it('shows the chosen columns in the table, else the first four', () => {
    expect(
      tableColumns({ ...text('P'), type: 'rows', rows: { columns } }).map((c) => c.code),
    ).toEqual(['NAME', 'START', 'LETTER', 'NOTE']);
    expect(
      tableColumns({
        ...text('P'),
        type: 'rows',
        rows: { columns, tableColumns: ['NOTE', 'NAME'] },
      }).map((c) => c.code),
    ).toEqual(['NAME', 'NOTE']);
  });

  it('turns a rows question into a rows field with its columns, rules and limits', () => {
    const version = {
      serviceCode: 'S',
      number: 1,
      questions: {
        PROJECTS: {
          code: 'PROJECTS',
          labelAr: 'المشاريع',
          labelEn: 'Projects',
          hintAr: null,
          hintEn: null,
          type: 'ROWS',
          lov: null,
          lovParentQuestion: null,
          options: [],
          multiple: false,
          validation: {
            min: null,
            max: null,
            maxLength: null,
            pattern: null,
            minRows: 1,
            maxRows: 5,
          },
          prefill: null,
          columns: [
            {
              code: 'NAME',
              labelAr: 'الاسم',
              labelEn: 'Name',
              type: 'TEXT',
              required: true,
              options: [],
              multiple: false,
              validation: { min: null, max: null, maxLength: 50, pattern: null },
              inTable: true,
            },
            {
              code: 'LETTER',
              labelAr: 'الخطاب',
              labelEn: 'Letter',
              type: 'FILE',
              required: false,
              requiredWhen: { '==': [{ var: 'OWNER' }, 'GOV'] },
              options: [],
              multiple: false,
              validation: { min: null, max: null, maxLength: null, pattern: null },
            },
          ],
        },
      },
      definition: {
        steps: [
          {
            code: 'P',
            type: 'FORM',
            titleAr: 'م',
            titleEn: 'P',
            sections: [
              {
                code: 'S',
                titleAr: 'م',
                titleEn: 'S',
                questions: [{ code: 'PROJECTS', required: true, readonly: false }],
              },
            ],
          },
        ],
      },
    } as unknown as ResolvedVersion;
    const field = stepSchema(version, version.definition.steps[0]).sections[0].fields[0];
    expect(field.type).toBe('rows');
    expect(field.rows?.minRows).toBe(1);
    expect(field.rows?.maxRows).toBe(5);
    expect(field.rows?.tableColumns).toEqual(['NAME']);
    expect(field.rows?.columns.map((c) => [c.code, c.type, c.required, c.maxLength])).toEqual([
      ['NAME', 'text', true, 50],
      ['LETTER', 'file', false, undefined],
    ]);
    expect(field.rows?.columns[1].requiredWhen).toEqual({ '==': [{ var: 'OWNER' }, 'GOV'] });
  });
});
