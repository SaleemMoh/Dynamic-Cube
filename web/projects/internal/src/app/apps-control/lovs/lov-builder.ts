import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  AppsControlApi,
  BusinessAppContext,
  ConfigRow,
  I18nService,
  LocalizePipe,
  TranslatePipe,
  problemMessages,
  SelectSearch,
  matchesSearch,
} from '@dynamic-cube/shared';
import { catchError, firstValueFrom, of } from 'rxjs';
import { ConfigEditor, ConfigEditorData } from '../config/config-editor';
import { CONFIG_SECTIONS } from '../config/config-types';
import { payloadOf } from '../config/form-values';
import { DraftsService } from '../drafts.service';
import { LovBuilderPreview, LovPreview } from './lov-builder-preview';
import { LOV_BUILDER_TRANSLATIONS } from './lov-builder-translations';
import {
  BuilderQuery,
  ColumnCategory,
  NO_VALUE,
  OPERATORS,
  QueryCondition,
  QueryGroup,
  QuerySourceInfo,
  RULE_FACTS,
  RoleGrantRow,
  SourceKind,
  USER_FACTS,
  columnsOf,
  emptyGroup,
  emptyQuery,
  fromLegacy,
  nextAlias,
  registerTables,
  stepOf,
  toDefinition,
  withGrant,
} from './lov-builder-model';

type Preview = LovPreview;

/**
 * The entry of the lists of values screen: a visual query builder. Step by step the administrator chooses a source
 * (a class, a registered table, or another table of the modules, registered with the list), joins others, maps the value and its labels, filters, sorts and decides who sees
 * which values (conditions on the user's data, and each role's grant, saved on the roles). The preview runs the query
 * as it is built and shows the SQL it becomes. Lists written in SQL open in an SQL editor.
 */
