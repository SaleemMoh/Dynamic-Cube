import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  model,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { I18nService, TranslatePipe } from '@dynamic-cube/shared';
import {
  PRINT_FIELDS,
  PageSize,
  PrintDesign,
  PrintElement,
  PrintKind,
  clampToPage,
  newElement,
  pageMm,
} from './print-model';

const MIME = 'application/x-dc-print-element';
const PX_PER_MM = 96 / 25.4;
const MAX_IMAGE_BYTES = 200 * 1024;

const PALETTE: { kind: PrintKind; icon: string }[] = [
  { kind: 'text', icon: 'title' },
  { kind: 'field', icon: 'data_object' },
  { kind: 'table', icon: 'table' },
  { kind: 'image', icon: 'image' },
  { kind: 'qr', icon: 'qr_code_2' },
  { kind: 'line', icon: 'horizontal_rule' },
  { kind: 'box', icon: 'crop_square' },
];

interface Drag {
  id: string;
  mode: 'move' | 'resize';
  startX: number;
  startY: number;
  origin: PrintElement;
}

/**
 * Lays out a print template on its page: elements are dragged from the palette onto the page, moved and resized with
 * the mouse (or the arrow keys), and edited in the side panel. Every change goes into the layout (with undo and redo);
 * the template's HTML is generated from it.
 */
