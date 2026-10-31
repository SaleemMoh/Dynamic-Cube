import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of, throwError, timer, map } from 'rxjs';
import { QuestionSnapshot, ResolvedVersion } from '../service.models';
import { AssistantContext, AutoNoteTexts, assistantView, matchQuestion, words } from './assistant';
import {
  LiveResultFeed,
  formatValue,
  placeColor,
  scalePlace,
  shownItems,
  shownValue,
} from './live-result';
import { PanelAssistant } from './panel-assistant';
import { PanelReadiness } from './panel-readiness';
import { PanelResult } from './panel-result';
import { AssistantPanelDefinition, LiveResultView, ResultPanelDefinition } from './panel.models';
import { computeReadiness, openGroups } from './readiness';

const question = (
  code: string,
  type: QuestionSnapshot['type'] = 'TEXT',
  pattern: string | null = null,
): QuestionSnapshot => ({
  code,
  labelAr: `س ${code}`,
  labelEn: `Q ${code}`,
  hintAr: null,
  hintEn: null,
  type,
  lov: null,
  lovParentQuestion: null,
  options: [],
  multiple: false,
  validation: { min: null, max: null, maxLength: null, pattern },
  prefill: null,
});

const version: ResolvedVersion = {
  serviceCode: 'CLS',
  number: 3,
  definition: {
    levels: [{ code: 'STD', nameAr: 'عادي', nameEn: 'Standard' }],
    eligibility: [],
    validations: [],
    fees: [],
    steps: [
      {
        code: 'BASICS',
        type: 'FORM',
        titleAr: 'البيانات',
        titleEn: 'Basics',
        sections: [
          {
            code: 'MAIN',
            titleAr: 'أ',
            titleEn: 'A',
            questions: [
              { code: 'NAME', required: true, readonly: false },
              { code: 'CR', required: true, readonly: false },
              { code: 'HAS_ISO', required: false, readonly: false },
              {
                code: 'ISO_FILE',
                required: false,
                requiredWhen: { '==': [{ var: 'HAS_ISO' }, true] },
                readonly: false,
              },
              { code: 'NOTE', required: false, readonly: false },
            ],
          },
        ],
      },
      {
        code: 'DOCS',
        type: 'FORM',
        titleAr: 'المرفقات',
        titleEn: 'Documents',
        sections: [
          {
            code: 'FILES',
            titleAr: 'م',
            titleEn: 'F',
            questions: [
              // Asked again here: counted once, where it first shows.
              { code: 'NAME', required: true, readonly: false },
              { code: 'PROFILE', required: false, readonly: false },
              {
                code: 'EXTRA',
                required: true,
                visibleWhen: { '==': [{ var: 'NAME' }, 'show'] },
                readonly: false,
              },
            ],
          },
        ],
      },
      {
        code: 'VERIFY',
        type: 'INTEGRATION',
        titleAr: 'التحقق',
        titleEn: 'Verify',
        sections: [],
        integration: { operation: 'CR', inputs: [], outputs: [], required: true },
      },
      { code: 'LEVEL', type: 'LEVEL', titleAr: 'المستوى', titleEn: 'Level', sections: [] },
      { code: 'REVIEW', type: 'REVIEW', titleAr: 'المراجعة', titleEn: 'Review', sections: [] },
    ],
  },
  questions: {
    NAME: question('NAME'),
    CR: question('CR', 'TEXT', '\\d{10}'),
    HAS_ISO: question('HAS_ISO', 'CHECKBOX'),
    ISO_FILE: question('ISO_FILE', 'FILE'),
    NOTE: question('NOTE'),
    PROFILE: question('PROFILE', 'FILE'),
    EXTRA: question('EXTRA'),
  },
};

const texts: AutoNoteTexts = {
  missing: { ar: 'ينقص: {items}', en: 'Missing: {items}' },
  conditional: { ar: 'مشروط: {items}', en: 'Conditional: {items}' },
  uploads: { ar: 'مرفقات: {items}', en: 'Uploads: {items}' },
  problems: { ar: 'ملاحظات: {items}', en: 'Notes: {items}' },
  ready: { ar: 'جاهز', en: 'Ready' },
};

