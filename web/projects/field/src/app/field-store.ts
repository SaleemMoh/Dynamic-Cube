import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { AuthService, FieldReport, VisitDetail, VisitsApi, problemMessages } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { idb } from './idb';

/** A report waiting to be sent: kept on the device until the server has it (or refuses it, with why). */
export interface OutboxItem {
  submissionId: string;
  visitId: string;
  report: FieldReport;
  queuedAt: string;
  error: string[] | null;
}

/** What the inspector filled in so far for a visit, so closing the app loses nothing. */
export interface ReportDraft {
  outcome: string | null;
  data: Record<string, unknown>;
  report: string;
  photos: FieldReport['photos'];
  signature: string | null;
  signedBy: string;
}

/** How often the app checks for new visits and sends waiting reports while online. */
const SYNC_MS = 60_000;

/**
 * The inspector's visits on the device: cached in IndexedDB so the app works without a connection, refreshed from the
 * server when online. Reports go to an outbox first and are sent as soon as there is a connection; a report the server
 * already has is not recorded twice (it carries its submission id).
 */
@Injectable({ providedIn: 'root' })
export class FieldStore {
  private readonly api = inject(VisitsApi);
  private readonly auth = inject(AuthService);

  readonly visits = signal<VisitDetail[]>([]);
  readonly outbox = signal<OutboxItem[]>([]);
  readonly online = signal(navigator.onLine);
  readonly syncing = signal(false);
  readonly lastSync = signal<string | null>(null);
  readonly loaded = signal(false);
  /** The last refresh failed for another reason than being offline. */
  readonly error = signal<string | null>(null);

  readonly pendingVisits = computed(() => new Set(this.outbox().map((o) => o.visitId)));
  readonly open = computed(() => this.visits().filter((v) => v.visit.status !== 'COMPLETED' && !this.pendingVisits().has(v.visit.id)));
  readonly done = computed(() => this.visits().filter((v) => v.visit.status === 'COMPLETED' || this.pendingVisits().has(v.visit.id)));

  private started = false;

  constructor() {
    const online = () => {
      this.online.set(true);
      void this.sync();
    };
    const offline = () => this.online.set(false);
    window.addEventListener('online', online);
    window.addEventListener('offline', offline);
    const timer = setInterval(() => {
      if (this.online()) void this.sync();
    }, SYNC_MS);
    inject(DestroyRef).onDestroy(() => {
      window.removeEventListener('online', online);
      window.removeEventListener('offline', offline);
      clearInterval(timer);
    });
  }

  /** Loads what the device has, then refreshes from the server. Safe to call more than once. */
  async start(): Promise<void> {
    if (this.started) return;
    this.started = true;
    await this.forUser();
    const [visits, outbox, last] = await Promise.all([idb.all<VisitDetail>('visits'), idb.all<OutboxItem>('outbox'), idb.get<string>('meta', 'lastSync')]);
    this.visits.set(sortVisits(visits));
    this.outbox.set(outbox.sort((a, b) => a.queuedAt.localeCompare(b.queuedAt)));
    this.lastSync.set(last ?? null);
    this.loaded.set(true);
    await this.sync();
  }

  visit(id: string): VisitDetail | undefined {
    return this.visits().find((v) => v.visit.id === id);
  }

  pending(id: string): OutboxItem | undefined {
    return this.outbox().find((o) => o.visitId === id);
  }

  /** Sends waiting reports, then fetches the inspector's visits. */
  async sync(): Promise<void> {
    if (this.syncing() || !this.online()) return;
    this.syncing.set(true);
    try {
      for (const item of [...this.outbox()]) {
        if (!(await this.send(item))) break;
      }
      await this.refresh();
    } finally {
      this.syncing.set(false);
    }
  }

