import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { DocumentsApi, I18nService, LocalizePipe, TranslatePipe, VerifyView, docStatusBadge, fieldText } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';

/** Public verification of a document (no sign-in): opened from the QR code, or by typing the code printed on it. */
@Component({
  selector: 'dc-verify-page',
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTableModule,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'verify.title' | dcTranslate }}</h1>
    </header>
    <p class="dc-muted">{{ 'verify.lead' | dcTranslate }}</p>
    <form class="dc-verify__form" (ngSubmit)="check()">
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'verify.code' | dcTranslate }}</mat-label>
        <input matInput dir="ltr" name="code" autocomplete="off" [ngModel]="typed()" (ngModelChange)="typed.set($event)" data-testid="verify-code" />
      </mat-form-field>
      <button matButton="filled" type="submit" [disabled]="!typed().trim()" data-testid="verify-check">{{ 'verify.check' | dcTranslate }}</button>
    </form>

    @if (loading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @if (notFound()) {
      <div class="dc-card dc-verify__result dc-verify__result--bad" role="alert" data-testid="verify-not-found">
        <mat-icon fontSet="material-symbols-outlined">gpp_bad</mat-icon>
        <p>{{ 'verify.notFound' | dcTranslate }}</p>
      </div>
    }
    @if (result(); as r) {
      <div
        class="dc-card dc-verify__result"
        [class.dc-verify__result--good]="r.status === 'ACTIVE'"
        [class.dc-verify__result--warn]="r.status !== 'ACTIVE'"
        role="status"
        data-testid="verify-result"
      >
        <mat-icon fontSet="material-symbols-outlined">{{ r.status === 'ACTIVE' ? 'verified' : 'error' }}</mat-icon>
        <div>
          <strong>{{ (r.status === 'ACTIVE' ? 'verify.genuine' : 'verify.notInForce') | dcTranslate }}</strong>
          <p class="dc-muted">{{ asOf(r) }}</p>
        </div>
      </div>
      <section class="dc-card">
        <dl class="dc-verify__facts">
          <div>
            <dt>{{ 'documents.type' | dcTranslate }}</dt>
            <dd>{{ { ar: r.typeNameAr, en: r.typeNameEn } | dcLocalize }}</dd>
          </div>
          <div>
            <dt>{{ 'documents.number' | dcTranslate }}</dt>
            <dd dir="ltr" class="dc-verify__number">{{ r.number }}</dd>
          </div>
          <div>
            <dt>{{ 'documents.holder' | dcTranslate }}</dt>
            <dd>{{ { ar: r.holderNameAr, en: r.holderNameEn } | dcLocalize }}</dd>
          </div>
          <div>
            <dt>{{ 'documents.status' | dcTranslate }}</dt>
            <dd>
              <span class="dc-badge" [class]="badge()">{{ 'docStatus.' + r.status | dcTranslate }}</span>
            </dd>
          </div>
          <div>
            <dt>{{ 'documents.issuedOn' | dcTranslate }}</dt>
            <dd dir="ltr">{{ r.issuedAt | date: 'yyyy-MM-dd' }}</dd>
          </div>
          <div>
            <dt>{{ 'documents.expiresOn' | dcTranslate }}</dt>
            <dd dir="ltr">{{ r.expiresAt ? (r.expiresAt | date: 'yyyy-MM-dd') : ('documents.noExpiry' | dcTranslate) }}</dd>
          </div>
          @for (f of r.fields; track $index) {
            <div>
              <dt>{{ { ar: f.nameAr, en: f.nameEn } | dcLocalize }}</dt>
              <dd>{{ text(f.value) }}</dd>
            </div>
          }
        </dl>
      </section>
      @if (r.lines.length && r.lineFields.length) {
        <section class="dc-card">
          <h2>{{ 'documents.lines' | dcTranslate }}</h2>
          <table mat-table [dataSource]="r.lines" class="dc-table">
            @for (c of r.lineFields; track c.code) {
              <ng-container [matColumnDef]="c.code">
                <th mat-header-cell *matHeaderCellDef>{{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}</th>
                <td mat-cell *matCellDef="let line">{{ text(line[c.code]) }}</td>
              </ng-container>
            }
            <tr mat-header-row *matHeaderRowDef="lineCodes()"></tr>
            <tr mat-row *matRowDef="let row; columns: lineCodes()"></tr>
          </table>
        </section>
      }
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: 1rem;
      max-width: 56rem;
    }
    .dc-verify__form {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.75rem;
    }
    .dc-verify__form mat-form-field {
      flex: 1;
      min-width: 16rem;
      max-width: 28rem;
    }
    .dc-verify__result {
      display: flex;
      align-items: center;
      gap: 1rem;
    }
    .dc-verify__result p {
      margin: 0.25rem 0 0;
    }
    .dc-verify__result mat-icon {
      font-size: 40px;
      width: 40px;
      height: 40px;
    }
    .dc-verify__result--good {
      border-color: #079455;
      background: #ecfdf3;
      color: #085d3a;
    }
    .dc-verify__result--warn {
      border-color: #dc6803;
      background: #fffaeb;
      color: #b54708;
    }
    .dc-verify__result--bad {
      border-color: #d92d20;
      background: #fef3f2;
      color: #b42318;
    }
    .dc-verify__facts {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr));
      gap: 1rem 1.5rem;
      margin: 0;
    }
    .dc-verify__facts dt {
      color: var(--dga-gray-500, #6c737f);
      font-size: 13px;
    }
    .dc-verify__facts dd {
      margin: 2px 0 0;
      font-weight: 500;
    }
    .dc-verify__number {
      font-family: ui-monospace, monospace;
      text-align: start;
    }
  `,
})
export class VerifyPage {
  /** From the QR link (/verify/CODE); empty on /verify. */
  readonly code = input<string | undefined>();

  private readonly api = inject(DocumentsApi);
  private readonly router = inject(Router);
  private readonly i18n = inject(I18nService);
  protected readonly typed = signal('');
  protected readonly result = signal<VerifyView | null>(null);
  protected readonly notFound = signal(false);
  protected readonly loading = signal(false);
  protected readonly badge = computed(() => (this.result() ? docStatusBadge(this.result()!.status) : ''));
  protected readonly lineCodes = computed(() => this.result()?.lineFields.map((f) => f.code) ?? []);

  constructor() {
    effect(() => {
      const code = this.code();
      if (code) {
        this.typed.set(code);
        void this.lookUp(code);
      }
    });
  }

  protected async check(): Promise<void> {
    const code = this.typed().trim().toUpperCase();
    if (code) await this.router.navigate(['/verify', code]);
  }

  protected asOf(r: VerifyView): string {
    return this.i18n.translate('verify.asOf').replace('{n}', String(r.version)).replace('{date}', r.versionIssuedAt.slice(0, 10));
  }

  protected text(value: unknown): string {
    return fieldText(value);
  }

  private async lookUp(code: string): Promise<void> {
    this.loading.set(true);
    this.notFound.set(false);
    this.result.set(null);
    try {
      this.result.set(await firstValueFrom(this.api.verify(code)));
    } catch (error) {
      this.notFound.set(error instanceof HttpErrorResponse);
    } finally {
      this.loading.set(false);
    }
  }
}
