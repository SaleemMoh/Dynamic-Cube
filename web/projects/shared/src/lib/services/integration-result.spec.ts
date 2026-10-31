import { TestBed } from '@angular/core/testing';
import { AnswerChanges, IntegrationResult } from './integration-result';
import {
  IntegrationStepRun,
  QuestionSnapshot,
  ResolvedVersion,
  StepDefinition,
} from './service.models';

const question = (code: string, options: QuestionSnapshot['options'] = []): QuestionSnapshot => ({
  code,
  labelAr: `س ${code}`,
  labelEn: `Q ${code}`,
  hintAr: null,
  hintEn: null,
  type: options.length ? 'SELECT' : 'TEXT',
  lov: null,
  lovParentQuestion: null,
  options,
  multiple: false,
  validation: { min: null, max: null, maxLength: null, pattern: null },
  prefill: null,
});

const step: StepDefinition = {
  code: 'VERIFY',
  type: 'INTEGRATION',
  titleAr: 'تحقق',
  titleEn: 'Verify',
  sections: [],
  integration: {
    operation: 'CR_VERIFY',
    inputs: [],
    outputs: [{ output: 'status', question: 'STATUS' }],
    required: true,
    display: [
      { output: 'status', labelAr: 'الحالة', labelEn: 'Status' },
      { output: 'active', labelAr: 'نشط', labelEn: 'Active' },
    ],
  },
};

const version = {
  serviceCode: 'S',
  number: 1,
  definition: { levels: [], eligibility: [], validations: [], fees: [], steps: [step] },
  questions: {
    STATUS: question('STATUS'),
    CITY: question('CITY', [
      { value: 'RUH', labelAr: 'الرياض', labelEn: 'Riyadh' },
      { value: 'JED', labelAr: 'جدة', labelEn: 'Jeddah' },
    ]),
  },
} as unknown as ResolvedVersion;

describe('IntegrationResult', () => {
  const render = (run: IntegrationStepRun, display = step) => {
    const fixture = TestBed.createComponent(IntegrationResult);
    fixture.componentRef.setInput('version', version);
    fixture.componentRef.setInput('step', display);
    fixture.componentRef.setInput('run', run);
    fixture.componentRef.setInput('data', { STATUS: 'ACTIVE' });
    fixture.detectChanges();
    return fixture;
  };

  it('shows a success with the chosen values and their labels', () => {
    const fixture = render({
      success: true,
      code: '000',
      messageAr: null,
      messageEn: null,
      at: '2026-10-31T10:00:00Z',
      values: { status: 'ACTIVE', active: true },
    });
    const card = fixture.nativeElement.querySelector(
      '[data-testid="integration-result"]',
    ) as HTMLElement;

    expect(card.dataset['success']).toBe('true');
    const values = [...card.querySelectorAll('.dc-result__value')].map(
      (v) =>
        `${v.querySelector('dt')?.textContent?.trim()}: ${v.querySelector('dd')?.textContent?.trim()}`,
    );
    expect(values).toEqual(['الحالة: ACTIVE', 'نشط: نعم']);
  });

  it('shows a failure with the provider message, and asks to try again', () => {
    const fixture = render({
      success: false,
      code: '101',
      messageAr: 'السجل غير نشط',
      messageEn: 'Not active',
      at: '2026-10-31T10:00:00Z',
      values: null,
    });
    const element = fixture.nativeElement as HTMLElement;
    let retried = 0;
    fixture.componentInstance.retry.subscribe(() => retried++);

    expect(element.textContent).toContain('السجل غير نشط');
    expect(element.querySelector('.dc-result__note')).not.toBeNull();
    (element.querySelector('[data-testid="integration-retry"]') as HTMLButtonElement).click();
    expect(retried).toBe(1);
  });

  it('falls back to the answers the step filled when no values are chosen', () => {
    const plain = { ...step, integration: { ...step.integration!, display: [] } };
    const fixture = render(
      { success: true, code: '000', messageAr: null, messageEn: null, at: '2026-10-31T10:00:00Z' },
      plain,
    );

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-output="STATUS"]')?.textContent,
    ).toContain('ACTIVE');
  });
});

describe('AnswerChanges', () => {
  it('lists each changed answer from → to (option labels, empty values) and can be closed', () => {
    const fixture = TestBed.createComponent(AnswerChanges);
    fixture.componentRef.setInput('version', version);
    fixture.componentRef.setInput('changes', [
      { question: 'CITY', labelAr: 'المدينة', labelEn: 'City', from: 'RUH', to: 'JED' },
      { question: 'STATUS', labelAr: 'الحالة', labelEn: 'Status', from: null, to: 'ACTIVE' },
    ]);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    const city = element.querySelector('[data-question="CITY"]')!;
    expect(city.querySelector('.dc-changes__from')?.textContent?.trim()).toBe('الرياض');
    expect(city.querySelector('.dc-changes__to')?.textContent?.trim()).toBe('جدة');
    expect(
      element.querySelector('[data-question="STATUS"] .dc-changes__from')?.textContent?.trim(),
    ).toBe('فارغ');

    (element.querySelector('[data-testid="wizard-changes-close"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(element.querySelector('[data-testid="wizard-changes"]')).toBeNull();
  });
});
