import { DraftAutosave } from './draft-autosave';

describe('DraftAutosave', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('saves once, a moment after the last change', async () => {
    const saved: string[] = [];
    const autosave = new DraftAutosave<string>(async (value) => void saved.push(value), 1500);

    autosave.change('a');
    await vi.advanceTimersByTimeAsync(1000);
    autosave.change('ab');
    await vi.advanceTimersByTimeAsync(1000);
    expect(saved).toEqual([]);
    await vi.advanceTimersByTimeAsync(600);

    expect(saved).toEqual(['ab']);
    expect(autosave.savedAt()).not.toBeNull();
    expect(autosave.hasPending()).toBe(false);
  });

  it('saves at once when flushed (the page is hidden) and says it is leaving', async () => {
    const saved: [string, boolean][] = [];
    const autosave = new DraftAutosave<string>(
      async (value, leaving) => void saved.push([value, leaving]),
    );

    autosave.change('typed');
    await autosave.flush(true);

    expect(saved).toEqual([['typed', true]]);
    await vi.advanceTimersByTimeAsync(5000);
    expect(saved).toHaveLength(1);
  });

  it('drops a waiting change and lets another save wait for the one running', async () => {
    const order: string[] = [];
    let finish!: () => void;
    const autosave = new DraftAutosave<string>(
      (value) =>
        new Promise<void>((resolve) => {
          order.push(`start ${value}`);
          finish = () => {
            order.push(`end ${value}`);
            resolve();
          };
        }),
    );

    autosave.change('first');
    const running = autosave.flush();
    autosave.change('second');
    autosave.discard();
    const move = autosave.exclusive(async () => void order.push('move'));
    await vi.advanceTimersByTimeAsync(0);
    expect(order).toEqual(['start first']);
    finish();
    await running;
    await move;

    expect(order).toEqual(['start first', 'end first', 'move']);
    await vi.advanceTimersByTimeAsync(5000);
    expect(order).not.toContain('start second');
  });

  it('reports a failed save and recovers on the next one', async () => {
    let fail = true;
    const autosave = new DraftAutosave<string>(async () => {
      if (fail) throw new Error('offline');
    });

    autosave.change('a');
    await autosave.flush();
    expect(autosave.failed()).toBe(true);

    fail = false;
    autosave.change('b');
    await autosave.flush();
    expect(autosave.failed()).toBe(false);
  });
});
