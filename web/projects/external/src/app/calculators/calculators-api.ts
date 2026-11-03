import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

const DECISIONS_API = '/api/decisions/v1';

export interface CalculatorSummary {
  code: string;
  titleAr: string;
  titleEn: string;
  introAr: string | null;
  introEn: string | null;
  icon: string | null;
}

export interface CalculatorOption {
  value: string;
  labelAr: string;
  labelEn: string;
}

export interface CalculatorInput {
  code: string;
  labelAr: string;
  labelEn: string;
  hintAr: string | null;
  hintEn: string | null;
  type: 'NUMBER' | 'TEXT' | 'SELECT' | 'CHECKBOX' | 'DATE';
  required: boolean;
  options: CalculatorOption[];
  min: number | null;
  max: number | null;
  /** The field starts from the signed-in applicant's data. */
  prefilled: boolean;
}

export interface CalculatorResult {
  path: string;
  labelAr: string;
  labelEn: string;
  format: 'TEXT' | 'NUMBER' | 'MONEY' | 'YES_NO';
  headline: boolean;
}

export interface CalculatorView extends CalculatorSummary {
  inputs: CalculatorInput[];
  results: CalculatorResult[];
  noteAr: string | null;
  noteEn: string | null;
}

/** Public calculators: public rule sets evaluated without signing in, nothing recorded. */
@Injectable({ providedIn: 'root' })
export class CalculatorsApi {
  private readonly http = inject(HttpClient);

  list(): Observable<CalculatorSummary[]> {
    return this.http.get<CalculatorSummary[]>(`${DECISIONS_API}/calculators`);
  }

  get(code: string): Observable<CalculatorView> {
    return this.http.get<CalculatorView>(`${DECISIONS_API}/calculators/${code}`);
  }

  /** Starting values from the acting party's latest data (signed in). */
  prefill(code: string): Observable<Record<string, unknown>> {
    return this.http.get<Record<string, unknown>>(`${DECISIONS_API}/calculators/${code}/prefill`);
  }

  calculate(code: string, input: Record<string, unknown>): Observable<Record<string, unknown>> {
    return this.http.post<Record<string, unknown>>(`${DECISIONS_API}/calculators/${code}`, input);
  }
}
