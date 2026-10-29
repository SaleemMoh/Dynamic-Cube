import { ChangeDetectionStrategy, Component, ElementRef, effect, input, output, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe } from '../i18n/translate.pipe';

/** Elements that never belong in authored content (they run code or embed other pages). */
const BLOCKED = 'script,style,iframe,frame,frameset,object,embed,link,meta,base,form,input,button,textarea,select';

/**
 * Authored HTML made safe to show and edit: blocked elements, event handlers (`on…`) and script URLs are removed;
 * formatting, inline styles, tables and images stay (emails need them).
 */
export function cleanHtml(html: string | null | undefined): string {
  if (!html) return '';
  const doc = new DOMParser().parseFromString(`<body>${html}</body>`, 'text/html');
  doc.body.querySelectorAll(BLOCKED).forEach((e) => e.remove());
  doc.body.querySelectorAll('*').forEach((e) => {
    for (const attr of [...e.attributes]) {
      const name = attr.name.toLowerCase();
      // Browsers ignore whitespace and control characters inside a URL scheme.
      const value = [...attr.value]
        .filter((c) => c.charCodeAt(0) > 32)
        .join('')
        .toLowerCase();
      if (
        name.startsWith('on') ||
        ((name === 'href' || name === 'src' || name === 'xlink:href') &&
          (value.startsWith('javascript:') || value.startsWith('vbscript:') || value.startsWith('data:text')))
      ) {
        e.removeAttribute(attr.name);
      }
    }
  });
  return doc.body.innerHTML;
}

/** True when the HTML shows no text (an emptied editor leaves `<br>` or empty paragraphs). */
export function isBlankHtml(html: string | null | undefined): boolean {
  if (!html) return true;
  const doc = new DOMParser().parseFromString(`<body>${html}</body>`, 'text/html');
  return !doc.body.textContent?.trim() && !doc.body.querySelector('img,table,hr');
}

interface Tool {
  command: string;
  icon: string;
  label: string;
  value?: string;
}

/**
 * A rich text editor for content such as email bodies: formatting from a toolbar (no HTML to write), fields inserted as
 * placeholders (`{{ request.number }}`), and an HTML view for those who want to see or paste the markup.
 */
