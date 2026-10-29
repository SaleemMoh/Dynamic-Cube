import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import {
  AppsControlApi,
  CHANNELS,
  ChannelPreview,
  CommunicationApi,
  ConfigRow,
  I18nService,
  JsonLogicRule,
  LocalizePipe,
  MessageTemplateWording,
  RuleEditor,
  RuleVariable,
  TranslatePipe,
  localize,
  problemMessages,
  varPath,
} from '@dynamic-cube/shared';
import { catchError, debounceTime, of, switchMap } from 'rxjs';
import { ConfigEditorData } from '../apps-control/config/config-editor';
import { DraftsService } from '../apps-control/drafts.service';
import { samplePaths } from './message-template-editor';

type Channel = (typeof CHANNELS)[number];
type RecipientKind = 'PARTY' | 'ROLE' | 'USER';

/** A recipient as edited: the party, roles, named employees, or employees named by the event (e.g. the assignee). */
interface RecipientRow {
  kind: RecipientKind;
  /** Named roles or employees: role codes / user names. */
  codes: string[];
  /** Roles or employees named by the event: the field holding the role code or user name (e.g. the assignee). */
  fromEvent: string | null;
}

const CODE = /^[A-Z][A-Z0-9_-]{0,50}$/;

/** Fields of a template each channel needs (in both languages) before a rule may use it. */
const CHANNEL_FIELDS: Record<Channel, (keyof MessageTemplateWording)[]> = {
  EMAIL: ['emailSubjectAr', 'emailSubjectEn', 'emailBodyAr', 'emailBodyEn'],
  SMS: ['smsAr', 'smsEn'],
  IN_APP: ['inAppTitleAr', 'inAppTitleEn', 'inAppBodyAr', 'inAppBodyEn'],
};

export function toRows(value: unknown): RecipientRow[] {
  const list = Array.isArray(value) ? (value as { kind: string; value?: unknown }[]) : [];
  return list.map((r) => {
    const kind = (r.kind?.toUpperCase() as RecipientKind) ?? 'PARTY';
    const path = varPath(r.value as never);
    if (path !== null) return { kind, codes: [], fromEvent: path };
    const codes = Array.isArray(r.value) ? r.value.map(String) : r.value === null || r.value === undefined ? [] : [String(r.value)];
    return { kind, codes, fromEvent: null };
  });
}

export function fromRows(rows: RecipientRow[]): { kind: string; value?: JsonLogicRule }[] {
  return rows.map((r) => {
    if (r.kind === 'PARTY') return { kind: 'PARTY' };
    if (r.fromEvent) return { kind: r.kind, value: { var: r.fromEvent } };
    return { kind: r.kind, value: r.codes.length === 1 ? r.codes[0] : r.codes };
  });
}

/**
 * Notification rule builder: when an event happens (and its condition holds, built without JSON over the event's
 * facts), send a template on the chosen channels to the party, holders of roles, named employees or the employee the
 * event names. The message is previewed on the event's sample facts as it will be sent.
 */
