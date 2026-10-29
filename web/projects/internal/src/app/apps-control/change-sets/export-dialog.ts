import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatRadioModule } from '@angular/material/radio';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { AppsControlApi, ExportedPackage, I18nService, LocalizePipe, LocalizedText, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { ALL_CONFIG_SECTIONS } from '../config/module-sections';
import { ConfigExport } from './config-export';
import { CHILD_TYPES, orderTypes, typeDependencies } from './export-order';
import { templateEntities } from './package-template';

/** Rows of one screen (`entityType`, with the rows ticked there), or whole types chosen in the dialog. */
export interface ExportDialogData {
  entityType?: string;
  title?: LocalizedText;
  selected?: string[];
}

interface TypeChoice {
  entityType: string;
  title: LocalizedText;
}

interface SectionChoice {
  title: LocalizedText;
  types: TypeChoice[];
}

const entities = templateEntities(ALL_CONFIG_SECTIONS);
const titles = new Map(entities.map((e) => [e.entityType, e.title]));

/**
 * Exports configuration as a package file that the import (here or in another environment) takes back as is. Related
 * rows (referenced rows, values of classes, versions) come along unless turned off, so the package stands on its own.
 */
@Component({
  selector: 'dc-export-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatRadioModule,
    MatSlideToggleModule,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'export.title' | dcTranslate }}</h2>
    <mat-dialog-content class="dc-export">
      <p class="dc-muted">{{ 'export.hint' | dcTranslate }}</p>
      <mat-form-field subscriptSizing="dynamic" class="dc-export__name">
        <mat-label>{{ 'packages.packageTitle' | dcTranslate }}</mat-label>
        <input matInput [ngModel]="title()" (ngModelChange)="title.set($event)" data-testid="export-title" />
      </mat-form-field>

      @if (data.entityType) {
        <mat-radio-group class="dc-export__scope" [ngModel]="scope()" (ngModelChange)="scope.set($event)">
          <mat-radio-button value="selected" [disabled]="!selectedCount()" data-testid="export-scope-selected">
            {{ ('export.selected' | dcTranslate).replace('{count}', '' + selectedCount()) }}
          </mat-radio-button>
          <mat-radio-button value="all" data-testid="export-scope-all">
            {{ ('export.allOf' | dcTranslate).replace('{type}', (data.title | dcLocalize) ?? data.entityType) }}
          </mat-radio-button>
        </mat-radio-group>
      } @else {
        <div class="dc-export__types-head">
          <strong>{{ 'export.types' | dcTranslate }}</strong>
          <button matButton type="button" (click)="selectAll(true)">{{ 'export.selectAll' | dcTranslate }}</button>
          <button matButton type="button" (click)="selectAll(false)">{{ 'export.clear' | dcTranslate }}</button>
        </div>
        @if (!types.hasValue()) {
          <mat-progress-bar mode="indeterminate" />
        }
        <div class="dc-export__sections">
          @for (section of sections(); track $index) {
            <fieldset class="dc-export__section">
              <legend>
                <mat-checkbox
                  [checked]="sectionState(section) === 'all'"
                  [indeterminate]="sectionState(section) === 'some'"
                  (change)="toggleSection(section, $event.checked)"
                >
                  {{ section.title | dcLocalize }}
                </mat-checkbox>
              </legend>
              @for (type of section.types; track type.entityType) {
                <mat-checkbox
                  [checked]="chosen().has(type.entityType)"
                  (change)="toggle(type.entityType, $event.checked)"
                  [attr.data-testid]="'export-type-' + type.entityType"
                >
                  {{ type.title | dcLocalize }}
                </mat-checkbox>
              }
            </fieldset>
          }
        </div>
      }

      <div class="dc-export__options">
        <mat-slide-toggle [checked]="related()" (change)="related.set($event.checked)" data-testid="export-related">{{
          'export.related' | dcTranslate
        }}</mat-slide-toggle>
        <span class="dc-muted">{{ 'export.relatedHint' | dcTranslate }}</span>
        <mat-slide-toggle [checked]="system()" (change)="system.set($event.checked)">{{ 'export.system' | dcTranslate }}</mat-slide-toggle>
        <span class="dc-muted">{{ 'export.systemHint' | dcTranslate }}</span>
      </div>

      @if (busy()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (error of errors(); track $index) {
            <li dir="auto">{{ error }}</li>
          }
        </ul>
      }
      @if (result(); as pkg) {
        <div class="dc-export__done" data-testid="export-summary">
          <mat-icon fontSet="material-symbols-outlined">check_circle</mat-icon>
          <span>{{ ('export.done' | dcTranslate).replace('{count}', '' + pkg.items.length) }}</span>
        </div>
        <ul class="dc-export__counts">
          @for (count of counts(); track count.entityType) {
            <li>
              <span>{{ typeTitle(count.entityType) | dcLocalize }}</span>
              <strong>{{ count.count }}</strong>
            </li>
          }
        </ul>
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      <button matButton mat-dialog-close>{{ (result() ? 'export.close' : 'form.cancel') | dcTranslate }}</button>
      <button matButton="filled" type="button" [disabled]="busy() || !canExport()" (click)="export()" data-testid="export-run">
        <mat-icon fontSet="material-symbols-outlined">download</mat-icon>
        {{ 'export.run' | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-export {
      display: flex;
      flex-direction: column;
      gap: 12px;
      min-width: min(44rem, 90vw);
    }
    .dc-export__name {
      width: 100%;
    }
    .dc-export__scope,
    .dc-export__options {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .dc-export__options .dc-muted {
      margin-inline-start: 52px;
      margin-block-end: 6px;
      font-size: 12px;
    }
    .dc-export__types-head {
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .dc-export__types-head strong {
      margin-inline-end: auto;
    }
    .dc-export__sections {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr));
      gap: 8px;
      max-height: 22rem;
      overflow: auto;
    }
    .dc-export__section {
      display: flex;
      flex-direction: column;
      margin: 0;
      padding: 4px 8px 8px;
      border: 1px solid var(--dga-gray-200, #e4e7ec);
      border-radius: 8px;
    }
    .dc-export__section legend {
      font-weight: 600;
    }
    .dc-export__done {
      display: flex;
      align-items: center;
      gap: 6px;
      color: #067647;
      font-weight: 600;
    }
    .dc-export__counts {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr));
      gap: 4px 16px;
      margin: 0;
      padding: 0;
      list-style: none;
      font-size: 13px;
    }
    .dc-export__counts li {
      display: flex;
      justify-content: space-between;
      gap: 8px;
    }
  `,
})
export class ExportDialog {
  protected readonly data = inject<ExportDialogData>(MAT_DIALOG_DATA);
  private readonly api = inject(AppsControlApi);
  private readonly exporter = inject(ConfigExport);
  private readonly i18n = inject(I18nService);

  protected readonly title = signal(this.defaultTitle());
  protected readonly scope = signal<'selected' | 'all'>(this.data.selected?.length ? 'selected' : 'all');
  protected readonly related = signal(true);
  protected readonly system = signal(false);
  protected readonly chosen = signal(new Set<string>());
  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly result = signal<ExportedPackage | null>(null);
  protected readonly selectedCount = computed(() => this.data.selected?.length ?? 0);

  protected readonly types = rxResource({ stream: () => this.api.types() });
  /** Types the user may view, grouped by change area (as change sets are); child types come with their parents. */
  protected readonly sections = computed<SectionChoice[]>(() => {
    if (!this.types.hasValue()) return [];
    const children = new Set(Object.values(CHILD_TYPES));
    const groups = new Map<string, SectionChoice>();
    for (const info of this.types.value()) {
      if (children.has(info.entityType)) continue;
      const area = this.i18n.translate('area.' + info.area);
      const group = groups.get(info.area) ?? { title: { ar: area, en: area }, types: [] };
      group.types.push({ entityType: info.entityType, title: this.typeTitle(info.entityType) });
      groups.set(info.area, group);
    }
    return [...groups.values()];
  });
  protected readonly counts = computed(() => {
    const counts = new Map<string, number>();
    for (const item of this.result()?.items ?? []) counts.set(item.entityType, (counts.get(item.entityType) ?? 0) + 1);
    return [...counts].map(([entityType, count]) => ({ entityType, count }));
  });
  protected readonly canExport = computed(() => !!this.title().trim() && (!!this.data.entityType || this.chosen().size > 0));

  protected typeTitle(type: string): LocalizedText {
    return titles.get(type) ?? { ar: type, en: type };
  }

  protected toggle(type: string, on: boolean): void {
    const next = new Set(this.chosen());
    if (on) next.add(type);
    else next.delete(type);
    this.chosen.set(next);
  }

  protected toggleSection(section: SectionChoice, on: boolean): void {
    const next = new Set(this.chosen());
    for (const type of section.types) {
      if (on) next.add(type.entityType);
      else next.delete(type.entityType);
    }
    this.chosen.set(next);
  }

  protected sectionState(section: SectionChoice): 'all' | 'some' | 'none' {
    const count = section.types.filter((t) => this.chosen().has(t.entityType)).length;
    return count === 0 ? 'none' : count === section.types.length ? 'all' : 'some';
  }

  protected selectAll(on: boolean): void {
    this.chosen.set(on ? new Set(this.sections().flatMap((s) => s.types.map((t) => t.entityType))) : new Set());
  }

  protected async export(): Promise<void> {
    this.busy.set(true);
    this.errors.set([]);
    this.result.set(null);
    const selection = new Map<string, Set<string> | null>();
    if (this.data.entityType) {
      selection.set(this.data.entityType, this.scope() === 'selected' ? new Set(this.data.selected) : null);
    } else {
      // Whole types in apply order; the server lists each type's rows parents first.
      for (const type of orderTypes(this.chosen(), typeDependencies(entities))) selection.set(type, null);
    }
    try {
      const pkg = await this.exporter.build(selection, { title: this.title().trim(), includeRelated: this.related(), includeSystem: this.system() });
      this.result.set(pkg);
      if (pkg.items.length) {
        this.exporter.download(pkg, `${this.fileStem()}-${new Date().toISOString().slice(0, 10)}.json`);
      } else {
        this.errors.set([this.i18n.translate('export.nothing')]);
      }
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }

  private defaultTitle(): string {
    const what = this.data.title ? this.i18n.localize(this.data.title) : this.i18n.translate('export.environment');
    return `${what} — ${new Date().toISOString().slice(0, 10)}`;
  }

  private fileStem(): string {
    return (this.data.entityType ?? 'configuration').replace(/[^a-z0-9.-]+/gi, '-');
  }
}
