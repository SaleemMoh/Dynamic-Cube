import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { TranslatePipe } from '@dynamic-cube/shared';
import { stepOf } from './lov-builder-model';

/** What the preview of a query shows: its values (or its problems), and the SQL it becomes. */
export interface LovPreview {
  sql: string | null;
  items: { value: string; labelAr: string; labelEn: string; parent: string | null }[];
  hasMore: boolean;
  problems: { field: string; message: string }[];
  error: string | null;
}

/** The live preview beside the query builder: the first values, a search to try, the problems and the resulting SQL. */
@Component({
  selector: 'dc-lov-builder-preview',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './lov-builder-preview.html',
  styleUrl: './lov-builder-preview.scss',
})
export class LovBuilderPreview {
  readonly preview = input<LovPreview | null>(null);
  readonly previewing = input(false);
  readonly hasParent = input(false);
  /** The search typed to try the list. */
  readonly searched = output<string>();
  /** A problem was chosen: the field it is about. */
  readonly problemChosen = output<string>();

  protected readonly search = signal('');
  protected readonly showSql = signal(false);
  protected readonly stepOf = stepOf;
  protected readonly steps = ['get', 'join', 'map', 'filter', 'sort', 'access', 'details'];
}
