import { ChangeDetectionStrategy, Component, HostListener, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppsControlApi, BusinessAppContext, ConfigRow, I18nService, LocalizePipe, problemMessages, TranslatePipe } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { FullWindow } from '../../common/full-window';
import { DraftsService } from '../../apps-control/drafts.service';
import { PrivilegesService } from '../../apps-control/privileges.service';
import { DesignerCanvas, PALETTE_MIME } from './designer-canvas';
import { NodeType, Problem, WorkflowDraft, arrange, disconnect, removeNode, toDefinition } from './designer-model';
import { DesignerPanel } from './designer-panel';
import { DesignerReferences } from './designer-references';
import { DesignerStore } from './designer-store';
import { DesignerTestRun } from './designer-test-run';

const STARTER: WorkflowDraft = {
  slaWorkingDays: 10,
  nodes: [
    { code: 'START', type: 'START', nameAr: 'البداية', nameEn: 'Start', next: 'APPROVED' },
    { code: 'APPROVED', type: 'END', nameAr: 'معتمد', nameEn: 'Approved', outcome: 'APPROVED' },
  ],
};

/**
 * The Workflow Designer: a service's workflow drawn on a canvas, edited step by step in the side panel, checked as
 * it is drawn, tried with sample data and saved as the workflow's next version (a change set, maker-checker).
 * Published versions never change; the designer always prepares the next one, starting from any earlier version.
 */
@Component({
  selector: 'dc-designer-page',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTabsModule,
    MatTooltipModule,
    LocalizePipe,
    TranslatePipe,
    DesignerCanvas,
    DesignerPanel,
    DesignerTestRun,
  ],
  providers: [DesignerStore, DesignerReferences],
  hostDirectives: [FullWindow],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './designer-page.html',
  styleUrl: './designer-page.scss',
})
export class DesignerPage {
  private readonly api = inject(AppsControlApi);
  protected readonly fullWindow = inject(FullWindow);
  protected readonly drafts = inject(DraftsService);
  private readonly privileges = inject(PrivilegesService);
  protected readonly i18n = inject(I18nService);
  protected readonly store = inject(DesignerStore);
  protected readonly refs = inject(DesignerReferences);
  private readonly canvas = viewChild(DesignerCanvas);

  /** Query parameters: the workflow, and the version to start from (default: the latest). */
  readonly workflowCode = input<string | undefined>();
  readonly version = input<string | undefined>();

  private readonly appContext = inject(BusinessAppContext);
  protected readonly workflows = rxResource({ params: () => ({ app: this.appContext.current() }), stream: ({ params }) => this.api.list('wf.workflow', null, params.app) });
  private readonly versions = rxResource({ stream: () => this.api.list('wf.workflow-version') });
  protected readonly workflow = signal<string | null>(null);
  protected readonly base = signal<number | null>(null);
  protected readonly zoom = signal(1);
  protected readonly view = signal<'canvas' | 'json' | 'test'>('canvas');
  protected readonly jsonText = signal('');
  protected readonly jsonError = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly serverProblems = signal<string[] | null>(null);
  protected readonly savedAs = signal<string | null>(null);
  protected readonly versionNameAr = signal('');
  protected readonly versionNameEn = signal('');
  protected readonly busy = signal(false);
  protected readonly canCreate = computed(() => this.privileges.can('workflows.config', 'CREATE'));

  protected readonly versionsOf = computed(() => {
    const code = this.workflow();
    return (this.versions.value() ?? []).filter((r) => code && r.code.startsWith(code + '/')).sort((a, b) => number(b.code) - number(a.code));
  });
  protected readonly nextNumber = computed(() => 1 + Math.max(0, ...this.versionsOf().map((r) => number(r.code))));
  protected readonly serviceCode = computed(
    () => (this.workflows.value() ?? []).find((w) => w.code === this.workflow())?.['serviceCode'] as string | undefined,
  );
  protected readonly errorsOnly = computed(() => this.store.problems().filter((p) => !p.warning));

  constructor() {
    // Open what the link asks for once the lists are in.
    effect(() => {
      const list = this.workflows.value();
      if (!list || !this.versions.hasValue() || this.workflow()) return;
      const wanted = this.workflowCode();
      if (wanted && list.some((w) => w.code === wanted)) untracked(() => this.open(wanted, this.version() ? Number(this.version()) : null));
    });
    effect(() => {
      const service = this.serviceCode();
      untracked(() => this.refs.load(service ?? null));
    });
  }

  @HostListener('window:beforeunload', ['$event'])
  protected beforeUnload(event: BeforeUnloadEvent): void {
    if (this.store.dirty()) event.preventDefault();
  }

