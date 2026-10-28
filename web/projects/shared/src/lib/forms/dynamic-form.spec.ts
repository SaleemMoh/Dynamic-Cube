import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DynamicForm, FormValue } from './dynamic-form';
import { FormSchema } from './form-schema';

const schema: FormSchema = {
  code: 'classification-request',
  sections: [
    {
      code: 'establishment',
      title: { ar: 'بيانات المنشأة', en: 'Establishment' },
      fields: [
        { code: 'name', type: 'text', label: { ar: 'اسم المنشأة', en: 'Establishment name' }, required: true },
        {
          code: 'kind',
          type: 'select',
          label: { ar: 'نوع المنشأة', en: 'Establishment type' },
          required: true,
          options: [
            { value: 'contractor', label: { ar: 'مقاول', en: 'Contractor' } },
            { value: 'consultancy', label: { ar: 'مكتب استشاري هندسي', en: 'Engineering consultancy' } },
          ],
        },
        {
          code: 'engineeringLicense',
          type: 'text',
          label: { ar: 'رقم الترخيص الهندسي', en: 'Engineering license number' },
          visibleWhen: { '==': [{ var: 'kind' }, 'consultancy'] },
          requiredWhen: { '==': [{ var: 'kind' }, 'consultancy'] },
        },
      ],
    },
    {
      code: 'declaration',
      title: { ar: 'الإقرار', en: 'Declaration' },
      fields: [{ code: 'pledge', type: 'checkbox', label: { ar: 'أقر بصحة البيانات', en: 'I confirm the data is correct' }, required: true }],
    },
  ],
};

describe('DynamicForm', () => {
  let fixture: ComponentFixture<DynamicForm>;
  let emitted: FormValue[];

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({ imports: [DynamicForm] }).compileComponents();
    fixture = TestBed.createComponent(DynamicForm);
    fixture.componentRef.setInput('schema', schema);
    emitted = [];
    fixture.componentInstance.submitted.subscribe((value) => emitted.push(value));
    fixture.detectChanges();
    await fixture.whenStable();
  });

  const element = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const field = (code: string): HTMLElement | null => element().querySelector(`[data-field="${code}"]`);

  it('renders Arabic labels by default', () => {
    expect(field('name')?.textContent).toContain('اسم المنشأة');
    expect(element().querySelector('legend')?.textContent).toContain('بيانات المنشأة');
  });

  it('shows a conditional field only when its rule is satisfied', async () => {
    expect(field('engineeringLicense')).toBeNull();

    fixture.componentInstance.formGroup().controls['kind'].setValue('consultancy');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(field('engineeringLicense')).not.toBeNull();
  });

  it('does not submit while required fields are empty', () => {
    fixture.componentInstance.formGroup().controls['name'].setValue('شركة الحداثة للمقاولات');
    (element().querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    expect(emitted).toHaveLength(0);
  });

  it('applies conditional requirement and excludes hidden fields from the value', async () => {
    const group = fixture.componentInstance.formGroup();
    group.controls['name'].setValue('مكتب الهندسة المتقدمة');
    group.controls['kind'].setValue('consultancy');
    group.controls['pledge'].setValue(true);
    fixture.detectChanges();

    (element().querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    expect(emitted).toHaveLength(0);

    group.controls['engineeringLicense'].setValue('10/123456');
    (element().querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    expect(emitted).toEqual([{ name: 'مكتب الهندسة المتقدمة', kind: 'consultancy', engineeringLicense: '10/123456', pledge: true }]);

    group.controls['kind'].setValue('contractor');
    (element().querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    expect(emitted[1]).toEqual({ name: 'مكتب الهندسة المتقدمة', kind: 'contractor', pledge: true });
  });
});
