import { JsonLogicRule, LocalizedText, LogicField } from '@dynamic-cube/shared';

/** The scoring matrix configuration (engine MATRIX, ADR-0050), as the server reads it. */
export interface Matrix {
  grades: MatrixGrade[];
  inputs: MatrixInput[];
  optionSets: MatrixOptionSet[];
  sizes?: MatrixSizes | null;
  scales: MatrixScale[];
  rows: MatrixRow[];
  /** The input whose answer lists the rows applied for (each answer selects the row whose `answer`, or code, it equals). */
  rowsInput?: string | null;
  columns: MatrixColumn[];
  cells: MatrixCell[];
  /** Calculations several cells use (a cell of kind `shared` names one); changing one changes every linked cell. */
  shared?: MatrixShared[];
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
  /** The answer of the rows input that selects this row (its code when empty). */
  answer?: string | null;
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

export type CellKind = 'points' | 'worst' | 'formula' | 'same' | 'shared' | 'empty';
/** The kinds a cell editor offers (a shared link is made from the grid). */
export const CELL_KINDS: CellKind[] = ['points', 'worst', 'formula', 'same', 'empty'];
export const CELL_ICONS: Record<CellKind, string> = {
  points: 'scoreboard',
  worst: 'vertical_align_bottom',
  formula: 'function',
  same: 'content_copy',
  shared: 'link',
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
  /** For `shared`: the code of the shared calculation. */
  shared?: string | null;
  gradeRule?: JsonLogicRule | null;
  scoreRule?: JsonLogicRule | null;
  detailsRule?: JsonLogicRule | null;
  noteAr?: string | null;
  noteEn?: string | null;
}

/** A named calculation several cells use; its cell holds the calculation (row and column unused). */
export interface MatrixShared {
  code: string;
  nameAr: string;
  nameEn: string;
  cell: MatrixCell;
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
  /** For `options` scoring: the points or grades follow the weights the question bank gives each answer. */
  fromBank?: boolean;
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
    shared: [],
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
    shared: (m.shared ?? []).map((x) => ({
      ...x,
      cell: { ...x.cell, groups: x.cell?.groups ?? [] },
    })),
    gates: m.gates ?? [],
    adjustments: m.adjustments ?? [],
  };
}

export function cellOf(m: Matrix, row: string, column: string): MatrixCell | null {
  return m.cells.find((c) => same(c.row, row) && same(c.column, column)) ?? null;
}

/** The cell a "same as" cell copies, followed up to five steps (null when it leads nowhere). */
export function resolveCell(m: Matrix, cell: MatrixCell | null, depth = 0): MatrixCell | null {
  if (cell?.kind === 'shared') return sharedOf(m, cell.shared)?.cell ?? null;
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
  if (cell.kind === 'shared') {
    const shared = sharedOf(m, cell.shared);
    if (!shared) return `${t('mx.cell.sharedMissing')} ${cell.shared ?? ''}`;
    const inner = cellSummary(m, shared.cell, t);
    return `${shared.nameAr || shared.code} · ${inner}`;
  }
  if (cell.kind === 'formula') return t('mx.kind.formula');
  const count = criteriaOf(cell).length;
  const scale =
    cell.kind === 'points'
      ? (m.scales.find((s) => s.code === cell.scale)?.nameAr ?? cell.scale ?? '?')
      : t('mx.kind.worst');
  return `${count} ${t('mx.cell.criteria')} · ${scale}`;
}

/** The bank question an input's value is the answer of: an applicant input's question, or the answer a form integration step fills. */
export function bankQuestionCode(i: MatrixInput): string | null {
  if (i.source.kind === 'applicant') return i.source.question || i.code;
  if (i.source.kind === 'integration') return i.source.question || null;
  return null;
}

/** An input's answers as choices: its bank question's answers when it has them, else its option set's. */
export type AnswersOf = (i: MatrixInput) =>
  | {
      value: string;
      labelAr: string;
      labelEn: string;
      points?: number | null;
      grade?: number | null;
    }[]
  | null
  | undefined;

