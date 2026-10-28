import { InjectionToken } from '@angular/core';
import { Observable } from 'rxjs';
import { FormOption } from './form-schema';

/** Supplies options for LOV-backed select fields. Apps provide it with {@link provideLovSource}. */
export interface LovSource {
  options(lov: string, parent: string | null): Observable<FormOption[]>;
}

export const LOV_SOURCE = new InjectionToken<LovSource>('dc.lovSource');
