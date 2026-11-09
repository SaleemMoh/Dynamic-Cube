import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  AppsControlApi,
  ConfigRow,
  I18nService,
  LocalizePipe,
  LocalizedText,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { catchError, firstValueFrom, forkJoin, of } from 'rxjs';
import { ConfigEditor, ConfigEditorData } from '../config/config-editor';
import { CONFIG_SECTIONS, ConfigType } from '../config/config-types';
import { PrivilegesService } from '../privileges.service';
import {
  CompanyNode,
  OrganizationNode,
  buildStructure,
  matchingOrganizations,
} from './structure-tree';

const RESOURCE = 'apps-control.installation';

const TRANSLATIONS = {
  ar: {
    'structure.title': 'الجهات والشركات',
    'structure.hint':
      'الجهات (وما يتبعها من جهات)، وشركات كل جهة، وفروع كل شركة، والتطبيقات التي تستخدمها. كل تعديل يمر بطلب تغيير.',
    'structure.search': 'بحث بالاسم أو الرمز',
    'structure.expandAll': 'فتح الكل',
    'structure.collapseAll': 'طي الكل',
    'structure.empty': 'لا توجد جهات.',
    'structure.addOrganization': 'جهة جديدة',
    'structure.addChild': 'جهة تابعة',
    'structure.addCompany': 'شركة جديدة',
    'structure.addBranch': 'فرع جديد',
    'structure.edit': 'تعديل',
    'structure.view': 'عرض',
    'structure.companies': 'شركات',
    'structure.branches': 'الفروع',
    'structure.apps': 'التطبيقات',
    'structure.noApps': 'لا تطبيقات',
    'structure.inactive': 'غير فعّالة',
    'structure.default': 'افتراضي',
  },
  en: {
    'structure.title': 'Organizations & companies',
    'structure.hint':
      'Organizations (and those that belong to them), the companies of each, the branches of each company and the apps it uses. Every change goes through a change set.',
    'structure.search': 'Search by name or code',
    'structure.expandAll': 'Expand all',
    'structure.collapseAll': 'Collapse all',
    'structure.empty': 'No organizations.',
    'structure.addOrganization': 'New organization',
    'structure.addChild': 'Sub-organization',
    'structure.addCompany': 'New company',
    'structure.addBranch': 'New branch',
    'structure.edit': 'Edit',
    'structure.view': 'View',
    'structure.companies': 'companies',
    'structure.branches': 'Branches',
    'structure.apps': 'Apps',
    'structure.noApps': 'No apps',
    'structure.inactive': 'Inactive',
    'structure.default': 'default',
  },
};

/**
 * Organizations → sub-organizations → companies → branches, with the apps of each company, on one screen like the
 * legacy Apps Control's instance and company setup. Adding and editing open the usual forms; changes go into a change set.
 */
