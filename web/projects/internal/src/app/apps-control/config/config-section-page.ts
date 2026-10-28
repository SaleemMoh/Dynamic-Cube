import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { LocalizePipe, TranslatePipe } from '@dynamic-cube/shared';
import { ConfigTable } from './config-table';
import { CONFIG_SECTIONS } from './config-types';

/** An Apps Control screen showing one or more configuration types (tabs). */
@Component({
  selector: 'dc-config-section-page',
  imports: [MatTabsModule, ConfigTable, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (sectionDefinition(); as s) {
      <header class="dc-page__header">
        <h1>{{ s.title | dcLocalize }}</h1>
      </header>
      <p class="dc-muted">{{ 'config.changeSetHint' | dcTranslate }}</p>
      @if (s.types.length === 1) {
        <dc-config-table [type]="s.types[0]" [resource]="s.resource" />
      } @else {
        <mat-tab-group mat-stretch-tabs="false" animationDuration="0ms">
          @for (type of s.types; track type.entityType) {
            <mat-tab [label]="type.title | dcLocalize">
              <dc-config-table [type]="type" [resource]="s.resource" />
            </mat-tab>
          }
        </mat-tab-group>
      }
    }
  `,
})
export class ConfigSectionPage {
  /** Section path, from route data. */
  readonly section = input.required<string>();

  protected readonly sectionDefinition = computed(() => CONFIG_SECTIONS.find((s) => s.path === this.section()));
}
