import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { LocalizedText } from '../../i18n/language';
import { FeeLine, FieldError, ResolvedVersion } from '../service.models';
import { AssistantContext } from './assistant';
import { PanelAssistant } from './panel-assistant';
import { PanelResult } from './panel-result';
import { PanelSummary } from './panel-summary';
import { LiveResultView, PanelDefinition } from './panel.models';
import { Readiness } from './readiness';

/**
 * The side column of a request form: the assistant, the request summary and the live result, each shown when the service
 * version turns it on. The readiness check is a separate component ({@link PanelReadiness}) placed where the page wants it.
 */
@Component({
  selector: 'dc-service-panel',
  imports: [PanelAssistant, PanelResult, PanelSummary],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (panel().assistant.enabled) {
      <dc-panel-assistant [definition]="panel().assistant" [context]="context()" />
    }
    @if (panel().summary.enabled) {
      <dc-panel-summary
        [definition]="panel().summary"
        [version]="version()"
        [service]="service()"
        [data]="data()"
        [levelCode]="levelCode()"
        [number]="number()"
        [readiness]="readiness()"
        [fees]="fees()"
        [files]="files()"
        [result]="panel().result"
        [live]="live()"
      />
    }
    @if (panel().result.enabled) {
      <dc-panel-result
        [definition]="panel().result"
        [view]="live()"
        [loading]="liveLoading()"
        [error]="liveError()"
      />
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: 16px;
      align-content: start;
    }
  `,
})
export class ServicePanel {
  readonly panel = input.required<PanelDefinition>();
  readonly version = input.required<ResolvedVersion>();
  readonly service = input.required<LocalizedText>();
  readonly data = input<Record<string, unknown>>({});
  readonly levelCode = input<string | null>(null);
  /** The step shown in the wizard. */
  readonly step = input<string | null>(null);
  readonly number = input<string | null>(null);
  readonly readiness = input.required<Readiness>();
  readonly fees = input<FeeLine[]>([]);
  readonly files = input<{ id: string; fileName: string }[]>([]);
  readonly errors = input<FieldError[]>([]);
  readonly live = input<LiveResultView | null>(null);
  readonly liveLoading = input(false);
  readonly liveError = input<LocalizedText | null>(null);

  protected readonly context = computed<AssistantContext>(() => ({
    version: this.version(),
    data: this.data(),
    levelCode: this.levelCode(),
    step: this.step(),
    readiness: this.readiness(),
    result: this.live()?.output ?? null,
    service: this.service(),
    errors: this.errors(),
  }));
}
