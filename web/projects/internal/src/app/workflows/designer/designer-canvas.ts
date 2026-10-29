import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, input, signal, viewChild } from '@angular/core';
import { I18nService, localize } from '@dynamic-cube/shared';
import { DesignerStore } from './designer-store';
import {
  Edge,
  GRID,
  NODE_H,
  NODE_TYPES,
  NODE_W,
  NodeDef,
  NodeType,
  OutletKind,
  Position,
  addNode,
  connect,
  handles,
  moveNode,
  sameOutlet,
} from './designer-model';

/** Icon of each node type (Material Symbols). */
export const NODE_ICONS: Record<NodeType, string> = {
  START: 'play_circle',
  HUMAN_TASK: 'person',
  PARALLEL_SPLIT: 'call_split',
  JOIN: 'merge',
  GATEWAY: 'alt_route',
  DECISION: 'rule',
  DOCUMENT: 'description',
  PAYMENT: 'payments',
  END: 'flag',
};

/** The data type carried when a palette item is dragged onto the canvas. */
export const PALETTE_MIME = 'application/x-dc-workflow-node';

type Drag =
  | { mode: 'move'; code: string; dx: number; dy: number }
  | {
      mode: 'connect';
      from: string;
      kind: OutletKind;
      x1: number;
      y1: number;
      x2: number;
      y2: number;
      over: string | null;
    };

interface DrawnEdge {
  edge: Edge;
  d: string;
  lx: number;
  ly: number;
  label: string;
  dashed: boolean;
  selected: boolean;
}

/**
 * The workflow drawn as boxes and arrows: drag a box to move it, drag from a handle under a box to another box to
 * connect them, click an arrow or a box to select it. Palette items can be dropped on it.
 */
