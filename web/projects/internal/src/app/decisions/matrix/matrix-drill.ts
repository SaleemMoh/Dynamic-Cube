import {
  Matrix,
  MatrixBand,
  MatrixCell,
  MatrixCriterion,
  MatrixGroup,
  cellOf,
  detachCell,
  freeCode,
  newCell,
  newCriterion,
  newGroup,
  placed,
  resolveCell,
  sharedOf,
  sharedUsers,
  withCell,
  withShared,
} from './matrix-model';

/** How a sector's track is calculated, seen from the sector page. */
export type TrackMode = 'own' | 'shared' | 'same' | 'empty';

export interface TrackView {
  /** The calculation the sector uses (its own, the shared one or the one it copies); null when there is none. */
  calculation: MatrixCell | null;
  mode: TrackMode;
  /** For a shared track: the shared calculation's code and the other sectors linked to it. */
  sharedCode?: string;
  sharedWith: string[];
  /** For a "same as" track: the sector it copies. */
  sameAs?: string;
}

/** What the sector page shows for one sector and track. */
export function trackOf(m: Matrix, row: string, column: string): TrackView {
  const cell = cellOf(m, row, column);
  if (!cell || cell.kind === 'empty') return { calculation: null, mode: 'empty', sharedWith: [] };
  if (cell.kind === 'shared') {
    const code = cell.shared ?? '';
    return {
      calculation: sharedOf(m, code)?.cell ?? null,
      mode: 'shared',
      sharedCode: code,
      sharedWith: sharedUsers(m, code)
        .map((c) => c.row)
        .filter((r) => r.toLowerCase() !== row.toLowerCase()),
    };
  }
  if (cell.kind === 'same')
    return {
      calculation: resolveCell(m, cell),
      mode: 'same',
      sameAs: cell.sameRow ?? undefined,
      sharedWith: [],
    };
  return { calculation: cell, mode: 'own', sharedWith: [] };
}

/**
 * Changes a sector's track calculation where it lives: the shared calculation (every linked sector follows) or the
 * sector's own cell. A "same as" or empty track is not changed (the page offers its own copy or a new calculation first).
 */
export function editTrack(
  m: Matrix,
  row: string,
  column: string,
  change: (calculation: MatrixCell) => MatrixCell,
): Matrix {
  const view = trackOf(m, row, column);
  if (!view.calculation) return m;
  if (view.mode === 'shared') return withShared(m, view.sharedCode!, change(view.calculation));
  if (view.mode === 'own') return withCell(m, { ...change(view.calculation), row, column });
  return m;
}

/** Gives the sector its own copy of a shared or "same as" calculation, so it can differ from the others. */
export function ownTrack(m: Matrix, row: string, column: string): Matrix {
  const view = trackOf(m, row, column);
  if (view.mode === 'shared') return detachCell(m, row, column);
  if (view.mode === 'same' && view.calculation)
    return withCell(m, placed(view.calculation, row, column));
  return m;
}

/** Starts a calculation of a kind for the sector's track (points: one group, ready for criteria). */
export function startTrack(
  m: Matrix,
  row: string,
  column: string,
  kind: 'points' | 'worst' | 'formula' | 'empty',
): Matrix {
  const view = trackOf(m, row, column);
  if (view.mode === 'own' && view.calculation)
    return withCell(m, newCell(row, column, kind, view.calculation));
  return withCell(m, newCell(row, column, kind));
}

// Groups ---------------------------------------------------------------------------------------------------------------

export function addGroup(c: MatrixCell): MatrixCell {
  const code = freeCode(
    c.groups.map((g) => g.code),
    'group',
  );
  return { ...c, groups: [...c.groups, newGroup(code)] };
}

export function setGroup(c: MatrixCell, gi: number, change: Partial<MatrixGroup>): MatrixCell {
  return { ...c, groups: c.groups.map((g, i) => (i === gi ? { ...g, ...change } : g)) };
}

export function removeGroup(c: MatrixCell, gi: number): MatrixCell {
  return { ...c, groups: c.groups.filter((_, i) => i !== gi) };
}