  /** The inspector arrived: shown at once, told to the server when it can be (a report works without it). */
  async begin(id: string): Promise<void> {
    this.patchVisit(id, (v) => (v.visit.status === 'SCHEDULED' ? { ...v, visit: { ...v.visit, status: 'IN_PROGRESS' } } : v));
    if (!this.online()) return;
    try {
      this.replace(await firstValueFrom(this.api.start(id)));
    } catch {
      // Not important enough to bother the inspector: the report records the visit either way.
    }
  }

  /** Queues the report and tries to send it now. */
  async submit(visitId: string, report: FieldReport): Promise<void> {
    const item: OutboxItem = { submissionId: report.submissionId, visitId, report, queuedAt: new Date().toISOString(), error: null };
    await idb.put('outbox', item.submissionId, item);
    await idb.delete('drafts', visitId);
    this.outbox.update((list) => [...list.filter((o) => o.visitId !== visitId), item]);
    void this.sync();
  }

  /** Drops a report the server refused (the visit can be reported again). */
  async discard(item: OutboxItem): Promise<void> {
    await idb.delete('outbox', item.submissionId);
    this.outbox.update((list) => list.filter((o) => o.submissionId !== item.submissionId));
  }

  draft(visitId: string): Promise<ReportDraft | undefined> {
    return idb.get<ReportDraft>('drafts', visitId);
  }

  saveDraft(visitId: string, draft: ReportDraft): Promise<unknown> {
    return idb.put('drafts', visitId, draft);
  }

  /** True when the item left the outbox (sent, or refused and kept with the reason); false when offline. */
  private async send(item: OutboxItem): Promise<boolean> {
    try {
      const detail = await firstValueFrom(this.api.report(item.visitId, item.report));
      await idb.delete('outbox', item.submissionId);
      this.outbox.update((list) => list.filter((o) => o.submissionId !== item.submissionId));
      this.replace(detail);
      return true;
    } catch (error) {
      if (error instanceof HttpErrorResponse && (error.status === 0 || error.status >= 500 || error.status === 401)) {
        return false;
      }
      const refused = { ...item, error: problemMessages(error) };
      await idb.put('outbox', item.submissionId, refused);
      this.outbox.update((list) => list.map((o) => (o.submissionId === item.submissionId ? refused : o)));
      return true;
    }
  }

  private async refresh(): Promise<void> {
    try {
      const visits = await firstValueFrom(this.api.mine());
      await idb.clear('visits');
      await Promise.all(visits.map((v) => idb.put('visits', v.visit.id, v)));
      const at = new Date().toISOString();
      await idb.put('meta', 'lastSync', at);
      this.visits.set(sortVisits(visits));
      this.lastSync.set(at);
      this.error.set(null);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 0) {
        this.online.set(navigator.onLine);
      } else {
        this.error.set(problemMessages(error).join(' '));
      }
    }
  }

  private replace(detail: VisitDetail): void {
    void idb.put('visits', detail.visit.id, detail);
    this.visits.update((list) => sortVisits([...list.filter((v) => v.visit.id !== detail.visit.id), detail]));
  }

  private patchVisit(id: string, change: (v: VisitDetail) => VisitDetail): void {
    const current = this.visit(id);
    if (current) this.replace(change(current));
  }

  /** Another inspector signing in on the same device does not see the previous one's visits or reports. */
  private async forUser(): Promise<void> {
    const user = this.auth.user()?.userName;
    if (!user) return;
    const previous = await idb.get<string>('meta', 'user');
    if (previous && previous !== user) {
      await Promise.all([idb.clear('visits'), idb.clear('outbox'), idb.clear('drafts'), idb.clear('meta')]);
    }
    await idb.put('meta', 'user', user);
  }
}

function sortVisits(visits: VisitDetail[]): VisitDetail[] {
  const at = (v: VisitDetail) => v.visit.scheduledAt ?? v.visit.dueAt ?? v.visit.requestedAt;
  return [...visits].sort((a, b) => at(a).localeCompare(at(b)));
}
