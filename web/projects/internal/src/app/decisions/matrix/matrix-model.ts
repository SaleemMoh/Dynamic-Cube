import { JsonLogicRule, LocalizedText, LogicField } from '@dynamic-cube/shared';

/** The scoring matrix configuration (engine MATRIX, ADR-0050), as the server reads it. */
export interface Matrix {
  grades: MatrixGrade[];
  inputs: MatrixInput[];
  optionSets: MatrixOptionSet[];
  sizes?: MatrixSizes | null;
  scales: MatrixScale[];
  rows: MatrixRow[];
  columns: MatrixColumn[];
  cells: MatrixCell[];
  gates: MatrixGate[];
  adjustments: MatrixAdjustment[];
  projects?: MatrixProjects | null;
  belowWorst?: 'stay' | 'notGraded';
}

export interface MatrixGrade {
  rank: number;
  code: string;
  nameAr: string;
  nameEn: string;
}

export type SourceKind =
  'applicant' | 'reviewer' | 'integration' | 'party' | 'derived' | 'projects' | 'given';
export const SOURCE_KINDS: SourceKind[] = [
  'applicant',
  'reviewer',
  'integration',
  'party',
  'derived',
  'projects',
  'given',
];
export const SOURCE_ICONS: Record<SourceKind, string> = {
  applicant: 'person_edit',
  reviewer: 'badge',
  integration: 'cable',
  party: 'domain',
  derived: 'function',
  projects: 'stacked_bar_chart',
  given: 'input',
};

export interface MatrixSource {
  kind: SourceKind;
  question?: string | null;
  step?: string | null;
  field?: string | null;
  operation?: string | null;
  output?: string | null;
  parameters?: Record<string, JsonLogicRule | null> | null;
  path?: string | null;
  rule?: JsonLogicRule | null;
  function?: 'sum' | 'max' | 'count' | 'distinct' | null;
  column?: string | null;
  where?: JsonLogicRule | null;
  allowOverride?: boolean;
}

export type InputType = 'number' | 'text' | 'option' | 'boolean' | 'date' | 'list';
export const INPUT_TYPES: InputType[] = ['number', 'text', 'option', 'boolean', 'date', 'list'];

export interface MatrixInput {
  code: string;
  nameAr: string;
  nameEn: string;
  type: InputType;
  unit?: string | null;
  optionSet?: string | null;
  min?: number | null;
  max?: number | null;
  source: MatrixSource;
}

export interface MatrixOptionSet {
  code: string;
  nameAr: string;
  nameEn: string;
  items: { code: string; labelAr: string; labelEn: string; ordinal: number }[];
}

export interface MatrixSizes {
  input: string;
  categories: {
    code: string;
    nameAr: string;
    nameEn: string;
    min?: number | null;
    max?: number | null;
  }[];
}

export interface MatrixScale {
  code: string;
  nameAr: string;
  nameEn: string;
  steps: { min?: number | null; max?: number | null; grade: number }[];
}

export interface MatrixRow {
  code: string;
  nameAr: string;
  nameEn: string;
  active: boolean;
  final: 'worst' | 'weighted' | 'formula';
  finalRule?: JsonLogicRule | null;
  notesAr?: string | null;
  notesEn?: string | null;
  settings?: Record<string, unknown> | null;
}

export interface MatrixColumn {
  code: string;
  nameAr: string;
  nameEn: string;
  includeInFinal: boolean;
  weight?: number | null;
}

export type CellKind = 'points' | 'worst' | 'formula' | 'same' | 'empty';
export const CELL_KINDS: CellKind[] = ['points', 'worst', 'formula', 'same', 'empty'];
export const CELL_ICONS: Record<CellKind, string> = {
  points: 'scoreboard',
  worst: 'vertical_align_bottom',
  formula: 'function',
  same: 'content_copy',
  empty: 'block',
};

export interface MatrixCell {
  row: string;
  column: string;
  kind: CellKind;
  scale?: string | null;
  groups: MatrixGroup[];
  sameRow?: string | null;
  sameColumn?: string | null;
  gradeRule?: JsonLogicRule | null;
  scoreRule?: JsonLogicRule | null;
  detailsRule?: JsonLogicRule | null;
  noteAr?: string | null;
  noteEn?: string | null;
}

