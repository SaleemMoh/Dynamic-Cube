import { HttpInterceptorFn } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { PartyApi } from './party.api';
import { ActingContext, ContextWarning } from './party.models';

const STORAGE_KEY = 'dc.actingParty';
export const ACTING_PARTY_HEADER = 'X-Acting-Party';

/**
 * The portal's acting context: the applicant themselves or a party they may represent. The choice is kept for the browser
 * session and sent with every API call; the API checks it against the applicant's relationships.
 */
@Injectable({ providedIn: 'root' })
export class ActingPartyService {
  private readonly api = inject(PartyApi);
  private readonly state = signal<{ individual: ActingContext; contexts: ActingContext[]; warnings: ContextWarning[] } | null>(null);
  private readonly selectedId = signal<string | null>(read());
  readonly loading = signal(false);

  readonly individual = computed(() => this.state()?.individual ?? null);
  readonly contexts = computed(() => this.state()?.contexts ?? []);
  readonly warnings = computed(() => this.state()?.warnings ?? []);
  readonly current = computed(() => {
    const id = this.selectedId();
    return this.contexts().find((c) => c.partyId === id) ?? this.individual();
  });

  /** Party id to send, or null when acting for oneself. */
  headerValue(): string | null {
    const current = this.current();
    return current && current.partyId !== this.individual()?.partyId ? current.partyId : null;
  }

  async load(refresh = false): Promise<void> {
    this.loading.set(true);
    try {
      const result = await firstValueFrom(this.api.contexts(refresh));
      this.state.set({ individual: result.individual, contexts: result.contexts, warnings: result.warnings });
      if (this.selectedId() && !result.contexts.some((c) => c.partyId === this.selectedId())) {
        this.select(null);
      }
    } finally {
      this.loading.set(false);
    }
  }

  select(partyId: string | null): void {
    this.selectedId.set(partyId);
    try {
      if (partyId) sessionStorage.setItem(STORAGE_KEY, partyId);
      else sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // Storage unavailable (private mode): the choice lasts until reload.
    }
  }
}

/** Sends the acting party with API calls (portal only); the contexts call itself never carries it. */
export const actingPartyInterceptor: HttpInterceptorFn = (request, next) => {
  const acting = inject(ActingPartyService).headerValue();
  return acting && request.url.startsWith('/api/') && !request.url.endsWith('/me/contexts')
    ? next(request.clone({ setHeaders: { [ACTING_PARTY_HEADER]: acting } }))
    : next(request);
};

function read(): string | null {
  try {
    return sessionStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}
