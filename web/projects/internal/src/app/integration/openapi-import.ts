import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import {
  AppsControlApi,
  AuthType,
  ConfigRow,
  I18nService,
  IntegrationApi,
  LocalizePipe,
  OpenApiReadResult,
  TranslatePipe,
  problemMessages,
  SearchableSelectModule,
} from '@dynamic-cube/shared';
import { catchError, firstValueFrom, of } from 'rxjs';
import {
  ConnectionChoice,
  ImportChoices,
  OperationChoice,
  ProviderChoice,
  fieldsFor,
  importItems,
  importProblems,
  initialChoices,
  matchingEnvelope,
  withEnvelope,
  withPrefix,
} from './openapi-import-model';
import { OPERATION_DESIGNER_TRANSLATIONS } from './operation-designer-translations';

const AUTH_TYPES: AuthType[] = [
  'NONE',
  'BASIC',
  'API_KEY',
  'OAUTH2_CLIENT_CREDENTIALS',
  'WS_SECURITY',
];

/**
 * Imports an OpenAPI 3.x or Swagger 2.0 description (ADR-0041), or a WSDL 1.1 one with SOAP operations (ADR-0044): reads
 * it on the server (nothing saved), suggests a new
 * provider and connection (or uses existing ones) and lists its operations with their inputs and response fields; the
 * ticked operations, with the outputs chosen, go into a change set like any configuration.
 */