export interface MatrixGroup {
  code: string;
  nameAr: string;
  nameEn: string;
  maxPoints?: number | null;
  mandatory: boolean;
  minPoints?: number;
  criteria: MatrixCriterion[];
}

export interface MatrixCriterion {
  code: string;
  nameAr: string;
  nameEn: string;
  input: string;
  scoring: 'bands' | 'options' | 'direct';
  output: 'points' | 'grade';
  useOrdinal?: boolean;
  maxPoints?: number | null;
  appliesWhen?: JsonLogicRule | null;
  bands: MatrixBand[];
  options: {
    value: string;
    points?: number | null;
    grade?: number | null;
    attachment?: string | null;
  }[];
  alternativeKey?: string | null;
  note?: string | null;
}

export interface MatrixBand {
  size?: string | null;
  min?: number | null;
  minInclusive: boolean;
  max?: number | null;
  maxInclusive: boolean;
  points?: number | null;
  grade?: number | null;
  label?: string | null;
}

export interface MatrixGate {
  code: string;
  rows: string[];
  rule?: JsonLogicRule | null;
  messageAr: string;
  messageEn: string;
  blocking: boolean;
}

export interface MatrixAdjustment {
  code: string;
  nameAr: string;
  nameEn: string;
  rows: string[];
  rule?: JsonLogicRule | null;
  demote: number;
}

export interface MatrixProjects {
  input: string;
  rules: {
    code: string;
    matches?: JsonLogicRule | null;
    minCompletion?: number | null;
    maxAgeYears?: number | null;
    minValue?: number | null;
    useCertifiedValue: boolean;
  }[];
  valueField: string;
  certifiedValueField?: string | null;
  completionField?: string | null;
  dateField?: string | null;
  fields: MatrixInput[];
  splitThreshold?: number | null;
}

/** A new, empty matrix: the grades 1–5, one row and the four usual columns, no inputs. */
export function emptyMatrix(): Matrix {
  return {
    grades: [1, 2, 3, 4, 5].map((rank) => ({
      rank,
      code: `G${rank}`,
      nameAr: `الدرجة ${rank}`,
      nameEn: `Grade ${rank}`,
    })),
    inputs: [],
    optionSets: [],
    sizes: null,
    scales: [],
    rows: [{ code: 'ROW1', nameAr: 'القطاع 1', nameEn: 'Sector 1', active: true, final: 'worst' }],
    columns: [
      { code: 'technical', nameAr: 'التقييم الفني', nameEn: 'Technical', includeInFinal: true },
      { code: 'performance', nameAr: 'الأداء', nameEn: 'Performance', includeInFinal: true },
      { code: 'governance', nameAr: 'الحوكمة', nameEn: 'Governance', includeInFinal: true },
      { code: 'financial', nameAr: 'التقييم المالي', nameEn: 'Financial', includeInFinal: true },
    ],
    cells: [],
    gates: [],
    adjustments: [],
    projects: null,
    belowWorst: 'stay',
  };
}

/** A matrix as read from a saved configuration: every list present, so the screens need no checks. */
export function readMatrix(value: unknown): Matrix {
  const m = (value ?? {}) as Partial<Matrix>;
  return {
    ...emptyMatrix(),
    ...m,
    grades: m.grades ?? emptyMatrix().grades,
    inputs: (m.inputs ?? []).map((i) => ({ ...i, source: i.source ?? { kind: 'applicant' } })),
    optionSets: m.optionSets ?? [],
    scales: m.scales ?? [],
    rows: m.rows ?? [],
    columns: m.columns ?? [],
    cells: (m.cells ?? []).map((c) => ({ ...c, groups: c.groups ?? [] })),
    gates: m.gates ?? [],
    adjustments: m.adjustments ?? [],
  };
}

export function cellOf(m: Matrix, row: string, column: string): MatrixCell | null {
  return m.cells.find((c) => same(c.row, row) && same(c.column, column)) ?? null;
}

/** The cell a "same as" cell copies, followed up to five steps (null when it leads nowhere). */
export function resolveCell(m: Matrix, cell: MatrixCell | null, depth = 0): MatrixCell | null {
  if (!cell || cell.kind !== 'same') return cell;
  if (depth >= 5 || !cell.sameRow) return null;
  return resolveCell(m, cellOf(m, cell.sameRow, cell.sameColumn ?? cell.column), depth + 1);
}

