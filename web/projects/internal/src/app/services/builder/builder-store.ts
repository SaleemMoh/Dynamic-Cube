import { Injectable, computed, signal } from '@angular/core';
import { FormPath, ServiceDraft, check, normalize } from './builder-model';

const HISTORY = 100;

/** The service version being built, with undo and redo; every change goes through `apply`. */
@Injectable()
export class BuilderStore {
  private readonly current = signal<ServiceDraft>(normalize(null));
  private readonly past = signal<ServiceDraft[]>([]);
  private readonly future = signal<ServiceDraft[]>([]);

  readonly definition = this.current.asReadonly();
  /** The selected step, section or question of the form. */
  readonly selection = signal<FormPath | null>(null);
  /** Codes of the question bank (to flag questions that do not exist). */
  readonly bank = signal<ReadonlySet<string>>(new Set());
  /** The service being built (its own questions are offered besides the shared bank). */
  readonly service = signal<string | null>(null);
  readonly problems = computed(() => check(this.current(), this.bank()));
  readonly canUndo = computed(() => this.past().length > 0);
  readonly canRedo = computed(() => this.future().length > 0);
  readonly dirty = signal(false);

  load(definition: Partial<ServiceDraft> | null | undefined): void {
    this.current.set(normalize(definition));
    this.past.set([]);
    this.future.set([]);
    this.selection.set(null);
    this.dirty.set(false);
  }

  apply(change: (def: ServiceDraft) => ServiceDraft): void {
    const before = this.current();
    const after = change(before);
    if (after === before) return;
    this.past.update((p) => [...p.slice(-HISTORY + 1), before]);
    this.future.set([]);
    this.current.set(after);
    this.dirty.set(true);
    this.keepSelection();
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

  /** Drops (or shortens) the selection when what it pointed to is gone. */
  private keepSelection(): void {
    const s = this.selection();
    if (!s) return;
    const step = this.current().steps[s.step];
    if (!step) return this.selection.set(null);
    if (s.section === undefined) return;
    const section = step.sections[s.section];
    if (!section) return this.selection.set({ step: s.step });
    if (s.question !== undefined && !section.questions[s.question]) this.selection.set({ step: s.step, section: s.section });
  }
}
