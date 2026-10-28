import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { DynamicForm, FormValue } from './dynamic-form';
import { FormOption, FormSchema } from './form-schema';
import { LOV_SOURCE, LovSource } from './lov-source';

const schema: FormSchema = {
  code: 'address',
  sections: [
    {
      code: 'address',
      title: { ar: 'العنوان', en: 'Address' },
      fields: [
        { code: 'reference', type: 'text', label: { ar: 'المرجع', en: 'Reference' }, readonly: true },
        { code: 'region', type: 'select', label: { ar: 'المنطقة', en: 'Region' }, lov: 'LOV_REGION', required: true },
        { code: 'city', type: 'select', label: { ar: 'المدينة', en: 'City' }, lov: 'LOV_CITY', lovParentField: 'region' },
        { code: 'activities', type: 'select', label: { ar: 'الأنشطة', en: 'Activities' }, multiple: true, options: [] },
      ],
    },
  ],
};

const DATA: Record<string, FormOption[]> = {
  LOV_REGION: [
    { value: 'R1', label: { ar: 'الرياض', en: 'Riyadh' } },
    { value: 'R2', label: { ar: 'مكة المكرمة', en: 'Makkah' } },
  ],
  'LOV_CITY:R1': [{ value: 'RUH', label: { ar: 'الرياض', en: 'Riyadh' } }],
  'LOV_CITY:R2': [{ value: 'JED', label: { ar: 'جدة', en: 'Jeddah' } }],
};

class FakeLovSource implements LovSource {
  readonly calls: string[] = [];

  options(lov: string, parent: string | null): Observable<FormOption[]> {
    const key = parent ? `${lov}:${parent}` : lov;
    this.calls.push(key);
    return of(DATA[key] ?? []);
  }
}

describe('DynamicForm with LOV fields', () => {
  let fixture: ComponentFixture<DynamicForm>;
  let source: FakeLovSource;
  let emitted: FormValue[];

  beforeEach(async () => {
    localStorage.clear();
    source = new FakeLovSource();
    await TestBed.configureTestingModule({ imports: [DynamicForm], providers: [{ provide: LOV_SOURCE, useValue: source }] }).compileComponents();
    fixture = TestBed.createComponent(DynamicForm);
    fixture.componentRef.setInput('schema', schema);
    fixture.componentRef.setInput('initialValue', { reference: 'REQ-1' });
    emitted = [];
    fixture.componentInstance.submitted.subscribe((value) => emitted.push(value));
    fixture.detectChanges();
    await fixture.whenStable();
  });

  const group = () => fixture.componentInstance.formGroup();

  it('loads LOV options, waiting for the parent value of cascading lists', () => {
    expect(source.calls).toEqual(['LOV_REGION']);

    group().controls['region'].setValue('R1');
    expect(source.calls).toEqual(['LOV_REGION', 'LOV_CITY:R1']);
  });

  it('clears a cascading value when its parent changes', () => {
    group().controls['region'].setValue('R1');
    group().controls['city'].setValue('RUH');

    group().controls['region'].setValue('R2');

    expect(group().controls['city'].value).toBeNull();
    expect(source.calls.at(-1)).toBe('LOV_CITY:R2');
  });

  it('submits read-only fields and empty multi-selects', () => {
    group().controls['region'].setValue('R2');
    (fixture.nativeElement as HTMLElement).querySelector('form')!.dispatchEvent(new Event('submit'));

    expect(emitted).toHaveLength(1);
    expect(emitted[0]).toEqual({ reference: 'REQ-1', region: 'R2', city: null, activities: [] });
    expect(group().controls['reference'].disabled).toBe(true);
  });
});
