import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { InstanceView, LocalizePipe, SlaChip, TimelineItem, TranslatePipe } from '@dynamic-cube/shared';

/** A request's workflow: status and deadline, its steps (open and done) and everything that happened, notes included. */
@Component({
  selector: 'dc-workflow-card',
  imports: [DatePipe, RouterLink, LocalizePipe, SlaChip, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let i = instance();
    <section class="dc-card dc-workflow" data-testid="workflow-card">
      <header class="dc-workflow__header">
        <h2>{{ 'workflow.title' | dcTranslate }}</h2>
        <span
          class="dc-badge"
          [class.dc-badge--info]="i.status === 'RUNNING'"
          [class.dc-badge--warning]="i.status === 'WAITING_CUSTOMER'"
          [class.dc-badge--success]="i.status === 'COMPLETED'"
          data-testid="instance-status"
        >
          {{ 'instance.' + (i.outcome ?? i.status) | dcTranslate }}
        </span>
      </header>
      <p class="dc-muted">
        <span dir="ltr" class="dc-code">{{ i.workflowCode }}/{{ i.versionNumber }}</span>
        · {{ 'workflow.started' | dcTranslate }}
        <span dir="ltr">{{ i.startedAt | date: 'yyyy-MM-dd' }}</span>
        @if (i.status !== 'COMPLETED' && i.status !== 'CANCELLED') {
          · <dc-sla-chip [sla]="i.sla" [dueAt]="i.dueAt" />
        }
      </p>
      <h3>{{ 'workflow.steps' | dcTranslate }}</h3>
      <ul class="dc-workflow__steps">
        @for (t of i.tasks; track t.id) {
          <li [class.dc-current]="t.id === currentTask()">
            <span class="dc-badge" [class.dc-badge--info]="t.status === 'OPEN'" [class.dc-badge--success]="t.status === 'COMPLETED'">{{
              'taskStatus.' + t.status | dcTranslate
            }}</span>
            @if (linkTasks() && t.status === 'OPEN') {
              <a class="dc-link" [routerLink]="['/action-center/tasks', t.id]">{{ { ar: t.nameAr, en: t.nameEn } | dcLocalize }}</a>
            } @else {
              <span>{{ { ar: t.nameAr, en: t.nameEn } | dcLocalize }}</span>
            }
            <span class="dc-muted">{{
              t.status === 'COMPLETED'
                ? (t.completedBy ?? '')
                : t.assignee
                  ? ({ ar: t.assigneeNameAr ?? t.assignee, en: t.assigneeNameEn ?? t.assignee } | dcLocalize)
                  : ('tasks.unassigned' | dcTranslate)
            }}</span>
          </li>
        }
      </ul>
      <h3>{{ 'requests.timeline' | dcTranslate }}</h3>
      <ol class="dc-timeline" data-testid="workflow-timeline">
        @for (e of i.timeline; track $index) {
          <li [class.dc-timeline__note]="e.type === 'NOTE'">
            <strong>{{ label(e) | dcTranslate }}</strong>
            @if (e.actionNameAr || e.nodeNameAr) {
              <span>{{
                e.actionNameAr
                  ? ({ ar: e.actionNameAr, en: e.actionNameEn ?? '' } | dcLocalize)
                  : ({ ar: e.nodeNameAr ?? '', en: e.nodeNameEn ?? '' } | dcLocalize)
              }}</span>
            }
            <span class="dc-muted"
              ><span dir="ltr">{{ e.at | date: 'yyyy-MM-dd HH:mm' }}</span> ·
              @switch (e.actor) {
                @case ('system') {
                  {{ 'workflow.system' | dcTranslate }}
                }
                @case ('applicant') {
                  {{ 'workflow.applicant' | dcTranslate }}
                }
                @default {
                  {{ { ar: e.actorNameAr, en: e.actorNameEn } | dcLocalize }}
                }
              }
              @if (e.onBehalfOf) {
                ({{ 'workflow.onBehalfOf' | dcTranslate }} {{ e.onBehalfOf }})
              }
              @if (e.target) {
                → {{ { ar: e.targetNameAr ?? e.target, en: e.targetNameEn ?? e.target } | dcLocalize }}
              }
            </span>
            @if (e.comment) {
              <span dir="auto" class="dc-timeline__comment">
                {{ e.comment }}
                @if (e.visibleToApplicant) {
                  <span class="dc-badge">{{ 'workflow.applicantSees' | dcTranslate }}</span>
                }
              </span>
            }
          </li>
        }
      </ol>
    </section>
  `,
  styles: `
    .dc-workflow h2,
    .dc-workflow h3 {
      margin: 0;
      font-size: 18px;
    }
    .dc-workflow h3 {
      margin-top: 1rem;
      font-size: 16px;
    }
    .dc-workflow__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
    }
    .dc-workflow__steps {
      display: grid;
      gap: 0.5rem;
      margin: 0.5rem 0 0;
      padding: 0;
      list-style: none;
    }
    .dc-workflow__steps li {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
    }
    .dc-current {
      font-weight: 600;
    }
    .dc-timeline {
      display: grid;
      gap: 0.75rem;
      margin: 0.5rem 0 0;
      padding-inline-start: 1.25rem;
    }
    .dc-timeline li {
      display: grid;
      gap: 0.125rem;
    }
    .dc-timeline__note {
      list-style: '✎  ';
    }
    .dc-timeline__comment {
      white-space: pre-line;
    }
  `,
})
export class WorkflowCard {
  readonly instance = input.required<InstanceView>();
  /** The task being worked on, highlighted in the steps. */
  readonly currentTask = input<string | null>(null);
  /** Links open steps to their task page. */
  readonly linkTasks = input(false);

  protected label(e: TimelineItem): string {
    return 'wfEvent.' + e.type;
  }
}
