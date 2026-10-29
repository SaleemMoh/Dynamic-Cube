import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { I18nService, TranslatePipe, localize } from '@dynamic-cube/shared';
import { BuilderReferences } from './builder-references';

interface WorkflowNode {
  code: string;
  type: string;
  nameAr?: string;
  nameEn?: string;
  assignment?: { role?: string | null; user?: string | null } | null;
  documentType?: string | null;
  ruleSet?: string | null;
  provider?: string | null;
  visitType?: string | null;
  outcome?: string | null;
}

/**
 * How the service's requests are processed and what they produce: read from the latest version of the service's own
 * workflow (the review tasks, the documents it issues, the rules it evaluates, the bills it raises, the outcomes), with
 * the way to the Workflow Designer to change it.
 */
@Component({
  selector: 'dc-builder-workflow-tab',
  imports: [MatButtonModule, MatIconModule, RouterLink, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-wt">
      @if (workflow(); as w) {
        <section class="dc-card">
          <header class="dc-wt__head">
            <div>
              <h2>{{ 'builder.wf.title' | dcTranslate }}</h2>
              <p class="dc-wt__muted" dir="ltr">{{ w.code }}{{ w.number ? '/' + w.number : '' }}</p>
            </div>
            <a matButton="filled" routerLink="/workflows/designer" [queryParams]="{ workflowCode: w.code }" data-testid="builder-open-designer">
              <mat-icon fontSet="material-symbols-outlined">design_services</mat-icon>
              {{ 'builder.wf.openDesigner' | dcTranslate }}
            </a>
          </header>
          <p class="dc-wt__muted">{{ 'builder.wf.hint' | dcTranslate }}</p>
        </section>
        @for (group of groups(); track group.key) {
          <section class="dc-card" [attr.data-testid]="'wf-' + group.key">
            <h3>
              <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{ group.icon }}</mat-icon>
              {{ 'builder.wf.' + group.key | dcTranslate }}
            </h3>
            @if (group.items.length) {
              <ul>
                @for (n of group.items; track n.code) {
                  <li>
                    <strong>{{ name(n) }}</strong>
                    @if (detail(n); as d) {
                      <span class="dc-wt__muted" dir="ltr"> · {{ d }}</span>
                    }
                  </li>
                }
              </ul>
            } @else {
              <p class="dc-wt__muted">{{ 'builder.wf.none.' + group.key | dcTranslate }}</p>
            }
          </section>
        }
      } @else {
        <p class="dc-wt__muted">{{ 'builder.wf.noWorkflow' | dcTranslate }}</p>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
      height: 100%;
      overflow: auto;
    }
    .dc-wt {
      display: grid;
      gap: 1rem;
      max-width: 56rem;
      padding: 0.75rem;
    }
    .dc-wt__head {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 1rem;
    }
    .dc-wt h2 {
      margin: 0;
      font-size: 17px;
    }
    .dc-wt h3 {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      margin: 0 0 0.5rem;
      font-size: 15px;
    }
    .dc-wt ul {
      margin: 0;
      padding-inline-start: 1.25rem;
    }
    .dc-wt__muted {
      margin: 0.25rem 0 0;
      color: var(--dga-text-3, #6c737f);
      font-size: 13px;
    }
  `,
})
export class WorkflowTab {
  private readonly refs = inject(BuilderReferences);
  private readonly i18n = inject(I18nService);
  readonly service = input<string | null>(null);

  protected readonly workflow = computed(() => this.refs.workflowOf(this.service()));

  protected readonly groups = computed(() => {
    const nodes = (this.workflow()?.definition.nodes ?? []) as unknown as WorkflowNode[];
    const of = (type: string) => nodes.filter((n) => n.type === type);
    return [
      { key: 'tasks', icon: 'person', items: of('HUMAN_TASK') },
      { key: 'documents', icon: 'description', items: of('DOCUMENT') },
      { key: 'decisions', icon: 'rule', items: of('DECISION') },
      { key: 'payments', icon: 'payments', items: of('PAYMENT') },
      { key: 'visits', icon: 'location_on', items: of('VISIT') },
      { key: 'outcomes', icon: 'flag', items: of('END') },
    ];
  });

  protected name(n: WorkflowNode): string {
    return localize({ ar: n.nameAr || n.code, en: n.nameEn || n.code }, this.i18n.language());
  }

  protected detail(n: WorkflowNode): string | null {
    switch (n.type) {
      case 'HUMAN_TASK':
        return n.assignment?.role ?? n.assignment?.user ?? null;
      case 'DOCUMENT':
        return n.documentType ?? null;
      case 'DECISION':
        return n.ruleSet ?? null;
      case 'PAYMENT':
        return n.provider ?? this.i18n.translate('designer.defaultProvider');
      case 'VISIT':
        return n.visitType ?? null;
      case 'END':
        return n.outcome ?? null;
      default:
        return null;
    }
  }
}