export function moveGroup(c: MatrixCell, gi: number, by: -1 | 1): MatrixCell {
  return { ...c, groups: move(c.groups, gi, by) };
}

// Criteria -------------------------------------------------------------------------------------------------------------

export function addCriterion(c: MatrixCell, gi: number, input: string): MatrixCell {
  const all = c.groups.flatMap((g) => g.criteria.map((x) => x.code));
  const output = c.kind === 'worst' ? 'grade' : 'points';
  return setGroup(c, gi, {
    criteria: [...c.groups[gi].criteria, newCriterion(freeCode(all, 'criterion'), input, output)],
  });
}

export function setCriterion(
  c: MatrixCell,
  gi: number,
  ci: number,
  change: Partial<MatrixCriterion>,
): MatrixCell {
  return setGroup(c, gi, {
    criteria: c.groups[gi].criteria.map((x, i) => (i === ci ? { ...x, ...change } : x)),
  });
}

export function removeCriterion(c: MatrixCell, gi: number, ci: number): MatrixCell {
  return setGroup(c, gi, { criteria: c.groups[gi].criteria.filter((_, i) => i !== ci) });
}

export function moveCriterion(c: MatrixCell, gi: number, ci: number, by: -1 | 1): MatrixCell {
  return setGroup(c, gi, { criteria: move(c.groups[gi].criteria, ci, by) });
}

// Bands and answers ----------------------------------------------------------------------------------------------------

export function addBand(c: MatrixCell, gi: number, ci: number, size?: string | null): MatrixCell {
  const criterion = c.groups[gi].criteria[ci];
  const band: MatrixBand = {
    size: size ?? null,
    min: null,
    minInclusive: true,
    max: null,
    maxInclusive: false,
    points: null,
    grade: null,
  };
  return setCriterion(c, gi, ci, { bands: [...criterion.bands, band] });
}

export function setBand(
  c: MatrixCell,
  gi: number,
  ci: number,
  bi: number,
  change: Partial<MatrixBand>,
): MatrixCell {
  const criterion = c.groups[gi].criteria[ci];
  return setCriterion(c, gi, ci, {
    bands: criterion.bands.map((b, i) => (i === bi ? { ...b, ...change } : b)),
  });
}

export function removeBand(c: MatrixCell, gi: number, ci: number, bi: number): MatrixCell {
  const criterion = c.groups[gi].criteria[ci];
  return setCriterion(c, gi, ci, { bands: criterion.bands.filter((_, i) => i !== bi) });
}

/** Copies the bands of one size to every other size (replacing theirs), to start from the same ranges. */
export function copyBandsToSizes(
  c: MatrixCell,
  gi: number,
  ci: number,
  from: string,
  sizes: string[],
): MatrixCell {
  const criterion = c.groups[gi].criteria[ci];
  const source = criterion.bands.filter((b) => (b.size ?? '') === from);
  const bands = [
    ...criterion.bands.filter((b) => (b.size ?? '') === from || !sizes.includes(b.size ?? '')),
    ...sizes.filter((s) => s !== from).flatMap((s) => source.map((b) => ({ ...b, size: s }))),
  ];
  return setCriterion(c, gi, ci, { bands });
}

/** Sets the points or grade of one answer of a criterion scored by answers (adding the answer when missing). */
export function setAnswer(
  c: MatrixCell,
  gi: number,
  ci: number,
  value: string,
  change: { points?: number | null; grade?: number | null },
): MatrixCell {
  const criterion = c.groups[gi].criteria[ci];
  const existing = criterion.options.find((o) => o.value === value);
  const options = existing
    ? criterion.options.map((o) => (o.value === value ? { ...o, ...change } : o))
    : [...criterion.options, { value, ...change }];
  return setCriterion(c, gi, ci, { options });
}

function move<T>(list: T[], index: number, by: -1 | 1): T[] {
  const to = index + by;
  if (to < 0 || to >= list.length) return list;
  const copy = [...list];
  [copy[index], copy[to]] = [copy[to], copy[index]];
  return copy;
}