@Component({
  selector: 'dc-lov-builder',
  imports: [
    NgTemplateOutlet,
    SelectSearch,
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTabsModule,
    MatTooltipModule,
    LocalizePipe,
    LovBuilderPreview,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './lov-builder.html',
  styleUrl: './lov-builder.scss',
})
export class LovBuilder {
  protected readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<LovBuilder, boolean>);
  private readonly api = inject(AppsControlApi);
  protected readonly drafts = inject(DraftsService);
  private readonly i18n = inject(I18nService);
  private readonly dialog = inject(MatDialog);
  private readonly appContext = inject(BusinessAppContext);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly row: ConfigRow | null = this.data.row;
  protected readonly isNew = !this.row;
  private readonly legacy = this.row ? fromLegacy(this.row) : emptyQuery();
  protected readonly hasStaticFilter =
    !!this.row?.['staticFilter'] && this.row?.['sourceType'] === 'CLASS';

  protected readonly mode = signal<'VISUAL' | 'SQL'>(this.legacy === null ? 'SQL' : 'VISUAL');
  protected readonly query = signal<BuilderQuery>(this.legacy ?? emptyQuery());
  protected readonly step = signal(0);

  protected readonly code = signal(this.row?.code ?? '');
  protected readonly nameAr = signal(this.row?.nameAr ?? '');
  protected readonly nameEn = signal(this.row?.nameEn ?? '');
  protected readonly isActive = signal(this.row?.isActive ?? true);
  protected readonly entityCode = signal<string | null>(
    (this.row?.['entityCode'] as string | null) ?? null,
  );
  protected readonly businessApps = signal<string[]>(
    (this.row?.['businessApps'] as string[] | undefined) ??
      (this.row ? [] : this.appContext.current() ? [this.appContext.current()!] : []),
  );
  protected readonly searchMode = signal<string>((this.row?.['searchMode'] as string) ?? 'FULL');
  protected readonly maxRows = signal<number>((this.row?.['maxRows'] as number) ?? 500);
  protected readonly selectionMode = signal<string>(
    (this.row?.['defaultSelectionMode'] as string) ?? 'SINGLE',
  );
  protected readonly isPublic = signal<boolean>(!!this.row?.['isPublic']);
  protected readonly requiresGrant = signal<boolean>(!!this.row?.['requiresGrant']);

  protected readonly sqlText = signal<string>((this.row?.['sqlText'] as string) ?? '');
  protected readonly sqlColumns = signal({
    value: (this.row?.['valueColumn'] as string) ?? '',
    labelAr: (this.row?.['displayArColumn'] as string) ?? '',
    labelEn: (this.row?.['displayEnColumn'] as string) ?? '',
    parent: (this.row?.['parentColumn'] as string) ?? '',
  });

  protected readonly sources = toSignal(
    this.api
      .lovBuilderSources<QuerySourceInfo>()
      .pipe(catchError(() => of([] as QuerySourceInfo[]))),
    {
      initialValue: [] as QuerySourceInfo[],
    },
  );
  protected readonly entities = toSignal(
    this.api.list('apps.entity').pipe(catchError(() => of([] as ConfigRow[]))),
    { initialValue: [] as ConfigRow[] },
  );
  protected readonly apps = toSignal(
    this.api.list('apps.business-app').pipe(catchError(() => of([] as ConfigRow[]))),
    { initialValue: [] as ConfigRow[] },
  );

  protected readonly roles = signal<RoleGrantRow[]>([]);
  protected readonly changedRoles = signal<ReadonlySet<string>>(new Set());

  protected readonly preview = signal<Preview | null>(null);
  protected readonly previewing = signal(false);
  protected readonly previewSearch = signal('');
  protected readonly saving = signal(false);
  protected readonly errors = signal<string[]>([]);
  private previewRun = 0;

  protected readonly stepOf = stepOf;
  protected readonly operators = OPERATORS;
  protected readonly noValue = NO_VALUE;
  protected readonly userFacts = USER_FACTS;
  protected readonly ruleFacts = RULE_FACTS;
  protected readonly grantTypes: RoleGrantRow['grantType'][] = [
    null,
    'ALL',
    'VALUES',
    'RULE',
    'NONE',
  ];

  /** Every column of the query's sources, grouped by source for the column pickers. */
  protected readonly columns = computed(() => columnsOf(this.query(), this.sources()));
  protected readonly columnGroups = computed(() => {
    const groups = new Map<
      string,
      {
        alias: string;
        title: { ar: string; en: string };
        options: { ref: string; label: string; category: ColumnCategory }[];
      }
    >();
    for (const c of this.columns()) {
      const group = groups.get(c.alias) ?? {
        alias: c.alias,
        title: { ar: `${c.alias} · ${c.source.nameAr}`, en: `${c.alias} · ${c.source.nameEn}` },
        options: [],
      };
      group.options.push({
        ref: c.ref,
        label: this.columnLabel(c.column),
        category: c.column.category,
      });
      groups.set(c.alias, group);
    }
    return [...groups.values()];
  });
  protected readonly classes = computed(() => this.sources().filter((s) => s.kind === 'CLASS'));
  protected readonly registeredTables = computed(() =>
    this.sources().filter((s) => s.kind === 'DATA_OBJECT'),
  );
  /** Tables and views not registered yet: the list registers those it uses when it is saved. */
  protected readonly otherTables = computed(() => this.sources().filter((s) => s.kind === 'TABLE'));
  /** What is typed in the search box of the open source picker (cleared when it closes). */
  protected readonly sourceSearch = signal('');

  /** Classes matching the search (the chosen one always stays, so the picker keeps showing it). */
  protected classOptions(keep: string | null): QuerySourceInfo[] {
    const search = this.sourceSearch();
    return this.classes().filter(
      (s) => s.code === keep || matchesSearch(search, s.code, s.nameAr, s.nameEn),
    );
  }

  protected registeredOptions(chosen: { kind: SourceKind; code: string }): QuerySourceInfo[] {
    const search = this.sourceSearch();
    const keep = chosen.kind === 'DATA_OBJECT' ? chosen.code : null;
    return this.registeredTables().filter(
      (s) => s.code === keep || matchesSearch(search, s.code, s.nameAr, s.nameEn),
    );
  }

  protected otherOptions(chosen: { kind: SourceKind; code: string }): QuerySourceInfo[] {
    const search = this.sourceSearch();
    const keep = chosen.kind === 'TABLE' ? chosen.code : null;
    return this.otherTables().filter((s) => s.code === keep || matchesSearch(search, s.code));
  }

  protected searchOpened(open: boolean, box: SelectSearch): void {
    if (open) box.focus();
    else this.sourceSearch.set('');
  }

  /** The tables this list will register when saved. */
  protected readonly pendingTables = computed(() => {
    const q = this.query();
    return [
      ...new Set(
        [q.from, ...q.joins].filter((s) => s.kind === 'TABLE' && s.code).map((s) => s.code),
      ),
    ];
  });
  protected readonly fromSource = computed(
    () =>
      this.sources().find(
        (s) => s.kind === this.query().from.kind && s.code === this.query().from.code,
      ) ?? null,
  );
  /** Values the preview found, offered when a role is given some values. */
  protected readonly knownValues = computed(() => {
    const values = new Map<string, string>();
    for (const item of this.preview()?.items ?? [])
      values.set(item.value, this.i18n.language() === 'ar' ? item.labelAr : item.labelEn);
    for (const role of this.roles())
      for (const v of role.grantedValues) if (!values.has(v)) values.set(v, v);
    return [...values.entries()].map(([value, label]) => ({ value, label }));
  });
  protected readonly stepProblems = computed(() => {
    const counts = new Array(7).fill(0);
    for (const p of this.preview()?.problems ?? []) counts[stepOf(p.field)]++;
    return counts;
  });

  constructor() {
    this.i18n.register(LOV_BUILDER_TRANSLATIONS);
    if (!this.isNew) {
      this.api
        .lovRoleGrants<RoleGrantRow>(this.row!.code)
        .pipe(catchError(() => of([] as RoleGrantRow[])))
        .subscribe((rows) => this.roles.set(rows));
    }

    // The preview follows the query as it is built.
    effect((onCleanup) => {
      const query = this.query();
      const search = this.previewSearch();
      if (this.mode() !== 'VISUAL') return;
      const timer = setTimeout(() => void this.runPreview(query, search), 450);
      onCleanup(() => clearTimeout(timer));
    });
  }

  // ---------------------------------------------------------------- query edits

  protected edit(change: (query: BuilderQuery) => void): void {
    const next = structuredClone(this.query());
    change(next);
    this.query.set(next);
  }

  /** A table choice is the kind and code together (`DATA_OBJECT|BRANCHES`, `TABLE|party.party`). */
  protected tableKey(source: { kind: SourceKind; code: string }): string {
    return source.kind === 'CLASS' || !source.code ? '' : `${source.kind}|${source.code}`;
  }

  protected pickFrom(key: string): void {
    const [kind, code] = key.split('|') as [SourceKind, string];
    this.setFrom(kind, code);
  }

  protected pickJoin(index: number, key: string): void {
    const [kind, code] = key.split('|') as [SourceKind, string];
    this.edit((q) => Object.assign(q.joins[index], { kind, code }));
  }

  protected setFrom(kind: SourceKind, code: string): void {
    this.edit((q) => {
      const previous = q.from;
      q.from = { kind, code, alias: previous.alias || 'a' };
      // A class maps itself; a table keeps what was chosen if its columns still exist.
      if (kind === 'CLASS' && code && previous.code !== code) {
        const a = q.from.alias;
        q.map = {
          value: `${a}.code`,
          labelAr: `${a}.name_ar`,
          labelEn: `${a}.name_en`,
          parent: null,
        };
      }
    });
  }

  protected addJoin(): void {
    this.edit((q) =>
      q.joins.push({
        kind: 'CLASS',
        code: '',
        alias: nextAlias(q),
        type: 'LEFT',
        on: [{ left: '', right: '' }],
      }),
    );
  }

  protected addCondition(group: number | null): void {
    this.edit((q) => this.groupOf(q, group).conditions.push({ column: '', op: 'EQ', value: null }));
  }

  protected addGroup(): void {
    this.edit((q) => q.filter.groups.push(emptyGroup(q.filter.match === 'ALL' ? 'ANY' : 'ALL')));
  }

  protected setCondition(
    group: number | null,
    index: number,
    change: Partial<QueryCondition>,
  ): void {
    this.edit((q) => {
      const condition = this.groupOf(q, group).conditions[index];
      Object.assign(condition, change);
      if (change.column !== undefined) {
        // A new column may compare differently: keep the comparison only if it still applies.
        const category = this.categoryOf(condition.column);
        if (!OPERATORS[category].includes(condition.op)) condition.op = OPERATORS[category][0];
        condition.value = null;
      }
    });
  }

  protected removeCondition(group: number | null, index: number): void {
    this.edit((q) => this.groupOf(q, group).conditions.splice(index, 1));
  }

  protected setAlias(alias: string): void {
    this.edit((q) => (q.from.alias = alias.trim().toLowerCase()));
  }

  protected setJoin(
    index: number,
    change: Partial<{
      kind: SourceKind;
      code: string;
      type: 'INNER' | 'LEFT';
      alias: string;
    }>,
  ): void {
    this.edit((q) => {
      Object.assign(q.joins[index], change);
      if (change.kind) q.joins[index].code = '';
    });
  }

  protected removeJoin(index: number): void {
    this.edit((q) => q.joins.splice(index, 1));
  }

  protected setPair(
    join: number,
    pair: number,
    change: Partial<{ left: string; right: string }>,
  ): void {
    this.edit((q) => Object.assign(q.joins[join].on[pair], change));
  }

  protected addPair(join: number): void {
    this.edit((q) => q.joins[join].on.push({ left: '', right: '' }));
  }

  protected removePair(join: number, pair: number): void {
    this.edit((q) => q.joins[join].on.splice(pair, 1));
  }

  protected setMap(key: 'value' | 'labelAr' | 'labelEn' | 'parent', ref: string | null): void {
    this.edit((q) => (q.map[key] = ref || null));
  }

  protected setMatch(group: number | null, match: 'ALL' | 'ANY'): void {
    this.edit((q) => (this.groupOf(q, group).match = match));
  }

  protected removeGroup(index: number): void {
    this.edit((q) => q.filter.groups.splice(index, 1));
  }

  protected addSort(): void {
    this.edit((q) => q.sort.push({ column: '', direction: 'ASC' }));
  }

  protected setSort(
    index: number,
    change: Partial<{ column: string; direction: 'ASC' | 'DESC' }>,
  ): void {
    this.edit((q) => Object.assign(q.sort[index], change));
  }

  protected removeSort(index: number): void {
    this.edit((q) => q.sort.splice(index, 1));
  }

  protected setDistinct(on: boolean): void {
    this.edit((q) => (q.distinct = on));
  }

  protected addUserCondition(): void {
    this.edit((q) => q.userConditions.push({ column: '', op: 'IN', user: 'BRANCHES' }));
  }

  protected setUserCondition(index: number, change: Partial<QueryCondition>): void {
    this.edit((q) => Object.assign(q.userConditions[index], change));
  }

  protected removeUserCondition(index: number): void {
    this.edit((q) => q.userConditions.splice(index, 1));
  }

  protected moveSort(index: number, by: number): void {
    this.edit((q) => {
      const [item] = q.sort.splice(index, 1);
      q.sort.splice(Math.max(0, Math.min(q.sort.length, index + by)), 0, item);
    });
  }

  protected categoryOf(ref: string): ColumnCategory {
    return this.columns().find((c) => c.ref === ref)?.column.category ?? 'TEXT';
  }

  protected sourceColumns(alias: string): { ref: string; label: string }[] {
    return this.columns()
      .filter((c) => c.alias === alias)
      .map((c) => ({ ref: c.ref, label: this.columnLabel(c.column) }));
  }

  /** Columns of the sources before a join (what it can match on). */
  protected columnsBefore(join: number): { ref: string; label: string }[] {
    const aliases = [
      this.query().from.alias,
      ...this.query()
        .joins.slice(0, join)
        .map((j) => j.alias),
    ];
    return this.columns()
      .filter((c) => aliases.includes(c.alias))
      .map((c) => ({ ref: c.ref, label: `${c.alias} · ${this.columnLabel(c.column)}` }));
  }

  protected valuesText(value: unknown): string {
    return Array.isArray(value) ? value.join(', ') : ((value as string | null) ?? '');
  }

  protected valuesFrom(text: string): string[] {
    return text
      .split(',')
      .map((v) => v.trim())
      .filter((v) => v.length > 0);
  }

  protected numberFrom(text: string): number | null {
    return text === '' || isNaN(Number(text)) ? null : Number(text);
  }

  // ---------------------------------------------------------------- roles

  protected setRole(code: string, change: Partial<RoleGrantRow>): void {
    this.roles.update((rows) => rows.map((r) => (r.roleCode === code ? { ...r, ...change } : r)));
    this.changedRoles.update((set) => new Set([...set, code]));
  }

  protected ruleFact(rule: unknown): string | null {
    const path = (rule as { var?: string } | null)?.var;
    return typeof path === 'string' ? path : null;
  }

  // ---------------------------------------------------------------- preview

  protected goTo(field: string): void {
    this.step.set(stepOf(field));
  }

  private async runPreview(query: BuilderQuery, search: string): Promise<void> {
    if (!query.from.code) {
      this.preview.set(null);
      return;
    }
    const run = ++this.previewRun;
    this.previewing.set(true);
    try {
      const result = await firstValueFrom(
        this.api.previewLovQuery<Preview>(toDefinition(query), 20, search),
      );
      if (run === this.previewRun) this.preview.set(result);
    } catch (error) {
      if (run === this.previewRun)
        this.preview.set({
          sql: null,
          items: [],
          hasMore: false,
          problems: [],
          error: problemMessages(error).join(' '),
        });
    } finally {
      if (run === this.previewRun) this.previewing.set(false);
    }
  }

  // ---------------------------------------------------------------- modes and registration

  protected setSqlColumn(key: string, value: string): void {
    this.sqlColumns.set({ ...this.sqlColumns(), [key]: value });
  }

  protected useSql(): void {
    this.mode.set('SQL');
  }

  protected rebuild(): void {
    this.query.set(emptyQuery());
    this.mode.set('VISUAL');
    this.step.set(0);
  }

  protected async registerTable(): Promise<void> {
    const type = CONFIG_SECTIONS.flatMap((s) => s.types).find(
      (t) => t.entityType === 'apps.data-object',
    );
    if (!type) return;
    await firstValueFrom(
      this.dialog
        .open<ConfigEditor, ConfigEditorData, boolean>(ConfigEditor, {
          data: { type, row: null },
          width: '52rem',
          maxWidth: '96vw',
        })
        .afterClosed(),
    );
  }

  // ---------------------------------------------------------------- save

  protected async save(submit = false): Promise<void> {
    this.errors.set([]);
    const code = this.code().trim();
    if (!code || !this.nameAr().trim() || !this.nameEn().trim()) {
      this.errors.set([this.i18n.translate('qb.required')]);
      this.step.set(6);
      return;
    }

    const visual = this.mode() === 'VISUAL';
    const columns = this.sqlColumns();
    // Tables not registered yet become data objects, registered in the same change as the list.
    const { definition, registrations } = visual
      ? registerTables(toDefinition(this.query()), this.sources(), this.entityCode())
      : { definition: null, registrations: [] };
    const payload: Record<string, unknown> = {
      ...payloadOf(this.row),
      nameAr: this.nameAr().trim(),
      nameEn: this.nameEn().trim(),
      isActive: this.isActive(),
      entityCode: this.entityCode() ?? '',
      businessApps: this.businessApps(),
      sourceType: visual ? 'QUERY' : 'SQL',
      query: definition,
      classCode: null,
      dataObjectCode: null,
      staticFilter: null,
      sqlText: visual ? null : this.sqlText(),
      valueColumn: visual ? null : columns.value || null,
      displayArColumn: visual ? null : columns.labelAr || null,
      displayEnColumn: visual ? null : columns.labelEn || null,
      parentColumn: visual ? null : columns.parent || null,
      searchMode: this.searchMode(),
      maxRows: this.maxRows(),
      defaultSelectionMode: this.selectionMode(),
      requiresGrant: this.requiresGrant(),
      isPublic: this.isPublic(),
      bindings: (this.row?.['bindings'] as unknown[]) ?? [],
    };

    this.saving.set(true);
    try {
      const operation = this.isNew ? 'CREATE' : 'UPDATE';
      if (registrations.length && submit && this.drafts.needsApproval('apps.lov')) {
        // Sent for approval together: one change set with the tables' registrations and the list.
        const sets = await firstValueFrom(
          this.api.importPackage({
            title: `${this.i18n.translate('qb.title')} — ${code}`,
            items: [
              ...registrations.map((r) => ({
                entityType: 'apps.data-object',
                entityCode: r.code,
                operation: 'CREATE',
                payload: r.payload,
              })),
              { entityType: 'apps.lov', entityCode: code, operation, payload },
            ],
          }),
        );
        for (const set of sets) await firstValueFrom(this.api.submit(set.changeSet.id));
        this.snackBar.open(this.i18n.translate('drafts.submitted'), undefined, { duration: 6000 });
      } else {
        for (const r of registrations) {
          await this.drafts.addChange('apps.data-object', r.code, 'CREATE', r.payload);
        }
        await this.drafts.addChange('apps.lov', code, operation, payload, { submit });
      }

      // Role grants live on the roles (Security changes); a new list must be approved before roles can refer to it.
      if (!this.isNew) {
        for (const roleCode of this.changedRoles()) {
          const grant = this.roles().find((r) => r.roleCode === roleCode)!;
          const role = await firstValueFrom(this.api.get('apps.role', roleCode));
          const rolePayload = payloadOf(role);
          rolePayload['grants'] = withGrant(
            role['grants'] as Record<string, unknown>[] | undefined,
            code,
            grant,
          );
          await this.drafts.addChange('apps.role', roleCode, 'UPDATE', rolePayload, { submit });
        }
      }
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected close(): void {
    this.ref.close(false);
  }

  private groupOf(query: BuilderQuery, group: number | null): QueryGroup {
    return group === null ? query.filter : query.filter.groups[group];
  }

  private columnLabel(column: {
    name: string;
    labelAr: string | null;
    labelEn: string | null;
  }): string {
    const label =
      this.i18n.language() === 'ar'
        ? column.labelAr || column.labelEn
        : column.labelEn || column.labelAr;
    return label ? `${label} (${column.name})` : column.name;
  }
}