@Component({
  selector: 'dc-rich-text-editor',
  imports: [FormsModule, MatButtonModule, MatButtonToggleModule, MatIconModule, MatMenuModule, MatTooltipModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-rte" [class.dc-rte--disabled]="disabled()">
      @if (label()) {
        <span class="dc-rte__label">{{ label() }}</span>
      }
      <div class="dc-rte__bar" role="toolbar" [attr.aria-label]="label()">
        @if (mode() === 'visual') {
          @for (group of tools; track $index) {
            <span class="dc-rte__group">
              @for (t of group; track t.command + (t.value ?? '')) {
                <button
                  type="button"
                  class="dc-rte__tool"
                  (mousedown)="$event.preventDefault()"
                  (click)="run(t)"
                  [matTooltip]="t.label | dcTranslate"
                  [attr.aria-label]="t.label | dcTranslate"
                  [disabled]="disabled()"
                  [attr.data-testid]="'rte-' + t.command + (t.value ? '-' + t.value : '')"
                >
                  <mat-icon fontSet="material-symbols-outlined">{{ t.icon }}</mat-icon>
                </button>
              }
            </span>
          }
          <button
            type="button"
            class="dc-rte__tool"
            (mousedown)="$event.preventDefault()"
            (click)="link()"
            [matTooltip]="'rte.link' | dcTranslate"
            [attr.aria-label]="'rte.link' | dcTranslate"
            [disabled]="disabled()"
          >
            <mat-icon fontSet="material-symbols-outlined">link</mat-icon>
          </button>
          @if (fields().length) {
            <button
              type="button"
              matButton
              class="dc-rte__fields"
              (mousedown)="$event.preventDefault()"
              [matMenuTriggerFor]="fieldMenu"
              [disabled]="disabled()"
              data-testid="rte-insert-field"
            >
              <mat-icon fontSet="material-symbols-outlined">data_object</mat-icon>
              {{ 'rte.insertField' | dcTranslate }}
            </button>
            <mat-menu #fieldMenu="matMenu" class="dc-rte__menu">
              @for (f of fields(); track f) {
                <button mat-menu-item type="button" (click)="insertField(f)" dir="ltr">
                  <code>{{ f }}</code>
                </button>
              }
            </mat-menu>
          }
        }
        <mat-button-toggle-group class="dc-rte__mode" [ngModel]="mode()" (ngModelChange)="setMode($event)" hideSingleSelectionIndicator>
          <mat-button-toggle value="visual" data-testid="rte-visual">{{ 'rte.visual' | dcTranslate }}</mat-button-toggle>
          <mat-button-toggle value="html" data-testid="rte-html">HTML</mat-button-toggle>
        </mat-button-toggle-group>
      </div>
      <div
        #area
        class="dc-rte__area"
        [hidden]="mode() !== 'visual'"
        [attr.contenteditable]="!disabled()"
        [attr.dir]="dir()"
        role="textbox"
        aria-multiline="true"
        [attr.aria-label]="label()"
        [style.min-height.rem]="minRows() * 1.6"
        (input)="changed()"
        (blur)="changed()"
        [attr.data-testid]="testId()"
      ></div>
      @if (mode() === 'html') {
        <textarea
          class="dc-rte__source"
          dir="ltr"
          spellcheck="false"
          [rows]="minRows() + 2"
          [ngModel]="html()"
          (ngModelChange)="sourceChanged($event)"
          [disabled]="disabled()"
          [attr.data-testid]="testId() ? testId() + '-html' : null"
        ></textarea>
      }
    </div>
  `,
  styles: `
    .dc-rte {
      display: grid;
      border: 1px solid var(--dga-gray-300, #d0d5dd);
      border-radius: var(--shell-radius, 8px);
      background: #fff;
      overflow: hidden;
    }
    .dc-rte:focus-within {
      border-color: var(--dynamic-cube-gold, #b08d57);
      box-shadow: 0 0 0 1px var(--dynamic-cube-gold, #b08d57);
    }
    .dc-rte__label {
      padding: 6px 10px 0;
      color: var(--dga-gray-500, #667085);
      font-size: 12px;
    }
    .dc-rte__bar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 2px 6px;
      padding: 4px 6px;
      border-bottom: 1px solid var(--dga-gray-200, #eaecf0);
      background: var(--dga-gray-50, #f9fafb);
    }
    .dc-rte__group {
      display: inline-flex;
      gap: 1px;
      padding-inline-end: 6px;
      border-inline-end: 1px solid var(--dga-gray-200, #eaecf0);
    }
    .dc-rte__tool {
      display: grid;
      place-items: center;
      width: 30px;
      height: 30px;
      border: 0;
      border-radius: 6px;
      background: none;
      color: var(--dga-gray-700, #344054);
      cursor: pointer;
    }
    .dc-rte__tool:hover:not(:disabled) {
      background: var(--dga-gray-200, #eaecf0);
    }
    .dc-rte__tool mat-icon {
      width: 20px;
      height: 20px;
      font-size: 20px;
    }
    .dc-rte__fields {
      min-height: 30px;
    }
    .dc-rte__mode {
      margin-inline-start: auto;
      height: 30px;
      align-items: center;
      font-size: 12px;
    }
    .dc-rte__area {
      padding: 10px 12px;
      outline: none;
      line-height: 1.7;
      overflow-wrap: anywhere;
    }
    .dc-rte__area[hidden] {
      display: none;
    }
    .dc-rte__area:empty::before {
      content: attr(data-placeholder);
      color: var(--dga-gray-400, #98a2b3);
    }
    .dc-rte__source {
      width: 100%;
      box-sizing: border-box;
      padding: 10px 12px;
      border: 0;
      outline: none;
      resize: vertical;
      font-family: ui-monospace, monospace;
      font-size: 13px;
    }
    .dc-rte--disabled .dc-rte__area {
      background: var(--dga-gray-50, #f9fafb);
    }
  `,
})
export class RichTextEditor {
  /** The HTML content. */
  readonly value = input<string | null>(null);
  readonly label = input<string | null>(null);
  readonly dir = input<'rtl' | 'ltr'>('rtl');
  /** Field paths offered by "Insert field" (inserted as `{{ path }}`). */
  readonly fields = input<string[]>([]);
  readonly disabled = input(false);
  readonly minRows = input(6);
  readonly testId = input<string | null>(null);
  readonly valueChange = output<string>();

  private readonly area = viewChild.required<ElementRef<HTMLDivElement>>('area');
  protected readonly mode = signal<'visual' | 'html'>('visual');
  protected readonly html = signal('');
  /** The last value sent out, so it is not written back into the editor (which would move the caret). */
  private emitted: string | null = null;

  protected readonly tools: Tool[][] = [
    [
      { command: 'bold', icon: 'format_bold', label: 'rte.bold' },
      { command: 'italic', icon: 'format_italic', label: 'rte.italic' },
      { command: 'underline', icon: 'format_underlined', label: 'rte.underline' },
    ],
    [
      { command: 'formatBlock', value: 'h2', icon: 'format_h2', label: 'rte.heading' },
      { command: 'formatBlock', value: 'h3', icon: 'format_h3', label: 'rte.subheading' },
      { command: 'formatBlock', value: 'p', icon: 'format_paragraph', label: 'rte.paragraph' },
    ],
    [
      { command: 'insertUnorderedList', icon: 'format_list_bulleted', label: 'rte.bullets' },
      { command: 'insertOrderedList', icon: 'format_list_numbered', label: 'rte.numbers' },
    ],
    [
      { command: 'justifyRight', icon: 'format_align_right', label: 'rte.alignRight' },
      { command: 'justifyCenter', icon: 'format_align_center', label: 'rte.alignCenter' },
      { command: 'justifyLeft', icon: 'format_align_left', label: 'rte.alignLeft' },
    ],
    [
      { command: 'insertHorizontalRule', icon: 'horizontal_rule', label: 'rte.line' },
      { command: 'removeFormat', icon: 'format_clear', label: 'rte.clear' },
    ],
  ];

  constructor() {
    // Outside changes (a new row, an import) replace the content; the editor's own changes come back unchanged.
    effect(() => {
      const raw = this.value() ?? '';
      const area = this.area().nativeElement;
      if (raw === this.emitted) return;
      untracked(() => {
        const value = cleanHtml(raw);
        this.html.set(value);
        area.innerHTML = value;
      });
    });
  }

  protected run(tool: Tool): void {
    this.focus();
    document.execCommand(tool.command, false, tool.value);
    this.changed();
  }

  protected link(): void {
    const url = prompt('URL', 'https://');
    if (!url) return;
    this.focus();
    document.execCommand('createLink', false, url);
    this.changed();
  }

  protected insertField(path: string): void {
    this.focus();
    document.execCommand('insertText', false, `{{ ${path} }}`);
    this.changed();
  }

  protected changed(): void {
    const value = this.area().nativeElement.innerHTML;
    if (value === this.html()) return;
    this.html.set(value);
    this.emit(isBlankHtml(value) ? '' : value);
  }

  protected sourceChanged(source: string): void {
    this.html.set(source);
    this.emit(source);
  }

  protected setMode(mode: 'visual' | 'html'): void {
    if (mode === 'visual') {
      const clean = cleanHtml(this.html());
      this.area().nativeElement.innerHTML = clean;
      this.html.set(clean);
    }
    this.mode.set(mode);
  }

  private emit(value: string): void {
    this.emitted = value;
    this.valueChange.emit(value);
  }

  private focus(): void {
    const area = this.area().nativeElement;
    if (document.activeElement !== area && !area.contains(document.activeElement)) area.focus();
  }
}