/** Whether bank answers carry weights (points or grades). */
export function hasWeights(
  answers: { points?: number | null; grade?: number | null }[] | null | undefined,
): boolean {
  return !!answers?.some(
    (a) =>
      (a.points !== null && a.points !== undefined) || (a.grade !== null && a.grade !== undefined),
  );
}

/** A criterion's answers scored with the bank's weights (keeping each answer's attachment). */
export function bankOptions(
  c: MatrixCriterion,
  answers: { value: string; points?: number | null; grade?: number | null }[],
): MatrixCriterion['options'] {
  return answers.map((a) => ({
    value: a.value,
    points: a.points ?? null,
    grade: a.grade ?? null,
    attachment:
      c.options.find((o) => o.value.toLowerCase() === a.value.toLowerCase())?.attachment ?? null,
  }));
}

/** Every criterion that follows the bank, with the weights the bank gives now (what a saved version keeps). */
export function syncFromBank(m: Matrix, answersOf: AnswersOf): Matrix {
  const byCode = new Map(m.inputs.map((i) => [i.code, i]));
  const sync = (cell: MatrixCell): MatrixCell => ({
    ...cell,
    groups: cell.groups.map((g) => ({
      ...g,
      criteria: g.criteria.map((c) => {
        const input = byCode.get(c.input);
        const answers = c.fromBank && c.scoring === 'options' && input ? answersOf(input) : null;
        return answers && hasWeights(answers) ? { ...c, options: bankOptions(c, answers) } : c;
      }),
    })),
  });
  return {
    ...m,
    cells: m.cells.map(sync),
    shared: (m.shared ?? []).map((s) => ({ ...s, cell: sync(s.cell) })),
  };
}

/** Whether a criterion that follows the bank holds weights other than the bank's (they change on the next save). */
export function bankWeightsChanged(
  c: MatrixCriterion,
  answers: { value: string; points?: number | null; grade?: number | null }[] | null | undefined,
): boolean {
  if (!c.fromBank || !answers || !hasWeights(answers)) return false;
  return (
    JSON.stringify(bankOptions(c, answers).map((o) => [o.value, o.points, o.grade])) !==
    JSON.stringify(c.options.map((o) => [o.value, o.points ?? null, o.grade ?? null]))
  );
}

