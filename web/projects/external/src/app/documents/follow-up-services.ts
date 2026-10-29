import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { FieldError, LocalizePipe, ServicesApi, TranslatePipe, requestErrors } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';

/**
 * The services that act on a document (renewal, extension…): each available now, or locked with the reason and the dates of
 * its window; a request already open on the document is continued instead of started again.
 */
@Component({
  selector: 'dc-follow-up-services',
  imports: [MatButtonModule, MatIconModule, RouterLink, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (followUps.value()?.length) {
      <section class="dc-followups" data-testid="follow-ups">
        <h2>{{ 'followUps.title' | dcTranslate }}</h2>
        <ul>
          @for (f of followUps.value(); track f.service.code) {
            <li class="dc-followup" [class.dc-followup--locked]="!f.service.isEligible" [attr.data-testid]="'follow-up-' + f.service.code">
              <span class="dc-followup__icon" aria-hidden="true">
                <mat-icon fontSet="material-symbols-outlined">{{ f.service.isEligible ? (f.service.icon || 'autorenew') : 'lock' }}</mat-icon>
              </span>
              <div class="dc-followup__body">
                <strong>{{ { ar: f.service.nameAr, en: f.service.nameEn } | dcLocalize }}</strong>
                @if (f.document.opensOn || f.document.closesOn) {
                  <span class="dc-muted" data-testid="follow-up-window">
                    @if (f.document.opensOn && f.document.closesOn) {
                      {{ 'followUps.window' | dcTranslate }} <span dir="ltr">{{ f.document.opensOn }}</span> – <span dir="ltr">{{ f.document.closesOn }}</span>
                    } @else if (f.document.opensOn) {
                      {{ 'followUps.from' | dcTranslate }} <span dir="ltr">{{ f.document.opensOn }}</span>
                    } @else {
                      {{ 'followUps.until' | dcTranslate }} <span dir="ltr">{{ f.document.closesOn }}</span>
                    }
                  </span>
                }
                @if (!f.service.isEligible && !f.document.openRequestId) {
                  <ul class="dc-followup__reasons">
                    @for (r of f.service.reasons; track r.code) {
                      <!-- The window line above already says when it opens. -->
                      @if (r.code !== 'NOT_YET_OPEN') {
                        <li>{{ { ar: r.messageAr, en: r.messageEn } | dcLocalize }}</li>
                      }
                    }
                  </ul>
                }
              </div>
              <div class="dc-followup__action">
                @if (f.document.openRequestId; as requestId) {
                  <a matButton="outlined" [routerLink]="['/requests', requestId]" [attr.data-testid]="'continue-' + f.service.code">
                    {{ 'followUps.continue' | dcTranslate }} {{ f.document.openRequestNumber ?? '' }}
                  </a>
                } @else if (f.service.isEligible) {
                  <button matButton="filled" (click)="start(f.service.code)" [disabled]="starting()" [attr.data-testid]="'start-' + f.service.code">
                    {{ 'portal.startService' | dcTranslate }}
                  </button>
                }
              </div>
            </li>
          }
        </ul>
        @if (errors().length) {
          <ul class="dc-errors" role="alert">
            @for (e of errors(); track $index) {
              <li>{{ { ar: e.messageAr, en: e.messageEn } | dcLocalize }}</li>
            }
          </ul>
        }
      </section>
    }
  `,
  styles: `
    .dc-followups h2 {
      margin: 0 0 12px;
      font-size: 20px;
    }
    .dc-followups > ul {
      display: flex;
      flex-direction: column;
      gap: 12px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .dc-followup {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 12px 16px;
      padding: 16px 16px;
      border: 1px solid var(--dga-border);
      border-radius: 12px;
      background: #ffffff;
    }
    .dc-followup__icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 44px;
      height: 32px;
      border-radius: 10px;
      background: var(--dga-green-50);
      color: var(--dga-green-700);
    }
    .dc-followup--locked .dc-followup__icon {
      background: var(--dga-gray-100);
      color: var(--dga-text-3);
    }
    .dc-followup__body {
      display: flex;
      flex: 1 1 16rem;
      flex-direction: column;
      gap: 2px;
    }
    .dc-followup__reasons {
      margin: 4px 0 0;
      padding-inline-start: 18px;
      color: #b54708;
      font-size: 14px;
    }
  `,
})
export class FollowUpServices {
  readonly documentId = input.required<string>();
  private readonly api = inject(ServicesApi);
  private readonly router = inject(Router);
  protected readonly followUps = rxResource({ params: () => this.documentId(), stream: ({ params }) => this.api.followUps(params) });
  protected readonly starting = signal(false);
  protected readonly errors = signal<FieldError[]>([]);

  protected async start(serviceCode: string): Promise<void> {
    this.starting.set(true);
    this.errors.set([]);
    try {
      const draft = await firstValueFrom(this.api.create(serviceCode, this.documentId()));
      await this.router.navigate(['/requests', draft.request.id]);
    } catch (error) {
      this.errors.set(requestErrors(error));
    } finally {
      this.starting.set(false);
    }
  }
}