describe('readiness', () => {
  it('counts each requirement once with its status and the share met', () => {
    const r = computeReadiness({ version, data: { NAME: 'Acme', CR: '12' }, levelCode: null });
    const status = Object.fromEntries(r.items.map((i) => [i.key, i.status]));
    expect(status).toEqual({
      'q:NAME': 'complete',
      'q:CR': 'missing', // does not match its pattern
      'q:ISO_FILE': 'conditional', // required only when HAS_ISO is ticked
      'q:PROFILE': 'optional',
      'i:VERIFY': 'missing',
      'l:LEVEL': 'missing',
    });
    expect([r.complete, r.missing, r.conditional]).toEqual([1, 3, 1]);
    expect(r.percent).toBe(25);
    expect([r.answered, r.asked]).toEqual([2, 6]);
  });

  it('follows the answers: a condition met makes the upload required, a call that succeeded and a level complete it', () => {
    const r = computeReadiness({
      version,
      data: { NAME: 'show', CR: '1234567890', HAS_ISO: true, ISO_FILE: ['f1', 'f2'], EXTRA: 'x' },
      levelCode: 'STD',
      integrations: {
        VERIFY: { success: true, code: null, messageAr: null, messageEn: null, at: '2026-10-31' },
      },
    });
    expect(r.items.find((i) => i.key === 'q:ISO_FILE')?.status).toBe('complete');
    expect(r.items.find((i) => i.key === 'q:EXTRA')?.status).toBe('complete');
    expect(r.missing).toBe(0);
    expect(r.percent).toBe(100);
    expect(r.files).toBe(2);
  });

  it('groups what is open by the step that completes it, missing first', () => {
    const r = computeReadiness({ version, data: {}, levelCode: null });
    const groups = openGroups(version, r);
    expect(groups.map((g) => g.step)).toEqual(['BASICS', 'VERIFY', 'LEVEL']);
    expect(groups[0].items.map((i) => i.key)).toEqual(['q:NAME', 'q:CR', 'q:ISO_FILE']);
  });
});

const assistant: AssistantPanelDefinition = {
  enabled: true,
  titleAr: 'وكيل التصنيف',
  titleEn: 'Classification assistant',
  autoNotes: true,
  lines: [
    {
      step: 'BASICS',
      kind: 'MESSAGE',
      textAr: 'أهلاً بك في {service}، بقي {missing}',
      textEn: 'Welcome to {service}, {missing} left',
    },
    { step: 'DOCS', kind: 'MESSAGE', textAr: 'المرفقات', textEn: 'Documents' },
    {
      kind: 'IMPACT',
      textAr: 'شهادة الأيزو ترفع التقييم',
      textEn: 'ISO raises the score',
      whyAr: 'القاعدة 4',
      whyEn: 'Rule 4',
      when: { '==': [{ var: 'data.HAS_ISO' }, true] },
    },
    {
      kind: 'QUESTION',
      questionAr: 'كيف تحسب الدرجة؟',
      questionEn: 'How is the grade worked out?',
      textAr: 'من المعايير الفنية والمالية',
      textEn: 'From the technical and financial criteria',
    },
    {
      kind: 'QUESTION',
      questionAr: 'ما المرفقات المطلوبة؟',
      questionEn: 'Which documents are needed?',
      textAr: 'السجل التجاري والقوائم',
      textEn: 'The register and statements',
    },
    {
      kind: 'ADVICE',
      textAr: 'نتيجتك {readiness}%',
      textEn: 'You are {readiness}%',
      when: { '>=': [{ var: 'result.grade' }, 1] },
    },
  ],
};

const context = (
  data: Record<string, unknown>,
  step = 'BASICS',
  result: Record<string, unknown> | null = null,
): AssistantContext => ({
  version,
  data,
  levelCode: null,
  step,
  readiness: computeReadiness({ version, data, levelCode: null }),
  result,
  service: { ar: 'التصنيف', en: 'Classification' },
  errors: [
    {
      field: 'CR',
      code: 'PATTERN',
      messageAr: 'رقم السجل غير صحيح',
      messageEn: 'Invalid register number',
      step: 'BASICS',
    },
  ],
});

