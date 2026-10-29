import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { I18nService, JsonLogicRule, LocalizePipe, RuleEditor, RuleVariable, TranslatePipe, localize } from '@dynamic-cube/shared';
import { NODE_ICONS } from './designer-canvas';
import { ActionDef, AssignmentDef, NodeDef, NodeType, Outlet, WorkflowDraft, newAction, replaceNode } from './designer-model';
import { DesignerReferences } from './designer-references';
import { DesignerStore } from './designer-store';
import { MappingEditor } from './mapping-editor';

const CODE = /^[A-Z][A-Z0-9_-]{0,50}$/;

/** The side panel: the selected step's settings, the selected arrow, or (nothing selected) the version's. */
@Component({
  selector: 'dc-designer-panel',
  imports: [
    FormsModule,
    NgTemplateOutlet,
    MatButtonModule,
    MatCheckboxModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatSelectModule,
    MatTooltipModule,
    LocalizePipe,
    TranslatePipe,
    RuleEditor,
    MappingEditor,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './designer-panel.html',
  styleUrl: './designer-panel.scss',
})
export class DesignerPanel {
  protected readonly store = inject(DesignerStore);
  protected readonly refs = inject(DesignerReferences);
  private readonly i18n = inject(I18nService);
  readonly versionNameAr = input('');
  readonly versionNameEn = input('');
  readonly versionNameArChange = output<string>();
  readonly versionNameEnChange = output<string>();
  readonly remove = output<void>();

  protected readonly icons = NODE_ICONS;
  protected readonly actionKinds: ActionDef['kind'][] = ['COMPLETE', 'APPROVE', 'REJECT', 'RETURN'];
  protected readonly node = this.store.selectedNode;

  protected readonly nodes = computed(() => this.store.definition().nodes);
  /** Nodes an arrow can go to (anything but START and the node itself). */
  protected readonly targets = computed(() => this.nodes().filter((n) => n.type !== 'START' && n.code !== this.node()?.code));
  protected readonly tasks = computed(() => this.nodes().filter((n) => n.type === 'HUMAN_TASK'));

  /** The selected arrow and the node it leaves. */
  protected readonly edge = computed(() => {
    const s = this.store.selection();
    if (s?.kind !== 'edge') return null;
    const from = this.nodes().find((n) => n.code === s.from);
    const e = this.store.edges().find((x) => x.from === s.from && x.outlet.kind === s.outlet.kind && (x.outlet.index ?? -1) === (s.outlet.index ?? -1));
    return from && e ? { from, edge: e, outlet: s.outlet } : null;
  });

  /** What rules in this workflow can read. */
  protected readonly variables = computed<RuleVariable[]>(() => {
    const t = (ar: string, en: string) => ({ ar, en });
    const list: RuleVariable[] = this.refs.questions().map((q) => ({
      path: `data.${q.code}`,
      label: q.label,
      type: q.type === 'NUMBER' ? 'number' : q.type === 'DATE' ? 'date' : q.type === 'CHECKBOX' ? 'boolean' : 'text',
      options: q.type === 'CHECKBOX' ? [{ value: 'true', label: t('نعم', 'Yes') }] : q.options,
    }));
    list.push({
      path: 'level',
      label: t('مستوى الخدمة', 'Service level'),
      options: this.refs.levels(),
    });
    for (const task of this.tasks()) {
      list.push({
        path: `actions.${task.code}`,
        label: t(`آخر إجراء في: ${task.nameAr}`, `Last action at: ${task.nameEn}`),
        options: (task.actions ?? []).map((a) => ({
          value: a.code,
          label: { ar: a.nameAr, en: a.nameEn },
        })),
      });
    }
    for (const d of this.nodes().filter((n) => n.type === 'DECISION')) {
      list.push({
        path: `decisions.${d.result || d.code}`,
        label: t(`نتائج: ${d.nameAr}`, `Results of: ${d.nameEn}`),
        type: 'list',
      });
    }
    for (const p of this.nodes().filter((n) => n.type === 'PAYMENT')) {
      list.push({
        path: `payments.${p.code}.status`,
        label: t(`حالة فاتورة: ${p.nameAr}`, `Bill status: ${p.nameEn}`),
        options: ['PAID', 'EXPIRED', 'CANCELLED', 'NOT_NEEDED'].map((v) => ({
          value: v,
          label: t(v, v),
        })),
      });
    }
    list.push({ path: 'feeTotal', label: t('إجمالي الرسوم', 'Total fees'), type: 'number' });
    list.push({ path: 'party.id', label: t('معرّف المستفيد', 'Beneficiary id') });
    list.push({
      path: 'document.status',
      label: t('حالة المستند المطلوب عليه', 'Status of the document requested for'),
      options: ['ACTIVE', 'EXPIRED', 'SUSPENDED'].map((v) => ({ value: v, label: t(v, v) })),
    });
    list.push({
      path: 'document.expiresAt',
      label: t('تاريخ انتهاء المستند', 'Document expiry date'),
      type: 'date',
    });
    return list;
  });

  protected typeName(type: NodeType): string {
    return this.i18n.translate('designer.type.' + type);
  }

  protected name(n: { nameAr?: string; nameEn?: string; code: string }): string {
    return localize({ ar: n.nameAr || n.code, en: n.nameEn || n.code }, this.i18n.language());
  }

  /** Replaces the selected node with a changed copy (one undo step). */
  protected patch(change: Partial<NodeDef>): void {
    const node = this.node();
    if (!node) return;
    this.store.apply((def) => replaceNode(def, node.code, { ...node, ...change }));
  }

  protected setCode(code: string): void {
    const node = this.node();
    const next = code.trim().toUpperCase();
    if (!node || next === node.code || !CODE.test(next) || this.nodes().some((n) => n.code === next)) return;
    this.store.apply((def) => replaceNode(def, node.code, { ...node, code: next }));
    this.store.selection.set({ kind: 'node', code: next });
  }

  protected codeTaken(code: string): boolean {
    const next = code.trim().toUpperCase();
    return next !== this.node()?.code && this.nodes().some((n) => n.code === next);
  }

  protected assignment(change: Partial<AssignmentDef>): void {
    const node = this.node();
    const current: AssignmentDef = node?.assignment ?? { target: 'ROLE', mode: 'QUEUE' };
    this.patch({ assignment: { ...current, ...change } });
  }

  protected sla(change: Record<string, unknown>): void {
    const current = this.node()?.sla ?? {};
    const next = { ...current, ...change };
    const empty =
      !next.workingDays && !Object.keys(next.byLevel ?? {}).length && !next.escalateToRole && (next.warnPercent === undefined || next.warnPercent === 75);
    this.patch({ sla: empty ? null : next });
  }

  protected levelDays(level: string, days: number | null): void {
    const byLevel = { ...(this.node()?.sla?.byLevel ?? {}) };
    if (days) byLevel[level] = days;
    else delete byLevel[level];
    this.sla({ byLevel });
  }

  protected action(index: number, change: Partial<ActionDef>): void {
    const actions = [...(this.node()?.actions ?? [])];
    const next = { ...actions[index], ...change };
    if (change.kind === 'RETURN') next.to = null;
    actions[index] = next;
    this.patch({ actions });
  }

  protected addAction(kind: ActionDef['kind']): void {
    const node = this.node();
    if (!node) return;
    this.patch({ actions: [...(node.actions ?? []), newAction(node, kind, null)] });
  }

  protected removeAction(index: number): void {
    this.patch({ actions: (this.node()?.actions ?? []).filter((_, i) => i !== index) });
  }

  protected moveAction(index: number, by: number): void {
    const actions = [...(this.node()?.actions ?? [])];
    const to = index + by;
    if (to < 0 || to >= actions.length) return;
    [actions[index], actions[to]] = [actions[to], actions[index]];
    this.patch({ actions });
  }

  protected branch(index: number, to: string): void {
    const branches = [...(this.node()?.branches ?? [])];
    branches[index] = to;
    this.patch({ branches });
  }

  protected addBranch(): void {
    const node = this.node();
    const free = this.targets().find((t) => !(node?.branches ?? []).includes(t.code));
    if (free) this.patch({ branches: [...(node?.branches ?? []), free.code] });
  }

  protected removeBranch(index: number): void {
    this.patch({ branches: (this.node()?.branches ?? []).filter((_, i) => i !== index) });
  }

  protected route(index: number, change: { when?: JsonLogicRule | null; to?: string }): void {
    const routes = [...(this.node()?.routes ?? [])];
    routes[index] = { ...routes[index], ...change };
    this.patch({ routes });
  }

  protected addRoute(): void {
    const node = this.node();
    const first = this.targets()[0];
    if (first) this.patch({ routes: [...(node?.routes ?? []), { when: null, to: first.code }] });
  }

  protected removeRoute(index: number): void {
    this.patch({ routes: (this.node()?.routes ?? []).filter((_, i) => i !== index) });
  }

  protected moveRoute(index: number, by: number): void {
    const routes = [...(this.node()?.routes ?? [])];
    const to = index + by;
    if (to < 0 || to >= routes.length) return;
    [routes[index], routes[to]] = [routes[to], routes[index]];
    this.patch({ routes });
  }

  /** Points the selected arrow at another node. */
  protected retarget(to: string): void {
    const e = this.edge();
    if (!e) return;
    const outlet: Outlet = e.outlet;
    const from = e.from;
    const i = outlet.index ?? -1;
    const change: Partial<NodeDef> = {};
    switch (outlet.kind) {
      case 'next':
      case 'default':
      case 'expired':
        change[outlet.kind] = to;
        break;
      case 'branch':
        change.branches = (from.branches ?? []).map((b, k) => (k === i ? to : b));
        break;
      case 'route':
        change.routes = (from.routes ?? []).map((r, k) => (k === i ? { ...r, to } : r));
        break;
      case 'action':
        change.actions = (from.actions ?? []).map((a, k) => (k === i ? { ...a, to } : a));
        break;
      case 'resume':
        change.actions = (from.actions ?? []).map((a, k) => (k === i ? { ...a, resumeAt: to } : a));
        break;
    }
    this.store.apply((def: WorkflowDraft) => replaceNode(def, from.code, { ...from, ...change }));
  }

  /** The condition of the selected route arrow. */
  protected edgeCondition(from: string, index: number, when: JsonLogicRule | null): void {
    this.store.apply((def) => ({
      ...def,
      nodes: def.nodes.map((n) => (n.code === from ? { ...n, routes: (n.routes ?? []).map((r, i) => (i === index ? { ...r, when } : r)) } : n)),
    }));
  }

  protected selectNode(code: string): void {
    this.store.selection.set({ kind: 'node', code });
  }

  protected slaDays(value: number | null): void {
    this.store.apply((def) => ({ ...def, slaWorkingDays: value || null }));
  }

  protected num(value: unknown): number | null {
    return value === '' || value === null || value === undefined || Number.isNaN(Number(value)) ? null : Number(value);
  }
}