function same(a: string, b: string): boolean {
  return a.toLowerCase() === b.toLowerCase();
}

export function criteriaOf(cell: MatrixCell | null): MatrixCriterion[] {
  return cell?.groups.flatMap((g) => g.criteria) ?? [];
}

/** The inputs a cell reads: its criteria's inputs and those named in its rules. */
export function cellInputs(m: Matrix, cell: MatrixCell | null): MatrixInput[] {
  const target = resolveCell(m, cell);
  if (!target) return [];
  const codes = new Set(criteriaOf(target).map((c) => c.input));
  for (const rule of [
    target.gradeRule,
    target.scoreRule,
    target.detailsRule,
    ...criteriaOf(target).map((c) => c.appliesWhen),
  ]) {
    for (const code of varsOf(rule)) codes.add(code);
  }
  return m.inputs.filter((i) => codes.has(i.code));
}

/** How many of a cell's inputs come from each source, for the grid's source icons. */
export function sourceCounts(inputs: MatrixInput[]): { kind: SourceKind; count: number }[] {
  return SOURCE_KINDS.map((kind) => ({
    kind,
    count: inputs.filter((i) => i.source.kind === kind).length,
  })).filter((s) => s.count > 0);
}

/** Variable names a JSONLogic rule reads. */
export function varsOf(rule: unknown, into = new Set<string>()): Set<string> {
  if (Array.isArray(rule)) {
    rule.forEach((r) => varsOf(r, into));
  } else if (rule && typeof rule === 'object') {
    for (const [key, value] of Object.entries(rule)) {
      if (key === 'var') {
        const name = Array.isArray(value) ? value[0] : value;
        if (typeof name === 'string' && name) into.add(name.split('.')[0]);
      } else {
        varsOf(value, into);
      }
    }
  }
  return into;
}

/** A short line for a cell in the grid, e.g. "8 criteria · technical_standard". */
export function cellSummary(
  m: Matrix,
  cell: MatrixCell | null,
  t: (key: string) => string,
): string {
  if (!cell || cell.kind === 'empty') return t('mx.cell.none');
  if (cell.kind === 'same') {
    const row = m.rows.find((r) => same(r.code, cell.sameRow ?? ''));
    return `${t('mx.cell.sameAs')} ${row ? row.nameAr || row.code : (cell.sameRow ?? '?')}`;
  }
  if (cell.kind === 'formula') return t('mx.kind.formula');
  const count = criteriaOf(cell).length;
  const scale =
    cell.kind === 'points'
      ? (m.scales.find((s) => s.code === cell.scale)?.nameAr ?? cell.scale ?? '?')
      : t('mx.kind.worst');
  return `${count} ${t('mx.cell.criteria')} · ${scale}`;
}

/** The inputs as Logic Builder fields (by code, with their option set's answers as choices). */
export function inputFields(m: Matrix, exclude?: string): LogicField[] {
  const group: LocalizedText = { ar: 'مدخلات المصفوفة', en: 'Matrix inputs' };
  return m.inputs
    .filter((i) => i.code !== exclude)
    .map((i) => {
      const set = i.optionSet ? m.optionSets.find((s) => s.code === i.optionSet) : undefined;
      return {
        path: i.code,
        label: { ar: i.nameAr || i.code, en: i.nameEn || i.code },
        type: i.type === 'option' ? 'text' : i.type === 'list' ? 'list' : i.type,
        options: set?.items.map((o) => ({
          value: o.code,
          label: { ar: o.labelAr, en: o.labelEn },
        })),
        group,
      } satisfies LogicField;
    });
}

/** What a sector's final-grade formula reads: the inputs and each track's grade and points (`columns.<track>.grade`). */
export function trackFields(m: Matrix): LogicField[] {
  const group: LocalizedText = { ar: 'نتائج المسارات', en: 'Track results' };
  return [
    ...inputFields(m),
    ...m.columns.flatMap((c) => [
      {
        path: `columns.${c.code}.grade`,
        label: { ar: `درجة ${c.nameAr || c.code}`, en: `${c.nameEn || c.code} grade` },
        type: 'number' as const,
        group,
      },
      {
        path: `columns.${c.code}.points`,
        label: { ar: `نقاط ${c.nameAr || c.code}`, en: `${c.nameEn || c.code} points` },
        type: 'number' as const,
        group,
      },
    ]),
  ];
}

