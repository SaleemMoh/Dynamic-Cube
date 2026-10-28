import { Injectable, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AppsControlApi } from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';

/** The signed-in user's Apps Control privileges (resource → actions); screens use it to show or hide actions. */
@Injectable({ providedIn: 'root' })
export class PrivilegesService {
  private readonly privileges = toSignal(inject(AppsControlApi).privileges().pipe(catchError(() => of({} as Record<string, string[]>))), {
    initialValue: {} as Record<string, string[]>,
  });

  readonly loaded = computed(() => Object.keys(this.privileges()).length > 0);

  can(resource: string, action: string): boolean {
    return this.privileges()[resource]?.includes(action) ?? false;
  }
}
