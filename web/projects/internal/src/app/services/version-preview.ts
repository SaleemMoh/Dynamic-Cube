import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { ResolvedVersion, ServiceWizard, TranslatePipe, WizardMove } from '@dynamic-cube/shared';

/** Walks through a version as an applicant would, without saving anything (fees are calculated only for real requests). */
@Component({
  selector: 'dc-version-preview',
  imports: [MatButtonModule, MatDialogModule, ServiceWizard, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title dir="ltr">{{ version.serviceCode }}{{ version.number ? '/' + version.number : '' }} — {{ 'versions.preview' | dcTranslate }}</h2>
    <mat-dialog-content>
      <dc-service-wizard
        [version]="version"
        [data]="data()"
        [levelCode]="level()"
        [currentStep]="step()"
        [preview]="true"
        (moved)="move($event)"
      />
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close>{{ 'common.close' | dcTranslate }}</button>
    </mat-dialog-actions>
  `,
})
export class VersionPreview {
  protected readonly version = inject<ResolvedVersion>(MAT_DIALOG_DATA);
  protected readonly data = signal<Record<string, unknown>>({});
  protected readonly level = signal<string | null>(null);
  protected readonly step = signal<string | null>(this.version.definition.steps[0]?.code ?? null);

  protected move(move: WizardMove): void {
    this.data.set(move.data);
    this.level.set(move.levelCode);
    this.step.set(move.to);
  }
}