describe('assistant', () => {
  it('shows the lines of the step whose conditions hold, with the words filled in and the worked-out notes', () => {
    const view = assistantView(assistant, context({ NAME: 'A', HAS_ISO: true }), texts);
    expect(view.message).toEqual({
      ar: 'أهلاً بك في التصنيف، بقي 4',
      en: 'Welcome to Classification, 4 left',
    });
    expect(view.notes.map((n) => n.tone)).toEqual(['IMPACT', 'MANDATORY', 'MANDATORY']);
    expect(view.notes[0].why).toEqual({ ar: 'القاعدة 4', en: 'Rule 4' });
    expect(view.notes[1].text.en).toBe('Missing: Q CR, Q ISO_FILE');
    expect(view.notes[2].text.en).toBe('Notes: Invalid register number');
    expect(view.questions.map((q) => q.question.en)).toEqual([
      'How is the grade worked out?',
      'Which documents are needed?',
    ]);

    const docs = assistantView(assistant, context({}, 'DOCS', { grade: 2 }), texts);
    expect(docs.message?.en).toBe('Documents');
    expect(docs.notes.map((n) => n.key)).toContain('auto:uploads');
    expect(docs.notes.find((n) => n.tone === 'ADVICE' && n.key.startsWith('line'))?.text.en).toBe(
      'You are 0%',
    );
  });

  it('leaves out the worked-out notes when they are off', () => {
    const view = assistantView({ ...assistant, autoNotes: false }, context({}), texts);
    expect(view.notes).toEqual([]);
  });

  it('matches a typed question by its words in either language, ignoring the article and marks', () => {
    const { questions } = assistantView(assistant, context({}), texts);
    expect(words('ما هي الدَّرجة؟')).toEqual(new Set(['درجه']));
    expect(matchQuestion('كيف يتم حساب الدرجة', questions, 'ar')?.key).toBe(questions[0].key);
    expect(matchQuestion('which documents do I upload', questions, 'en')?.key).toBe(
      questions[1].key,
    );
    expect(matchQuestion('weather tomorrow', questions, 'en')).toBeNull();
  });
});

const resultPanel: ResultPanelDefinition = {
  enabled: true,
  step: 'GRADE',
  headline: {
    output: 'bestGrade',
    labelAr: 'الدرجة المتوقعة',
    labelEn: 'Expected grade',
    onScale: true,
  },
  scale: [
    { value: '1', labelAr: 'الأولى', labelEn: 'First' },
    { value: '2', labelAr: 'الثانية', labelEn: 'Second' },
    { value: '3', labelAr: 'الثالثة', labelEn: 'Third' },
  ],
  unknownAr: 'غير مصنف',
  unknownEn: 'Unclassified',
  values: [
    { output: 'sizeAr', outputEn: 'sizeEn', labelAr: 'الحجم', labelEn: 'Size', onScale: false },
  ],
  items: {
    output: 'items',
    titleOutput: 'nameAr',
    titleOutputEn: 'nameEn',
    resultOutput: 'grade',
    columns: [{ output: 'score', labelAr: 'النقاط', labelEn: 'Score', onScale: false }],
  },
};

const live: LiveResultView = {
  step: 'GRADE',
  ruleSetCode: 'CLS_GRADING',
  version: 2,
  output: {
    bestGrade: 2,
    sizeAr: 'متوسطة',
    sizeEn: 'Medium',
    items: [
      { nameAr: 'المباني', nameEn: 'Building', grade: 2, score: 71.256 },
      { nameAr: 'الاتصالات', nameEn: 'ICT', grade: null, score: 0 },
    ],
  },
};