/** The project fields as Logic Builder fields (for "which projects" conditions). */
export function projectFields(m: Matrix): LogicField[] {
  const group: LocalizedText = { ar: 'حقول المشروع', en: 'Project fields' };
  return (m.projects?.fields ?? []).map((f) => ({
    path: f.code,
    label: { ar: f.nameAr || f.code, en: f.nameEn || f.code },
    type: f.type === 'option' ? 'text' : f.type === 'list' ? 'list' : f.type,
    group,
  }));
}

/** A code not used yet, e.g. ROW3. */
export function freeCode(used: string[], prefix: string): string {
  let n = used.length + 1;
  while (used.some((u) => u.toLowerCase() === `${prefix}${n}`.toLowerCase())) n++;
  return `${prefix}${n}`;
}

/** Puts a cell in place of the one at its row and column (or adds it). */
export function withCell(m: Matrix, cell: MatrixCell): Matrix {
  const rest = m.cells.filter((c) => !(same(c.row, cell.row) && same(c.column, cell.column)));
  return { ...m, cells: cell.kind === 'empty' ? rest : [...rest, cell] };
}

/** A new cell of a kind, keeping what still fits from the old one. */
export function newCell(
  row: string,
  column: string,
  kind: CellKind,
  old?: MatrixCell | null,
): MatrixCell {
  return {
    row,
    column,
    kind,
    scale: old?.scale ?? null,
    groups: old?.groups ?? (kind === 'points' || kind === 'worst' ? [newGroup('main')] : []),
    sameRow: old?.sameRow ?? null,
    sameColumn: old?.sameColumn ?? null,
    gradeRule: old?.gradeRule ?? null,
    scoreRule: old?.scoreRule ?? null,
    detailsRule: old?.detailsRule ?? null,
  };
}

export function newGroup(code: string): MatrixGroup {
  return {
    code,
    nameAr: 'المعايير',
    nameEn: 'Criteria',
    maxPoints: null,
    mandatory: false,
    minPoints: 0,
    criteria: [],
  };
}

export function newCriterion(
  code: string,
  input: string,
  output: 'points' | 'grade',
): MatrixCriterion {
  return {
    code,
    nameAr: '',
    nameEn: '',
    input,
    scoring: 'bands',
    output,
    maxPoints: null,
    appliesWhen: null,
    bands: [],
    options: [],
  };
}

/** Renames a row or column code everywhere it is used. */
export function renameRow(m: Matrix, from: string, to: string): Matrix {
  return {
    ...m,
    rows: m.rows.map((r) => (r.code === from ? { ...r, code: to } : r)),
    cells: m.cells.map((c) => ({
      ...c,
      row: c.row === from ? to : c.row,
      sameRow: c.sameRow === from ? to : c.sameRow,
    })),
    gates: m.gates.map((g) => ({ ...g, rows: g.rows.map((r) => (r === from ? to : r)) })),
    adjustments: m.adjustments.map((a) => ({
      ...a,
      rows: a.rows.map((r) => (r === from ? to : r)),
    })),
  };
}

export function renameColumn(m: Matrix, from: string, to: string): Matrix {
  return {
    ...m,
    columns: m.columns.map((c) => (c.code === from ? { ...c, code: to } : c)),
    cells: m.cells.map((c) => ({
      ...c,
      column: c.column === from ? to : c.column,
      sameColumn: c.sameColumn === from ? to : c.sameColumn,
    })),
  };
}

export function removeRow(m: Matrix, code: string): Matrix {
  return {
    ...m,
    rows: m.rows.filter((r) => r.code !== code),
    cells: m.cells
      .filter((c) => c.row !== code)
      .map((c) => (c.sameRow === code ? { ...c, kind: 'empty' as const, sameRow: null } : c)),
    gates: m.gates.map((g) => ({ ...g, rows: g.rows.filter((r) => r !== code) })),
    adjustments: m.adjustments.map((a) => ({ ...a, rows: a.rows.filter((r) => r !== code) })),
  };
}

export function removeColumn(m: Matrix, code: string): Matrix {
  return {
    ...m,
    columns: m.columns.filter((c) => c.code !== code),
    cells: m.cells.filter((c) => c.column !== code),
  };
}