@Component({
  selector: 'dc-print-designer',
  imports: [
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(keydown)': 'onKey($event)' },
  template: `
    <div class="pd__toolbar" role="toolbar">
      <button
        matIconButton
        type="button"
        (click)="undo()"
        [disabled]="!canUndo()"
        [matTooltip]="'designer.undo' | dcTranslate"
        data-testid="pd-undo"
      >
        <mat-icon fontSet="material-symbols-outlined">undo</mat-icon>
      </button>
      <button
        matIconButton
        type="button"
        (click)="redo()"
        [disabled]="!canRedo()"
        [matTooltip]="'designer.redo' | dcTranslate"
        data-testid="pd-redo"
      >
        <mat-icon fontSet="material-symbols-outlined">redo</mat-icon>
      </button>
      <span class="pd__sep"></span>
      <button
        matIconButton
        type="button"
        (click)="zoomBy(-0.1)"
        [matTooltip]="'print.zoomOut' | dcTranslate"
      >
        <mat-icon fontSet="material-symbols-outlined">zoom_out</mat-icon>
      </button>
      <span class="pd__zoom">{{ (zoom() * 100).toFixed(0) }}%</span>
      <button
        matIconButton
        type="button"
        (click)="zoomBy(0.1)"
        [matTooltip]="'print.zoomIn' | dcTranslate"
      >
        <mat-icon fontSet="material-symbols-outlined">zoom_in</mat-icon>
      </button>
      <span class="pd__sep"></span>
      <mat-checkbox [ngModel]="grid() === 5" (ngModelChange)="grid.set($event ? 5 : 1)">{{
        'print.snap' | dcTranslate
      }}</mat-checkbox>
      <span class="pd__grow"></span>
      @if (selected(); as s) {
        <button
          matIconButton
          type="button"
          (click)="toFront(s.id)"
          [matTooltip]="'print.toFront' | dcTranslate"
        >
          <mat-icon fontSet="material-symbols-outlined">flip_to_front</mat-icon>
        </button>
        <button
          matIconButton
          type="button"
          (click)="duplicate(s.id)"
          [matTooltip]="'print.duplicate' | dcTranslate"
        >
          <mat-icon fontSet="material-symbols-outlined">content_copy</mat-icon>
        </button>
        <button
          matIconButton
          type="button"
          (click)="remove(s.id)"
          [matTooltip]="'designer.delete' | dcTranslate"
          data-testid="pd-delete"
        >
          <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
        </button>
      }
    </div>

    <div class="pd__body">
      <aside class="pd__palette" [attr.aria-label]="'print.palette' | dcTranslate">
        <h3>{{ 'print.palette' | dcTranslate }}</h3>
        @for (p of palette; track p.kind) {
          <button
            type="button"
            class="pd__tool"
            draggable="true"
            (dragstart)="startPaletteDrag($event, p.kind)"
            (click)="add(p.kind)"
            [attr.data-testid]="'pd-tool-' + p.kind"
          >
            <mat-icon fontSet="material-symbols-outlined">{{ p.icon }}</mat-icon>
            {{ 'print.kind.' + p.kind | dcTranslate }}
          </button>
        }
        <p class="dc-muted pd__tip">{{ 'print.tip' | dcTranslate }}</p>
      </aside>

      <div class="pd__canvas" (pointerdown)="select(null)" dir="ltr">
        <div
          #pageEl
          class="pd__page"
          [dir]="design().direction"
          [style.width.px]="page().w * scale()"
          [style.height.px]="page().h * scale()"
          [style.background]="design().background || '#ffffff'"
          [style.--grid.px]="grid() * scale()"
          (dragover)="$event.preventDefault()"
          (drop)="dropOnPage($event)"
          data-testid="pd-page"
        >
          @for (e of design().elements; track e.id) {
            <div
              class="pd__el"
              [class.pd__el--selected]="e.id === selectedId()"
              [class.pd__el--line]="e.kind === 'line'"
              [style.left.px]="e.x * scale()"
              [style.top.px]="e.y * scale()"
              [style.width.px]="e.w * scale()"
              [style.height.px]="e.kind === 'line' ? null : e.h * scale()"
              [style.font-size.px]="(e.fontSize ?? 12) * 1.333 * zoom()"
              [style.font-weight]="e.bold ? 700 : 400"
              [style.text-align]="e.align ?? 'start'"
              [style.color]="e.color || null"
              [style.background]="e.background || null"
              [style.border]="
                e.kind !== 'line' && e.border
                  ? e.border * 1.333 * zoom() + 'px solid ' + (e.borderColor || '#9da4ae')
                  : null
              "
              [style.border-top]="
                e.kind === 'line'
                  ? (e.border ?? 1) * 1.333 * zoom() + 'px solid ' + (e.borderColor || '#d2d6db')
                  : null
              "
              (pointerdown)="startDrag($event, e, 'move')"
              tabindex="0"
              [attr.aria-label]="'print.kind.' + e.kind | dcTranslate"
              [attr.data-testid]="'pd-el-' + e.id"
            >
              @switch (e.kind) {
                @case ('text') {
                  <span class="pd__text">{{ e.text }}</span>
                }
                @case ('field') {
                  <span class="pd__text"
                    >{{ e.text }}<span class="pd__field">{{ fieldName(e.field) }}</span></span
                  >
                }
                @case ('image') {
                  @if (e.src) {
                    <img [src]="e.src" alt="" draggable="false" />
                  } @else {
                    <mat-icon fontSet="material-symbols-outlined" class="pd__placeholder"
                      >image</mat-icon
                    >
                  }
                }
                @case ('qr') {
                  <mat-icon fontSet="material-symbols-outlined" class="pd__placeholder pd__qr"
                    >qr_code_2</mat-icon
                  >
                }
                @case ('table') {
                  <table class="pd__table">
                    <tr>
                      @for (c of e.columns ?? []; track $index) {
                        <th>{{ c.title }}</th>
                      }
                    </tr>
                    <tr>
                      @for (c of e.columns ?? []; track $index) {
                        <td>{{ c.field }}</td>
                      }
                    </tr>
                  </table>
                }
              }
              @if (e.id === selectedId()) {
                <span
                  class="pd__handle"
                  (pointerdown)="startDrag($event, e, 'resize')"
                  [attr.aria-label]="'print.resize' | dcTranslate"
                ></span>
              }
            </div>
          }
        </div>
      </div>

      <aside class="pd__props" data-testid="pd-props">
        @if (selected(); as s) {
          <h3>{{ 'print.kind.' + s.kind | dcTranslate }}</h3>
          <div class="pd__grid4">
            @for (k of boxKeys; track k) {
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'print.' + k | dcTranslate }}</mat-label>
                <input
                  matInput
                  type="number"
                  step="1"
                  [ngModel]="s[k]"
                  (ngModelChange)="setBox(s.id, k, $event)"
                  [attr.data-testid]="'pd-' + k"
                />
              </mat-form-field>
            }
          </div>
          @if (s.kind === 'text') {
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'print.text' | dcTranslate }}</mat-label>
              <textarea
                matInput
                rows="3"
                [ngModel]="s.text"
                (ngModelChange)="update(s.id, { text: $event })"
                data-testid="pd-text"
              ></textarea>
            </mat-form-field>
          }
          @if (s.kind === 'field') {
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'print.field' | dcTranslate }}</mat-label>
              <mat-select
                [ngModel]="knownField(s.field) ? s.field : '__custom'"
                (ngModelChange)="$event !== '__custom' && update(s.id, { field: $event })"
                data-testid="pd-field"
              >
                @for (f of fields; track f.path) {
                  <mat-option [value]="f.path">{{
                    i18n.localize({ ar: f.ar, en: f.en })
                  }}</mat-option>
                }
                <mat-option value="__custom">{{ 'print.customField' | dcTranslate }}</mat-option>
              </mat-select>
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'print.path' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="s.field"
                (ngModelChange)="update(s.id, { field: $event.trim() })"
                placeholder="data.city"
              />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'print.label' | dcTranslate }}</mat-label>
              <input matInput [ngModel]="s.text" (ngModelChange)="update(s.id, { text: $event })" />
            </mat-form-field>
          }
          @if (s.kind === 'image') {
            <input
              #file
              type="file"
              accept="image/png,image/jpeg,image/webp,image/svg+xml"
              hidden
              (change)="loadImage(s.id, file)"
            />
            <button matButton="outlined" type="button" (click)="file.click()">
              <mat-icon fontSet="material-symbols-outlined">upload</mat-icon
              >{{ 'print.chooseImage' | dcTranslate }}
            </button>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'print.imageUrl' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="s.src?.startsWith('data:') ? '' : s.src"
                (ngModelChange)="update(s.id, { src: $event.trim() })"
                placeholder="https://"
              />
            </mat-form-field>
            @if (imageError()) {
              <span class="pd__bad">{{ imageError()! | dcTranslate }}</span>
            }
          }
          @if (s.kind === 'table') {
            <h4>{{ 'print.columns' | dcTranslate }}</h4>
            @for (c of s.columns ?? []; track $index; let i = $index) {
              <div class="pd__column">
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ 'print.columnTitle' | dcTranslate }}</mat-label>
                  <input
                    matInput
                    [ngModel]="c.title"
                    (ngModelChange)="setColumn(s, i, { title: $event })"
                  />
                </mat-form-field>
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ 'print.columnField' | dcTranslate }}</mat-label>
                  <input
                    matInput
                    dir="ltr"
                    [ngModel]="c.field"
                    (ngModelChange)="setColumn(s, i, { field: $event.trim() })"
                  />
                </mat-form-field>
                <button
                  matIconButton
                  type="button"
                  (click)="removeColumn(s, i)"
                  [attr.aria-label]="'config.delete' | dcTranslate"
                >
                  <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
                </button>
              </div>
            }
            <button matButton type="button" (click)="addColumn(s)">
              <mat-icon fontSet="material-symbols-outlined">add</mat-icon
              >{{ 'print.addColumn' | dcTranslate }}
            </button>
          }
          @if (s.kind === 'text' || s.kind === 'field' || s.kind === 'table') {
            <div class="pd__grid2">
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'print.fontSize' | dcTranslate }}</mat-label>
                <input
                  matInput
                  type="number"
                  min="6"
                  max="72"
                  [ngModel]="s.fontSize ?? 12"
                  (ngModelChange)="update(s.id, { fontSize: +$event })"
                />
              </mat-form-field>
              <mat-checkbox [ngModel]="!!s.bold" (ngModelChange)="update(s.id, { bold: $event })">{{
                'print.bold' | dcTranslate
              }}</mat-checkbox>
            </div>
            <mat-button-toggle-group
              [value]="s.align ?? 'start'"
              (change)="update(s.id, { align: $event.value })"
              class="pd__align"
            >
              <mat-button-toggle value="start"
                ><mat-icon fontSet="material-symbols-outlined"
                  >format_align_right</mat-icon
                ></mat-button-toggle
              >
              <mat-button-toggle value="center"
                ><mat-icon fontSet="material-symbols-outlined"
                  >format_align_center</mat-icon
                ></mat-button-toggle
              >
              <mat-button-toggle value="end"
                ><mat-icon fontSet="material-symbols-outlined"
                  >format_align_left</mat-icon
                ></mat-button-toggle
              >
            </mat-button-toggle-group>
          }
          <div class="pd__grid2">
            @if (s.kind !== 'line' && s.kind !== 'image' && s.kind !== 'qr') {
              <label class="pd__color">
                <span>{{ 'print.color' | dcTranslate }}</span>
                <input
                  type="color"
                  [value]="s.color || '#111927'"
                  (change)="update(s.id, { color: $any($event.target).value })"
                />
              </label>
            }
            @if (s.kind !== 'line') {
              <label class="pd__color">
                <span>{{ 'print.background' | dcTranslate }}</span>
                <input
                  type="color"
                  [value]="s.background || '#ffffff'"
                  (change)="update(s.id, { background: $any($event.target).value })"
                />
              </label>
            }
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'print.border' | dcTranslate }}</mat-label>
              <input
                matInput
                type="number"
                min="0"
                max="10"
                [ngModel]="s.border ?? 0"
                (ngModelChange)="update(s.id, { border: +$event })"
              />
            </mat-form-field>
            <label class="pd__color">
              <span>{{ 'print.borderColor' | dcTranslate }}</span>
              <input
                type="color"
                [value]="s.borderColor || '#9da4ae'"
                (change)="update(s.id, { borderColor: $any($event.target).value })"
              />
            </label>
          </div>
        } @else {
          <h3>{{ 'print.page' | dcTranslate }}</h3>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'docTemplates.pageSize' | dcTranslate }}</mat-label>
            <mat-select
              [ngModel]="design().pageSize"
              (ngModelChange)="setPage({ pageSize: $event })"
              data-testid="pd-page-size"
            >
              @for (size of sizes; track size) {
                <mat-option [value]="size">{{ size }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-checkbox
            [ngModel]="design().landscape"
            (ngModelChange)="setPage({ landscape: $event })"
            >{{ 'docTemplates.landscape' | dcTranslate }}</mat-checkbox
          >
          <mat-button-toggle-group
            [value]="design().direction"
            (change)="setPage({ direction: $event.value })"
          >
            <mat-button-toggle value="rtl">العربية (RTL)</mat-button-toggle>
            <mat-button-toggle value="ltr">English (LTR)</mat-button-toggle>
          </mat-button-toggle-group>
          <label class="pd__color">
            <span>{{ 'print.background' | dcTranslate }}</span>
            <input
              type="color"
              [value]="design().background || '#ffffff'"
              (change)="setPage({ background: $any($event.target).value })"
            />
          </label>
          <p class="dc-muted">{{ 'print.pageHint' | dcTranslate }}</p>
        }
      </aside>
    </div>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 8px;
      min-height: 0;
      outline: none;
    }
    .pd__toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 4px;
      padding: 2px 6px;
      border: 1px solid var(--dga-gray-200);
      border-radius: 8px;
      background: #fff;
    }
    .pd__sep {
      width: 1px;
      height: 24px;
      margin-inline: 4px;
      background: var(--dga-gray-200);
    }
    .pd__grow {
      flex: 1;
    }
    .pd__zoom {
      min-width: 3rem;
      text-align: center;
      font-size: 13px;
    }
    .pd__body {
      display: grid;
      grid-template-columns: 11rem minmax(0, 1fr) 17rem;
      gap: 8px;
      flex: 1;
      min-height: 0;
    }
    .pd__palette,
    .pd__props {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 10px;
      border: 1px solid var(--dga-gray-200);
      border-radius: 8px;
      background: #fff;
      overflow: auto;
    }
    .pd__palette h3,
    .pd__props h3 {
      margin: 0;
      font-size: 15px;
    }
    .pd__tool {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 10px;
      border: 1px solid var(--dga-gray-200);
      border-radius: 8px;
      background: var(--dga-gray-50);
      font: inherit;
      cursor: grab;
      text-align: start;
    }
    .pd__tool:hover {
      border-color: var(--dynamic-cube-gold);
    }
    .pd__tip {
      font-size: 12px;
    }
    .pd__canvas {
      overflow: auto;
      padding: 24px;
      border-radius: 8px;
      background: var(--dga-gray-100);
    }
    .pd__page {
      position: relative;
      margin-inline: auto;
      box-shadow: 0 2px 12px rgb(0 0 0 / 15%);
      background-image:
        linear-gradient(to right, rgb(0 0 0 / 4%) 1px, transparent 1px),
        linear-gradient(to bottom, rgb(0 0 0 / 4%) 1px, transparent 1px);
      background-size: var(--grid) var(--grid);
    }
    .pd__el {
      position: absolute;
      box-sizing: border-box;
      overflow: hidden;
      line-height: 1.35;
      cursor: move;
      outline: 1px dashed rgb(0 0 0 / 15%);
      user-select: none;
      touch-action: none;
    }
    .pd__el--line {
      overflow: visible;
      height: 0;
      outline-offset: 3px;
    }
    .pd__el--selected {
      outline: 2px solid var(--dynamic-cube-gold);
      overflow: visible;
    }
    .pd__el img {
      width: 100%;
      height: 100%;
      object-fit: contain;
      pointer-events: none;
    }
    .pd__field {
      padding: 0 4px;
      border-radius: 4px;
      background: var(--dynamic-cube-gold-100);
      color: var(--dynamic-cube-gold-700);
      white-space: nowrap;
    }
    .pd__placeholder {
      width: 100%;
      height: 100%;
      font-size: 28px;
      display: grid;
      place-items: center;
      color: var(--dga-gray-400);
    }
    .pd__qr {
      font-size: 64px;
      color: var(--dga-gray-700);
    }
    .pd__table {
      width: 100%;
      border-collapse: collapse;
    }
    .pd__table th,
    .pd__table td {
      border: 1px solid #d2d6db;
      padding: 2px 6px;
    }
    .pd__table th {
      background: #f3f4f6;
    }
    .pd__table td {
      color: var(--dga-gray-500);
      font-family: ui-monospace, monospace;
      font-size: 0.8em;
    }
    .pd__handle {
      position: absolute;
      right: -6px;
      bottom: -6px;
      width: 12px;
      height: 12px;
      border-radius: 50%;
      background: var(--dynamic-cube-gold);
      cursor: nwse-resize;
    }
    .pd__el--line .pd__handle {
      bottom: -6px;
    }
    .pd__grid4 {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 6px;
    }
    .pd__grid2 {
      display: grid;
      grid-template-columns: 1fr 1fr;
      align-items: center;
      gap: 6px;
    }
    .pd__color {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 6px;
      font-size: 13px;
    }
    .pd__color input {
      width: 36px;
      height: 28px;
      padding: 0;
      border: 1px solid var(--dga-gray-300);
      border-radius: 6px;
    }
    .pd__column {
      display: grid;
      grid-template-columns: 1fr 1fr auto;
      align-items: center;
      gap: 4px;
    }
    .pd__bad {
      color: var(--dga-red-700, #b42318);
      font-size: 13px;
    }
    .pd__align {
      align-self: start;
    }
  `,
})
export class PrintDesigner {
  protected readonly i18n = inject(I18nService);
  /** The layout being designed (two-way). */
  readonly design = model.required<PrintDesign>();

  protected readonly palette = PALETTE;
  protected readonly fields = PRINT_FIELDS;
  protected readonly sizes: PageSize[] = ['A4', 'A5', 'LETTER'];
  protected readonly boxKeys = ['x', 'y', 'w', 'h'] as const;
  protected readonly zoom = signal(0.8);
  protected readonly grid = signal(1);
  protected readonly selectedId = signal<string | null>(null);
  protected readonly imageError = signal<string | null>(null);
  protected readonly page = computed(() => pageMm(this.design().pageSize, this.design().landscape));
  protected readonly scale = computed(() => PX_PER_MM * this.zoom());
  protected readonly selected = computed(
    () => this.design().elements.find((e) => e.id === this.selectedId()) ?? null,
  );

  private readonly pageRef = viewChild.required<ElementRef<HTMLElement>>('pageEl');
  private readonly past = signal<PrintDesign[]>([]);
  private readonly future = signal<PrintDesign[]>([]);
  protected readonly canUndo = computed(() => this.past().length > 0);
  protected readonly canRedo = computed(() => this.future().length > 0);
  private drag: Drag | null = null;

  protected fieldName(path: string | undefined): string {
    const known = PRINT_FIELDS.find((f) => f.path === path);
    return known ? this.i18n.localize({ ar: known.ar, en: known.en }) : (path ?? '');
  }

  protected knownField(path: string | undefined): boolean {
    return PRINT_FIELDS.some((f) => f.path === path);
  }

  protected select(id: string | null): void {
    this.selectedId.set(id);
    this.imageError.set(null);
  }

  /** Records the layout as it is before a change, for undo. */
  private commit(next: PrintDesign): void {
    this.past.update((p) => [...p.slice(-99), this.design()]);
    this.future.set([]);
    this.design.set(next);
  }

  protected undo(): void {
    const previous = this.past().at(-1);
    if (!previous) return;
    this.future.update((f) => [this.design(), ...f]);
    this.past.update((p) => p.slice(0, -1));
    this.design.set(previous);
  }

  protected redo(): void {
    const next = this.future()[0];
    if (!next) return;
    this.past.update((p) => [...p, this.design()]);
    this.future.update((f) => f.slice(1));
    this.design.set(next);
  }

  protected zoomBy(step: number): void {
    this.zoom.update((z) => Math.min(2, Math.max(0.3, Math.round((z + step) * 10) / 10)));
  }

  private nextId(kind: PrintKind): string {
    const used = new Set(this.design().elements.map((e) => e.id));
    let n = 1;
    while (used.has(`${kind}${n}`)) n++;
    return `${kind}${n}`;
  }

  /** Adds an element (from a click on the palette: near the top of the page). */
  protected add(kind: PrintKind, x = 20, y = 20): void {
    const element = clampToPage(
      newElement(kind, x, y, this.nextId(kind)),
      this.page(),
      this.grid(),
    );
    this.commit({ ...this.design(), elements: [...this.design().elements, element] });
    this.select(element.id);
  }

  protected startPaletteDrag(event: DragEvent, kind: PrintKind): void {
    event.dataTransfer?.setData(MIME, kind);
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'copy';
  }

  protected dropOnPage(event: DragEvent): void {
    const kind = event.dataTransfer?.getData(MIME) as PrintKind | undefined;
    if (!kind || !PALETTE.some((p) => p.kind === kind)) return;
    event.preventDefault();
    const rect = this.pageRef().nativeElement.getBoundingClientRect();
    this.add(
      kind,
      (event.clientX - rect.left) / this.scale(),
      (event.clientY - rect.top) / this.scale(),
    );
  }

  protected startDrag(event: PointerEvent, element: PrintElement, mode: 'move' | 'resize'): void {
    event.stopPropagation();
    event.preventDefault();
    this.select(element.id);
    (event.target as HTMLElement).setPointerCapture?.(event.pointerId);
    this.drag = {
      id: element.id,
      mode,
      startX: event.clientX,
      startY: event.clientY,
      origin: element,
    };
    const before = this.design();
    let moved = false;
    const onMove = (e: PointerEvent) => {
      if (!this.drag) return;
      const dx = (e.clientX - this.drag.startX) / this.scale();
      const dy = (e.clientY - this.drag.startY) / this.scale();
      if (!moved && Math.abs(dx) + Math.abs(dy) < 0.5) return;
      moved = true;
      const o = this.drag.origin;
      const changed =
        this.drag.mode === 'move'
          ? { ...o, x: o.x + dx, y: o.y + dy }
          : { ...o, w: o.w + dx, h: o.kind === 'line' ? o.h : o.h + dy };
      this.design.set(this.replace(clampToPage(changed, this.page(), this.grid())));
    };
    const onUp = () => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
      this.drag = null;
      if (moved) {
        // One undo step for the whole drag.
        this.past.update((p) => [...p.slice(-99), before]);
        this.future.set([]);
      }
    };
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
  }

  private replace(element: PrintElement): PrintDesign {
    return {
      ...this.design(),
      elements: this.design().elements.map((e) => (e.id === element.id ? element : e)),
    };
  }

  protected update(id: string, change: Partial<PrintElement>): void {
    const element = this.design().elements.find((e) => e.id === id);
    if (!element) return;
    const next = { ...element, ...change };
    const boxChanged = ['x', 'y', 'w', 'h'].some((k) => k in change);
    this.commit(this.replace(boxChanged ? clampToPage(next, this.page(), 0.1) : next));
  }

  protected setBox(id: string, key: 'x' | 'y' | 'w' | 'h', value: number | string): void {
    const n = Number(value);
    if (Number.isFinite(n)) this.update(id, { [key]: n });
  }

  protected setPage(change: Partial<PrintDesign>): void {
    const next = { ...this.design(), ...change };
    const page = pageMm(next.pageSize, next.landscape);
    this.commit({ ...next, elements: next.elements.map((e) => clampToPage(e, page, 0.1)) });
  }

  protected remove(id: string): void {
    this.commit({ ...this.design(), elements: this.design().elements.filter((e) => e.id !== id) });
    this.select(null);
  }

  protected duplicate(id: string): void {
    const element = this.design().elements.find((e) => e.id === id);
    if (!element) return;
    const copy = clampToPage(
      {
        ...structuredClone(element),
        id: this.nextId(element.kind),
        x: element.x + 5,
        y: element.y + 5,
      },
      this.page(),
      0.1,
    );
    this.commit({ ...this.design(), elements: [...this.design().elements, copy] });
    this.select(copy.id);
  }

  protected toFront(id: string): void {
    const element = this.design().elements.find((e) => e.id === id);
    if (!element) return;
    this.commit({
      ...this.design(),
      elements: [...this.design().elements.filter((e) => e.id !== id), element],
    });
  }

  protected setColumn(
    element: PrintElement,
    index: number,
    change: { title?: string; field?: string },
  ): void {
    const columns = (element.columns ?? []).map((c, i) => (i === index ? { ...c, ...change } : c));
    this.update(element.id, { columns });
  }

  protected addColumn(element: PrintElement): void {
    this.update(element.id, {
      columns: [...(element.columns ?? []), { field: 'field', title: '...' }],
    });
  }

  protected removeColumn(element: PrintElement, index: number): void {
    this.update(element.id, { columns: (element.columns ?? []).filter((_, i) => i !== index) });
  }

  /** Embeds a small image in the layout (so the template carries it). */
  protected loadImage(id: string, input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (file.size > MAX_IMAGE_BYTES) {
      this.imageError.set('print.imageTooBig');
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      this.imageError.set(null);
      this.update(id, { src: String(reader.result) });
    };
    reader.readAsDataURL(file);
  }

  protected onKey(event: KeyboardEvent): void {
    const target = event.target as HTMLElement;
    if (target.closest('input, textarea, mat-select, .mat-mdc-select')) return;
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') {
      event.preventDefault();
      if (event.shiftKey) this.redo();
      else this.undo();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'y') {
      event.preventDefault();
      this.redo();
      return;
    }
    const s = this.selected();
    if (!s) return;
    if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      this.remove(s.id);
      return;
    }
    const step = event.shiftKey ? 5 : 1;
    const moves: Record<string, [number, number]> = {
      ArrowLeft: [-step, 0],
      ArrowRight: [step, 0],
      ArrowUp: [0, -step],
      ArrowDown: [0, step],
    };
    const move = moves[event.key];
    if (move) {
      event.preventDefault();
      this.update(s.id, { x: s.x + move[0], y: s.y + move[1] });
    }
  }
}
