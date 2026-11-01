import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { I18nService, TranslatePipe } from '@dynamic-cube/shared';
import { PrivilegesService } from '../apps-control/privileges.service';
import { OPERATION_DESIGNER_TRANSLATIONS } from './operation-designer-translations';

/** Above the operations: how to start from a provider's OpenAPI / Swagger description, and the button that does it. */
@Component({
  selector: 'dc-openapi-intro',
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-card oi-intro" data-testid="openapi-intro">
      <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">upload_file</mat-icon>
      <p>{{ 'oi.intro' | dcTranslate }}</p>
      @if (canImport()) {
        <button matButton="outlined" type="button" (click)="open()" data-testid="openapi-import">
          <mat-icon fontSet="material-symbols-outlined">upload</mat-icon
          >{{ 'oi.import' | dcTranslate }}
        </button>
      }
    </div>
  `,
  styles: `
    .oi-intro {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 12px;
    }
    .oi-intro mat-icon {
      flex: none;
      color: var(--dynamic-cube-gold, #ca9f45);
    }
    .oi-intro p {
      flex: 1;
      margin: 0;
      color: var(--dga-text-2, #384250);
      font-size: 13px;
    }
  `,
})
export class OpenApiIntro {
  private readonly dialog = inject(MatDialog);
  private readonly privileges = inject(PrivilegesService);
  protected readonly canImport = computed(() =>
    this.privileges.can('integration.config', 'CREATE'),
  );

  constructor() {
    inject(I18nService).register(OPERATION_DESIGNER_TRANSLATIONS);
  }

  protected async open(): Promise<void> {
    const { OpenApiImport } = await import('./openapi-import');
    this.dialog.open(OpenApiImport, {
      width: '84rem',
      maxWidth: '96vw',
      autoFocus: 'first-tabbable',
    });
  }
}
