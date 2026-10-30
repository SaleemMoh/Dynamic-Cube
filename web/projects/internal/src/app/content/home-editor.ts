import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog } from '@angular/material/dialog';
import { AppsControlApi, ConfigRow, TranslatePipe, pickIcon, problemMessages } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { DraftsService } from '../apps-control/drafts.service';
import { HomeSetting, METRICS, Tile, homeProblems, normalizeHome } from './home-model';
import { SubmitButton } from '../apps-control/submit-button';

/**
 * The portal home page without JSON: hero wording and banner, the statistics tiles (counted or fixed) and the help
 * links, previewed as the portal shows them. Saved as the public setting `PORTAL_HOME` of Apps Control.
 */
@Component({
  selector: 'dc-home-editor',
  imports: [
    SubmitButton,
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTooltipModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home-editor.html',
  styleUrl: './home-editor.scss',
})
export class HomeEditor {
  private readonly api = inject(AppsControlApi);
  private readonly dialog = inject(MatDialog);
  protected readonly drafts = inject(DraftsService);
  protected readonly metrics = METRICS;

  protected readonly settings = rxResource({ stream: () => this.api.list('apps.setting') });
  private readonly row = computed<ConfigRow | null>(() => this.settings.value()?.find((s) => s.code === 'PORTAL_HOME' && !s['appCode']) ?? null);
  protected readonly home = signal<HomeSetting>(normalizeHome({}));
  private readonly saved = signal('');
  protected readonly previewLanguage = signal<'ar' | 'en'>('ar');
  protected readonly errors = signal<string[]>([]);
  protected readonly saving = signal(false);

  protected readonly problems = computed(() => homeProblems(this.home()));
  protected readonly dirty = computed(() => JSON.stringify(this.home()) !== this.saved());

  constructor() {
    effect(() => {
      const row = this.row();
      if (!row) return;
      untracked(() => this.load(row));
    });
  }

  protected set<K extends keyof HomeSetting>(key: K, value: HomeSetting[K]): void {
    this.home.update((h) => ({ ...h, [key]: value }));
  }

  protected setHelp(key: keyof HomeSetting['help'], value: string): void {
    this.home.update((h) => ({ ...h, help: { ...h.help, [key]: value } }));
  }

  protected tile(index: number, change: Partial<Tile>): void {
    this.home.update((h) => ({ ...h, statistics: h.statistics.map((t, i) => (i === index ? { ...t, ...change } : t)) }));
  }

  protected tileKind(index: number, kind: 'metric' | 'value'): void {
    this.tile(index, kind === 'metric' ? { metric: 'SERVICES', value: undefined } : { metric: undefined, value: 0 });
  }

  protected async chooseIcon(index: number): Promise<void> {
    const icon = await firstValueFrom(pickIcon(this.dialog, this.home().statistics[index]?.icon));
    if (icon !== undefined) this.tile(index, { icon: icon || undefined });
  }

  protected addTile(): void {
    this.home.update((h) => ({ ...h, statistics: [...h.statistics, { metric: 'COMPLETED', icon: 'task_alt', labelAr: '', labelEn: '' }] }));
  }

  protected removeTile(index: number): void {
    this.home.update((h) => ({ ...h, statistics: h.statistics.filter((_, i) => i !== index) }));
  }

  protected moveTile(index: number, by: -1 | 1): void {
    this.home.update((h) => {
      const list = [...h.statistics];
      const [tile] = list.splice(index, 1);
      list.splice(index + by, 0, tile);
      return { ...h, statistics: list };
    });
  }

  protected reset(): void {
    const row = this.row();
    if (row) this.load(row);
  }

  protected async save(submit = false): Promise<void> {
    const row = this.row();
    if (!row) return;
    this.errors.set([]);
    this.saving.set(true);
    try {
      const value = this.home();
      await this.drafts.addChange('apps.setting', 'PORTAL_HOME', 'UPDATE', { ...row, value }, { submit });
      this.saved.set(JSON.stringify(value));
      this.settings.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.saving.set(false);
    }
  }

  private load(row: ConfigRow): void {
    const home = normalizeHome(row['value']);
    this.home.set(home);
    this.saved.set(JSON.stringify(home));
  }
}
