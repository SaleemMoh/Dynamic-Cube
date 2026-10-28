import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { I18nService, localize } from '@dynamic-cube/shared';

/** The parts of a workflow definition the preview draws (the server validates the rest). */
export interface GraphDefinition {
  nodes: GraphNode[];
}

export interface GraphNode {
  code: string;
  type: 'START' | 'HUMAN_TASK' | 'PARALLEL_SPLIT' | 'JOIN' | 'GATEWAY' | 'END';
  nameAr?: string;
  nameEn?: string;
  next?: string | null;
  branches?: string[];
  routes?: { to: string }[];
  default?: string | null;
  actions?: { code: string; kind: string; nameAr?: string; nameEn?: string; to?: string | null }[];
  assignment?: {
    target?: string;
    role?: string | null;
    user?: string | null;
    step?: string | null;
    mode?: string;
  } | null;
  outcome?: string | null;
}

export interface GraphEdge {
  from: string;
  to: string;
  label: string;
}

export interface PlacedNode {
  node: GraphNode;
  x: number;
  y: number;
  layer: number;
}

export const NODE_W = 180;
export const NODE_H = 56;
const GAP_X = 36;
const GAP_Y = 64;

/** Edges in reading order: start/join next, split branches, gateway routes and default, task actions. */
export function graphEdges(definition: GraphDefinition, language: 'ar' | 'en'): GraphEdge[] {
  const edges: GraphEdge[] = [];
  for (const n of definition.nodes) {
    if (n.next) edges.push({ from: n.code, to: n.next, label: '' });
    for (const b of n.branches ?? []) edges.push({ from: n.code, to: b, label: '' });
    (n.routes ?? []).forEach((r, i) => edges.push({ from: n.code, to: r.to, label: `#${i + 1}` }));
    if (n.default) edges.push({ from: n.code, to: n.default, label: '*' });
    for (const a of n.actions ?? []) {
      if (a.to)
        edges.push({
          from: n.code,
          to: a.to,
          label: localize({ ar: a.nameAr ?? a.code, en: a.nameEn ?? a.code }, language),
        });
    }
  }
  return edges;
}

/** Layers by distance from START (breadth first, so loops back do not push nodes down); unreachable nodes go last. */
export function layout(definition: GraphDefinition, edges: GraphEdge[]): { nodes: PlacedNode[]; width: number; height: number } {
  const byCode = new Map(definition.nodes.map((n) => [n.code, n]));
  const layerOf = new Map<string, number>();
  const start = definition.nodes.find((n) => n.type === 'START') ?? definition.nodes[0];
  const queue: string[] = [];
  if (start) {
    layerOf.set(start.code, 0);
    queue.push(start.code);
  }
  while (queue.length) {
    const code = queue.shift()!;
    for (const e of edges.filter((x) => x.from === code)) {
      if (byCode.has(e.to) && !layerOf.has(e.to)) {
        layerOf.set(e.to, layerOf.get(code)! + 1);
        queue.push(e.to);
      }
    }
  }
  const last = Math.max(-1, ...layerOf.values());
  for (const n of definition.nodes) {
    if (!layerOf.has(n.code)) layerOf.set(n.code, last + 1);
  }
  const layers: GraphNode[][] = [];
  for (const n of definition.nodes) {
    (layers[layerOf.get(n.code)!] ??= []).push(n);
  }
  const widest = Math.max(1, ...layers.map((l) => l?.length ?? 0));
  const width = widest * NODE_W + (widest - 1) * GAP_X + 2 * GAP_X;
  const placed: PlacedNode[] = [];
  layers.forEach((nodes, layer) => {
    const rowWidth = nodes.length * NODE_W + (nodes.length - 1) * GAP_X;
    nodes.forEach((node, i) =>
      placed.push({
        node,
        layer,
        x: (width - rowWidth) / 2 + i * (NODE_W + GAP_X),
        y: GAP_Y / 2 + layer * (NODE_H + GAP_Y),
      }),
    );
  });
  return { nodes: placed, width, height: layers.length * (NODE_H + GAP_Y) };
}

/**
 * A read-only drawing of a workflow graph: steps top to bottom from the start, arrows labelled with the action
 * (or the gateway route), and a ↩ on tasks that can return the request to the applicant.
 */
