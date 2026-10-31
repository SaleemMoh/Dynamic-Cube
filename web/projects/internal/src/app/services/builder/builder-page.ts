import { ChangeDetectionStrategy, Component, HostListener, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppsControlApi, BusinessAppContext, ConfigRow, I18nService, LocalizePipe, problemMessages, TranslatePipe } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { FullWindow } from '../../common/full-window';
import { DraftsService } from '../../apps-control/drafts.service';
import { payloadOf } from '../../apps-control/config/form-values';
import { PrivilegesService } from '../../apps-control/privileges.service';
import { BuilderReferences } from './builder-references';
import { BuilderStore } from './builder-store';
import { Problem, STARTER, ServiceDraft, normalize } from './builder-model';
import { CardTab } from './card-tab';
import { FormTab } from './form-tab';
import { PreviewTab } from './preview-tab';
import { RulesTab } from './rules-tab';
import { WorkflowTab } from './workflow-tab';
import { BUILDER_TRANSLATIONS } from './builder-translations';
import { DESIGNER_TRANSLATIONS } from '../../workflows/designer/designer-translations';
import { SubmitButton } from '../../apps-control/submit-button';

export type BuilderView = 'form' | 'rules' | 'card' | 'workflow' | 'preview' | 'json';
const VIEWS: BuilderView[] = ['form', 'rules', 'card', 'workflow', 'preview', 'json'];

/**
 * The Service Builder: one designer per service. It opens a service's latest version (or an earlier one) and prepares
 * the next: the form (steps, sections, bank questions, conditions, layout), the rules (levels, eligibility, checks,
 * fees, availability), the portal card, a view of the workflow and a live preview. Saving adds the next version to a
 * change set; published versions never change.
 */
@Component({
  selector: 'dc-builder-page',
  imports: [
    SubmitButton,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    LocalizePipe,
    TranslatePipe,
    FormTab,
    RulesTab,
    CardTab,
    WorkflowTab,
    PreviewTab,
  ],
  providers: [BuilderStore, BuilderReferences],
  hostDirectives: [FullWindow],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './builder-page.html',
  styleUrl: './builder-page.scss',
})
export class BuilderPage {
  private readonly api = inject(AppsControlApi);
  protected readonly fullWindow = inject(FullWindow);
  protected readonly drafts = inject(DraftsService);
  private readonly privileges = inject(PrivilegesService);
  protected readonly i18n = inject(I18nService);
  protected readonly store = inject(BuilderStore);
  protected readonly refs = inject(BuilderReferences);

  /** Query parameters: the service, and the version to start from (default: the latest). */
  readonly service = input<string | undefined>();
  readonly version = input<string | undefined>();
  /** Query parameter: an archetype whose service template is edited instead of a service's version. */
  readonly archetype = input<string | undefined>();

  protected readonly views = VIEWS;
  private readonly appContext = inject(BusinessAppContext);
  protected readonly services = rxResource({ params: () => ({ app: this.appContext.current() }), stream: ({ params }) => this.api.list('svc.service', null, params.app) });
  private readonly versions = rxResource({ stream: () => this.api.list('svc.service-version') });
  protected readonly current = signal<string | null>(null);
  /** The archetype whose template is open (instead of a service). */
  protected readonly archetypeCode = signal<string | null>(null);
  /** What is open: a service, or an archetype's template. */
  protected readonly editing = computed(() => this.current() ?? this.archetypeCode());
  /** The change saving adds: the service's next version, or the archetype with its new template. */
  protected readonly target = computed(() => (this.archetypeCode() ? 'svc.archetype' : 'svc.service-version'));
  protected readonly archetypeRow = computed(() => this.refs.archetypes().find((a) => a.code === this.archetypeCode()) ?? null);
  protected readonly base = signal<number | null>(null);
  protected readonly view = signal<BuilderView>('form');
  protected readonly jsonText = signal('');
  protected readonly jsonError = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly serverProblems = signal<string[] | null>(null);
  protected readonly savedAs = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly canCreate = computed(() => this.privileges.can('services.config', 'CREATE'));

  protected readonly serviceRow = computed(() => (this.services.value() ?? []).find((s) => s.code === this.current()) ?? null);
  protected readonly documentType = computed(() => (this.serviceRow()?.['documentType'] as string | null | undefined) ?? null);
  protected readonly versionsOf = computed(() => {
    const code = this.current();
    return (this.versions.value() ?? []).filter((r) => code && r.code.startsWith(code + '/')).sort((a, b) => number(b.code) - number(a.code));
  });
  protected readonly nextNumber = computed(() => 1 + Math.max(0, ...this.versionsOf().map((r) => number(r.code))));
  protected readonly problems = computed(() => this.store.problems());
  protected readonly problemCount = computed(() => {
    const count: Record<string, number> = {};
    for (const p of this.problems()) count[p.tab] = (count[p.tab] ?? 0) + 1;
    return count;
  });

  constructor() {
    // The builder's texts (and the designer's it shares) load with it.
    this.i18n.register(DESIGNER_TRANSLATIONS);
    this.i18n.register(BUILDER_TRANSLATIONS);
    effect(() => {
      const list = this.services.value();
      if (!list || !this.versions.hasValue() || this.current()) return;
      const wanted = this.service();
      if (wanted && list.some((s) => s.code === wanted)) untracked(() => this.open(wanted, this.version() ? Number(this.version()) : null));
    });
    effect(() => {
      const wanted = this.archetype();
      const archetypes = this.refs.archetypes();
      if (wanted && !this.editing() && archetypes.some((a) => a.code === wanted)) untracked(() => this.openArchetype(wanted));
    });
    effect(() => this.store.bank.set(this.refs.bank()));
  }

