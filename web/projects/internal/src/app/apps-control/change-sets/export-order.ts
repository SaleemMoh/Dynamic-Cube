import { TemplateEntity } from './package-template';

/** A row by type and change-item key. */
export interface RowRef {
  entityType: string;
  code: string;
}

/**
 * Types in the order a package applies them when nothing else decides: platform rows first, then the modules, each
 * with its referenced types ahead. Declared references (see {@link typeDependencies}) refine it; unknown types go last.
 */
export const BASE_TYPE_ORDER: readonly string[] = [
  'apps.installation',
  'apps.entity',
  'apps.app',
  'apps.org-unit-type',
  'apps.org-unit',
  'apps.position',
  'apps.resource',
  'apps.menu-group',
  'apps.menu',
  'apps.role',
  'apps.class',
  'apps.domain',
  'apps.data-object',
  'apps.lov',
  'apps.setting',
  'apps.ui-text',
  'apps.holiday',
  'apps.user',
  'integ.provider',
  'integ.envelope-profile',
  'integ.connection',
  'integ.operation',
  'party.relationship-type',
  'party.credential-type',
  'party.sanction-type',
  'party.profile-field',
  'party.context-source',
  'doc.template',
  'doc.document-type',
  'com.template',
  'com.rule',
  'dec.rule-set',
  'dec.rule-set-version',
  'pay.provider',
  'svc.category',
  'svc.question',
  'svc.service',
  'wf.workflow',
  'wf.workflow-version',
  'svc.service-version',
  'cms.page',
  'cms.faq',
];

/** Types whose rows hang under a row of another type, listed by that parent's code. */
export const CHILD_TYPES: Readonly<Record<string, string>> = {
  'apps.class': 'apps.domain',
  'svc.service': 'svc.service-version',
  'wf.workflow': 'wf.workflow-version',
  'dec.rule-set': 'dec.rule-set-version',
};

/** The parent type of a child type (values of a class, versions of a service, workflow or rule set). */
export function parentType(entityType: string): string | null {
  return Object.entries(CHILD_TYPES).find(([, child]) => child === entityType)?.[0] ?? null;
}

/** Which types each type refers to: its reference fields, and its parent for values and versions. */
export function typeDependencies(entities: readonly TemplateEntity[]): Map<string, Set<string>> {
  const deps = new Map<string, Set<string>>();
  for (const entity of entities) {
    const set = deps.get(entity.entityType) ?? new Set<string>();
    for (const field of entity.fields) {
      if (field.ref && field.ref !== entity.entityType) set.add(field.ref);
    }
    const parent = parentType(entity.entityType);
    if (parent) set.add(parent);
    deps.set(entity.entityType, set);
  }
  return deps;
}

/**
 * Types in apply order: every type after the types it refers to, otherwise in {@link BASE_TYPE_ORDER}. Reference
 * cycles keep the base order.
 */
export function orderTypes(types: Iterable<string>, deps: ReadonlyMap<string, ReadonlySet<string>>): string[] {
  const rank = (type: string) => {
    const index = BASE_TYPE_ORDER.indexOf(type);
    return index < 0 ? BASE_TYPE_ORDER.length : index;
  };
  const pending = [...new Set(types)].sort((a, b) => rank(a) - rank(b) || a.localeCompare(b));
  const wanted = new Set(pending);
  const ordered: string[] = [];
  const placed = new Set<string>();
  while (pending.length) {
    // The first type whose referenced types (among those exported) are all placed; on a cycle, the first one.
    const index = pending.findIndex((type) => [...(deps.get(type) ?? [])].every((dep) => !wanted.has(dep) || placed.has(dep)));
    const [next] = pending.splice(Math.max(index, 0), 1);
    ordered.push(next);
    placed.add(next);
  }
  return ordered;
}

/**
 * The rows a payload refers to through its reference fields. App-scoped targets are keyed `APP/CODE`; a bare code
 * takes the row's own app.
 */
export function referencesOf(payload: Record<string, unknown>, entity: TemplateEntity, keys: ReadonlyMap<string, TemplateEntity['key']>): RowRef[] {
  const refs: RowRef[] = [];
  const app = typeof payload['appCode'] === 'string' ? payload['appCode'] : null;
  const add = (type: string, value: unknown) => {
    if (typeof value !== 'string' || !value.trim()) return;
    const code = keys.get(type) === 'appScoped' && app && !value.includes('/') ? `${app}/${value}` : value;
    refs.push({ entityType: type, code });
  };
  for (const field of entity.fields) {
    if (!field.ref) continue;
    const value = payload[field.code];
    const values = Array.isArray(value) ? value : [value];
    for (const item of values) {
      add(field.ref, field.itemKey && item && typeof item === 'object' ? (item as Record<string, unknown>)[field.itemKey] : item);
    }
  }
  const parent = parentType(entity.entityType);
  if (parent && typeof payload['code'] === 'string' && payload['code'].includes('/')) {
    add(parent, payload['code'].slice(0, payload['code'].lastIndexOf('/')));
  }
  return refs;
}
