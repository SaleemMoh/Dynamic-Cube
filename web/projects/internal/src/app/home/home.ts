import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import {
  AppsControlApi,
  AuthService,
  HostInfo,
  HostInfoService,
  I18nService,
  InboxBox,
  InboxCounts,
  LocalizePipe,
  SlaChip,
  TranslatePipe,
  WorkflowApi,
} from '@dynamic-cube/shared';
import { catchError, map, of } from 'rxjs';
import { StatusChip } from '../apps-control/change-sets/status-chip';
import { PrivilegesService } from '../apps-control/privileges.service';

type ApiStatus = { state: 'loading' } | { state: 'connected'; info: HostInfo } | { state: 'unavailable' };

interface Tile {
  key: string;
  icon: string;
  count: number;
  link: string;
  query: Record<string, string>;
  alert?: boolean;
  testId: string;
}

const BOXES: { box: InboxBox; count: keyof InboxCounts; icon: string }[] = [
  { box: 'MINE', count: 'mine', icon: 'assignment_ind' },
  { box: 'QUEUE', count: 'queue', icon: 'inbox' },
  { box: 'DISTRIBUTE', count: 'distribute', icon: 'call_split' },
  { box: 'ESCALATED', count: 'escalated', icon: 'running_with_errors' },
];

/**
 * The internal home page: what is waiting for the signed-in employee. Tasks from the Action Center (counts per box and
 * the first of their own), configuration change sets to review or still in their hands, and whether the server is
 * reachable. Each part shows only to those with the privilege for it.
 */