@Component({
  selector: 'dc-workflow-graph',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let g = graph();
    <div class="dc-graph" dir="ltr">
      <svg
        [attr.viewBox]="'0 0 ' + g.width + ' ' + g.height"
        [attr.width]="g.width"
        [attr.height]="g.height"
        role="img"
        [attr.aria-label]="label()"
        data-testid="workflow-graph"
      >
        <defs>
          <marker id="dc-arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
            <path d="M0,0 L10,5 L0,10 z" class="dc-graph__head" />
          </marker>
        </defs>
        @for (e of g.lines; track $index) {
          <path [attr.d]="e.d" class="dc-graph__edge" [class.dc-graph__edge--back]="e.back" marker-end="url(#dc-arrow)" />
          @if (e.label) {
            <text [attr.x]="e.lx" [attr.y]="e.ly" class="dc-graph__label" text-anchor="middle">
              {{ e.label }}
            </text>
          }
        }
        @for (p of g.nodes; track p.node.code) {
          <g [attr.transform]="'translate(' + p.x + ',' + p.y + ')'" [attr.data-node]="p.node.code">
            <rect
              [attr.width]="w"
              [attr.height]="h"
              [attr.rx]="p.node.type === 'START' || p.node.type === 'END' ? 28 : 8"
              [class]="'dc-graph__node dc-graph__node--' + p.node.type"
            />
            <text [attr.x]="w / 2" y="23" text-anchor="middle" class="dc-graph__name">
              {{ name(p.node) }}
            </text>
            <text [attr.x]="w / 2" y="42" text-anchor="middle" class="dc-graph__meta">
              {{ meta(p.node) }}
            </text>
          </g>
        }
      </svg>
    </div>
  `,
  styles: `
    .dc-graph {
      overflow: auto;
      max-height: 70vh;
      border: 1px solid var(--dga-gray-200);
      border-radius: 8px;
      background: var(--dga-gray-25, #fcfcfd);
    }
    svg {
      display: block;
      margin-inline: auto;
      font-family: inherit;
    }
    .dc-graph__edge {
      fill: none;
      stroke: var(--dga-gray-500, #6c737f);
      stroke-width: 1.5;
    }
    .dc-graph__edge--back {
      stroke-dasharray: 5 4;
    }
    .dc-graph__head {
      fill: var(--dga-gray-500, #6c737f);
    }
    .dc-graph__label {
      font-size: 11px;
      fill: var(--dga-text-2, #384250);
      paint-order: stroke;
      stroke: #fff;
      stroke-width: 4px;
    }
    .dc-graph__node {
      fill: #fff;
      stroke: var(--dga-gray-300, #d2d6db);
      stroke-width: 1.5;
    }
    .dc-graph__node--START,
    .dc-graph__node--END {
      fill: var(--dynamic-cube-green-50, #e8f3ed);
      stroke: var(--dynamic-cube-green, #136f40);
    }
    .dc-graph__node--HUMAN_TASK {
      fill: var(--dynamic-cube-gold-50, #faf7f0);
      stroke: var(--dynamic-cube-gold, #ca9f45);
    }
    .dc-graph__node--PARALLEL_SPLIT,
    .dc-graph__node--JOIN,
    .dc-graph__node--GATEWAY {
      fill: var(--dga-gray-50, #f9fafb);
    }
    .dc-graph__name {
      font-size: 13px;
      font-weight: 600;
      fill: var(--dga-text-1, #161b23);
    }
    .dc-graph__meta {
      font-size: 11px;
      fill: var(--dga-text-3, #6c737f);
    }
  `,
})
export class WorkflowGraph {
  readonly definition = input.required<GraphDefinition>();
  private readonly i18n = inject(I18nService);
  protected readonly w = NODE_W;
  protected readonly h = NODE_H;

  protected readonly graph = computed(() => {
    const definition = this.definition();
    const edges = graphEdges(definition, this.i18n.language());
    const placed = layout(definition, edges);
    const at = new Map(placed.nodes.map((p) => [p.node.code, p]));
    const lines = edges
      .filter((e) => at.has(e.from) && at.has(e.to))
      .map((e) => {
        const a = at.get(e.from)!;
        const b = at.get(e.to)!;
        const back = b.layer <= a.layer;
        const x1 = a.x + NODE_W / 2;
        const y1 = a.y + NODE_H;
        const x2 = b.x + NODE_W / 2;
        const y2 = back ? b.y + NODE_H : b.y;
        // Forward: a gentle S-curve down; backwards (a loop): out to the side and up.
        const d = back
          ? `M${a.x + NODE_W},${a.y + NODE_H / 2} C${a.x + NODE_W + 60},${a.y + NODE_H / 2} ${b.x + NODE_W + 60},${b.y + NODE_H / 2} ${b.x + NODE_W},${b.y + NODE_H / 2}`
          : `M${x1},${y1} C${x1},${(y1 + y2) / 2} ${x2},${(y1 + y2) / 2} ${x2},${y2}`;
        return {
          d,
          back,
          label: e.label,
          lx: back ? Math.max(a.x, b.x) + NODE_W + 40 : (x1 + x2) / 2,
          ly: back ? (a.y + b.y + NODE_H) / 2 : (y1 + y2) / 2 + 4,
        };
      });
    return { nodes: placed.nodes, lines, width: placed.width + 80, height: placed.height };
  });

  protected readonly label = computed(() =>
    this.definition()
      .nodes.map((n) => this.name(n))
      .join(' → '),
  );

  protected name(node: GraphNode): string {
    return localize({ ar: node.nameAr || node.code, en: node.nameEn || node.code }, this.i18n.language());
  }

  protected meta(node: GraphNode): string {
    switch (node.type) {
      case 'HUMAN_TASK': {
        const who = node.assignment?.role ?? node.assignment?.user ?? node.assignment?.step ?? '';
        const returns = node.actions?.some((a) => a.kind === 'RETURN') ? ' ↩' : '';
        return `${who} · ${node.assignment?.mode ?? 'QUEUE'}${returns}`;
      }
      case 'END':
        return node.outcome ?? '';
      default:
        return node.type.replace('_', ' ');
    }
  }
}
