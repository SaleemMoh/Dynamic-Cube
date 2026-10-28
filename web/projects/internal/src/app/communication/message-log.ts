import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DomSanitizer } from '@angular/platform-browser';
import { CHANNELS, CommunicationApi, LocalizePipe, MESSAGE_STATUSES, MessageStatus, MessageView, TranslatePipe, problemMessages } from '@dynamic-cube/shared';
import { debounceTime, firstValueFrom } from 'rxjs';
import { PrivilegesService } from '../apps-control/privileges.service';

/** Badge modifier of a message status. */
export function messageBadge(status: MessageStatus): string {
  return status === 'SENT' ? 'dc-badge--success' : status === 'FAILED' ? 'dc-badge--danger' : status === 'PENDING' ? 'dc-badge--info' : '';
}

/** One message: who it went to, what it said (an email in a frame without scripts) and how delivery went. */
@Component({
  selector: 'dc-message-dialog',
  imports: [DatePipe, MatButtonModule, MatDialogModule, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let m = view().message;
    <h2 mat-dialog-title>{{ 'channel.' + m.channel | dcTranslate }} — {{ m.recipientName ?? m.recipient }}</h2>
    <mat-dialog-content class="dc-msgview">
      <dl class="dc-msgview__facts">
        <dt>{{ 'messages.status' | dcTranslate }}</dt>
        <dd>
          <span class="dc-badge" [class]="badge(m.status)">{{ 'msgStatus.' + m.status | dcTranslate }}</span>
        </dd>
        <dt>{{ 'messages.address' | dcTranslate }}</dt>
        <dd dir="ltr">{{ m.address ?? '—' }}</dd>
        <dt>{{ 'messages.event' | dcTranslate }}</dt>
        <dd dir="ltr">{{ m.eventCode }} · {{ m.ruleCode }}</dd>
        <dt>{{ 'messages.subject' | dcTranslate }}</dt>
        <dd dir="ltr">{{ m.subjectNumber ?? m.subjectId }}</dd>
        <dt>{{ 'messages.created' | dcTranslate }}</dt>
        <dd dir="ltr">{{ m.createdAt | date: 'yyyy-MM-dd HH:mm:ss' }}</dd>
        @if (m.sentAt) {
          <dt>{{ 'messages.sent' | dcTranslate }}</dt>
          <dd dir="ltr">{{ m.sentAt | date: 'yyyy-MM-dd HH:mm:ss' }}</dd>
        }
        <dt>{{ 'messages.attempts' | dcTranslate }}</dt>
        <dd>{{ m.attempts }}</dd>
      </dl>
      @if (m.error) {
        <p class="dc-errors" role="alert">{{ m.error }}</p>
      }
      @if (m.title) {
        <p class="dc-msgview__title">{{ m.title }}</p>
      }
      @if (m.channel === 'EMAIL') {
        <iframe class="dc-msgview__frame" sandbox="" [srcdoc]="html()" [title]="m.title ?? ''"></iframe>
      } @else {
        <p class="dc-msgview__body">{{ view().body }}</p>
      }
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li>{{ e }}</li>
          }
        </ul>
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      @if (m.status === 'FAILED' && data.canResend) {
        <button matButton="filled" type="button" (click)="resend()" data-testid="message-resend">
          <mat-icon fontSet="material-symbols-outlined">send</mat-icon>
          {{ 'messages.resend' | dcTranslate }}
        </button>
      }
      <button matButton mat-dialog-close>{{ 'common.close' | dcTranslate }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-msgview {
      display: grid;
      gap: 0.75rem;
    }
    .dc-msgview__facts {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 0.25rem 1rem;
      margin: 0;
    }
    .dc-msgview__facts dt {
      color: var(--dga-gray-500, #6c737f);
    }
    .dc-msgview__facts dd {
      margin: 0;
    }
    .dc-msgview__title {
      margin: 0;
      font-weight: 600;
    }
    .dc-msgview__body {
      margin: 0;
      white-space: pre-wrap;
    }
    .dc-msgview__frame {
      width: 100%;
      height: 22rem;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 8px;
      background: #ffffff;
    }
  `,
})
export class MessageDialog {
  protected readonly data = inject<{ view: MessageView; canResend: boolean }>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<MessageDialog, boolean>);
  private readonly api = inject(CommunicationApi);
  private readonly sanitizer = inject(DomSanitizer);
  protected readonly view = signal(this.data.view);
  protected readonly errors = signal<string[]>([]);
  protected readonly html = computed(() => this.sanitizer.bypassSecurityTrustHtml(this.view().body));

  protected badge(status: MessageStatus): string {
    return messageBadge(status);
  }

  protected async resend(): Promise<void> {
    try {
      await firstValueFrom(this.api.resend(this.view().message.id));
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}

/** Every message the platform created: in-app, email and SMS, with their delivery and errors. */
@Component({
  selector: 'dc-message-log',
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'messages.title' | dcTranslate }}</h1>
    </header>
    <div class="dc-toolbar">
      <mat-form-field class="dc-toolbar__search" subscriptSizing="dynamic">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input
          matInput
          [placeholder]="'messages.searchHint' | dcTranslate"
          [ngModel]="search()"
          (ngModelChange)="search.set($event); skip.set(0)"
          data-testid="message-search"
        />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'messages.status' | dcTranslate }}</mat-label>
        <mat-select [ngModel]="status()" (ngModelChange)="status.set($event); skip.set(0)">
          <mat-option [value]="null">{{ 'requests.allStatuses' | dcTranslate }}</mat-option>
          @for (s of statuses; track s) {
            <mat-option [value]="s">{{ 'msgStatus.' + s | dcTranslate }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'messages.channel' | dcTranslate }}</mat-label>
        <mat-select [ngModel]="channel()" (ngModelChange)="channel.set($event); skip.set(0)">
          <mat-option [value]="null">{{ 'messages.allChannels' | dcTranslate }}</mat-option>
          @for (c of channels; track c) {
            <mat-option [value]="c">{{ 'channel.' + c | dcTranslate }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'messages.event' | dcTranslate }}</mat-label>
        <mat-select [ngModel]="eventCode()" (ngModelChange)="eventCode.set($event); skip.set(0)">
          <mat-option [value]="null">{{ 'messages.allEvents' | dcTranslate }}</mat-option>
          @for (e of events.value() ?? []; track e.code) {
            <mat-option [value]="e.code">{{ { ar: e.nameAr, en: e.nameEn } | dcLocalize }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>
    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (e of errors(); track $index) {
          <li>{{ e }}</li>
        }
      </ul>
    }
    <table mat-table [dataSource]="page.value()?.items ?? []" class="dc-table">
      <ng-container matColumnDef="created">
        <th mat-header-cell *matHeaderCellDef>{{ 'messages.created' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let m" dir="ltr" class="dc-nowrap">{{ m.createdAt | date: 'yyyy-MM-dd HH:mm' }}</td>
      </ng-container>
      <ng-container matColumnDef="channel">
        <th mat-header-cell *matHeaderCellDef>{{ 'messages.channel' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let m">{{ 'channel.' + m.channel | dcTranslate }}</td>
      </ng-container>
      <ng-container matColumnDef="recipient">
        <th mat-header-cell *matHeaderCellDef>{{ 'messages.recipient' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let m">
          {{ m.recipientName ?? m.recipient }}
          @if (m.address) {
            <div class="dc-muted" dir="ltr">{{ m.address }}</div>
          }
        </td>
      </ng-container>
      <ng-container matColumnDef="title">
        <th mat-header-cell *matHeaderCellDef>{{ 'messages.message' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let m" class="dc-msglog__title">{{ m.title ?? m.eventCode }}</td>
      </ng-container>
      <ng-container matColumnDef="subject">
        <th mat-header-cell *matHeaderCellDef>{{ 'messages.subject' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let m" dir="ltr" class="dc-nowrap">{{ m.subjectNumber }}</td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef>{{ 'messages.status' | dcTranslate }}</th>
        <td mat-cell *matCellDef="let m">
          <span class="dc-badge" [class]="badge(m.status)" [matTooltip]="m.error ?? ''">{{ 'msgStatus.' + m.status | dcTranslate }}</span>
        </td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let m" class="dc-table__actions">
          <button matIconButton type="button" (click)="open(m.id)" [attr.aria-label]="'messages.open' | dcTranslate" [attr.data-testid]="'message-' + m.id">
            <mat-icon fontSet="material-symbols-outlined">visibility</mat-icon>
          </button>
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
    @if ((page.value()?.items ?? []).length === 0 && !page.isLoading()) {
      <p class="dc-empty">{{ 'messages.none' | dcTranslate }}</p>
    }
    <mat-paginator [length]="page.value()?.total ?? 0" [pageSize]="take()" [pageSizeOptions]="[25, 50, 100]" (page)="paginate($event)" />
  `,
  styles: `
    .dc-nowrap {
      white-space: nowrap;
    }
    .dc-msglog__title {
      max-width: 28rem;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  `,
})
export class MessageLog {
  private readonly api = inject(CommunicationApi);
  private readonly dialog = inject(MatDialog);
  private readonly privileges = inject(PrivilegesService);
  protected readonly columns = ['created', 'channel', 'recipient', 'title', 'subject', 'status', 'actions'];
  protected readonly statuses = MESSAGE_STATUSES;
  protected readonly channels = CHANNELS;
  protected readonly events = rxResource({ stream: () => this.api.events() });
  protected readonly search = signal('');
  private readonly debounced = toSignal(toObservable(this.search).pipe(debounceTime(300)), { initialValue: '' });
  protected readonly status = signal<MessageStatus | null>(null);
  protected readonly channel = signal<string | null>(null);
  protected readonly eventCode = signal<string | null>(null);
  protected readonly skip = signal(0);
  protected readonly take = signal(50);
  protected readonly errors = signal<string[]>([]);
  protected readonly page = rxResource({
    params: () => ({
      search: this.debounced().trim() || null,
      status: this.status(),
      channel: this.channel(),
      eventCode: this.eventCode(),
      skip: this.skip(),
      take: this.take(),
    }),
    stream: ({ params }) => this.api.messages(params),
  });

  protected badge(status: MessageStatus): string {
    return messageBadge(status);
  }

  protected paginate(event: PageEvent): void {
    this.take.set(event.pageSize);
    this.skip.set(event.pageIndex * event.pageSize);
  }

  protected async open(id: string): Promise<void> {
    this.errors.set([]);
    try {
      const view = await firstValueFrom(this.api.message(id));
      const resent = await firstValueFrom(
        this.dialog
          .open(MessageDialog, { data: { view, canResend: this.privileges.can('communication.messages', 'EDIT') }, width: '48rem', maxWidth: '95vw' })
          .afterClosed(),
      );
      if (resent) this.page.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}