  @HostListener('keydown', ['$event'])
  protected key(event: KeyboardEvent): void {
    const target = event.target as HTMLElement;
    if (this.view() !== 'canvas' || target.closest('input, textarea, mat-select, [contenteditable], .dc-designer__panel')) return;
    const mod = event.ctrlKey || event.metaKey;
    if (mod && event.key.toLowerCase() === 'z') {
      event.preventDefault();
      if (event.shiftKey) this.store.redo();
      else this.store.undo();
    } else if (mod && event.key.toLowerCase() === 'y') {
      event.preventDefault();
      this.store.redo();
    } else if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      this.deleteSelection();
    } else if (event.key === 'Escape') {
      this.store.selection.set(null);
    }
  }

  protected open(code: string, from: number | null): void {
    if (this.store.dirty() && !confirm(this.i18n.translate('designer.discard'))) return;
    this.workflow.set(code);
    const versions = this.versionsOf();
    const chosen = (from !== null ? versions.find((r) => number(r.code) === from) : undefined) ?? versions[0];
    this.base.set(chosen ? number(chosen.code) : null);
    this.store.load((chosen?.['definition'] as WorkflowDraft | undefined) ?? STARTER);
    const next = this.nextNumber();
    this.versionNameAr.set(`الإصدار ${next}`);
    this.versionNameEn.set(`Version ${next}`);
    this.serverProblems.set(null);
    this.savedAs.set(null);
    this.errors.set([]);
    this.view.set('canvas');
  }

  protected startFrom(row: ConfigRow): void {
    const code = this.workflow();
    if (code) this.open(code, number(row.code));
  }

  protected dragStart(event: DragEvent, type: NodeType): void {
    event.dataTransfer?.setData(PALETTE_MIME, type);
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'copy';
  }

  protected add(type: NodeType): void {
    this.canvas()?.place(type);
  }

  protected arrange(): void {
    this.store.apply((def) => ({ ...def, layout: arrange(def) }));
  }

  protected zoomBy(step: number): void {
    this.zoom.update((z) => Math.min(2, Math.max(0.4, Math.round((z + step) * 10) / 10)));
  }

  protected deleteSelection(): void {
    const s = this.store.selection();
    if (!s) return;
    if (s.kind === 'node') {
      const node = this.store.definition().nodes.find((n) => n.code === s.code);
      if (node?.type === 'START') return;
      this.store.apply((def) => removeNode(def, s.code));
    } else {
      this.store.apply((def) => disconnect(def, s.from, s.outlet));
    }
    this.store.selection.set(null);
  }

  protected selectProblem(problem: Problem): void {
    if (problem.node) {
      this.view.set('canvas');
      this.store.selection.set({ kind: 'node', code: problem.node });
    }
  }

  protected problemText(problem: Problem): string {
    let text = this.i18n.translate(problem.key);
    for (const [k, v] of Object.entries(problem.params ?? {})) text = text.replace(`{${k}}`, String(v));
    return problem.node ? `${problem.node}: ${text}` : text;
  }

  protected showView(view: 'canvas' | 'json' | 'test'): void {
    if (this.view() === 'json' && view !== 'json' && !this.applyJson()) return;
    if (view === 'json') {
      this.jsonText.set(JSON.stringify(toDefinition(this.store.definition()), null, 2));
      this.jsonError.set(false);
    }
    this.view.set(view);
  }

  /** Takes the JSON as edited; false (and stays) when it is not a workflow. */
  private applyJson(): boolean {
    try {
      const value = JSON.parse(this.jsonText()) as WorkflowDraft;
      if (!Array.isArray(value?.nodes)) throw new Error('nodes');
      const current = JSON.stringify(toDefinition(this.store.definition()));
      if (JSON.stringify(value) !== current) this.store.apply(() => value);
      return true;
    } catch {
      this.jsonError.set(true);
      return false;
    }
  }

  private payload() {
    return {
      nameAr: this.versionNameAr().trim(),
      nameEn: this.versionNameEn().trim(),
      definition: toDefinition(this.store.definition()),
    };
  }

  /** Checks the version as publishing would (references, rules, numbering), saving nothing. */
  protected async check(): Promise<void> {
    if (this.view() === 'json' && !this.applyJson()) return;
    this.errors.set([]);
    this.busy.set(true);
    try {
      const result = await firstValueFrom(
        this.api.checkPackage({
          title: 'designer',
          items: [
            {
              entityType: 'wf.workflow-version',
              entityCode: `${this.workflow()}/${this.nextNumber()}`,
              operation: 'CREATE',
              payload: this.payload(),
            },
          ],
        }),
      );
      const messages = [...Object.entries(result.errors), ...Object.entries(result.items[0]?.errors ?? {})].flatMap(([field, list]) =>
        list.map((m) => `${field}: ${m}`),
      );
      this.serverProblems.set(messages);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }

  /** Adds the next version to the change set (published on approval, or at once where no approval is needed). */
  protected async save(): Promise<void> {
    if (this.view() === 'json' && !this.applyJson()) return;
    const code = `${this.workflow()}/${this.nextNumber()}`;
    this.errors.set([]);
    this.busy.set(true);
    try {
      await this.drafts.addChange('wf.workflow-version', code, 'CREATE', this.payload());
      this.store.saved();
      this.savedAs.set(code);
      this.serverProblems.set(null);
      this.versions.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }
}

function number(code: string): number {
  return Number(code.slice(code.lastIndexOf('/') + 1));
}