/** The inputs as Logic Builder fields (by code, with their answers as choices: the bank question's, else the option set's). */
export function inputFields(m: Matrix, exclude?: string, answersOf?: AnswersOf): LogicField[] {
  const group: LocalizedText = { ar: 'مدخلات المصفوفة', en: 'Matrix inputs' };
  return m.inputs
    .filter((i) => i.code !== exclude)
    .map((i) => {
      const set = i.optionSet ? m.optionSets.find((s) => s.code === i.optionSet) : undefined;
      const bank = answersOf?.(i);
      return {
        path: i.code,
        label: { ar: i.nameAr || i.code, en: i.nameEn || i.code },
        type: i.type === 'option' ? 'text' : i.type === 'list' ? 'list' : i.type,
        options: bank?.length
          ? bank.map((o) => ({ value: o.value, label: { ar: o.labelAr, en: o.labelEn } }))
          : set?.items.map((o) => ({ value: o.code, label: { ar: o.labelAr, en: o.labelEn } })),
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

/** A shared calculation by code (ignoring case). */
export function sharedOf(m: Matrix, code: string | null | undefined): MatrixShared | undefined {
  return code ? (m.shared ?? []).find((x) => same(x.code, code)) : undefined;
}

/** The cells linked to a shared calculation. */
export function sharedUsers(m: Matrix, code: string): MatrixCell[] {
  return m.cells.filter((c) => c.kind === 'shared' && same(c.shared ?? '', code));
}

/** A cell's own calculation (what a copy or a new shared calculation starts from): a link gives what it links to. */
export function ownCalculation(m: Matrix, cell: MatrixCell | null): MatrixCell | null {
  const target = resolveCell(m, cell);
  return target && target.kind !== 'empty' ? target : null;
}

/** A copy of a calculation placed at a row and column (nothing shared with the original). */
export function placed(calculation: MatrixCell, row: string, column: string): MatrixCell {
  return {
    ...structuredClone(calculation),
    row,
    column,
    sameRow: null,
    sameColumn: null,
    shared: null,
  };
}

/** Pastes a copy of `source` into a cell: an independent copy, or a link to the same shared calculation. */
export function pasteCell(
  m: Matrix,
  source: MatrixCell,
  row: string,
  column: string,
  asLink: boolean,
): Matrix {
  if (!asLink) {
    const own = ownCalculation(m, source);
    return own ? withCell(m, placed(own, row, column)) : m;
  }
  let next = m;
  let code = source.kind === 'shared' ? source.shared : null;
  if (!code) {
    // Linking to a cell that is not shared yet shares it first.
    code = freeCode(
      (m.shared ?? []).map((x) => x.code),
      `${source.column}-shared-`,
    );
    next = shareCells(m, source.row, source.column, code, [source.row]);
  }
  return withCell(next, { row, column, kind: 'shared', shared: code, groups: [] });
}

/**
 * Turns a cell's calculation into a shared calculation and links the cell and the chosen rows' cells (same column) to
 * it; the shared calculation is named after the column.
 */
export function shareCells(
  m: Matrix,
  row: string,
  column: string,
  code: string,
  rows: string[],
): Matrix {
  const own = ownCalculation(m, cellOf(m, row, column));
  if (!own) return m;
  const col = m.columns.find((c) => same(c.code, column));
  const shared: MatrixShared = {
    code,
    nameAr: `${col?.nameAr || column} (مشترك)`,
    nameEn: `${col?.nameEn || column} (shared)`,
    cell: placed(own, '', ''),
  };
  let next: Matrix = { ...m, shared: [...(m.shared ?? []), shared] };
  for (const r of new Set([row, ...rows]))
    next = withCell(next, { row: r, column, kind: 'shared', shared: code, groups: [] });
  return next;
}

/** Gives a linked cell its own copy of the shared calculation, so it can differ from the others. */
export function detachCell(m: Matrix, row: string, column: string): Matrix {
  const cell = cellOf(m, row, column);
  const own = cell?.kind === 'shared' ? sharedOf(m, cell.shared)?.cell : null;
  return own ? withCell(m, placed(own, row, column)) : m;
}

/** Changes a shared calculation (every linked cell follows). */
export function withShared(m: Matrix, code: string, cell: MatrixCell): Matrix {
  return {
    ...m,
    shared: (m.shared ?? []).map((x) =>
      same(x.code, code) ? { ...x, cell: placed(cell, '', '') } : x,
    ),
  };
}

/** What makes two calculations the same (not where they sit, nor their notes). */
function fingerprint(c: MatrixCell): string {
  return JSON.stringify({
    kind: c.kind,
    scale: c.scale ?? null,
    groups: c.groups,
    gradeRule: c.gradeRule ?? null,
    scoreRule: c.scoreRule ?? null,
    detailsRule: c.detailsRule ?? null,
  });
}

/** Sets of rows whose cells in a column hold the very same calculation (not yet linked). */
export function identicalCells(m: Matrix, column: string): string[][] {
  const groups = new Map<string, string[]>();
  for (const cell of m.cells.filter(
    (c) => same(c.column, column) && ['points', 'worst', 'formula'].includes(c.kind),
  )) {
    const key = fingerprint(cell);
    groups.set(key, [...(groups.get(key) ?? []), cell.row]);
  }
  return [...groups.values()].filter((rows) => rows.length > 1);
}

/** Links every set of identical cells in a column to one shared calculation each. */
export function shareIdentical(m: Matrix, column: string): Matrix {
  let next = m;
  for (const rows of identicalCells(m, column)) {
    const code = freeCode(
      (next.shared ?? []).map((x) => x.code),
      `${column}-shared-`,
    );
    next = shareCells(next, rows[0], column, code, rows);
  }
  return next;
}

/** Removes shared calculations no cell links to any more. */
export function dropUnusedShared(m: Matrix): Matrix {
  return { ...m, shared: (m.shared ?? []).filter((x) => sharedUsers(m, x.code).length > 0) };
}