@Component({
  selector: 'dc-notification-rule-editor',
  imports: [
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    LocalizePipe,
    TranslatePipe,
    RuleEditor,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './notification-rule-editor.html',
  styleUrl: './notification-rule-editor.scss',
})
export class NotificationRuleEditor {
  protected readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<NotificationRuleEditor, boolean>);
  private readonly communication = inject(CommunicationApi);
  private readonly api = inject(AppsControlApi);
  protected readonly drafts = inject(DraftsService);
  protected readonly i18n = inject(I18nService);
  private readonly sanitizer = inject(DomSanitizer);
  protected readonly channels = CHANNELS;
  protected readonly kinds: RecipientKind[] = ['PARTY', 'ROLE', 'USER'];

  private readonly row = this.data.row;
  protected readonly code = signal(this.row?.code ?? '');
  protected readonly nameAr = signal((this.row?.nameAr as string) ?? '');
  protected readonly nameEn = signal((this.row?.nameEn as string) ?? '');
  protected readonly active = signal((this.row?.['isActive'] as boolean | undefined) ?? true);
  protected readonly eventCode = signal<string | null>((this.row?.['eventCode'] as string) ?? null);
  protected readonly condition = signal<JsonLogicRule | null>((this.row?.['condition'] as JsonLogicRule) ?? null);
  protected readonly templateCode = signal<string | null>((this.row?.['templateCode'] as string) ?? null);
  protected readonly chosen = signal<Channel[]>((this.row?.['channels'] as Channel[]) ?? ['IN_APP']);
  protected readonly recipients = signal<RecipientRow[]>(this.row ? toRows(this.row['recipients']) : [{ kind: 'PARTY', codes: [], fromEvent: null }]);
  protected readonly language = signal<'AR' | 'EN'>((this.row?.['language'] as 'AR' | 'EN') ?? 'AR');
  protected readonly previewLanguage = signal<'ar' | 'en'>('ar');
  protected readonly errors = signal<string[]>([]);

  protected readonly events = toSignal(this.communication.events().pipe(catchError(() => of([]))), { initialValue: [] });
  private readonly templates = toSignal(this.api.list('com.template').pipe(catchError(() => of([] as ConfigRow[]))), { initialValue: [] as ConfigRow[] });
  private readonly roles = toSignal(this.api.list('apps.role').pipe(catchError(() => of([] as ConfigRow[]))), { initialValue: [] as ConfigRow[] });
  private readonly users = toSignal(this.api.list('apps.user').pipe(catchError(() => of([] as ConfigRow[]))), { initialValue: [] as ConfigRow[] });

  protected readonly modules = computed(() => [...new Set(this.events().map((e) => e.module))]);
  protected readonly event = computed(() => this.events().find((e) => e.code === this.eventCode()) ?? null);
  protected readonly template = computed(() => this.templates().find((t) => t.code === this.templateCode()) ?? null);
  protected readonly templateOptions = computed(() => this.templates().filter((t) => t['isActive'] !== false));
  protected readonly roleOptions = computed(() =>
    this.roles().map((r) => ({ value: r.code, label: { ar: (r.nameAr as string) || r.code, en: (r.nameEn as string) || r.code } })),
  );
  protected readonly userOptions = computed(() =>
    this.users().map((u) => ({ value: u.code, label: { ar: (u.nameAr as string) || u.code, en: (u.nameEn as string) || u.code } })),
  );

  /** Everything the chosen event's facts offer, for the condition and for "the employee named by the event". */
  protected readonly variables = computed<RuleVariable[]>(() => {
    const sample = this.event()?.sample ?? {};
    return samplePaths(sample).map((path) => {
      const value = path.split('.').reduce<unknown>((v, k) => (v as Record<string, unknown> | null)?.[k], sample);
      return { path, label: { ar: path, en: path }, type: typeof value === 'number' ? 'number' : typeof value === 'boolean' ? 'boolean' : 'text' };
    });
  });

  /** Channels the chosen template has wording for. */
  protected readonly covered = computed(() => {
    const t = this.template();
    if (!t) return new Set<Channel>();
    return new Set(CHANNELS.filter((c) => CHANNEL_FIELDS[c].every((f) => typeof t[f] === 'string' && (t[f] as string).trim() !== '')));
  });

  protected readonly problems = computed(() => {
    const list: string[] = [];
    if (!this.row && !CODE.test(this.code().trim())) list.push('notifyRule.problem.code');
    if (!this.nameAr().trim() || !this.nameEn().trim()) list.push('notifyRule.problem.names');
    if (!this.eventCode()) list.push('notifyRule.problem.event');
    if (!this.templateCode()) list.push('notifyRule.problem.template');
    if (!this.chosen().length) list.push('notifyRule.problem.channels');
    if (this.chosen().some((c) => this.template() && !this.covered().has(c))) list.push('notifyRule.problem.uncovered');
    if (!this.recipients().length) list.push('notifyRule.problem.recipients');
    if (this.recipients().some((r) => r.kind !== 'PARTY' && !r.fromEvent && !r.codes.length)) list.push('notifyRule.problem.recipientValue');
    return list;
  });

  /** The message as it will be sent, on the event's sample facts. */
  private readonly previewParams = computed(() => {
    const t = this.template();
    const event = this.eventCode();
    return t && event ? { event, wording: t as unknown as MessageTemplateWording } : null;
  });
  protected readonly previews = toSignal(
    toObservable(this.previewParams).pipe(
      debounceTime(200),
      switchMap((p) => (p ? this.communication.preview(p.event, p.wording).pipe(catchError(() => of([] as ChannelPreview[]))) : of([] as ChannelPreview[]))),
    ),
    { initialValue: [] as ChannelPreview[] },
  );
  protected readonly shownPreviews = computed(() => this.previews().filter((p) => this.chosen().includes(p.channel as Channel)));

  constructor() {
    // Channels the new template cannot send are dropped.
    effect(() => {
      const covered = this.covered();
      if (!this.template()) return;
      untracked(() => this.chosen.update((list) => list.filter((c) => covered.has(c))));
    });
  }

  protected eventName(code: string): string {
    const e = this.events().find((x) => x.code === code);
    return e ? localize({ ar: e.nameAr, en: e.nameEn }, this.i18n.language()) : code;
  }

  protected eventsOf(module: string) {
    return this.events().filter((e) => e.module === module);
  }

  protected toggleChannel(channel: Channel, on: boolean): void {
    this.chosen.update((list) => (on ? [...new Set([...list, channel])] : list.filter((c) => c !== channel)));
  }

  protected recipient(index: number, change: Partial<RecipientRow>): void {
    this.recipients.update((list) => list.map((r, i) => (i === index ? { ...r, ...change, ...(change.kind ? { codes: [], fromEvent: null } : {}) } : r)));
  }

  /** Named roles / employees, or the one the event names (a field that looks like one is suggested). */
  protected valueMode(index: number, mode: 'named' | 'event'): void {
    if (mode === 'named') return this.recipient(index, { fromEvent: null });
    const hint = this.recipients()[index].kind === 'ROLE' ? /role/i : /assignee|user/i;
    const paths = this.variables().map((v) => v.path);
    this.recipient(index, { codes: [], fromEvent: paths.find((p) => hint.test(p)) ?? paths[0] ?? '' });
  }

  protected addRecipient(): void {
    this.recipients.update((list) => [...list, { kind: 'ROLE', codes: [], fromEvent: null }]);
  }

  protected removeRecipient(index: number): void {
    this.recipients.update((list) => list.filter((_, i) => i !== index));
  }

  protected emailHtml(preview: ChannelPreview): SafeHtml {
    // Shown in a frame without scripts.
    return this.sanitizer.bypassSecurityTrustHtml(this.previewLanguage() === 'ar' ? preview.bodyAr : preview.bodyEn);
  }

  protected async save(): Promise<void> {
    this.errors.set([]);
    try {
      await this.drafts.addChange(this.data.type.entityType, this.code().trim().toUpperCase(), this.row ? 'UPDATE' : 'CREATE', {
        nameAr: this.nameAr().trim(),
        nameEn: this.nameEn().trim(),
        isActive: this.active(),
        eventCode: this.eventCode(),
        condition: this.condition(),
        templateCode: this.templateCode(),
        channels: this.chosen(),
        recipients: fromRows(this.recipients()),
        language: this.language(),
      });
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}