  @HostListener('window:beforeunload', ['$event'])
  protected beforeUnload(event: BeforeUnloadEvent): void {
    if (this.store.dirty()) event.preventDefault();
  }

  @HostListener('keydown', ['$event'])
  protected key(event: KeyboardEvent): void {
    const target = event.target as HTMLElement;
    if (target.closest('input, textarea, mat-select, [contenteditable]')) return;
    const mod = event.ctrlKey || event.metaKey;
    if (mod && event.key.toLowerCase() === 'z') {
      event.preventDefault();
      if (event.shiftKey) this.store.redo();
      else this.store.undo();
    } else if (mod && event.key.toLowerCase() === 'y') {
      event.preventDefault();
      this.store.redo();
    }
  }

  protected open(code: string, from: number | null): void {
    if (this.store.dirty() && !confirm(this.i18n.translate('designer.discard'))) return;
    this.current.set(code);
    this.archetypeCode.set(null);
    this.store.service.set(code);
    const versions = this.versionsOf();
    const chosen = (from !== null ? versions.find((r) => number(r.code) === from) : undefined) ?? versions[0];
    this.base.set(chosen ? number(chosen.code) : null);
    // A service without versions starts from its archetype's template (or the blank start).
    const archetype = this.refs.archetypes().find((a) => a.code === this.serviceRow()?.['archetype']);
    this.store.load((chosen?.['definition'] as ServiceDraft | undefined) ?? (archetype?.['template'] as ServiceDraft | null | undefined) ?? STARTER);
    this.serverProblems.set(null);
    this.savedAs.set(null);
    this.errors.set([]);
    if (this.view() === 'json') this.jsonText.set(this.json());
  }

  /** Opens an archetype's service template (shared questions only: no service owns it). */
  protected openArchetype(code: string): void {
    if (this.store.dirty() && !confirm(this.i18n.translate('designer.discard'))) return;
    this.current.set(null);
    this.archetypeCode.set(code);
    this.store.service.set(null);
    this.base.set(null);
    const row = this.refs.archetypes().find((a) => a.code === code);
    this.store.load((row?.['template'] as ServiceDraft | null | undefined) ?? STARTER);
    this.serverProblems.set(null);
    this.savedAs.set(null);
    this.errors.set([]);
    if (this.view() === 'json') this.jsonText.set(this.json());
  }

  /** The archetype as stored, with the template being edited. */
  private archetypePayload(): Record<string, unknown> {
    const payload: Record<string, unknown> = { ...payloadOf(this.archetypeRow()), template: this.store.definition() };
    delete payload['templateFrom'];
    delete payload['workflowFrom'];
    return payload;
  }

  protected showView(view: BuilderView): void {
    if (this.view() === 'json' && view !== 'json' && !this.applyJson()) return;
    if (view === 'json') {
      this.jsonText.set(this.json());
      this.jsonError.set(false);
    }
    this.view.set(view);
  }

  protected selectProblem(problem: Problem): void {
    this.showView(problem.tab);
    if (problem.path) this.store.selection.set(problem.path);
  }

  protected problemText(problem: Problem): string {
    let text = this.i18n.translate(problem.key);
    for (const [k, v] of Object.entries(problem.params ?? {})) text = text.replace(`{${k}}`, String(v));
    return text;
  }

  private json(): string {
    return JSON.stringify(this.store.definition(), null, 2);
  }

  private applyJson(): boolean {
    try {
      const value = JSON.parse(this.jsonText()) as ServiceDraft;
      if (!Array.isArray(value?.steps)) throw new Error('steps');
      if (JSON.stringify(value) !== this.json()) this.store.apply(() => normalize(value));
      return true;
    } catch {
      this.jsonError.set(true);
      return false;
    }
  }

  private payload(): { nameAr: string; nameEn: string; definition: ServiceDraft } {
    const n = this.nextNumber();
    return { nameAr: `الإصدار ${n}`, nameEn: `Version ${n}`, definition: this.store.definition() };
  }

  /** Checks the version as publishing would (questions, rules, numbering), saving nothing. */
  protected async check(): Promise<void> {
    if (this.view() === 'json' && !this.applyJson()) return;
    this.errors.set([]);
    this.busy.set(true);
    try {
      const archetype = this.archetypeCode();
      const item = archetype
        ? { entityType: 'svc.archetype', entityCode: archetype, operation: 'UPDATE' as const, payload: this.archetypePayload() }
        : { entityType: 'svc.service-version', entityCode: `${this.current()}/${this.nextNumber()}`, operation: 'CREATE' as const, payload: this.payload() };
      const result = await firstValueFrom(this.api.checkPackage({ title: 'builder', items: [item] }));
      this.serverProblems.set(
        [...Object.entries(result.errors), ...Object.entries(result.items[0]?.errors ?? {})].flatMap(([field, list]) => list.map((m) => `${field}: ${m}`)),
      );
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }

  protected async save(submit = false): Promise<void> {
    if (this.view() === 'json' && !this.applyJson()) return;
    const archetype = this.archetypeCode();
    const code = archetype ?? `${this.current()}/${this.nextNumber()}`;
    this.errors.set([]);
    this.busy.set(true);
    try {
      if (archetype) await this.drafts.addChange('svc.archetype', archetype, 'UPDATE', this.archetypePayload(), { submit });
      else await this.drafts.addChange('svc.service-version', code, 'CREATE', this.payload(), { submit });
      this.store.saved();
      this.savedAs.set(code);
      this.serverProblems.set(null);
      if (archetype) this.refs.reloadArchetypes();
      else this.versions.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }

  protected serviceName(row: ConfigRow): string {
    return this.i18n.localize({ ar: row.nameAr as string, en: row.nameEn as string });
  }
}

function number(code: string): number {
  return Number(code.slice(code.lastIndexOf('/') + 1));
}
