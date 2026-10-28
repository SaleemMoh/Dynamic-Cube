import { DatePipe, JsonPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import {
  AppsControlApi,
  AuthImageSrc,
  ChangeItemInfo,
  ChangeSetDetail as Detail,
  I18nService,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { Observable, firstValueFrom } from 'rxjs';
import { ConfirmDialog, ConfirmData } from '../confirm-dialog';
import { PrivilegesService } from '../privileges.service';
import { StatusChip } from './status-chip';

const CHANGE_SETS = 'apps-control.change-sets';

/** One change set: items with before/after, and the actions the current user may take in its current status. */
@Component({
  selector: 'dc-change-set-detail',
  imports: [
    AuthImageSrc,
    DatePipe,
    JsonPipe,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    RouterLink,
    StatusChip,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a matButton routerLink="..">
      <mat-icon fontSet="material-symbols-outlined">{{
        i18n.direction() === 'rtl' ? 'arrow_forward' : 'arrow_back'
      }}</mat-icon>
      {{ 'changeSets.back' | dcTranslate }}
    </a>
    @if (detail(); as d) {
      <header class="dc-page__header">
        <div>
          <h1>{{ d.changeSet.title }}</h1>
          <div class="dc-muted">
            {{ 'area.' + d.changeSet.area | dcTranslate }} · {{ 'changeSets.maker' | dcTranslate }}:
            {{ d.changeSet.createdBy }}
            @if (d.changeSet.reviewedBy) {
              · {{ 'changeSets.reviewer' | dcTranslate }}: {{ d.changeSet.reviewedBy }}
            }
          </div>
        </div>
        <dc-status-chip [status]="d.changeSet.status" data-testid="status" />
      </header>

      @if (d.changeSet.reviewComment) {
        <p class="dc-note" data-testid="review-comment">
          {{ 'changeSets.comment' | dcTranslate }}: {{ d.changeSet.reviewComment }}
        </p>
      }
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (error of errors(); track $index) {
            <li>{{ error }}</li>
          }
        </ul>
      }

      <div class="dc-actions">
        @if (mineAndDraft()) {
          <button
            matButton="filled"
            (click)="submit()"
            [disabled]="d.items.length === 0 || busy()"
            data-testid="submit"
          >
            <mat-icon fontSet="material-symbols-outlined">send</mat-icon>
            {{
              (d.changeSet.requiresApproval ? 'changeSets.submit' : 'changeSets.submitPublish')
                | dcTranslate
            }}
          </button>
        }
        @if (canReview()) {
          <button matButton="filled" (click)="approve()" [disabled]="busy()" data-testid="approve">
            <mat-icon fontSet="material-symbols-outlined">check</mat-icon>
            {{ 'changeSets.approve' | dcTranslate }}
          </button>
          <button matButton="outlined" (click)="reject()" [disabled]="busy()" data-testid="reject">
            <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
            {{ 'changeSets.reject' | dcTranslate }}
          </button>
        }
        @if (d.changeSet.isMine && d.changeSet.status === 'REJECTED') {
          <button
            matButton="filled"
            (click)="run(api.reopen(d.changeSet.id))"
            [disabled]="busy()"
            data-testid="reopen"
          >
            {{ 'changeSets.reopen' | dcTranslate }}
          </button>
        }
        @if (d.changeSet.status === 'APPROVED') {
          <button
            matButton="filled"
            (click)="run(api.publish(d.changeSet.id))"
            [disabled]="busy()"
            data-testid="publish"
          >
            {{ 'changeSets.publish' | dcTranslate }}
          </button>
        }
        @if (
          d.changeSet.isMine &&
          d.changeSet.status !== 'PUBLISHED' &&
          d.changeSet.status !== 'SUBMITTED'
        ) {
          <button matButton (click)="discard()" [disabled]="busy()" data-testid="discard">
            <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
            {{ 'changeSets.discard' | dcTranslate }}
          </button>
        }
      </div>

      @for (item of d.items; track item.id) {
        <mat-card
          appearance="outlined"
          class="dc-item"
          [attr.data-item]="item.entityType + ':' + item.entityCode"
        >
          <mat-card-header>
            <mat-card-title>
              <span class="dc-op" [attr.data-op]="item.operation">{{
                'op.' + item.operation | dcTranslate
              }}</span>
              {{ 'type.' + item.entityType | dcTranslate }} —
              <span dir="ltr">{{ item.entityCode }}</span>
            </mat-card-title>
            @if (mineAndDraft()) {
              <button
                matIconButton
                (click)="run(api.removeItem(d.changeSet.id, item.id))"
                [attr.aria-label]="'changeSets.removeItem' | dcTranslate"
              >
                <mat-icon fontSet="material-symbols-outlined">remove_circle</mat-icon>
              </button>
            }
          </mat-card-header>
          <mat-card-content>
            <div class="dc-diff" dir="ltr">
              @for (field of changedFields(item); track field) {
                <div class="dc-diff__row">
                  <span class="dc-diff__field">{{ field }}</span>
                  @if (field === 'logoFileKey') {
                    <span class="dc-diff__before">
                      @if (item.before?.[field]; as key) {
                        <img class="dc-diff__img" [dcAuthSrc]="assetUrl(key)" alt="" />
                      }
                    </span>
                    <span class="dc-diff__after">
                      @if (item.after?.[field]; as key) {
                        <img class="dc-diff__img" [dcAuthSrc]="assetUrl(key)" alt="" />
                      }
                    </span>
                  } @else {
                    <code class="dc-diff__before">{{ item.before?.[field] | json }}</code>
                    <code class="dc-diff__after">{{ item.after?.[field] | json }}</code>
                  }
                </div>
              } @empty {
                <span class="dc-muted">{{ 'changeSets.noFieldChanges' | dcTranslate }}</span>
              }
            </div>
          </mat-card-content>
        </mat-card>
      }

      <p class="dc-muted" dir="ltr">
        {{ d.changeSet.createdAt | date: 'yyyy-MM-dd HH:mm' }}
        @if (d.changeSet.publishedAt) {
          → {{ d.changeSet.publishedAt | date: 'yyyy-MM-dd HH:mm' }}
        }
      </p>
    }
  `,
  styles: `
    .dc-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      margin-block: 1rem;
    }
    .dc-item {
      margin-block-end: 0.75rem;
    }
    .dc-op {
      display: inline-block;
      padding: 0 0.5rem;
      border-radius: 4px;
      font: var(--mat-sys-label-medium);
      background: var(--mat-sys-secondary-container);
    }
    [data-op='DELETE'] {
      background: var(--mat-sys-error-container);
    }
    .dc-diff__row {
      display: grid;
      grid-template-columns: 12rem 1fr 1fr;
      gap: 0.75rem;
      padding-block: 0.25rem;
      border-block-end: 1px solid var(--mat-sys-outline-variant);
    }
    .dc-diff__field {
      font-weight: 600;
    }
    .dc-diff__before {
      color: var(--mat-sys-error);
      white-space: pre-wrap;
      word-break: break-word;
    }
    .dc-diff__after {
      color: var(--mat-sys-primary);
      white-space: pre-wrap;
      word-break: break-word;
    }
    .dc-diff__img {
      max-height: 3rem;
      max-width: 12rem;
    }
    .dc-note {
      padding: 0.75rem 1rem;
      border-radius: var(--dc-radius);
      background: var(--mat-sys-surface-container);
    }
  `,
})
export class ChangeSetDetail {
  readonly id = input.required<string>();

  protected readonly api = inject(AppsControlApi);
  private readonly privileges = inject(PrivilegesService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);
  protected readonly i18n = inject(I18nService);

  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);
  private readonly updated = signal<Detail | null>(null);
  private readonly resource = rxResource({
    params: () => this.id(),
    stream: ({ params }) => this.api.changeSet(params),
  });

  protected readonly detail = computed(
    () => this.updated() ?? (this.resource.hasValue() ? this.resource.value() : null),
  );
  protected readonly mineAndDraft = computed(() => {
    const d = this.detail();
    return !!d && d.changeSet.isMine && d.changeSet.status === 'DRAFT';
  });
  protected readonly canReview = computed(() => {
    const d = this.detail();
    return (
      !!d &&
      d.changeSet.status === 'SUBMITTED' &&
      !d.changeSet.isMine &&
      this.privileges.can(CHANGE_SETS, 'APPROVE')
    );
  });

  protected assetUrl(key: unknown): string {
    return `/api/apps-control/v1/branding/assets/${String(key)}`;
  }

  protected changedFields(item: ChangeItemInfo): string[] {
    const keys = new Set([...Object.keys(item.before ?? {}), ...Object.keys(item.after ?? {})]);
    // Key and ownership are not editable fields.
    keys.delete('owner');
    keys.delete('code');
    if (item.operation === 'UPDATE') {
      for (const key of [...keys]) {
        if (JSON.stringify(item.before?.[key]) === JSON.stringify(item.after?.[key])) {
          keys.delete(key);
        }
      }
    }
    return [...keys];
  }

  protected submit(): void {
    void this.run(this.api.submit(this.id()));
  }

  protected async approve(): Promise<void> {
    const comment = await this.ask({
      title: this.i18n.translate('changeSets.approveTitle'),
      confirmLabel: this.i18n.translate('changeSets.approve'),
      comment: true,
    });
    if (comment !== undefined) {
      await this.run(this.api.approve(this.id(), comment || undefined));
    }
  }

  protected async reject(): Promise<void> {
    const comment = await this.ask({
      title: this.i18n.translate('changeSets.rejectTitle'),
      confirmLabel: this.i18n.translate('changeSets.reject'),
      comment: true,
      commentRequired: true,
      danger: true,
    });
    if (comment) {
      await this.run(this.api.reject(this.id(), comment));
    }
  }

  protected async discard(): Promise<void> {
    const confirmed = await this.ask({
      title: this.i18n.translate('changeSets.discardTitle'),
      confirmLabel: this.i18n.translate('changeSets.discard'),
      danger: true,
    });
    if (confirmed !== undefined) {
      this.busy.set(true);
      try {
        await firstValueFrom(this.api.discard(this.id()));
        await this.router.navigate(['/apps-control/change-sets']);
      } catch (error) {
        this.errors.set(problemMessages(error));
      } finally {
        this.busy.set(false);
      }
    }
  }

  protected async run(request: Observable<Detail>): Promise<void> {
    this.busy.set(true);
    this.errors.set([]);
    try {
      const detail = await firstValueFrom(request);
      this.updated.set(detail);
      if (detail.changeSet.status === 'PUBLISHED') {
        this.snackBar.open(this.i18n.translate('changeSets.published'), undefined, {
          duration: 4000,
        });
      }
    } catch (error) {
      this.errors.set(problemMessages(error));
      this.resource.reload();
      this.updated.set(null);
    } finally {
      this.busy.set(false);
    }
  }

  private ask(data: ConfirmData): Promise<string | undefined> {
    return firstValueFrom(
      this.dialog.open<ConfirmDialog, ConfirmData, string>(ConfirmDialog, { data }).afterClosed(),
    );
  }
}
