import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { AppsControlApi, BusinessAppContext, ConfigRow, LocalizePipe, problemMessages, ServicesApi, TranslatePipe, VersionDefinition } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { DraftsService } from '../apps-control/drafts.service';
import { PrivilegesService } from '../apps-control/privileges.service';
import { VersionPreview } from './version-preview';
import { SubmitButton } from '../apps-control/submit-button';

const STARTER: VersionDefinition = {
  levels: [],
  steps: [
    { code: 'DETAILS', type: 'FORM', titleAr: 'البيانات', titleEn: 'Details', sections: [{ code: 'MAIN', titleAr: 'البيانات الأساسية', titleEn: 'Main data', questions: [] }] },
    { code: 'REVIEW', type: 'REVIEW', titleAr: 'المراجعة', titleEn: 'Review', sections: [] },
  ],
  eligibility: [],
  validations: [],
  fees: [],
};

/**
 * Service versions. A new version starts from the latest one's definition (JSON until the visual builder arrives), can be
 * previewed as the applicant's wizard, and goes into a change set; published versions never change, they are retired.
 */
@Component({
  selector: 'dc-version-list',
  imports: [SubmitButton, DatePipe, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule, MatTableModule, MatTooltipModule, RouterLink, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header dc-versions__header">
      <h1>{{ 'versions.title' | dcTranslate }}</h1>
      <a matButton="filled" routerLink="/services/builder" [queryParams]="{ service: service() ?? undefined }" data-testid="open-builder">
        <mat-icon fontSet="material-symbols-outlined">construction</mat-icon>
        {{ 'builder.open' | dcTranslate }}
      </a>
    </header>
    <p class="dc-muted">{{ 'versions.hint' | dcTranslate }}</p>

    @if (canCreate()) {
      <section class="dc-card dc-authoring">
        <h2>{{ 'versions.new' | dcTranslate }}</h2>
        <label class="dc-field-label" for="dc-version-service">{{ 'versions.service' | dcTranslate }}</label>
        <mat-form-field subscriptSizing="dynamic">
          <mat-select id="dc-version-service" [ngModel]="service()" (ngModelChange)="chooseService($event)" data-testid="version-service">
            @for (s of services.value() ?? []; track s.code) {
              <mat-option [value]="s.code">{{ { ar: s.nameAr, en: s.nameEn } | dcLocalize }} ({{ s.code }})</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (service()) {
          <p dir="ltr" class="dc-code">{{ service() }}/{{ nextNumber() }}</p>
          <label class="dc-field-label" for="dc-version-json">{{ 'versions.definition' | dcTranslate }}</label>
          <mat-form-field subscriptSizing="dynamic">
            <textarea id="dc-version-json" matInput rows="18" dir="ltr" class="dc-json" [(ngModel)]="definitionText" data-testid="version-definition"></textarea>
          </mat-form-field>
          @if (errors().length) {
            <ul class="dc-errors" role="alert">
              @for (e of errors(); track $index) {
                <li dir="ltr">{{ e }}</li>
              }
            </ul>
          }
          <div class="dc-authoring__actions">
            <button matButton="outlined" (click)="previewDraft()" data-testid="preview-definition">
              <mat-icon fontSet="material-symbols-outlined">visibility</mat-icon>
              {{ 'versions.preview' | dcTranslate }}
            </button>
            <dc-submit-button [entityType]="'svc.service-version'" (submitted)="addVersion(true)" />
            <button matButton="filled" (click)="addVersion()" data-testid="add-version">
              <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
              {{ drafts.saveLabel('svc.service-version') | dcTranslate }}
            </button>
          </div>
        }
      </section>
    }

    <table mat-table [dataSource]="rows()" class="dc-table">
      <ng-container matColumnDef="code">
        <th mat-header-cell *matHeaderCellDef>{{ 'versions.version' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr" class="dc-code">{{ row.code }}</td>
      </ng-container>
      <ng-container matColumnDef="name">
        <th mat-header-cell *matHeaderCellDef>{{ 'config.name' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row">{{ { ar: row.nameAr, en: row.nameEn } | dcLocalize }}</td>
      </ng-container>
      <ng-container matColumnDef="published">
        <th mat-header-cell *matHeaderCellDef>{{ 'versions.published' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let row" dir="ltr">{{ row['publishedAt'] | date: 'yyyy-MM-dd HH:mm' }}</td>
      </ng-container>
      <ng-container matColumnDef="state">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let row">
          @if (row['isRetired']) {
            <span class="dc-badge">{{ 'versions.retired' | dcTranslate }}</span>
          }
        </td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let row" class="dc-table__actions">
          <button matIconButton (click)="previewPublished(row)" [matTooltip]="'versions.preview' | dcTranslate" [attr.data-testid]="'preview-' + row.code">
            <mat-icon fontSet="material-symbols-outlined">visibility</mat-icon>
          </button>
          <a
            matIconButton
            routerLink="/services/builder"
            [queryParams]="{ service: row.code.split('/')[0], version: row.code.split('/')[1] }"
            [matTooltip]="'builder.openFrom' | dcTranslate"
            [attr.data-testid]="'build-' + row.code"
          >
            <mat-icon fontSet="material-symbols-outlined">construction</mat-icon>
          </a>
          @if (canEdit() && !row['isRetired']) {
            <button matIconButton (click)="retire(row)" [matTooltip]="'versions.retire' | dcTranslate">
              <mat-icon fontSet="material-symbols-outlined">archive</mat-icon>
            </button>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
  `,
  styles: `
    .dc-versions__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
    }
    .dc-authoring {
      display: grid;
      gap: 0.25rem;
      margin-bottom: 1.5rem;
    }
    .dc-authoring h2 {
      margin-top: 0;
    }
    .dc-json {
      font-family: ui-monospace, monospace;
      font-size: 13px;
    }
    .dc-authoring__actions {
      display: flex;
      gap: 0.5rem;
      justify-content: flex-end;
      margin-top: 0.5rem;
    }
  `,
})
export class VersionList {
  private readonly api = inject(AppsControlApi);
  private readonly servicesApi = inject(ServicesApi);
  private readonly dialog = inject(MatDialog);
  protected readonly drafts = inject(DraftsService);
  private readonly privileges = inject(PrivilegesService);
  protected readonly columns = ['code', 'name', 'published', 'state', 'actions'];
  private readonly appContext = inject(BusinessAppContext);
  protected readonly services = rxResource({ params: () => ({ app: this.appContext.current() }), stream: ({ params }) => this.api.list('svc.service', null, params.app) });
  private readonly versions = rxResource({ stream: () => this.api.list('svc.service-version') });
  protected readonly rows = computed(() => this.versions.value() ?? []);
  protected readonly service = signal<string | null>(null);
  protected readonly errors = signal<string[]>([]);
  protected readonly canCreate = computed(() => this.privileges.can('services.config', 'CREATE'));
  protected readonly canEdit = computed(() => this.privileges.can('services.config', 'EDIT'));
  protected definitionText = '';

  protected readonly nextNumber = computed(() => {
    const prefix = `${this.service()}/`;
    return 1 + Math.max(0, ...this.rows().filter((r) => r.code.startsWith(prefix)).map((r) => Number(r.code.slice(prefix.length))));
  });

  protected chooseService(code: string): void {
    this.service.set(code);
    const latest = this.rows().find((r) => r.code === `${code}/${this.nextNumber() - 1}`);
    this.definitionText = JSON.stringify((latest?.['definition'] as VersionDefinition | undefined) ?? STARTER, null, 2);
    this.errors.set([]);
  }

  protected async previewDraft(): Promise<void> {
    const definition = this.parse();
    if (!definition) return;
    try {
      const resolved = await firstValueFrom(this.servicesApi.preview(this.service()!, definition));
      this.dialog.open(VersionPreview, { data: resolved, width: '64rem', maxWidth: '95vw' });
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  protected async previewPublished(row: ConfigRow): Promise<void> {
    const [code, number] = row.code.split('/');
    const resolved = await firstValueFrom(this.servicesApi.version(code, Number(number)));
    this.dialog.open(VersionPreview, { data: resolved, width: '64rem', maxWidth: '95vw' });
  }

  protected async addVersion(submit = false): Promise<void> {
    const definition = this.parse();
    if (!definition) return;
    const number = this.nextNumber();
    try {
      await this.drafts.addChange('svc.service-version', `${this.service()}/${number}`, 'CREATE', {
        nameAr: `الإصدار ${number}`,
        nameEn: `Version ${number}`,
        definition,
      }, { submit });
      this.versions.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  protected async retire(row: ConfigRow): Promise<void> {
    await this.drafts.addChange('svc.service-version', row.code, 'UPDATE', { ...row, isRetired: true });
    this.versions.reload();
  }

  private parse(): VersionDefinition | null {
    this.errors.set([]);
    try {
      return JSON.parse(this.definitionText) as VersionDefinition;
    } catch {
      this.errors.set(['Invalid JSON']);
      return null;
    }
  }
}
