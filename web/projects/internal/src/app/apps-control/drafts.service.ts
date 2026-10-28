import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { AppsControlApi, ChangeOperation, ChangeSetDetail, EntityTypeInfo, I18nService } from '@dynamic-cube/shared';
import { Observable, firstValueFrom, shareReplay } from 'rxjs';

/**
 * Collects edits into the user's own draft change set for the entity type's area (creating one when needed).
 * Nothing changes until the change set is submitted and, where required, approved.
 */
@Injectable({ providedIn: 'root' })
export class DraftsService {
  private readonly api = inject(AppsControlApi);
  private readonly i18n = inject(I18nService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);
  private types$?: Observable<EntityTypeInfo[]>;

  types(): Observable<EntityTypeInfo[]> {
    this.types$ ??= this.api.types().pipe(shareReplay(1));
    return this.types$;
  }

  async addChange(entityType: string, entityCode: string, operation: ChangeOperation, payload: unknown): Promise<ChangeSetDetail> {
    const type = (await firstValueFrom(this.types())).find((t) => t.entityType === entityType);
    if (!type) {
      throw new Error(`Unknown entity type ${entityType}`);
    }

    const drafts = await firstValueFrom(this.api.changeSets({ status: 'DRAFT', area: type.area, mine: true }));
    const draftId =
      drafts[0]?.id ??
      (await firstValueFrom(this.api.createChangeSet(type.area, `${this.i18n.translate('area.' + type.area)} — ${new Date().toLocaleDateString('en-CA')}`)))
        .changeSet.id;

    const detail = await firstValueFrom(this.api.addItem(draftId, entityType, entityCode, operation, payload));
    this.snackBar
      .open(this.i18n.translate('drafts.added'), this.i18n.translate('drafts.open'), { duration: 6000 })
      .onAction()
      .subscribe(() => void this.router.navigate(['/apps-control/change-sets', draftId]));
    return detail;
  }
}
