import { signal } from '@angular/core';

/**
 * Saves a draft a moment after the last change, so nothing typed is lost if the page is closed by accident. Saves run one
 * at a time, and other saves of the same draft (moving between steps) can wait their turn with {@link exclusive}.
 */
export class DraftAutosave<T> {
  /** A save is running. */
  readonly saving = signal(false);
  /** When the last automatic save succeeded. */
  readonly savedAt = signal<Date | null>(null);
  /** The last automatic save failed (the next change tries again). */
  readonly failed = signal(false);

  private pending: T | null = null;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private queue: Promise<unknown> = Promise.resolve();

  constructor(
    private readonly save: (value: T, leaving: boolean) => Promise<void>,
    private readonly delayMs = 1500,
  ) {}

  /** A change to save: it is saved after the delay, unless another change comes first. */
  change(value: T): void {
    this.pending = value;
    clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.flush(), this.delayMs);
  }

  /** Whether a change is waiting to be saved. */
  hasPending(): boolean {
    return this.pending !== null;
  }

  /** Saves the waiting change now (e.g. the page is hidden or left). */
  flush(leaving = false): Promise<void> {
    clearTimeout(this.timer);
    const value = this.pending;
    this.pending = null;
    if (value === null) return this.queue.then(() => undefined);
    return this.exclusive(async () => {
      this.saving.set(true);
      try {
        await this.save(value, leaving);
        this.savedAt.set(new Date());
        this.failed.set(false);
      } catch {
        this.failed.set(true);
      } finally {
        this.saving.set(false);
      }
    });
  }

  /** Drops the waiting change (e.g. a move between steps saves the same answers). */
  discard(): void {
    clearTimeout(this.timer);
    this.pending = null;
  }

  /** Runs a task after the saves already started, so two saves of the draft never overlap. */
  exclusive<R>(task: () => Promise<R>): Promise<R> {
    const run = this.queue.then(task, task);
    this.queue = run.catch(() => undefined);
    return run;
  }
}
