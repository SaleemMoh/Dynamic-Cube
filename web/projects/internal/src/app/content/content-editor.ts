import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { AppsControlApi, ConfigRow, LocalizePipe, MarkdownView, TranslatePipe, pageSlug, problemMessages } from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';
import { ConfigEditorData } from '../apps-control/config/config-editor';
import { DraftsService } from '../apps-control/drafts.service';

const CODE = /^[A-Z][A-Z0-9_-]{0,50}$/;

/**
 * Writes a portal page or a frequently asked question: title / question and Markdown body / answer in both languages,
 * each shown as the portal will show it while typing.
 */
@Component({
  selector: 'dc-content-editor',
  imports: [
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    LocalizePipe,
    MarkdownView,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './content-editor.html',
  styleUrl: './content-editor.scss',
})
export class ContentEditor {
  protected readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ContentEditor, boolean>);
  private readonly api = inject(AppsControlApi);
  protected readonly drafts = inject(DraftsService);

  private readonly row = this.data.row;
  protected readonly isPage = this.data.type.entityType === 'cms.page';
  protected readonly code = signal(this.row?.code ?? '');
  protected readonly titleAr = signal((this.row?.nameAr as string) ?? '');
  protected readonly titleEn = signal((this.row?.nameEn as string) ?? '');
  protected readonly bodyAr = signal(this.text(this.isPage ? 'bodyAr' : 'answerAr'));
  protected readonly bodyEn = signal(this.text(this.isPage ? 'bodyEn' : 'answerEn'));
  protected readonly active = signal((this.row?.['isActive'] as boolean | undefined) ?? true);
  protected readonly order = signal(Number(this.row?.['order'] ?? 0));
  protected readonly showInFooter = signal(!!this.row?.['showInFooter']);
  protected readonly topicAr = signal(this.text('topicAr'));
  protected readonly topicEn = signal(this.text('topicEn'));
  protected readonly serviceCode = signal<string | null>((this.row?.['serviceCode'] as string) ?? null);
  protected readonly language = signal<'ar' | 'en'>('ar');
  protected readonly errors = signal<string[]>([]);

  private readonly services = toSignal(this.api.list('svc.service').pipe(catchError(() => of([] as ConfigRow[]))), { initialValue: [] as ConfigRow[] });
  private readonly faqs = toSignal(this.isPage ? of([] as ConfigRow[]) : this.api.list('cms.faq').pipe(catchError(() => of([] as ConfigRow[]))), {
    initialValue: [] as ConfigRow[],
  });
  protected readonly serviceOptions = computed(() =>
    this.services().map((s) => ({ code: s.code, name: { ar: (s.nameAr as string) || s.code, en: (s.nameEn as string) || s.code } })),
  );
  /** Topics already used, offered for consistency. */
  protected readonly topics = computed(() => {
    const seen = new Map<string, { ar: string; en: string }>();
    for (const f of this.faqs()) if (f['topicEn']) seen.set(f['topicEn'] as string, { ar: (f['topicAr'] as string) ?? '', en: f['topicEn'] as string });
    return [...seen.values()];
  });
  protected readonly slug = computed(() => pageSlug(this.code().trim() || 'CODE'));

  protected readonly problems = computed(() => {
    const list: string[] = [];
    if (!this.row && !CODE.test(this.code().trim())) list.push('content.problem.code');
    if (!this.titleAr().trim() || !this.titleEn().trim()) list.push(this.isPage ? 'content.problem.title' : 'content.problem.question');
    if (!this.bodyAr().trim() || !this.bodyEn().trim()) list.push(this.isPage ? 'content.problem.body' : 'content.problem.answer');
    if (!this.isPage && !this.topicAr().trim() !== !this.topicEn().trim()) list.push('content.problem.topic');
    return list;
  });

  protected useTopic(topic: { ar: string; en: string }): void {
    this.topicAr.set(topic.ar);
    this.topicEn.set(topic.en);
  }

  protected async save(): Promise<void> {
    this.errors.set([]);
    const common = { nameAr: this.titleAr().trim(), nameEn: this.titleEn().trim(), isActive: this.active(), order: Number(this.order()) || 0 };
    const payload = this.isPage
      ? { ...common, bodyAr: this.bodyAr(), bodyEn: this.bodyEn(), showInFooter: this.showInFooter() }
      : {
          ...common,
          answerAr: this.bodyAr(),
          answerEn: this.bodyEn(),
          topicAr: this.topicAr().trim() || null,
          topicEn: this.topicEn().trim() || null,
          serviceCode: this.serviceCode(),
        };
    try {
      await this.drafts.addChange(this.data.type.entityType, this.code().trim().toUpperCase(), this.row ? 'UPDATE' : 'CREATE', payload);
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  private text(field: string): string {
    return (this.row?.[field] as string) ?? '';
  }
}