@Component({
  selector: 'dc-home',
  imports: [NgTemplateOutlet, MatIconModule, RouterLink, LocalizePipe, SlaChip, StatusChip, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-home__hero">
      <div>
        <h1 data-testid="home-greeting">{{ greeting() }}</h1>
        <p class="dc-home__sub">
          <span data-testid="home-date">{{ today() }}</span> · {{ 'app.description' | dcTranslate }}
        </p>
      </div>
      @switch (status().state) {
        @case ('connected') {
          @if (status(); as s) {
            <span class="dc-home__server dc-home__server--ok" data-testid="home-server">
              <span class="dc-home__dot"></span>{{ 'home.api.connected' | dcTranslate }}
              @if (s.state === 'connected') {
                <bdi dir="ltr" class="dc-home__version">{{ s.info.version.split('+')[0] }}</bdi>
              }
            </span>
          }
        }
        @case ('unavailable') {
          <span class="dc-home__server dc-home__server--bad" data-testid="home-server"
            ><span class="dc-home__dot"></span>{{ 'home.api.unavailable' | dcTranslate }}</span
          >
        }
      }
    </header>

    @if (canTasks()) {
      <section class="dc-home__section" aria-labelledby="dc-home-tasks">
        <div class="dc-home__head">
          <h2 id="dc-home-tasks">{{ 'home.tasks' | dcTranslate }}</h2>
          <a class="dc-link" routerLink="/action-center">{{ 'home.allTasks' | dcTranslate }}</a>
        </div>
        <div class="dc-home__tiles">
          @for (t of taskTiles(); track t.key) {
            <ng-container *ngTemplateOutlet="tile; context: { $implicit: t }" />
          }
        </div>
        <div class="dc-card dc-home__list">
          <h3>{{ 'home.myTasks' | dcTranslate }}</h3>
          @if (myTasks.value()?.items; as items) {
            @if (items.length) {
              <ul>
                @for (t of items; track t.id) {
                  <li>
                    <a class="dc-link" [routerLink]="['/action-center/tasks', t.id]" [attr.data-testid]="'home-task-' + t.subjectNumber">
                      <bdi dir="ltr">{{ t.subjectNumber }}</bdi> · {{ { ar: t.nameAr, en: t.nameEn } | dcLocalize }}
                    </a>
                    <span class="dc-muted">{{ { ar: t.partyNameAr, en: t.partyNameEn } | dcLocalize }}</span>
                    <dc-sla-chip [sla]="t.sla" [dueAt]="t.dueAt" />
                  </li>
                }
              </ul>
            } @else {
              <p class="dc-muted">{{ 'home.noTasks' | dcTranslate }}</p>
            }
          }
        </div>
      </section>
    }

    @if (canChangeSets()) {
      <section class="dc-home__section" aria-labelledby="dc-home-changes">
        <div class="dc-home__head">
          <h2 id="dc-home-changes">{{ 'home.changes' | dcTranslate }}</h2>
          <a class="dc-link" routerLink="/apps-control/change-sets">{{ 'home.allChanges' | dcTranslate }}</a>
        </div>
        <div class="dc-home__tiles">
          @for (t of changeTiles(); track t.key) {
            <ng-container *ngTemplateOutlet="tile; context: { $implicit: t }" />
          }
        </div>
        @let list = canApprove() ? toReview() : mine();
        <div class="dc-card dc-home__list">
          <h3>{{ (canApprove() ? 'home.waitingReview' : 'home.inYourHands') | dcTranslate }}</h3>
          @if (list.length) {
            <ul>
              @for (c of list.slice(0, 5); track c.id) {
                <li>
                  <a class="dc-link" [routerLink]="['/apps-control/change-sets', c.id]" [attr.data-testid]="'home-change-' + c.id">{{ c.title }}</a>
                  <span class="dc-muted"
                    >{{ 'area.' + c.area | dcTranslate }} · {{ c.itemCount }} {{ 'home.items' | dcTranslate }} · <bdi dir="ltr">{{ c.createdBy }}</bdi></span
                  >
                  <dc-status-chip [status]="c.status" />
                </li>
              }
            </ul>
          } @else {
            <p class="dc-muted">{{ (canApprove() ? 'home.nothingToReview' : 'home.nothingInHands') | dcTranslate }}</p>
          }
        </div>
      </section>
    }

    @if (privileges.loaded() && !canTasks() && !canChangeSets()) {
      <section class="dc-card dc-home__welcome">
        <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">waving_hand</mat-icon>
        <p>{{ 'home.welcome' | dcTranslate }}</p>
      </section>
    }

    <ng-template #tile let-t>
      <a
        class="dc-home__tile"
        [class.dc-home__tile--alert]="t.alert && t.count > 0"
        [routerLink]="t.link"
        [queryParams]="t.query"
        [attr.data-testid]="t.testId"
      >
        <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{ t.icon }}</mat-icon>
        <span class="dc-home__count">{{ t.count }}</span>
        <span class="dc-home__label">{{ t.key | dcTranslate }}</span>
      </a>
    </ng-template>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .dc-home__hero {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      justify-content: space-between;
      gap: 1rem;
    }
    .dc-home__hero h1 {
      margin: 0;
    }
    .dc-home__sub {
      margin: 0.25rem 0 0;
      color: var(--dga-text-3, #6c737f);
    }
    .dc-home__server {
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
      padding: 0.25rem 0.75rem;
      border-radius: 999px;
      font-size: 13px;
      font-weight: 600;
    }
    .dc-home__server--ok {
      background: var(--dynamic-cube-green-50, #e8f3ed);
      color: var(--dynamic-cube-green, #136f40);
    }
    .dc-home__server--bad {
      background: #fef3f2;
      color: #b42318;
    }
    .dc-home__dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: currentColor;
    }
    .dc-home__version {
      font-family: ui-monospace, monospace;
      font-weight: 400;
    }
    .dc-home__section {
      display: grid;
      gap: 0.75rem;
    }
    .dc-home__head {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: 1rem;
    }
    .dc-home__head h2 {
      margin: 0;
      font-size: 18px;
    }
    .dc-home__tiles {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(12rem, 1fr));
      gap: 0.75rem;
    }
    .dc-home__tile {
      display: grid;
      grid-template-columns: auto 1fr;
      grid-template-areas: 'icon count' 'icon label';
      align-items: center;
      column-gap: 0.75rem;
      padding: 1rem 1.25rem;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 12px;
      background: #fff;
      color: inherit;
      text-decoration: none;
      transition:
        border-color 0.15s,
        box-shadow 0.15s;
    }
    .dc-home__tile:hover,
    .dc-home__tile:focus-visible {
      border-color: var(--dynamic-cube-gold, #ca9f45);
      box-shadow: 0 2px 8px rgba(16, 24, 40, 0.08);
    }
    .dc-home__tile mat-icon {
      grid-area: icon;
      width: 40px;
      height: 40px;
      padding: 8px;
      box-sizing: border-box;
      border-radius: 10px;
      background: var(--dynamic-cube-gold-50, #faf7f0);
      color: var(--dynamic-cube-gold-700, #8a6a26);
    }
    .dc-home__count {
      grid-area: count;
      font-size: 26px;
      font-weight: 700;
      line-height: 1.1;
    }
    .dc-home__label {
      grid-area: label;
      color: var(--dga-text-3, #6c737f);
      font-size: 14px;
    }
    .dc-home__tile--alert {
      border-color: #fda29b;
    }
    .dc-home__tile--alert mat-icon {
      background: #fef3f2;
      color: #b42318;
    }
    .dc-home__tile--alert .dc-home__count {
      color: #b42318;
    }
    .dc-home__list h3 {
      margin: 0 0 0.5rem;
      font-size: 15px;
    }
    .dc-home__list ul {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .dc-home__list li {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem 1rem;
      padding: 0.625rem 0;
      border-top: 1px solid var(--dga-gray-100, #f3f4f6);
    }
    .dc-home__list li:first-child {
      border-top: 0;
    }
    .dc-home__list li > :nth-child(2) {
      flex: 1;
      min-width: 10rem;
    }
    .dc-home__list p {
      margin: 0;
    }
    .dc-home__welcome {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    .dc-home__welcome p {
      margin: 0;
    }
  `,
})
export class Home {
  private readonly workflow = inject(WorkflowApi);
  private readonly appsControl = inject(AppsControlApi);
  protected readonly privileges = inject(PrivilegesService);
  protected readonly i18n = inject(I18nService);
  private readonly auth = inject(AuthService);
  /** Today in the Gregorian and the Hijri (Umm al-Qura) calendars. */
  protected readonly today = computed(() => {
    const now = new Date();
    const ar = this.i18n.language() === 'ar';
    const gregorian = new Intl.DateTimeFormat(ar ? 'ar-SA-u-ca-gregory-nu-latn' : 'en-GB', { dateStyle: 'full' }).format(now);
    const hijri = new Intl.DateTimeFormat(ar ? 'ar-SA-u-ca-islamic-umalqura-nu-latn' : 'en-GB-u-ca-islamic-umalqura', { dateStyle: 'long' }).format(now);
    return `${gregorian} · ${hijri}`;
  });
  protected readonly greeting = computed(() => {
    const name = this.auth.user()?.name;
    return name ? this.i18n.translate('home.greetingName').replace('{name}', name) : this.i18n.translate('home.greeting');
  });

  protected readonly canTasks = computed(() => this.privileges.can('action-center', 'VIEW'));
  protected readonly canChangeSets = computed(() => this.privileges.can('apps-control.change-sets', 'VIEW'));
  protected readonly canApprove = computed(() => this.privileges.can('apps-control.change-sets', 'APPROVE'));

  protected readonly status = toSignal(
    inject(HostInfoService)
      .get()
      .pipe(
        map((info): ApiStatus => ({ state: 'connected', info })),
        catchError(() => of<ApiStatus>({ state: 'unavailable' })),
      ),
    { initialValue: { state: 'loading' } as ApiStatus },
  );

  // Loaded only for those who may see them (no params → nothing is requested).
  protected readonly counts = rxResource({ params: () => (this.canTasks() ? true : undefined), stream: () => this.workflow.counts() });
  protected readonly myTasks = rxResource({ params: () => (this.canTasks() ? true : undefined), stream: () => this.workflow.inbox('MINE', null, 0, 5) });
  private readonly submitted = rxResource({
    params: () => (this.canApprove() ? true : undefined),
    stream: () => this.appsControl.changeSets({ status: 'SUBMITTED' }),
  });
  private readonly own = rxResource({ params: () => (this.canChangeSets() ? true : undefined), stream: () => this.appsControl.changeSets({ mine: true }) });

  protected readonly toReview = computed(() => (this.submitted.value() ?? []).filter((c) => !c.isMine));
  /** Still to be finished by the user: drafts, and changes the checker sent back. */
  protected readonly mine = computed(() => (this.own.value() ?? []).filter((c) => c.status === 'DRAFT' || c.status === 'REJECTED'));

  protected readonly taskTiles = computed<Tile[]>(() => {
    const counts = this.counts.value();
    return BOXES.map((b) => ({
      key: 'inbox.' + b.box,
      icon: b.icon,
      count: counts?.[b.count] ?? 0,
      link: '/action-center',
      query: { box: b.box },
      alert: b.box === 'ESCALATED',
      testId: 'home-box-' + b.box,
    }));
  });

  protected readonly changeTiles = computed<Tile[]>(() => {
    const own = this.own.value() ?? [];
    const tiles: Tile[] = [];
    if (this.canApprove()) {
      tiles.push({
        key: 'home.toReview',
        icon: 'fact_check',
        count: this.toReview().length,
        link: '/apps-control/change-sets',
        query: { filter: 'TO_REVIEW' },
        testId: 'home-changes-review',
      });
    }
    tiles.push(
      {
        key: 'home.drafts',
        icon: 'edit_note',
        count: own.filter((c) => c.status === 'DRAFT').length,
        link: '/apps-control/change-sets',
        query: { filter: 'MINE' },
        testId: 'home-changes-drafts',
      },
      {
        key: 'home.returned',
        icon: 'undo',
        count: own.filter((c) => c.status === 'REJECTED').length,
        link: '/apps-control/change-sets',
        query: { filter: 'MINE' },
        alert: true,
        testId: 'home-changes-returned',
      },
      {
        key: 'home.submitted',
        icon: 'hourglass_top',
        count: own.filter((c) => c.status === 'SUBMITTED').length,
        link: '/apps-control/change-sets',
        query: { filter: 'MINE' },
        testId: 'home-changes-submitted',
      },
    );
    return tiles;
  });
}
