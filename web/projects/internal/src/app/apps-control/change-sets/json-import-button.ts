import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { LocalizedText, TranslatePipe } from '@dynamic-cube/shared';
import { JsonImport } from './json-import';
import { ImportTarget } from './json-package';

/** "Import JSON" for a screen's header: rows of the screen's main type, package items or a whole package. */
@Component({
  selector: 'dc-json-import-button',
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button matButton="outlined" type="button" (click)="run()" [attr.data-testid]="'import-' + (target()?.entityType ?? 'package')">
      <mat-icon fontSet="material-symbols-outlined">upload</mat-icon>
      {{ 'jsonImport.button' | dcTranslate }}
    </button>
  `,
})
export class JsonImportButton {
  readonly title = input.required<LocalizedText>();
  readonly target = input<ImportTarget | null>(null);
  readonly example = input<unknown>(undefined);
  /** Emits the number of drafts created. */
  readonly imported = output<number>();
  private readonly jsonImport = inject(JsonImport);

  protected async run(): Promise<void> {
    const created = await this.jsonImport.run({ title: this.title(), target: this.target(), example: this.example() });
    if (created) this.imported.emit(created);
  }
}