@Component({
  selector: 'dc-openapi-import',
  imports: [
    FormsModule,
    LocalizePipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    SearchableSelectModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './openapi-import.html',
  styleUrl: './openapi-import.scss',
})
export class OpenApiImport {
  private readonly ref = inject(MatDialogRef<OpenApiImport, boolean>);
  private readonly api = inject(AppsControlApi);
  private readonly integration = inject(IntegrationApi);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly i18n = inject(I18nService);
  protected readonly authTypes = AUTH_TYPES;

  protected readonly text = signal('');
  protected readonly reading = signal(false);
  protected readonly read = signal<OpenApiReadResult | null>(null);
  protected readonly choices = signal<ImportChoices | null>(null);
  protected readonly prefix = signal('');
  protected readonly filter = signal('');
  protected readonly open = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly errors = signal<string[]>([]);

  private readonly list = (type: string) =>
    toSignal(this.api.list(type).pipe(catchError(() => of([] as ConfigRow[]))), {
      initialValue: [] as ConfigRow[],
    });
  protected readonly providers = this.list('integ.provider');
  protected readonly connections = this.list('integ.connection');
  protected readonly envelopes = this.list('integ.envelope-profile');
  private readonly operations = this.list('integ.operation');
  protected readonly usedCodes = computed(() => new Set(this.operations().map((o) => o.code)));

  protected readonly shown = computed(() => {
    const words = this.filter().trim().toLowerCase().split(/\s+/).filter(Boolean);
    return (this.choices()?.operations ?? []).filter((o) => {
      const text =
        `${o.code} ${o.name} ${o.source.method} ${o.source.pathTemplate} ${o.source.tag ?? ''}`.toLowerCase();
      return words.every((w) => text.includes(w));
    });
  });
  protected readonly selectedCount = computed(
    () => (this.choices()?.operations ?? []).filter((o) => o.selected).length,
  );
  protected readonly problems = computed(() => {
    const choices = this.choices();
    return choices ? importProblems(choices, this.usedCodes()) : [];
  });

  constructor() {
    this.i18n.register(OPERATION_DESIGNER_TRANSLATIONS);
  }

  protected async readFile(event: Event): Promise<void> {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (file) this.text.set(await file.text());
  }

  protected async readText(): Promise<void> {
    this.errors.set([]);
    this.reading.set(true);
    try {
      const read = await firstValueFrom(this.integration.readOpenApi(this.text()));
      let choices = initialChoices(read);
      // A provider already registered with the suggested code is used as it is.
      if (this.providers().some((p) => p.code === choices.provider.code))
        choices.provider.mode = 'existing';
      // An envelope whose result path holds every response is chosen: outputs are read from its data.
      const envelope = matchingEnvelope(read, this.envelopeRows());
      if (envelope) choices = withEnvelope(choices, envelope.code, envelope.resultPath);
      this.read.set(read);
      this.choices.set(choices);
      this.prefix.set(read.suggestedProviderCode);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.reading.set(false);
    }
  }

  protected doneText(read: OpenApiReadResult): string {
    return this.i18n
      .translate('oi.read.done')
      .replace('{title}', read.title)
      .replace('{count}', String(read.operations.length));
  }

  protected count(key: string, values: Record<string, number>): string {
    let text = this.i18n.translate(key);
    for (const [k, v] of Object.entries(values)) text = text.replace(`{${k}}`, String(v));
    return text;
  }

  private update(change: (c: ImportChoices) => void): void {
    const current = this.choices();
    if (!current) return;
    const next: ImportChoices = {
      ...current,
      provider: { ...current.provider },
      connection: { ...current.connection },
      operations: current.operations.map((o) => ({ ...o })),
    };
    change(next);
    this.choices.set(next);
  }

  protected setProvider(change: Partial<ProviderChoice>): void {
    this.update((c) => Object.assign(c.provider, change));
  }

  protected chooseProvider(code: string): void {
    const row = this.providers().find((p) => p.code === code);
    this.setProvider({ code, nameAr: row?.nameAr ?? '', nameEn: row?.nameEn ?? '' });
  }

  protected setConnection(change: Partial<ConnectionChoice>): void {
    this.update((c) => Object.assign(c.connection, change));
  }

  protected setEnvelope(code: string | null): void {
    const choices = this.choices();
    if (!choices) return;
    const row = this.envelopeRows().find((e) => e.code === code);
    this.choices.set(withEnvelope(choices, code, row?.resultPath ?? null));
  }

  /** The fields an operation's outputs can read (under the envelope's result path when one is chosen). */
  protected fields(choice: OperationChoice) {
    return fieldsFor(choice.source, this.choices()?.resultPath ?? null);
  }

  private envelopeRows(): { code: string; resultPath: string | null }[] {
    return this.envelopes().map((e) => ({
      code: e.code,
      resultPath: (e['resultPath'] as string | null | undefined) ?? null,
    }));
  }

  protected setPrefix(prefix: string): void {
    const value = prefix.toUpperCase();
    this.prefix.set(value);
    this.update((c) => c.operations.forEach((o) => (o.code = withPrefix(value, o.source.code))));
  }

  protected setOperation(index: number, change: Partial<OperationChoice>): void {
    this.update((c) => Object.assign(c.operations[index], change));
  }

  protected indexOf(choice: OperationChoice): number {
    return this.choices()!.operations.findIndex((o) => o.source === choice.source);
  }

  protected selectAll(selected: boolean): void {
    const shown = new Set(this.shown().map((o) => o.source));
    this.update((c) => c.operations.forEach((o) => shown.has(o.source) && (o.selected = selected)));
  }

  protected toggleOutput(choice: OperationChoice, path: string, on: boolean): void {
    const index = this.indexOf(choice);
    this.update((c) => {
      const outputs = new Set(c.operations[index].outputs);
      if (on) outputs.add(path);
      else outputs.delete(path);
      c.operations[index].outputs = outputs;
    });
  }

  protected async save(submit: boolean): Promise<void> {
    const choices = this.choices();
    if (!choices) return;
    if (this.problems().length) {
      this.errors.set(this.problems().map((p) => this.i18n.translate(p)));
      return;
    }
    this.errors.set([]);
    this.saving.set(true);
    try {
      const sets = await firstValueFrom(
        this.api.importPackage({
          title: `${this.i18n.translate('oi.import')} — ${this.read()?.title ?? ''}`,
          items: importItems(choices),
        }),
      );
      if (submit) for (const set of sets) await firstValueFrom(this.api.submit(set.changeSet.id));
      this.snackBar.open(this.i18n.translate(submit ? 'drafts.submitted' : 'oi.saved'), undefined, {
        duration: 5000,
      });
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected close(): void {
    this.ref.close(false);
  }
}
