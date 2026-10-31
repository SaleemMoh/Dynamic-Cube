import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  I18nService,
  JsonLogicRule,
  LocalizePipe,
  QuestionUse,
  RuleEditor,
  RuleVariable,
  SectionDefinition,
  StepDefinition,
  StepType,
  TranslatePipe,
  localize,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { ConfigEditor, ConfigEditorData } from '../../apps-control/config/config-editor';
import { SERVICE_SECTIONS } from '../../apps-control/config/module-sections';
import { BankQuestion, BuilderReferences } from './builder-references';
import { BuilderStore } from './builder-store';
import {
  FormPath,
  STEP_TYPES,
  addQuestion,
  addSection,
  addStep,
  moveQuestion,
  moveSection,
  moveStep,
  remove,
  updateQuestion,
  updateSection,
  updateStep,
  usedQuestions,
} from './builder-model';

const STEP_ICONS: Record<StepType, string> = {
  FORM: 'dynamic_form',
  LEVEL: 'workspace_premium',
  FEES: 'payments',
  REVIEW: 'fact_check',
  DECLARATION: 'gavel',
  INTEGRATION: 'sync_alt',
};
const CODE = /^[A-Z][A-Z0-9_-]{0,50}$/;

/**
 * The form of a service version: steps, their sections and the bank questions in them. Questions are dragged from the
 * bank into a section (or clicked into the selected one) and dragged to reorder or move them; the inspector edits what
 * is selected, conditions included, without JSON.
 */
