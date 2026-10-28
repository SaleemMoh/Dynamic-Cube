import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface HostInfo {
  app: string;
  version: string;
}

/** Calls `/api/info` on the app's own API host (proxied in development, reverse-proxied in production). */
@Injectable({ providedIn: 'root' })
export class HostInfoService {
  private readonly http = inject(HttpClient);

  get(): Observable<HostInfo> {
    return this.http.get<HostInfo>('/api/info');
  }
}