describe('live result', () => {
  it('reads values on the scale, plain values in each language and the items', () => {
    expect(scalePlace(resultPanel.scale, '2')).toBe(1);
    expect(scalePlace(resultPanel.scale, 2.0)).toBe(1);
    expect(scalePlace(resultPanel.scale, null)).toBe(-1);
    expect(shownValue(resultPanel.headline!, live.output, resultPanel).text).toEqual({
      ar: 'الثانية',
      en: 'Second',
    });
    expect(shownValue(resultPanel.headline!, {}, resultPanel).text).toEqual({
      ar: 'غير مصنف',
      en: 'Unclassified',
    });
    expect(shownValue(resultPanel.headline!, { bestGrade: 6 }, resultPanel).text.en).toBe(
      'Unclassified',
    );
    expect(
      shownValue(
        resultPanel.headline!,
        { bestGrade: 6 },
        { ...resultPanel, unknownAr: null, unknownEn: null },
      ).text.en,
    ).toBe('6');
    expect(shownValue(resultPanel.values[0], live.output, resultPanel).text).toEqual({
      ar: 'متوسطة',
      en: 'Medium',
    });
    const items = shownItems(resultPanel, live.output);
    expect(items.map((i) => [i.title.en, i.result?.place, i.columns[0].text.en])).toEqual([
      ['Building', 1, '71.256'],
      ['ICT', -1, '0'],
    ]);
    expect(formatValue(true, 'ar')).toBe('نعم');
    expect(placeColor(0, 5)).not.toBe(placeColor(4, 5));
  });

  it('fetches after a pause and keeps only the latest answer', async () => {
    const calls: string[] = [];
    const feed = new LiveResultFeed((data) => {
      calls.push(String(data?.['n']));
      const slow = data?.['n'] === 1;
      return timer(slow ? 40 : 5).pipe(
        map(() => ({ ...live, output: { bestGrade: data?.['n'] } })),
      );
    }, 10);
    feed.change({ n: 0 }, null);
    feed.change({ n: 1 }, null); // replaces n = 0 before the pause ends
    await firstValueFrom(timer(15));
    void feed.now({ n: 2 }, null); // overtakes the slow n = 1
    await firstValueFrom(timer(60));
    expect(calls).toEqual(['1', '2']);
    expect(feed.result()?.output['bestGrade']).toBe(2);
    expect(feed.loading()).toBe(false);

    const failing = new LiveResultFeed(
      () => throwError(() => ({ error: { title: 'No decision step' } })),
      0,
    );
    await failing.now(null, null);
    expect(failing.error()).not.toBeNull();
    const ok = new LiveResultFeed(() => of(live), 0);
    await ok.now(null, null);
    expect(ok.error()).toBeNull();
  });
});

describe('panel components', () => {
  it('readiness shows the share and opens the step of a group', () => {
    const fixture = TestBed.createComponent(PanelReadiness);
    fixture.componentRef.setInput('version', version);
    fixture.componentRef.setInput(
      'readiness',
      computeReadiness({ version, data: { NAME: 'A' }, levelCode: 'STD' }),
    );
    const opened: string[] = [];
    fixture.componentInstance.openStep.subscribe((s) => opened.push(s));
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-testid="readiness-percent"]')?.textContent).toBe('50%');
    (element.querySelector('[data-open-step="VERIFY"]') as HTMLButtonElement).click();
    expect(opened).toEqual(['VERIFY']);
  });

  it('assistant answers a chosen question after thinking', async () => {
    const fixture = TestBed.createComponent(PanelAssistant);
    fixture.componentRef.setInput('definition', assistant);
    fixture.componentRef.setInput('context', context({}));
    fixture.componentRef.setInput('thinkMs', 0);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-testid="assistant-message"]')?.textContent).toContain(
      'التصنيف',
    );
    (element.querySelector('.dc-ask__chip') as HTMLButtonElement).click();
    await firstValueFrom(timer(5));
    fixture.detectChanges();
    expect(element.querySelector('[data-testid="assistant-answer"]')?.textContent).toContain(
      'من المعايير الفنية والمالية',
    );
  });

  it('result shows the headline on the ladder and a card per item', () => {
    const fixture = TestBed.createComponent(PanelResult);
    fixture.componentRef.setInput('definition', resultPanel);
    fixture.componentRef.setInput('view', live);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(
      element.querySelector('[data-testid="result-headline"] strong')?.textContent?.trim(),
    ).toBe('الثانية');
    expect(element.querySelector('[aria-current="true"]')?.textContent?.trim()).toBe('2');
    expect(element.querySelectorAll('[data-testid="result-item"]').length).toBe(2);
  });
});