@Component({
  selector: 'dc-builder-form-tab',
  imports: [
    CdkDrag,
    CdkDragHandle,
    CdkDropList,
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatSelectModule,
    MatTooltipModule,
    LocalizePipe,
    TranslatePipe,
    RuleEditor,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './form-tab.html',
  styleUrl: './form-tab.scss',
})
export class FormTab {
  protected readonly store = inject(BuilderStore);
  protected readonly refs = inject(BuilderReferences);
  private readonly i18n = inject(I18nService);
  private readonly dialog = inject(MatDialog);
  protected readonly stepTypes = STEP_TYPES;
  protected readonly icons = STEP_ICONS;
  protected readonly search = signal('');

  protected readonly steps = computed(() => this.store.definition().steps);
  /** Ids of every question list, so a question can be dragged into any section. */
  protected readonly listIds = computed(() => ['bank', ...this.steps().flatMap((s, i) => s.sections.map((_, j) => `q-${i}-${j}`))]);
  protected readonly used = computed(() => new Set(usedQuestions(this.store.definition())));
  /** Shared bank questions and this service's own questions (another service's are not offered). */
  protected readonly bank = computed(() => {
    const term = this.search().trim().toLowerCase();
    const service = this.store.service();
    return this.refs
      .questions()
      .filter((q) => !q.serviceCode || q.serviceCode === service)
      .filter((q) => !term || q.code.toLowerCase().includes(term) || q.label.ar.includes(term) || q.label.en.toLowerCase().includes(term));
  });
  /** What section and question rules can read: the answers, by question code. */
  protected readonly answerVariables = computed<RuleVariable[]>(() => this.refs.questionVariables(usedQuestions(this.store.definition()), ''));

  protected readonly selected = computed(() => {
    const s = this.store.selection();
    const def = this.store.definition();
    if (!s) return null;
    const step = def.steps[s.step];
    const section = s.section !== undefined ? step?.sections[s.section] : undefined;
    const question = s.question !== undefined ? section?.questions[s.question] : undefined;
    return { path: s, step, section, question };
  });

  protected name(item: { titleAr?: string; titleEn?: string; code: string }): string {
    return localize({ ar: item.titleAr || item.code, en: item.titleEn || item.code }, this.i18n.language());
  }

  protected question(code: string): BankQuestion | undefined {
    return this.refs.byCode().get(code);
  }

  protected questionName(code: string): string {
    const q = this.question(code);
    return q ? localize(q.label, this.i18n.language()) : code;
  }

  protected isSelected(path: FormPath): boolean {
    const s = this.store.selection();
    return !!s && s.step === path.step && s.section === path.section && s.question === path.question;
  }

  protected select(path: FormPath, event?: Event): void {
    event?.stopPropagation();
    this.store.selection.set(path);
  }

  protected addStep(type: StepType): void {
    const after = this.store.selection()?.step;
    let index = 0;
    this.store.apply((def) => {
      const added = addStep(def, type, after);
      index = added.index;
      return added.def;
    });
    this.store.selection.set({ step: index });
  }

  protected addSection(step: number): void {
    let index = 0;
    this.store.apply((def) => {
      const added = addSection(def, step);
      index = added.index;
      return added.def;
    });
    this.store.selection.set({ step, section: index });
  }

  /** Puts a bank question into the selected section (or the first section of the selected or first form step). */
  protected addToSelected(code: string): void {
    const def = this.store.definition();
    const s = this.store.selection();
    let step = s?.step ?? def.steps.findIndex((x) => x.type === 'FORM');
    if (step < 0 || def.steps[step]?.type !== 'FORM') step = def.steps.findIndex((x) => x.type === 'FORM');
    if (step < 0) return;
    const section = s?.step === step && s.section !== undefined ? s.section : 0;
    if (!def.steps[step].sections[section]) return;
    this.store.apply((d) => addQuestion(d, step, section, code));
    const index = this.store.definition().steps[step].sections[section].questions.findIndex((q) => q.code === code);
    this.store.selection.set({ step, section, question: index });
  }

  protected dropStep(event: CdkDragDrop<unknown>): void {
    if (event.previousIndex !== event.currentIndex) this.store.apply((def) => moveStep(def, event.previousIndex, event.currentIndex));
    this.store.selection.set({ step: event.currentIndex });
  }

  protected dropSection(step: number, event: CdkDragDrop<unknown>): void {
    if (event.previousIndex !== event.currentIndex) this.store.apply((def) => moveSection(def, step, event.previousIndex, event.currentIndex));
  }

  protected dropQuestion(step: number, section: number, event: CdkDragDrop<unknown>): void {
    // The bank's list carries no data; a section's list carries where it is.
    const from = event.previousContainer.data as { step: number; section: number } | null;
    if (!from) {
      const code = event.item.data as string;
      this.store.apply((def) => addQuestion(def, step, section, code, event.currentIndex));
      this.store.selection.set({ step, section, question: event.currentIndex });
      return;
    }
    if (from.step === step && from.section === section && event.previousIndex === event.currentIndex) return;
    this.store.apply((def) =>
      moveQuestion(def, { step: from.step, section: from.section, question: event.previousIndex }, { step, section, index: event.currentIndex }),
    );
    this.store.selection.set({ step, section, question: event.currentIndex });
  }

  protected remove(path: FormPath, event?: Event): void {
    event?.stopPropagation();
    this.store.apply((def) => remove(def, path));
    this.store.selection.set(
      path.question !== undefined ? { step: path.step, section: path.section } : path.section !== undefined ? { step: path.step } : null,
    );
  }

  protected step(index: number, change: Partial<StepDefinition>): void {
    const before = this.store.definition().steps[index].code;
    if (change.code !== undefined) {
      change.code = change.code.trim().toUpperCase();
      if (!CODE.test(change.code) || this.steps().some((s, i) => i !== index && s.code === change.code)) return;
    }
    this.store.apply((def) => {
      const next = updateStep(def, index, change);
      // Validations name the step they check.
      if (change.code && change.code !== before) next.validations = next.validations.map((v) => (v.step === before ? { ...v, step: change.code } : v));
      return next;
    });
  }

  protected section(step: number, section: number, change: Partial<SectionDefinition>): void {
    if (change.code !== undefined) {
      change.code = change.code.trim().toUpperCase();
      if (
        !CODE.test(change.code) ||
        this.steps().some((s) => s.sections.some((x, j) => !(s === this.steps()[step] && j === section) && x.code === change.code))
      )
        return;
    }
    this.store.apply((def) => updateSection(def, step, section, change));
  }

  protected use(path: FormPath, change: Partial<QuestionUse>): void {
    this.store.apply((def) => updateQuestion(def, path as Required<FormPath>, change));
  }

  protected rule(value: JsonLogicRule | null): JsonLogicRule | null {
    return value;
  }

  protected sectionColumns(value: string): number | null {
    return value === 'auto' ? null : Number(value);
  }

  /** A new bank question (a change set like any other); the bank is read again afterwards. */
  protected async newQuestion(): Promise<void> {
    await this.editQuestion(null);
  }

  /** A new question of this service alone (a service-level question). */
  protected async newServiceQuestion(): Promise<void> {
    await this.editQuestion(null, { serviceCode: this.store.service() });
  }

  protected async editQuestion(code: string | null, defaults?: Record<string, unknown>): Promise<void> {
    const type = SERVICE_SECTIONS.flatMap((s) => s.types).find((t) => t.entityType === 'svc.question');
    if (!type) return;
    const row = code ? ((await firstValueFrom(this.refs.row(code))) ?? null) : null;
    const saved = await firstValueFrom(
      this.dialog
        .open<ConfigEditor, ConfigEditorData, boolean>(ConfigEditor, { data: { type, row, defaults }, width: '48rem', maxWidth: '95vw' })
        .afterClosed(),
    );
    if (saved) this.refs.loadQuestions();
  }

  /** Questions the version asks (where an integration step's outputs can go). */
  protected readonly askedQuestions = computed(() => usedQuestions(this.store.definition()));

  protected integration(index: number, change: Partial<NonNullable<StepDefinition['integration']>>): void {
    const current = this.store.definition().steps[index]?.integration ?? { operation: '', inputs: [], outputs: [], required: true };
    this.step(index, { integration: { ...current, ...change } });
  }

  protected addInput(index: number): void {
    const current = this.store.definition().steps[index]?.integration?.inputs ?? [];
    this.integration(index, { inputs: [...current, { parameter: '', from: '' }] });
  }

  protected setInput(index: number, row: number, change: Partial<{ parameter: string; from: string }>): void {
    const inputs = (this.store.definition().steps[index]?.integration?.inputs ?? []).map((x, i) => (i === row ? { ...x, ...change } : x));
    this.integration(index, { inputs });
  }

  protected removeInput(index: number, row: number): void {
    this.integration(index, { inputs: (this.store.definition().steps[index]?.integration?.inputs ?? []).filter((_, i) => i !== row) });
  }

  protected addOutput(index: number): void {
    const current = this.store.definition().steps[index]?.integration?.outputs ?? [];
    this.integration(index, { outputs: [...current, { output: '', question: '' }] });
  }

  protected setOutput(index: number, row: number, change: Partial<{ output: string; question: string }>): void {
    const outputs = (this.store.definition().steps[index]?.integration?.outputs ?? []).map((x, i) => (i === row ? { ...x, ...change } : x));
    this.integration(index, { outputs });
  }

  protected removeOutput(index: number, row: number): void {
    this.integration(index, { outputs: (this.store.definition().steps[index]?.integration?.outputs ?? []).filter((_, i) => i !== row) });
  }
}