@Component({
  selector: 'dc-structure-page',
  imports: [
    NgTemplateOutlet,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTooltipModule,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-structure__header">
      <div>
        <h1>{{ 'structure.title' | dcTranslate }}</h1>
        <p class="dc-muted">{{ 'structure.hint' | dcTranslate }}</p>
      </div>
      @if (canEdit()) {
        <button
          matButton="filled"
          type="button"
          (click)="add('apps.entity')"
          data-testid="structure-add-organization"
        >
          <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
          {{ 'structure.addOrganization' | dcTranslate }}
        </button>
      }
    </header>

    <div class="dc-structure__tools">
      <mat-form-field subscriptSizing="dynamic" class="dc-structure__search">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input
          matInput
          [ngModel]="search()"
          (ngModelChange)="search.set($event)"
          [placeholder]="'structure.search' | dcTranslate"
          data-testid="structure-search"
        />
      </mat-form-field>
      <button matButton type="button" (click)="expandAll()">
        {{ 'structure.expandAll' | dcTranslate }}
      </button>
      <button matButton type="button" (click)="collapseAll()">
        {{ 'structure.collapseAll' | dcTranslate }}
      </button>
    </div>

    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (error of errors(); track $index) {
          <li dir="auto">{{ error }}</li>
        }
      </ul>
    }

    @if (!data.hasValue()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (!visibleRoots().length) {
      <p class="dc-muted">{{ 'structure.empty' | dcTranslate }}</p>
    } @else {
      <ul class="dc-structure__tree" role="tree" data-testid="structure-tree">
        @for (node of visibleRoots(); track node.organization.code) {
          <ng-container
            *ngTemplateOutlet="organizationTpl; context: { $implicit: node, depth: 0 }"
          />
        }
      </ul>
    }

    <ng-template #organizationTpl let-node let-depth="depth">
      <li
        role="treeitem"
        aria-selected="false"
        [attr.aria-expanded]="isOpen(node)"
        [attr.data-testid]="'org-' + node.organization.code"
      >
        <div class="dc-structure__row dc-structure__row--org">
          <button
            matIconButton
            type="button"
            (click)="toggle(node.organization.code)"
            [attr.aria-label]="name(node.organization) | dcLocalize"
          >
            <mat-icon fontSet="material-symbols-outlined">{{
              isOpen(node) ? 'expand_more' : 'chevron_right'
            }}</mat-icon>
          </button>
          <mat-icon fontSet="material-symbols-outlined" class="dc-structure__kind">domain</mat-icon>
          <span class="dc-structure__name">{{ name(node.organization) | dcLocalize }}</span>
          <span class="mono dc-muted" dir="ltr">{{ node.organization.code }}</span>
          @if (node.organization['shortCode']; as short) {
            <span class="dc-structure__chip" dir="ltr">{{ short }}</span>
          }
          @if (!node.organization.isActive) {
            <span class="dc-structure__chip dc-structure__chip--off">{{
              'structure.inactive' | dcTranslate
            }}</span>
          }
          <span class="dc-muted dc-structure__count"
            >{{ node.companies.length }} {{ 'structure.companies' | dcTranslate }}</span
          >
          <span class="dc-structure__actions">
            <button
              matIconButton
              type="button"
              (click)="edit('apps.entity', node.organization)"
              [matTooltip]="(canEdit() ? 'structure.edit' : 'structure.view') | dcTranslate"
            >
              <mat-icon fontSet="material-symbols-outlined">{{
                canEdit() ? 'edit' : 'visibility'
              }}</mat-icon>
            </button>
            @if (canEdit()) {
              <button
                matIconButton
                type="button"
                (click)="add('apps.entity', { parentCode: node.organization.code })"
                [matTooltip]="'structure.addChild' | dcTranslate"
              >
                <mat-icon fontSet="material-symbols-outlined">account_tree</mat-icon>
              </button>
              <button
                matIconButton
                type="button"
                (click)="add('apps.company', { entityCode: node.organization.code })"
                [matTooltip]="'structure.addCompany' | dcTranslate"
              >
                <mat-icon fontSet="material-symbols-outlined">add_business</mat-icon>
              </button>
            }
          </span>
        </div>
        @if (isOpen(node)) {
          <ul role="group">
            @for (company of node.companies; track company.key) {
              <ng-container *ngTemplateOutlet="companyTpl; context: { $implicit: company }" />
            }
            @for (child of node.children; track child.organization.code) {
              <ng-container
                *ngTemplateOutlet="organizationTpl; context: { $implicit: child, depth: depth + 1 }"
              />
            }
          </ul>
        }
      </li>
    </ng-template>

    <ng-template #companyTpl let-company>
      <li role="treeitem" aria-selected="false" [attr.data-testid]="'company-' + company.key">
        <div class="dc-structure__row">
          <span class="dc-structure__spacer"></span>
          <mat-icon fontSet="material-symbols-outlined" class="dc-structure__kind"
            >apartment</mat-icon
          >
          <span class="dc-structure__name">{{ name(company.company) | dcLocalize }}</span>
          <span class="mono dc-muted" dir="ltr">{{ company.key }}</span>
          @if (company.company['abbreviation']; as abbreviation) {
            <span class="dc-structure__chip" dir="ltr">{{ abbreviation }}</span>
          }
          @if (!company.company.isActive) {
            <span class="dc-structure__chip dc-structure__chip--off">{{
              'structure.inactive' | dcTranslate
            }}</span>
          }
          <span class="dc-structure__actions">
            <button
              matIconButton
              type="button"
              (click)="edit('apps.company', company.company)"
              [matTooltip]="(canEdit() ? 'structure.edit' : 'structure.view') | dcTranslate"
            >
              <mat-icon fontSet="material-symbols-outlined">{{
                canEdit() ? 'edit' : 'visibility'
              }}</mat-icon>
            </button>
            @if (canEdit()) {
              <button
                matIconButton
                type="button"
                (click)="add('apps.branch', { companyCode: company.key })"
                [matTooltip]="'structure.addBranch' | dcTranslate"
              >
                <mat-icon fontSet="material-symbols-outlined">add_location_alt</mat-icon>
              </button>
            }
          </span>
        </div>
        <div class="dc-structure__details">
          <div>
            <span class="dc-muted">{{ 'structure.branches' | dcTranslate }}:</span>
            @for (branch of company.branches; track branch.code; let last = $last) {
              <button
                type="button"
                class="dc-structure__link"
                (click)="edit('apps.branch', branch)"
              >
                {{ name(branch) | dcLocalize }}
              </button>
              @if (branch['isDefault']) {
                <span class="dc-muted"> ({{ 'structure.default' | dcTranslate }})</span>
              }
              {{ last ? '' : '، ' }}
            }
          </div>
          <div>
            <span class="dc-muted">{{ 'structure.apps' | dcTranslate }}:</span>
            @for (app of company.apps; track app.code; let last = $last) {
              {{ name(app) | dcLocalize }}{{ last ? '' : '، ' }}
            } @empty {
              <span class="dc-muted">{{ 'structure.noApps' | dcTranslate }}</span>
            }
          </div>
        </div>
      </li>
    </ng-template>
  `,
  styles: `
    .dc-structure__header {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      align-items: flex-start;
      justify-content: space-between;
    }
    .dc-structure__header h1 {
      margin: 0 0 4px;
    }
    .dc-structure__tools {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      align-items: center;
      margin: 12px 0;
    }
    .dc-structure__search {
      flex: 1 1 260px;
      max-width: 420px;
    }
    .dc-structure__tree,
    .dc-structure__tree ul {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .dc-structure__tree ul {
      padding-inline-start: 24px;
      border-inline-start: 1px dashed var(--dga-gray-300);
      margin-inline-start: 20px;
    }
    .dc-structure__row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
      min-height: 40px;
    }
    .dc-structure__row--org .dc-structure__name {
      font-weight: 600;
    }
    .dc-structure__spacer {
      width: 40px;
    }
    .dc-structure__kind {
      color: var(--dga-gray-600);
    }
    .dc-structure__chip {
      font-size: 12px;
      padding: 1px 8px;
      border-radius: 10px;
      background: var(--dga-gray-100);
    }
    .dc-structure__chip--off {
      background: var(--dga-warning-50, #fffaeb);
      color: var(--dga-warning-700, #b54708);
    }
    .dc-structure__count {
      font-size: 13px;
    }
    .dc-structure__actions {
      margin-inline-start: auto;
      display: inline-flex;
    }
    .dc-structure__details {
      padding-inline-start: 46px;
      padding-bottom: 8px;
      font-size: 14px;
      display: grid;
      gap: 2px;
    }
    .dc-structure__link {
      background: none;
      border: none;
      padding: 0;
      color: var(--dga-primary-600, inherit);
      cursor: pointer;
      font: inherit;
      text-decoration: underline;
    }
  `,
})
export class StructurePage {
  private readonly api = inject(AppsControlApi);
  private readonly dialog = inject(MatDialog);
  private readonly privileges = inject(PrivilegesService);
  protected readonly search = signal('');
  protected readonly open = signal(new Set<string>());
  protected readonly errors = signal<string[]>([]);
  protected readonly canEdit = computed(() => this.privileges.can(RESOURCE, 'EDIT'));

  protected readonly data = rxResource({
    stream: () => {
      const list = (type: string) =>
        this.api.list(type).pipe(catchError(() => of([] as ConfigRow[])));
      return forkJoin({
        organizations: list('apps.entity'),
        companies: list('apps.company'),
        branches: list('apps.branch'),
        apps: list('apps.business-app'),
      });
    },
  });
  protected readonly roots = computed(() => {
    const d = this.data.value();
    return d ? buildStructure(d.organizations, d.companies, d.branches, d.apps) : [];
  });
  private readonly matching = computed(() => matchingOrganizations(this.roots(), this.search()));
  protected readonly visibleRoots = computed(() =>
    this.search().trim()
      ? this.roots().filter((r) => this.matching().has(r.organization.code))
      : this.roots(),
  );

  constructor() {
    inject(I18nService).register(TRANSLATIONS);
  }

  protected name(row: ConfigRow): LocalizedText {
    return { ar: row.nameAr, en: row.nameEn };
  }

  protected isOpen(node: OrganizationNode): boolean {
    return this.search().trim()
      ? this.matching().has(node.organization.code)
      : this.open().has(node.organization.code);
  }

  protected toggle(code: string): void {
    this.open.update((open) => {
      const next = new Set(open);
      if (!next.delete(code)) next.add(code);
      return next;
    });
  }

  protected expandAll(): void {
    const codes: string[] = [];
    const visit = (n: OrganizationNode) => {
      codes.push(n.organization.code);
      n.children.forEach(visit);
    };
    this.roots().forEach(visit);
    this.open.set(new Set(codes));
  }

  protected collapseAll(): void {
    this.open.set(new Set());
  }

  protected async edit(
    entityType: string,
    listed: ConfigRow | CompanyNode['company'],
  ): Promise<void> {
    this.errors.set([]);
    let row: ConfigRow;
    try {
      row = await firstValueFrom(this.api.get(entityType, listed.code));
    } catch (error) {
      this.errors.set(problemMessages(error));
      return;
    }
    await this.openEditor(entityType, row);
  }

  protected add(entityType: string, defaults?: Record<string, unknown>): Promise<void> {
    return this.openEditor(entityType, null, defaults);
  }

  private async openEditor(
    entityType: string,
    row: ConfigRow | null,
    defaults?: Record<string, unknown>,
  ): Promise<void> {
    const type = typeOf(entityType);
    if (!type) return;
    const saved = await firstValueFrom(
      this.dialog
        .open<unknown, ConfigEditorData, boolean>(
          type.editor ? await type.editor() : ConfigEditor,
          {
            data: { type, row, defaults },
            autoFocus: 'first-tabbable',
            width: '52rem',
            maxWidth: '96vw',
          },
        )
        .afterClosed(),
    );
    if (saved) this.data.reload();
  }
}

function typeOf(entityType: string): ConfigType | undefined {
  return CONFIG_SECTIONS.flatMap((s) => s.types).find((t) => t.entityType === entityType);
}
