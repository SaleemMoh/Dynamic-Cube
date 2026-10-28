import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialogModule } from '@angular/material/dialog';
import { LocalizePipe, LocalizedText, TranslatePipe } from '@dynamic-cube/shared';
import { ALL_CONFIG_SECTIONS } from '../config/module-sections';
import { TemplateEntity, buildTemplate, templateEntities } from './package-template';

interface Group {
  section: LocalizedText;
  entities: TemplateEntity[];
}

/** Chooses the entity types of a package template and downloads it as a documented JSON file. */
@Component({
  selector: 'dc-template-dialog',
  imports: [MatButtonModule, MatCheckboxModule, MatDialogModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'packages.templateTitle' | dcTranslate }}</h2>
    <mat-dialog-content>
      <p class="dc-muted">{{ 'packages.templateLead' | dcTranslate }}</p>
      @for (group of groups; track $index) {
        <section class="dc-template__group">
          <mat-checkbox
            [checked]="allOf(group)"
            [indeterminate]="someOf(group) && !allOf(group)"
            (change)="toggleGroup(group, $event.checked)"
            class="dc-template__section"
          >
            {{ group.section | dcLocalize }}
          </mat-checkbox>
          <div class="dc-template__types">
            @for (entity of group.entities; track entity.entityType) {
              <mat-checkbox [checked]="chosen().has(entity.entityType)" (change)="toggle(entity.entityType, $event.checked)" [attr.data-template-type]="entity.entityType">
                {{ entity.title | dcLocalize }} <span class="mono dc-muted">{{ entity.entityType }}</span>
              </mat-checkbox>
            }
          </div>
        </section>
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      <button matButton mat-dialog-close>{{ 'form.cancel' | dcTranslate }}</button>
      <button matButton="filled" [disabled]="chosen().size === 0" (click)="download()" data-testid="template-download">
        {{ ('packages.download' | dcTranslate).replace('{count}', '' + chosen().size) }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-template__group {
      padding-block: 8px;
      border-bottom: 1px solid var(--dga-gray-200);
    }
    .dc-template__section {
      font-weight: 600;
    }
    .dc-template__types {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr));
      padding-inline-start: 24px;
    }
  `,
})
export class TemplateDialog {
  private readonly entities = templateEntities(ALL_CONFIG_SECTIONS);
  protected readonly groups: Group[] = this.group();
  protected readonly chosen = signal<ReadonlySet<string>>(new Set());

  protected allOf(group: Group): boolean {
    return group.entities.every((e) => this.chosen().has(e.entityType));
  }

  protected someOf(group: Group): boolean {
    return group.entities.some((e) => this.chosen().has(e.entityType));
  }

  protected toggle(type: string, on: boolean): void {
    const next = new Set(this.chosen());
    if (on) {
      next.add(type);
    } else {
      next.delete(type);
    }
    this.chosen.set(next);
  }

  protected toggleGroup(group: Group, on: boolean): void {
    const next = new Set(this.chosen());
    group.entities.forEach((e) => (on ? next.add(e.entityType) : next.delete(e.entityType)));
    this.chosen.set(next);
  }

  protected download(): void {
    const template = buildTemplate(this.entities.filter((e) => this.chosen().has(e.entityType)));
    const blob = new Blob([JSON.stringify(template, null, 2)], { type: 'application/json' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = `configuration-package-template-${new Date().toISOString().slice(0, 10)}.json`;
    link.click();
    URL.revokeObjectURL(link.href);
  }

  private group(): Group[] {
    const groups: Group[] = [];
    for (const entity of this.entities) {
      const existing = groups.find((g) => g.section.en === entity.section.en);
      if (existing) {
        existing.entities.push(entity);
      } else {
        groups.push({ section: entity.section, entities: [entity] });
      }
    }
    return groups;
  }
}
