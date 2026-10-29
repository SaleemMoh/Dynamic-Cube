import { Injectable, computed, signal } from '@angular/core';
import { NodeDef, Outlet, WorkflowDraft, check, edges, positions } from './designer-model';

export type Selection = { kind: 'node'; code: string } | { kind: 'edge'; from: string; outlet: Outlet } | null;

const HISTORY = 100;

/**
 * The graph being designed, with undo and redo. Every change goes through `apply`; dragging a node updates the
 * graph live with `preview` and is recorded once when the drag ends.
 */
@Injectable()
export class DesignerStore {
  private readonly current = signal<WorkflowDraft>({ nodes: [] });
  private readonly past = signal<WorkflowDraft[]>([]);
  private readonly future = signal<WorkflowDraft[]>([]);
  /** The graph before a drag started. */
  private dragStart: WorkflowDraft | null = null;

  readonly definition = this.current.asReadonly();
  readonly selection = signal<Selection>(null);
  /** Nodes a test run is at (highlighted on the canvas). */
  readonly active = signal<string[]>([]);
  readonly edges = computed(() => edges(this.current()));
  readonly positions = computed(() => positions(this.current()));
  readonly problems = computed(() => check(this.current()));
  readonly canUndo = computed(() => this.past().length > 0);
  readonly canRedo = computed(() => this.future().length > 0);
  /** Changed since it was loaded or last saved. */
  readonly dirty = signal(false);

  readonly selectedNode = computed<NodeDef | null>(() => {
    const s = this.selection();
    return s?.kind === 'node' ? (this.current().nodes.find((n) => n.code === s.code) ?? null) : null;
  });

  /** Starts over with a graph (nothing to undo). */
  load(def: WorkflowDraft): void {
    this.current.set(structuredClone(def));
    this.past.set([]);
    this.future.set([]);
    this.selection.set(null);
    this.dirty.set(false);
  }

  apply(change: (def: WorkflowDraft) => WorkflowDraft): void {
    const before = this.current();
    const after = change(before);
    if (after === before) return;
    this.past.update((p) => [...p.slice(-HISTORY + 1), before]);
    this.future.set([]);
    this.current.set(after);
    this.dirty.set(true);
    this.keepSelection();
  }

  /** A change shown at once and recorded by `commit` (dragging). */
  preview(change: (def: WorkflowDraft) => WorkflowDraft): void {
    this.dragStart ??= this.current();
    this.current.set(change(this.current()));
  }

  commit(): void {
    const before = this.dragStart;
    this.dragStart = null;
    if (!before || before === this.current()) return;
    this.past.update((p) => [...p.slice(-HISTORY + 1), before]);
    this.future.set([]);
    this.dirty.set(true);
  }

  undo(): void {
    const past = this.past();
    if (!past.length) return;
    this.future.update((f) => [this.current(), ...f]);
    this.current.set(past[past.length - 1]);
    this.past.set(past.slice(0, -1));
    this.dirty.set(true);
    this.keepSelection();
  }

  redo(): void {
    const [next, ...rest] = this.future();
    if (!next) return;
    this.past.update((p) => [...p, this.current()]);
    this.current.set(next);
    this.future.set(rest);
    this.dirty.set(true);
    this.keepSelection();
  }

  saved(): void {
    this.dirty.set(false);
  }

  /** Drops the selection when what it pointed to is gone. */
  private keepSelection(): void {
    const s = this.selection();
    if (!s) return;
    const nodes = this.current().nodes;
    if (s.kind === 'node' && !nodes.some((n) => n.code === s.code)) this.selection.set(null);
    if (
      s.kind === 'edge' &&
      !this.edges().some((e) => e.from === s.from && e.outlet.kind === s.outlet.kind && (e.outlet.index ?? -1) === (s.outlet.index ?? -1))
    ) {
      this.selection.set(null);
    }
  }
}
