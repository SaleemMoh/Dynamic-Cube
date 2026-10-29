import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { AppsControlApi, ConfigRow, LocalizePipe, TranslatePipe } from '@dynamic-cube/shared';
import { catchError, map, of } from 'rxjs';
import { GrantRow, GrantType, ScreenList, ScreenNode, screenLists } from './role-models';

/** The values of one list a role may see on a screen, picked from the list's values (or typed when it has none to show). */
@Component({
  selector: 'dc-grant-values',
  imports: [FormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (options().length) {
      <mat-form-field subscriptSizing="dynamic" class="dc-grant-values">
        <mat-label>{{ 'roles.data.values' | dcTranslate }}</mat-label>
        <mat-select
          multiple
          [ngModel]="value()"
          (ngModelChange)="valueChange.emit($event)"
          [disabled]="readonly()"
          [attr.data-testid]="'grant-values-' + lovCode()"
        >
          @for (o of options(); track o.value) {
            <mat-option [value]="o.value">{{ o.label | dcLocalize }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    } @else if (!values.isLoading()) {
      <mat-form-field subscriptSizing="dynamic" class="dc-grant-values">
        <mat-label>{{ 'roles.data.typedValues' | dcTranslate }}</mat-label>
        <input matInput dir="ltr" [ngModel]="value().join(', ')" (change)="typed($any($event.target).value)" [disabled]="readonly()" />
      </mat-form-field>
    }
  `,
  styles: `
    .dc-grant-values {
      width: 100%;
    }
  `,
})
export class GrantValues {
  readonly lovCode = input.required<string>();
  readonly value = input<string[]>([]);
  readonly readonly = input(false);
  readonly valueChange = output<string[]>();

  private readonly api = inject(AppsControlApi);
  protected readonly values = rxResource({
    params: () => this.lovCode(),
    stream: ({ params }) =>
      this.api.lovValuesForGrants(params).pipe(
        map((page) => page.items.map((i) => ({ value: i.value, label: { ar: i.labelAr, en: i.labelEn } }))),
        catchError(() => of([])),
      ),
  });
  /** The list's values, plus granted values it no longer shows (so they can be removed). */
  protected readonly options = computed(() => {
    const listed = this.values.hasValue() ? this.values.value() : [];
    const known = new Set(listed.map((o) => o.value));
    return [
      ...listed,
      ...this.value()
        .filter((v) => !known.has(v))
        .map((v) => ({ value: v, label: { ar: v, en: v } })),
    ];
  });

  protected typed(text: string): void {
    this.valueChange.emit([
      ...new Set(
        text
          .split(',')
          .map((v) => v.trim())
          .filter(Boolean),
      ),
    ]);
  }
}

/**
 * The data a role sees in the lists (dropdowns) of its screens. Each screen shows the lists bound to its fields; for
 * each, the role sees all values, only some, none, the values a rule gives, or what its general grant says. Any other
 * list can be restricted on the screen too.
 */
@Component({
  selector: 'dc-screen-data',
  imports: [FormsModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule, GrantValues, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-data">
      @for (screen of screens(); track screen.code) {
        <section class="dc-data__screen" [attr.data-data-screen]="screen.code">
          <h4>{{ screen.name | dcLocalize }}</h4>
          @for (list of lists(screen); track list.lovCode + list.resourceCode) {
            <div class="dc-data__row" [attr.data-list]="list.lovCode">
              <div class="dc-data__name">
                <strong>{{ list.name | dcLocalize }}</strong>
                <span class="dc-muted mono" dir="ltr">{{ list.lovCode }}{{ list.fields.length ? ' · ' + list.fields.join(', ') : '' }}</span>
              </div>
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'roles.data.access' | dcTranslate }}</mat-label>
                <mat-select
                  [ngModel]="grantOf(list)?.grantType ?? ''"
                  (ngModelChange)="setType(list, $event)"
                  [disabled]="readonly()"
                  [attr.data-testid]="'grant-type-' + list.lovCode"
                >
                  <mat-option value="">{{ 'roles.data.inherit' | dcTranslate }}</mat-option>
                  @for (t of types; track t) {
                    <mat-option [value]="t">{{ 'roles.data.type.' + t | dcTranslate }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              @switch (grantOf(list)?.grantType) {
                @case ('VALUES') {
                  <dc-grant-values
                    [lovCode]="list.lovCode"
                    [value]="grantOf(list)!.grantedValues"
                    [readonly]="readonly()"
                    (valueChange)="patch(list, { grantedValues: $event })"
                  />
                }
                @case ('RULE') {
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'roles.data.rule' | dcTranslate }}</mat-label>
                    <input
                      matInput
                      dir="ltr"
                      class="mono"
                      [ngModel]="ruleText(list)"
                      (change)="setRule(list, $any($event.target).value)"
                      [disabled]="readonly()"
                      placeholder='{"var": "user.orgUnitAttributes.REGION"}'
                    />
                  </mat-form-field>
                }
                @default {
                  <span></span>
                }
              }
            </div>
          } @empty {
            <p class="dc-muted dc-data__none">{{ 'roles.data.noLists' | dcTranslate }}</p>
          }
          @if (!readonly()) {
            <mat-form-field subscriptSizing="dynamic" class="dc-data__add">
              <mat-icon matPrefix fontSet="material-symbols-outlined">add</mat-icon>
              <mat-label>{{ 'roles.data.addList' | dcTranslate }}</mat-label>
              <mat-select [ngModel]="null" (ngModelChange)="add(screen, $event)" [attr.data-testid]="'add-list-' + screen.code">
                @for (lov of others(screen); track lov.code) {
                  <mat-option [value]="lov.code">{{ { ar: lov.nameAr, en: lov.nameEn } | dcLocalize }} ({{ lov.code }})</mat-option>
                }
              </mat-select>
            </mat-form-field>
          }
        </section>
      } @empty {
        <p class="dc-muted">{{ 'roles.data.noScreens' | dcTranslate }}</p>
      }
    </div>
  `,
  styles: `
    .dc-data {
      display: grid;
      gap: 12px;
    }
    .dc-data__screen {
      display: grid;
      gap: 8px;
      padding: 12px 14px;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius-lg, 8px);
    }
    .dc-data__screen h4 {
      margin: 0;
      font-size: 14px;
    }
    .dc-data__row {
      display: grid;
      grid-template-columns: minmax(10rem, 1fr) 14rem minmax(12rem, 1.4fr);
      gap: 12px;
      align-items: center;
      padding-block: 4px;
      border-top: 1px solid var(--dga-gray-100);
    }
    .dc-data__name {
      display: grid;
      gap: 2px;
    }
    .dc-data__name span {
      font-size: 12px;
    }
    .dc-data__none {
      margin: 0;
      font-size: 13px;
    }
    .dc-data__add {
      max-width: 22rem;
    }
    @media (max-width: 900px) {
      .dc-data__row {
        grid-template-columns: minmax(0, 1fr);
      }
    }
  `,
})
export class ScreenData {
  readonly screens = input.required<ScreenNode[]>();
  readonly lovs = input<ConfigRow[]>([]);
  /** The role's grants on these screens. */
  readonly grants = input<GrantRow[]>([]);
  readonly readonly = input(false);
  readonly grantsChange = output<GrantRow[]>();

  protected readonly types: GrantType[] = ['ALL', 'VALUES', 'NONE', 'RULE'];
  /** Lists added on a screen that have no grant yet. */
  private readonly added = new Map<string, string[]>();

  protected lists(screen: ScreenNode): ScreenList[] {
    const lists = screenLists(screen, this.lovs(), this.grants());
    for (const code of this.added.get(screen.code) ?? []) {
      if (!lists.some((l) => l.lovCode === code)) {
        const lov = this.lovs().find((l) => l.code === code);
        lists.push({ lovCode: code, name: { ar: lov?.nameAr ?? code, en: lov?.nameEn ?? code }, resourceCode: screen.code, fields: [] });
      }
    }
    return lists;
  }

  protected others(screen: ScreenNode): ConfigRow[] {
    const listed = new Set(this.lists(screen).map((l) => l.lovCode));
    return this.lovs().filter((l) => !listed.has(l.code) && l.isActive !== false);
  }

  protected grantOf(list: ScreenList): GrantRow | undefined {
    return this.grants().find((g) => g.lovCode === list.lovCode && g.resourceCode === list.resourceCode);
  }

  protected add(screen: ScreenNode, lovCode: string | null): void {
    if (!lovCode) return;
    this.added.set(screen.code, [...(this.added.get(screen.code) ?? []), lovCode]);
    this.emit([...this.grants(), { lovCode, resourceCode: screen.code, grantType: 'ALL', grantedValues: [] }]);
  }

  protected setType(list: ScreenList, type: GrantType | ''): void {
    const others = this.grants().filter((g) => g !== this.grantOf(list));
    if (!type) {
      this.emit(others);
      return;
    }
    const current = this.grantOf(list);
    this.emit([
      ...others,
      {
        lovCode: list.lovCode,
        resourceCode: list.resourceCode,
        grantType: type,
        grantedValues: type === 'VALUES' ? (current?.grantedValues ?? []) : [],
        rule: type === 'RULE' ? (current?.rule ?? null) : null,
        defaultValue: current?.defaultValue ?? null,
      },
    ]);
  }

  protected patch(list: ScreenList, change: Partial<GrantRow>): void {
    const current = this.grantOf(list);
    if (!current) return;
    this.emit(this.grants().map((g) => (g === current ? { ...g, ...change } : g)));
  }

  protected ruleText(list: ScreenList): string {
    const rule = this.grantOf(list)?.rule;
    return rule === null || rule === undefined ? '' : JSON.stringify(rule);
  }

  protected setRule(list: ScreenList, text: string): void {
    let rule: unknown = text;
    try {
      rule = text.trim() ? JSON.parse(text) : null;
    } catch {
      // Kept as typed; the server reports the rule as invalid.
    }
    this.patch(list, { rule });
  }

  private emit(grants: GrantRow[]): void {
    this.grantsChange.emit(grants);
  }
}
