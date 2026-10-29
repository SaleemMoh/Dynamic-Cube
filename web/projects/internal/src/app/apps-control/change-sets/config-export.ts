import { Injectable, inject } from '@angular/core';
import { AppsControlApi, ExportedPackage, ExportSelection } from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { ALL_CONFIG_SECTIONS } from '../config/module-sections';
import { CHILD_TYPES, orderTypes, referencesOf, typeDependencies } from './export-order';
import { templateEntities } from './package-template';

export interface ExportOptions {
  title: string;
  /** Also export what the chosen rows need elsewhere: referenced rows, values of classes, versions of services. */
  includeRelated: boolean;
  /** Also export rows maintained by the product. */
  includeSystem: boolean;
}

/** Rounds of following references; real configurations settle in two or three. */
const MAX_ROUNDS = 8;

/**
 * Builds configuration packages from chosen rows or whole types. With related rows, references are followed until
 * nothing new turns up, so the package imports into an empty environment; types come in apply order.
 */
@Injectable({ providedIn: 'root' })
export class ConfigExport {
  private readonly api = inject(AppsControlApi);
  private readonly entities = templateEntities(ALL_CONFIG_SECTIONS);
  private readonly byType = new Map(this.entities.map((e) => [e.entityType, e]));
  private readonly keys = new Map(this.entities.map((e) => [e.entityType, e.key]));
  private readonly deps = typeDependencies(this.entities);

  /** `null` codes export every row of the type. */
  async build(selection: ReadonlyMap<string, ReadonlySet<string> | null>, options: ExportOptions): Promise<ExportedPackage> {
    const wanted = new Map<string, Set<string> | null>([...selection].map(([type, codes]) => [type, codes ? new Set(codes) : null]));
    if (options.includeRelated) {
      await this.addChildren(wanted, [...wanted.keys()]);
      const seen = new Set<string>();
      for (let round = 0; round < MAX_ROUNDS; round++) {
        const found = await this.fetch(wanted, options, true);
        const added = new Set<string>();
        for (const item of found.items) {
          seen.add(`${item.entityType} ${item.entityCode}`);
          const entity = this.byType.get(item.entityType);
          if (!entity) continue;
          for (const ref of referencesOf({ ...item.payload, code: item.entityCode }, entity, this.keys)) {
            const codes = wanted.get(ref.entityType);
            if (codes === null || codes?.has(ref.code) || seen.has(`${ref.entityType} ${ref.code}`)) continue;
            if (codes) codes.add(ref.code);
            else wanted.set(ref.entityType, new Set([ref.code]));
            added.add(ref.entityType);
          }
        }
        if (!added.size) return found;
        // A class reached by reference brings its values: rows refer to values of a class by code.
        await this.addChildren(
          wanted,
          [...added].filter((type) => type === 'apps.class'),
        );
      }
    }
    return this.fetch(wanted, options, options.includeRelated);
  }

  /** Saves a package as a JSON file. */
  download(pkg: ExportedPackage, fileName: string): void {
    const blob = new Blob([JSON.stringify(pkg, null, 2)], { type: 'application/json' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(link.href);
  }

  private fetch(wanted: ReadonlyMap<string, ReadonlySet<string> | null>, options: ExportOptions, skipMissing: boolean): Promise<ExportedPackage> {
    const types: ExportSelection[] = orderTypes(wanted.keys(), this.deps).map((entityType) => {
      const codes = wanted.get(entityType);
      return { entityType, codes: codes ? [...codes] : null };
    });
    return firstValueFrom(this.api.exportPackage({ title: options.title, types, includeSystem: options.includeSystem, skipMissing }));
  }

  /** Values of the chosen classes, versions of the chosen services, workflows and rule sets. */
  private async addChildren(wanted: Map<string, Set<string> | null>, parents: string[]): Promise<void> {
    for (const parent of parents) {
      const child = CHILD_TYPES[parent];
      if (!child || wanted.get(child) === null) continue;
      const codes = wanted.get(parent);
      if (codes === null) {
        wanted.set(child, null);
        continue;
      }
      for (const code of codes ?? []) {
        const rows = await firstValueFrom(this.api.list(child, code));
        const set = wanted.get(child) ?? new Set<string>();
        rows.forEach((row) => set.add(row.code));
        wanted.set(child, set);
      }
    }
  }
}