@Component({
  selector: 'dc-designer-canvas',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-canvas" dir="ltr" (dragover)="dragOver($event)" (drop)="drop($event)">
      <svg
        #svg
        [attr.viewBox]="'0 0 ' + size().w + ' ' + size().h"
        [attr.width]="size().w * zoom()"
        [attr.height]="size().h * zoom()"
        (pointerdown)="background($event)"
        (pointermove)="pointerMove($event)"
        (pointerup)="pointerUp($event)"
        (pointercancel)="drag.set(null)"
        data-testid="designer-canvas"
      >
        <defs>
          <pattern id="dc-grid" [attr.width]="grid * 2" [attr.height]="grid * 2" patternUnits="userSpaceOnUse">
            <circle cx="1" cy="1" r="1" class="dc-canvas__dot" />
          </pattern>
          <marker id="dc-designer-arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
            <path d="M0,0 L10,5 L0,10 z" class="dc-canvas__head" />
          </marker>
          <marker id="dc-designer-arrow-on" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
            <path d="M0,0 L10,5 L0,10 z" class="dc-canvas__head dc-canvas__head--on" />
          </marker>
        </defs>
        <rect [attr.width]="size().w" [attr.height]="size().h" fill="url(#dc-grid)" />

        @for (e of drawn(); track $index) {
          <g class="dc-canvas__edge" [class.dc-canvas__edge--on]="e.selected" [attr.data-edge]="e.edge.from + '>' + e.edge.to">
            <path [attr.d]="e.d" class="dc-canvas__hit" (pointerdown)="selectEdge($event, e.edge)" />
            <path
              [attr.d]="e.d"
              class="dc-canvas__line"
              [class.dc-canvas__line--dashed]="e.dashed"
              [attr.marker-end]="e.selected ? 'url(#dc-designer-arrow-on)' : 'url(#dc-designer-arrow)'"
            />
            @if (e.label) {
              <text [attr.x]="e.lx" [attr.y]="e.ly" text-anchor="middle" class="dc-canvas__label" (pointerdown)="selectEdge($event, e.edge)">
                {{ e.label }}
              </text>
            }
          </g>
        }

        @for (n of store.definition().nodes; track n.code) {
          @let at = store.positions()[n.code];
          @if (at) {
            <g
              [attr.transform]="'translate(' + at.x + ',' + at.y + ')'"
              class="dc-canvas__node"
              [class.dc-canvas__node--on]="isSelected(n.code)"
              [class.dc-canvas__node--target]="connectTarget() === n.code"
              [class.dc-canvas__node--active]="store.active().includes(n.code)"
              [attr.data-node]="n.code"
              tabindex="0"
              role="button"
              [attr.aria-label]="name(n) + ' (' + n.code + ')'"
              (pointerdown)="nodeDown($event, n.code)"
              (keydown.enter)="store.selection.set({ kind: 'node', code: n.code })"
            >
              <rect
                [attr.width]="w"
                [attr.height]="h"
                [attr.rx]="n.type === 'START' || n.type === 'END' ? h / 2 : 10"
                [class]="'dc-canvas__box dc-canvas__box--' + n.type"
              />
              <text x="14" y="38" class="dc-canvas__icon material-symbols-outlined" aria-hidden="true">
                {{ icons[n.type] }}
              </text>
              <text x="44" y="28" class="dc-canvas__name">{{ clip(name(n), 20) }}</text>
              <text x="44" y="46" class="dc-canvas__code">{{ clip(n.code, 24) }}</text>
              @if (problemCount()[n.code]; as count) {
                <g [attr.transform]="'translate(' + (w - 6) + ',6)'" class="dc-canvas__badge">
                  <circle r="9" />
                  <text y="4" text-anchor="middle">{{ count }}</text>
                </g>
              }
              @if (returns(n)) {
                <text [attr.x]="w - 22" [attr.y]="h - 10" class="dc-canvas__return" aria-hidden="true">↩</text>
              }
              @for (handle of handlesOf(n); track handle.kind; let i = $index; let count = $count) {
                <g
                  class="dc-canvas__handle"
                  [attr.transform]="'translate(' + (w * (i + 1)) / (count + 1) + ',' + h + ')'"
                  [attr.data-handle]="n.code + ':' + handle.kind"
                  (pointerdown)="handleDown($event, n.code, handle.kind, i, count)"
                >
                  <title>{{ i18n.translate(handle.hint) }}</title>
                  <circle r="8" />
                  <text y="4" text-anchor="middle">{{ handle.mark }}</text>
                </g>
              }
            </g>
          }
        }

        @if (connecting(); as c) {
          <path
            [attr.d]="'M' + c.x1 + ',' + c.y1 + ' L' + c.x2 + ',' + c.y2"
            class="dc-canvas__line dc-canvas__line--draft"
            marker-end="url(#dc-designer-arrow-on)"
          />
        }
      </svg>
    </div>
  `,
  styles: `
    :host {
      display: block;
      min-height: 0;
      min-width: 0;
      overflow: hidden;
    }
    .dc-canvas {
      height: 100%;
      overflow: auto;
      background: var(--dga-gray-25, #fcfcfd);
    }
    svg {
      display: block;
      font-family: inherit;
      user-select: none;
      touch-action: none;
    }
    .dc-canvas__dot {
      fill: var(--dga-gray-300, #d2d6db);
    }
    .dc-canvas__head {
      fill: var(--dga-gray-500, #6c737f);
    }
    .dc-canvas__head--on {
      fill: var(--dynamic-cube-gold-700, #8a6a26);
    }
    .dc-canvas__hit {
      fill: none;
      stroke: transparent;
      stroke-width: 14;
      cursor: pointer;
    }
    .dc-canvas__line {
      fill: none;
      stroke: var(--dga-gray-500, #6c737f);
      stroke-width: 1.5;
      pointer-events: none;
    }
    .dc-canvas__line--dashed,
    .dc-canvas__line--draft {
      stroke-dasharray: 5 4;
    }
    .dc-canvas__edge--on .dc-canvas__line,
    .dc-canvas__line--draft {
      stroke: var(--dynamic-cube-gold-700, #8a6a26);
      stroke-width: 2.5;
    }
    .dc-canvas__label {
      font-size: 11px;
      fill: var(--dga-text-2, #384250);
      paint-order: stroke;
      stroke: #fff;
      stroke-width: 4px;
      cursor: pointer;
    }
    .dc-canvas__node {
      cursor: grab;
      outline: none;
    }
    .dc-canvas__box {
      fill: #fff;
      stroke: var(--dga-gray-300, #d2d6db);
      stroke-width: 1.5;
    }
    .dc-canvas__box--START,
    .dc-canvas__box--END {
      fill: var(--dynamic-cube-green-50, #e8f3ed);
      stroke: var(--dynamic-cube-green, #136f40);
    }
    .dc-canvas__box--HUMAN_TASK {
      fill: var(--dynamic-cube-gold-50, #faf7f0);
      stroke: var(--dynamic-cube-gold, #ca9f45);
    }
    .dc-canvas__box--DECISION,
    .dc-canvas__box--DOCUMENT,
    .dc-canvas__box--PAYMENT {
      fill: #f0f6ff;
      stroke: #84adff;
    }
    .dc-canvas__node--on .dc-canvas__box,
    .dc-canvas__node:focus-visible .dc-canvas__box {
      stroke: var(--dynamic-cube-gold-700, #8a6a26);
      stroke-width: 3;
    }
    .dc-canvas__node--target .dc-canvas__box {
      stroke: var(--dynamic-cube-green, #136f40);
      stroke-width: 3;
    }
    .dc-canvas__node--active .dc-canvas__box {
      stroke: #2e90fa;
      stroke-width: 4;
      filter: drop-shadow(0 0 6px rgba(46, 144, 250, 0.6));
    }
    .dc-canvas__icon {
      font-size: 22px;
      fill: var(--dga-text-2, #384250);
    }
    .dc-canvas__name {
      font-size: 13px;
      font-weight: 600;
      fill: var(--dga-text-1, #161b23);
    }
    .dc-canvas__code {
      font-size: 11px;
      fill: var(--dga-text-3, #6c737f);
    }
    .dc-canvas__badge circle {
      fill: #d92d20;
    }
    .dc-canvas__badge text {
      font-size: 11px;
      font-weight: 700;
      fill: #fff;
    }
    .dc-canvas__return {
      font-size: 14px;
      fill: var(--dga-text-3, #6c737f);
    }
    .dc-canvas__handle {
      cursor: crosshair;
    }
    .dc-canvas__handle circle {
      fill: #fff;
      stroke: var(--dynamic-cube-gold-700, #8a6a26);
      stroke-width: 1.5;
    }
    .dc-canvas__handle:hover circle {
      fill: var(--dynamic-cube-gold-50, #faf7f0);
    }
    .dc-canvas__handle text {
      font-size: 11px;
      font-weight: 700;
      fill: var(--dynamic-cube-gold-700, #8a6a26);
      pointer-events: none;
    }
  `,
})
export class DesignerCanvas {
  protected readonly store = inject(DesignerStore);
  protected readonly i18n = inject(I18nService);
  readonly zoom = input(1);
  /** Nothing can be changed (a published version opened to look at). */
  readonly readOnly = input(false);
  private readonly svg = viewChild.required<ElementRef<SVGSVGElement>>('svg');

  protected readonly w = NODE_W;
  protected readonly h = NODE_H;
  protected readonly grid = GRID;
  protected readonly icons = NODE_ICONS;
  protected readonly drag = signal<Drag | null>(null);

  protected readonly connecting = computed(() => {
    const d = this.drag();
    return d?.mode === 'connect' ? d : null;
  });
  protected readonly connectTarget = computed(() => this.connecting()?.over ?? null);

  protected readonly size = computed(() => {
    const at = Object.values(this.store.positions());
    return {
      w: Math.max(1200, ...at.map((p) => p.x + NODE_W + 240)),
      h: Math.max(800, ...at.map((p) => p.y + NODE_H + 240)),
    };
  });

  protected readonly problemCount = computed(() => {
    const count: Record<string, number> = {};
    for (const p of this.store.problems()) if (p.node && !p.warning) count[p.node] = (count[p.node] ?? 0) + 1;
    return count;
  });

  protected readonly drawn = computed<DrawnEdge[]>(() => {
    const at = this.store.positions();
    const byCode = new Map(this.store.definition().nodes.map((n) => [n.code, n]));
    const selection = this.store.selection();
    const language = this.i18n.language();
    return this.store
      .edges()
      .filter((e) => at[e.from] && at[e.to] && byCode.has(e.from))
      .map((edge) => {
        const from = byCode.get(edge.from)!;
        const a = at[edge.from];
        const b = at[edge.to];
        const start = edge.outlet.kind === 'resume' ? { x: a.x + NODE_W, y: a.y + NODE_H / 2 } : this.handlePoint(from, a, edge.outlet.kind);
        const end = { x: b.x + NODE_W / 2, y: b.y };
        const path = curve(start, end);
        const label =
          typeof edge.label === 'string' ? (edge.label.startsWith('designer.') ? this.i18n.translate(edge.label) : edge.label) : localize(edge.label, language);
        return {
          edge,
          d: path.d,
          lx: path.mid.x,
          ly: path.mid.y + 4,
          label: clip(label, 22),
          dashed: edge.outlet.kind === 'resume' || edge.outlet.kind === 'expired',
          selected: selection?.kind === 'edge' && selection.from === edge.from && sameOutlet(selection.outlet, edge.outlet),
        };
      });
  });

  protected name(node: NodeDef): string {
    return localize({ ar: node.nameAr || node.code, en: node.nameEn || node.code }, this.i18n.language());
  }

  protected clip(text: string, max: number): string {
    return clip(text, max);
  }

  protected handlesOf(node: NodeDef) {
    return this.readOnly() ? [] : handles(node);
  }

  protected returns(node: NodeDef): boolean {
    return (node.actions ?? []).some((a) => a.kind === 'RETURN');
  }

  protected isSelected(code: string): boolean {
    const s = this.store.selection();
    return s?.kind === 'node' && s.code === code;
  }

  /** Where an outlet's arrows start: its handle under the box. */
  private handlePoint(node: NodeDef, at: Position, kind: OutletKind): Position {
    const list = handles(node);
    const i = Math.max(
      0,
      list.findIndex((h) => h.kind === kind),
    );
    return { x: at.x + (NODE_W * (i + 1)) / (list.length + 1), y: at.y + NODE_H };
  }

  /** A pointer position in canvas units. */
  private point(event: PointerEvent | DragEvent): Position {
    const svg = this.svg().nativeElement;
    const matrix = svg.getScreenCTM();
    if (!matrix) return { x: event.offsetX, y: event.offsetY };
    const p = new DOMPoint(event.clientX, event.clientY).matrixTransform(matrix.inverse());
    return { x: p.x, y: p.y };
  }

  private nodeAt(p: Position): string | null {
    const at = this.store.positions();
    const hit = Object.entries(at).find(([, q]) => p.x >= q.x && p.x <= q.x + NODE_W && p.y >= q.y && p.y <= q.y + NODE_H);
    return hit?.[0] ?? null;
  }

  protected background(event: PointerEvent): void {
    if (event.button !== 0) return;
    this.store.selection.set(null);
  }

  protected nodeDown(event: PointerEvent, code: string): void {
    if (event.button !== 0) return;
    event.stopPropagation();
    this.store.selection.set({ kind: 'node', code });
    if (this.readOnly()) return;
    const p = this.point(event);
    const at = this.store.positions()[code];
    this.drag.set({ mode: 'move', code, dx: p.x - at.x, dy: p.y - at.y });
    this.svg().nativeElement.setPointerCapture?.(event.pointerId);
  }

  protected handleDown(event: PointerEvent, code: string, kind: OutletKind, index: number, count: number): void {
    if (event.button !== 0) return;
    event.stopPropagation();
    const at = this.store.positions()[code];
    const x1 = at.x + (NODE_W * (index + 1)) / (count + 1);
    const y1 = at.y + NODE_H;
    this.drag.set({ mode: 'connect', from: code, kind, x1, y1, x2: x1, y2: y1 + 1, over: null });
    this.svg().nativeElement.setPointerCapture?.(event.pointerId);
  }

  protected selectEdge(event: PointerEvent, edge: Edge): void {
    event.stopPropagation();
    this.store.selection.set({ kind: 'edge', from: edge.from, outlet: edge.outlet });
  }

  protected pointerMove(event: PointerEvent): void {
    const d = this.drag();
    if (!d) return;
    const p = this.point(event);
    if (d.mode === 'move') {
      this.store.preview((def) => moveNode(def, d.code, { x: p.x - d.dx, y: p.y - d.dy }));
    } else {
      const over = this.nodeAt(p);
      this.drag.set({ ...d, x2: p.x, y2: p.y, over: over && over !== d.from ? over : null });
    }
  }

  protected pointerUp(event: PointerEvent): void {
    const d = this.drag();
    this.drag.set(null);
    this.svg().nativeElement.releasePointerCapture?.(event.pointerId);
    if (!d) return;
    if (d.mode === 'move') {
      this.store.commit();
      return;
    }
    const to = this.nodeAt(this.point(event));
    if (to && to !== d.from) this.store.apply((def) => connect(def, d.from, d.kind, to));
  }

  protected dragOver(event: DragEvent): void {
    if (!this.readOnly() && event.dataTransfer?.types.includes(PALETTE_MIME)) event.preventDefault();
  }

  protected drop(event: DragEvent): void {
    const type = event.dataTransfer?.getData(PALETTE_MIME) as NodeType | undefined;
    if (this.readOnly() || !type || !NODE_TYPES.includes(type)) return;
    event.preventDefault();
    const p = this.point(event);
    this.place(type, { x: p.x - NODE_W / 2, y: p.y - NODE_H / 2 });
  }

  /** Adds a node and selects it; without a place, below the lowest box. */
  place(type: NodeType, at?: Position): void {
    const positions = Object.values(this.store.positions());
    const where = at ?? {
      x: 48,
      y: positions.length ? Math.max(...positions.map((p) => p.y)) + NODE_H + 56 : 48,
    };
    let code = '';
    this.store.apply((def) => {
      const added = addNode(def, type, where);
      code = added.code;
      return added.def;
    });
    this.store.selection.set({ kind: 'node', code });
  }
}

/** A curve from a handle down to the top of a box; going back up, it swings out to the right. */
function curve(a: Position, b: Position): { d: string; mid: Position } {
  let c1: Position;
  let c2: Position;
  if (b.y >= a.y + 24) {
    const dy = Math.max(30, (b.y - a.y) / 2);
    c1 = { x: a.x, y: a.y + dy };
    c2 = { x: b.x, y: b.y - dy };
  } else {
    const out = NODE_W * 0.9;
    c1 = { x: a.x + out, y: a.y + 90 };
    c2 = { x: b.x + out, y: b.y - 90 };
  }
  const mid = {
    x: 0.125 * a.x + 0.375 * c1.x + 0.375 * c2.x + 0.125 * b.x,
    y: 0.125 * a.y + 0.375 * c1.y + 0.375 * c2.y + 0.125 * b.y,
  };
  return { d: `M${a.x},${a.y} C${c1.x},${c1.y} ${c2.x},${c2.y} ${b.x},${b.y}`, mid };
}

function clip(text: string, max: number): string {
  return text.length > max ? text.slice(0, max - 1) + '…' : text;
}
