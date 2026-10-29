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

describe('DynamicForm with a question in two sections', () => {
  const shared: FormSchema = {
    code: 'shared',
    sections: [
      { code: 'staff', title: { ar: 'الموظفون', en: 'Staff' }, fields: [{ code: 'employees', type: 'number', label: { ar: 'عدد', en: 'Count' }, required: true, min: 1 }] },
      {
        code: 'engineering',
        title: { ar: 'هندسي', en: 'Engineering' },
        visibleWhen: { in: ['BUILDING', { var: 'sectors' }] },
        fields: [{ code: 'employees', type: 'number', label: { ar: 'عدد', en: 'Count' }, required: true }],
      },
    ],
  };

  async function render(context: FormValue): Promise<ComponentFixture<DynamicForm>> {
    await TestBed.configureTestingModule({ imports: [DynamicForm] }).compileComponents();
    const fixture = TestBed.createComponent(DynamicForm);
    fixture.componentRef.setInput('schema', shared);
    fixture.componentRef.setInput('context', context);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  }

  it('shows it once and keeps it editable when a later section is hidden', async () => {
    const fixture = await render({ sectors: ['ICT'] });
    const inputs = (fixture.nativeElement as HTMLElement).querySelectorAll('input#employees');

    expect(inputs.length).toBe(1);
    expect((inputs[0] as HTMLInputElement).disabled).toBe(false);
  });

  it('shows it once when both sections are visible, using rules from the context', async () => {
    const fixture = await render({ sectors: ['BUILDING'] });
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[data-section="engineering"]')).not.toBeNull();
    expect(element.querySelectorAll('input#employees').length).toBe(1);
    expect(fixture.componentInstance.formGroup().controls['employees'].hasError('required')).toBe(true);
  });

  it('submits a step whose fields are all read-only (prefilled from a document)', async () => {
    const readOnly: FormSchema = {
      code: 'renewal',
      sections: [
        {
          code: 'certificate',
          title: { ar: 'الشهادة', en: 'Certificate' },
          fields: [{ code: 'number', type: 'text', label: { ar: 'رقم الشهادة', en: 'Certificate number' }, required: true, readonly: true }],
        },
      ],
    };
    const only = TestBed.createComponent(DynamicForm);
    only.componentRef.setInput('schema', readOnly);
    only.componentRef.setInput('initialValue', { number: 'CC-2026-000001' });
    const values: FormValue[] = [];
    only.componentInstance.submitted.subscribe((value) => values.push(value));
    only.detectChanges();
    await only.whenStable();

    ((only.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    expect(values).toEqual([{ number: 'CC-2026-000001' }]);
  });
});

describe('DynamicForm layouts', () => {
  async function render(display: FormSchema['display']): Promise<ComponentFixture<DynamicForm>> {
    await TestBed.configureTestingModule({ imports: [DynamicForm] }).compileComponents();
    const fixture = TestBed.createComponent(DynamicForm);
    const laidOut: FormSchema = { ...schema, display, sections: [{ ...schema.sections[0], columns: 2 }, schema.sections[1]] };
    laidOut.sections[0].fields = laidOut.sections[0].fields.map((f) => (f.code === 'kind' ? { ...f, width: 'full' } : f));
    fixture.componentRef.setInput('schema', laidOut);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  }

  it('places fields on the columns of their section', async () => {
    const fixture = await render(undefined);
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-section="establishment"] .dc-form__grid--cols')).not.toBeNull();
    expect((element.querySelector('[data-field="name"]') as HTMLElement).style.gridColumn).toContain('span 3');
    expect((element.querySelector('[data-field="kind"]') as HTMLElement).style.gridColumn).toContain('span 6');
  });

  it('shows sections as tabs and opens the tab of the first problem on submit', async () => {
    const fixture = await render('tabs');
    const element = fixture.nativeElement as HTMLElement;
    const tabs = element.querySelectorAll('[role="tab"]');
    expect(tabs.length).toBe(2);
    expect(tabs[1].textContent).toContain('الإقرار');
    // Fill the first tab, go to the second and submit: the first tab is fine, the pledge is not.
    (fixture.componentInstance.formGroup().controls['name'] as { setValue(v: unknown): void }).setValue('x');
    (fixture.componentInstance.formGroup().controls['kind'] as { setValue(v: unknown): void }).setValue('contractor');
    (tabs[1] as HTMLElement).click();
    fixture.detectChanges();
    await fixture.whenStable();
    (tabs[0] as HTMLElement).click();
    fixture.detectChanges();
    await fixture.whenStable();
    (element.querySelector('[data-testid="form-submit"]') as HTMLElement).click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(element.querySelector('[role="tab"][aria-selected="true"]')?.textContent).toContain('الإقرار');
  });

  it('shows sections as an accordion with the first one open', async () => {
    const fixture = await render('accordion');
    const element = fixture.nativeElement as HTMLElement;
    const panels = element.querySelectorAll('mat-expansion-panel');
    expect(panels.length).toBe(2);
    expect(panels[0].classList).toContain('mat-expanded');
    expect(panels[1].classList).not.toContain('mat-expanded');
  });
});
