import { Injectable, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { AppsControlApi, ChangeOperation, ChangeSetDetail, EntityTypeInfo, I18nService } from '@dynamic-cube/shared';
import { Observable, firstValueFrom, shareReplay } from 'rxjs';

/** How an edit in an area that needs approval is recorded: added to the user's draft, or sent for approval at once. */
export interface SaveOptions {
  submit?: boolean;
}

/**
 * Records edits. In areas that need approval they go into the user's own draft change set (created when needed) and
 * take effect after submission and approval, or are sent for approval at once in a change set of their own; in other
 * areas saving publishes at once. All keep before/after.
 */
@Injectable({ providedIn: 'root' })
export class DraftsService {
  private readonly api = inject(AppsControlApi);
  private readonly i18n = inject(I18nService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);
  private types$?: Observable<EntityTypeInfo[]>;
  private readonly typeList = toSignal(this.types(), { initialValue: [] as EntityTypeInfo[] });

  types(): Observable<EntityTypeInfo[]> {
    this.types$ ??= this.api.types().pipe(shareReplay(1));
    return this.types$;
  }

  /** Whether edits of the type wait for approval (true until known, the safe assumption). */
  needsApproval(entityType: string): boolean {
    return this.typeList().find((t) => t.entityType === entityType)?.requiresApproval ?? true;
  }

  /** Label key for the save button of an editor. */
  saveLabel(entityType: string): string {
    return this.needsApproval(entityType) ? 'config.addToDraft' : 'config.save';
  }

  async addChange(
    entityType: string,
    entityCode: string,
    operation: ChangeOperation,
    payload: unknown,
    options: SaveOptions = {},
  ): Promise<ChangeSetDetail> {
    const type = (await firstValueFrom(this.types())).find((t) => t.entityType === entityType);
    if (!type) {
      throw new Error(`Unknown entity type ${entityType}`);
    }

    if (!type.requiresApproval) {
      const saved = await firstValueFrom(this.api.applyNow(entityType, entityCode, operation, payload));
      this.snackBar.open(this.i18n.translate('drafts.saved'), undefined, { duration: 4000 });
      return saved;
    }

    if (options.submit) {
      const sent = await firstValueFrom(this.api.submitChange(entityType, entityCode, operation, payload));
      this.snackBar
        .open(this.i18n.translate('drafts.submitted'), this.i18n.translate('drafts.open'), { duration: 6000 })
        .onAction()
        .subscribe(() => void this.router.navigate(['/apps-control/change-sets', sent.changeSet.id]));
      return sent;
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
