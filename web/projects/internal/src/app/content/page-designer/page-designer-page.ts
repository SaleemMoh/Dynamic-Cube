import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import {
  AppsControlApi,
  ConfigRow,
  I18nService,
  LocalizePipe,
  PAGE_LAYOUTS,
  PageBackground,
  PageBlock,
  PageBlockKind,
  PageBlockView,
  PageBlocksView,
  PageCard,
  PageDesign,
  PageLayout,
  TranslatePipe,
  newBlock,
  problemMessages,
  readPageDesign,
  starterPage,
} from '@dynamic-cube/shared';
import { catchError, firstValueFrom, of } from 'rxjs';
import { FullWindow } from '../../common/full-window';
import { payloadOf } from '../../apps-control/config/form-values';
import { DraftsService } from '../../apps-control/drafts.service';
import { PrivilegesService } from '../../apps-control/privileges.service';
import { SubmitButton } from '../../apps-control/submit-button';
import {
  BlockPlace,
  addSection,
  findBlock,
  insertBlock,
  moveBlock,
  moveSection,
  nextId,
  removeBlock,
  removeSection,
  updateBlock,
  updateSection,
} from './page-editor-model';

const BLOCK_MIME = 'application/x-dc-page-block';
const SECTION_MIME = 'application/x-dc-page-section';
const MOVE_MIME = 'application/x-dc-page-move';
const MAX_IMAGE_BYTES = 300 * 1024;
const PAGE = 'cms.page';
const VERSION = 'cms.page-version';

const BLOCKS: { kind: PageBlockKind; icon: string }[] = [
  { kind: 'heading', icon: 'title' },
  { kind: 'text', icon: 'notes' },
  { kind: 'image', icon: 'image' },
  { kind: 'button', icon: 'smart_button' },
  { kind: 'cards', icon: 'view_module' },
  { kind: 'callout', icon: 'info' },
  { kind: 'divider', icon: 'horizontal_rule' },
  { kind: 'spacer', icon: 'height' },
];
const LAYOUTS: PageLayout[] = ['1', '1-1', '1-1-1', '2-1', '1-2'];
const BACKGROUNDS: PageBackground[] = ['none', 'light', 'brand', 'dark'];
const DEVICES = { desktop: null, tablet: 820, mobile: 400 } as const;

type Selection = { type: 'block'; id: string } | { type: 'section'; index: number } | null;

/**
 * Designs a portal page by drag and drop: sections (bands of one to three columns) and blocks (headings, text, images,
 * buttons, cards, callouts…) dropped from the palette, moved between columns and edited in the side panel. Saving adds
 * the next version of the page (versions never change); publishing makes a version the one the portal shows, in the
 * same change set or later, and an earlier version can be published again.
 */
@Component({
  selector: 'dc-page-designer-page',
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
    LocalizePipe,
    NgTemplateOutlet,
    PageBlockView,
    PageBlocksView,
    SubmitButton,
    TranslatePipe,
  ],
  hostDirectives: [FullWindow],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(keydown)': 'onKey($event)' },
  template: `
    <header class="dc-page__header pg__header">
      <h1>{{ 'pageDesigner.title' | dcTranslate }}</h1>
      <div class="pg__pick">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'pageDesigner.page' | dcTranslate }}</mat-label>
          <mat-select
            [value]="current()"
            (selectionChange)="choose($event.value)"
            data-testid="pg-page"
          >
            @for (p of pages.value() ?? []; track p.code) {
              <mat-option [value]="p.code"
                >{{ { ar: p.nameAr, en: p.nameEn } | dcLocalize }} ({{ p.code }})</mat-option
              >
            }
          </mat-select>
        </mat-form-field>
        @if (current()) {
          <span class="pg__target" dir="ltr" data-testid="pg-target"
            >→ {{ current() }}/{{ nextNumber() }}</span
          >
        }
      </div>
    </header>

    @if (current()) {
      <div class="pg__toolbar" role="toolbar">
        <button
          matIconButton
          type="button"
          (click)="undo()"
          [disabled]="!past().length"
          [matTooltip]="'designer.undo' | dcTranslate"
          data-testid="pg-undo"
        >
          <mat-icon fontSet="material-symbols-outlined">undo</mat-icon>
        </button>
        <button
          matIconButton
          type="button"
          (click)="redo()"
          [disabled]="!future().length"
          [matTooltip]="'designer.redo' | dcTranslate"
        >
          <mat-icon fontSet="material-symbols-outlined">redo</mat-icon>
        </button>
        <span class="pg__sep"></span>
        <mat-button-toggle-group
          [value]="device()"
          (change)="device.set($event.value)"
          [attr.aria-label]="'pageDesigner.device' | dcTranslate"
        >
          <mat-button-toggle value="desktop" [matTooltip]="'pageDesigner.desktop' | dcTranslate"
            ><mat-icon fontSet="material-symbols-outlined"
              >desktop_windows</mat-icon
            ></mat-button-toggle
          >
          <mat-button-toggle value="tablet" [matTooltip]="'pageDesigner.tablet' | dcTranslate"
            ><mat-icon fontSet="material-symbols-outlined">tablet</mat-icon></mat-button-toggle
          >
          <mat-button-toggle value="mobile" [matTooltip]="'pageDesigner.mobile' | dcTranslate"
            ><mat-icon fontSet="material-symbols-outlined">smartphone</mat-icon></mat-button-toggle
          >
        </mat-button-toggle-group>
        <mat-button-toggle-group
          [value]="preview() ? 'preview' : 'edit'"
          (change)="preview.set($event.value === 'preview')"
        >
          <mat-button-toggle value="edit">{{
            'pageDesigner.edit' | dcTranslate
          }}</mat-button-toggle>
          <mat-button-toggle value="preview" data-testid="pg-preview">{{
            'docTemplates.preview' | dcTranslate
          }}</mat-button-toggle>
        </mat-button-toggle-group>
        <span class="pg__grow"></span>
        <button
          matIconButton
          type="button"
          (click)="fullWindow.toggle()"
          [matTooltip]="
            (fullWindow.on() ? 'designer.exitFullWindow' : 'designer.fullWindow') | dcTranslate
          "
          data-testid="pg-full-window"
        >
          <mat-icon fontSet="material-symbols-outlined">{{
            fullWindow.on() ? 'close_fullscreen' : 'open_in_full'
          }}</mat-icon>
        </button>
        @if (canCreate()) {
          <mat-checkbox
            [ngModel]="publish()"
            (ngModelChange)="publish.set($event)"
            data-testid="pg-publish"
            >{{ 'pageDesigner.publishToo' | dcTranslate }}</mat-checkbox
          >
          <dc-submit-button
            [entityType]="versionType"
            [disabled]="busy() || !design().sections.length"
            (submitted)="save(true)"
          />
          <button
            matButton="filled"
            type="button"
            (click)="save()"
            [disabled]="busy() || !design().sections.length"
            data-testid="pg-save"
          >
            <mat-icon fontSet="material-symbols-outlined">save</mat-icon>
            {{ drafts.saveLabel(versionType) | dcTranslate }}
          </button>
        }
      </div>
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li dir="auto">{{ e }}</li>
          }
        </ul>
      }

      <div class="pg__body" [class.pg__body--preview]="preview()">
        @if (!preview()) {
          <aside class="pg__palette">
            <h3>{{ 'pageDesigner.sections' | dcTranslate }}</h3>
            <div class="pg__layouts">
              @for (l of layouts; track l) {
                <button
                  type="button"
                  class="pg__layout"
                  draggable="true"
                  (dragstart)="dragNewSection($event, l)"
                  (click)="addSectionAt(design().sections.length, l)"
                  [matTooltip]="'pageDesigner.layout.' + l | dcTranslate"
                  [attr.data-testid]="'pg-layout-' + l"
                >
                  @for (w of layoutWidths(l); track $index) {
                    <span [style.flex]="w"></span>
                  }
                </button>
              }
            </div>
            <h3>{{ 'pageDesigner.blocks' | dcTranslate }}</h3>
            @for (b of blocks; track b.kind) {
              <button
                type="button"
                class="pg__tool"
                draggable="true"
                (dragstart)="dragNewBlock($event, b.kind)"
                (click)="addBlockToSelection(b.kind)"
                [attr.data-testid]="'pg-tool-' + b.kind"
              >
                <mat-icon fontSet="material-symbols-outlined">{{ b.icon }}</mat-icon>
                {{ 'pageDesigner.kind.' + b.kind | dcTranslate }}
              </button>
            }
            <p class="dc-muted pg__tip">{{ 'pageDesigner.tip' | dcTranslate }}</p>
          </aside>
        }

        <div class="pg__canvas" (pointerdown)="select(null)">
          <div class="pg__frame" [style.max-width.px]="deviceWidth()" data-testid="pg-canvas">
            @if (preview()) {
              <dc-page-blocks [design]="design()" />
            } @else {
              @for (section of design().sections; track section.id; let s = $index) {
                <div
                  class="pg__section-drop"
                  (dragover)="allowDrop($event)"
                  (drop)="dropSection($event, s)"
                ></div>
                <section
                  class="pg__section"
                  [class]="'pg__section pg__section--' + section.background"
                  [class.pg__section--selected]="isSection(s)"
                  (pointerdown)="$event.stopPropagation()"
                  (click)="$event.stopPropagation(); select({ type: 'section', index: s })"
                  (keydown.enter)="select({ type: 'section', index: s })"
                  tabindex="0"
                  [attr.data-testid]="'pg-section-' + s"
                >
                  <div class="pg__section-bar">
                    <span>{{ 'pageDesigner.section' | dcTranslate }} {{ s + 1 }}</span>
                    <button
                      matIconButton
                      type="button"
                      (click)="$event.stopPropagation(); commit(moveSection(design(), s, -1))"
                      [disabled]="s === 0"
                      [attr.aria-label]="'menus.moveUp' | dcTranslate"
                    >
                      <mat-icon fontSet="material-symbols-outlined">arrow_upward</mat-icon>
                    </button>
                    <button
                      matIconButton
                      type="button"
                      (click)="$event.stopPropagation(); commit(moveSection(design(), s, 1))"
                      [disabled]="s === design().sections.length - 1"
                      [attr.aria-label]="'menus.moveDown' | dcTranslate"
                    >
                      <mat-icon fontSet="material-symbols-outlined">arrow_downward</mat-icon>
                    </button>
                    <button
                      matIconButton
                      type="button"
                      (click)="$event.stopPropagation(); removeSectionAt(s)"
                      [attr.aria-label]="'config.delete' | dcTranslate"
                    >
                      <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                    </button>
                  </div>
                  <div class="pg__columns" [style.grid-template-columns]="gridOf(section.layout)">
                    @for (column of section.columns; track $index; let c = $index) {
                      <div
                        class="pg__column"
                        [class.pg__column--target]="isTarget(s, c, column.length)"
                        (dragover)="overColumn($event, s, c, column.length)"
                        (drop)="drop($event)"
                        (dragleave)="leave()"
                        [attr.data-testid]="'pg-column-' + s + '-' + c"
                      >
                        @for (block of column; track block.id; let i = $index) {
                          @if (isTarget(s, c, i)) {
                            <div class="pg__indicator"></div>
                          }
                          <div
                            class="pg__block"
                            [class.pg__block--selected]="isBlock(block.id)"
                            draggable="true"
                            (dragstart)="dragExisting($event, block.id)"
                            (dragover)="overBlock($event, s, c, i)"
                            (pointerdown)="$event.stopPropagation()"
                            (click)="
                              $event.stopPropagation(); select({ type: 'block', id: block.id })
                            "
                            (keydown.enter)="
                              $event.stopPropagation(); select({ type: 'block', id: block.id })
                            "
                            tabindex="0"
                            [attr.data-testid]="'pg-block-' + block.id"
                          >
                            <dc-page-block [block]="block" [editing]="true" />
                          </div>
                        }
                        @if (isTarget(s, c, column.length) && column.length) {
                          <div class="pg__indicator"></div>
                        }
                        @if (!column.length) {
                          <p class="pg__empty">{{ 'pageDesigner.dropHere' | dcTranslate }}</p>
                        }
                      </div>
                    }
                  </div>
                </section>
              }
              <div
                class="pg__section-drop pg__section-drop--end"
                (dragover)="allowDrop($event)"
                (drop)="dropSection($event, design().sections.length)"
                data-testid="pg-section-end"
              >
                {{ 'pageDesigner.dropSection' | dcTranslate }}
              </div>
            }
          </div>
        </div>

        @if (!preview()) {
          <aside class="pg__side">
            <mat-button-toggle-group
              [value]="sidePanel()"
              (change)="sidePanel.set($event.value)"
              class="pg__side-tabs"
            >
              <mat-button-toggle value="props">{{
                'pageDesigner.properties' | dcTranslate
              }}</mat-button-toggle>
              <mat-button-toggle value="versions" data-testid="pg-versions-tab">{{
                'pageDesigner.versions' | dcTranslate
              }}</mat-button-toggle>
            </mat-button-toggle-group>

            @if (sidePanel() === 'versions') {
              <ul class="pg__versions" data-testid="pg-versions">
                @for (v of versionRows(); track v.code) {
                  <li>
                    <div>
                      <strong dir="ltr">{{ v.code }}</strong>
                      @if (number(v) === publishedNumber()) {
                        <span class="dc-pill dc-pill--green">{{
                          'pageDesigner.published' | dcTranslate
                        }}</span>
                      }
                      @if (number(v) === base()) {
                        <span class="dc-pill">{{ 'pageDesigner.open' | dcTranslate }}</span>
                      }
                    </div>
                    <div class="pg__version-actions">
                      <button matButton type="button" (click)="openVersion(number(v))">
                        {{ 'pageDesigner.startFrom' | dcTranslate }}
                      </button>
                      @if (canEdit() && number(v) !== publishedNumber()) {
                        <button
                          matButton
                          type="button"
                          (click)="publishVersion(number(v))"
                          [attr.data-testid]="'pg-publish-' + number(v)"
                        >
                          {{ 'pageDesigner.publish' | dcTranslate }}
                        </button>
                      }
                    </div>
                  </li>
                } @empty {
                  <li class="dc-muted">{{ 'pageDesigner.noVersions' | dcTranslate }}</li>
                }
              </ul>
              <p class="dc-muted pg__tip">{{ 'pageDesigner.versionsHint' | dcTranslate }}</p>
            } @else if (selectedBlock(); as b) {
              <h3>{{ 'pageDesigner.kind.' + b.kind | dcTranslate }}</h3>
              @switch (b.kind) {
                @case ('heading') {
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'pageDesigner.textAr' | dcTranslate }}</mat-label>
                    <input
                      matInput
                      [ngModel]="b.textAr"
                      (ngModelChange)="edit(b.id, { textAr: $event })"
                      data-testid="pg-text-ar"
                    />
                  </mat-form-field>
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'pageDesigner.textEn' | dcTranslate }}</mat-label>
                    <input
                      matInput
                      dir="ltr"
                      [ngModel]="b.textEn"
                      (ngModelChange)="edit(b.id, { textEn: $event })"
                    />
                  </mat-form-field>
                  <mat-button-toggle-group
                    [value]="b.level ?? 2"
                    (change)="edit(b.id, { level: $event.value })"
                  >
                    <mat-button-toggle [value]="1">H1</mat-button-toggle>
                    <mat-button-toggle [value]="2">H2</mat-button-toggle>
                    <mat-button-toggle [value]="3">H3</mat-button-toggle>
                  </mat-button-toggle-group>
                }
                @case ('image') {
                  <input
                    #file
                    type="file"
                    accept="image/png,image/jpeg,image/webp"
                    hidden
                    (change)="loadImage(b.id, file)"
                  />
                  <button matButton="outlined" type="button" (click)="file.click()">
                    <mat-icon fontSet="material-symbols-outlined">upload</mat-icon
                    >{{ 'pageDesigner.chooseImage' | dcTranslate }}
                  </button>
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'print.imageUrl' | dcTranslate }}</mat-label>
                    <input
                      matInput
                      dir="ltr"
                      [ngModel]="b.src?.startsWith('data:') ? '' : b.src"
                      (ngModelChange)="edit(b.id, { src: $event.trim() })"
                      placeholder="https://"
                    />
                  </mat-form-field>
                  @if (imageError()) {
                    <span class="pg__bad">{{ imageError()! | dcTranslate }}</span>
                  }
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'pageDesigner.altAr' | dcTranslate }}</mat-label>
                    <input
                      matInput
                      [ngModel]="b.altAr"
                      (ngModelChange)="edit(b.id, { altAr: $event })"
                    />
                  </mat-form-field>
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'pageDesigner.altEn' | dcTranslate }}</mat-label>
                    <input
                      matInput
                      dir="ltr"
                      [ngModel]="b.altEn"
                      (ngModelChange)="edit(b.id, { altEn: $event })"
                    />
                  </mat-form-field>
                }
                @case ('button') {
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'pageDesigner.labelAr' | dcTranslate }}</mat-label>
                    <input
                      matInput
                      [ngModel]="b.labelAr"
                      (ngModelChange)="edit(b.id, { labelAr: $event })"
                    />
                  </mat-form-field>
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'pageDesigner.labelEn' | dcTranslate }}</mat-label>
                    <input
                      matInput
                      dir="ltr"
                      [ngModel]="b.labelEn"
                      (ngModelChange)="edit(b.id, { labelEn: $event })"
                    />
                  </mat-form-field>
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'pageDesigner.url' | dcTranslate }}</mat-label>
                    <input
                      matInput
                      dir="ltr"
                      [ngModel]="b.url"
                      (ngModelChange)="edit(b.id, { url: $event.trim() })"
                      placeholder="/services"
                      data-testid="pg-url"
                    />
                  </mat-form-field>
                  <mat-button-toggle-group
                    [value]="b.style ?? 'primary'"
                    (change)="edit(b.id, { style: $event.value })"
                  >
                    <mat-button-toggle value="primary">{{
                      'pageDesigner.primary' | dcTranslate
                    }}</mat-button-toggle>
                    <mat-button-toggle value="secondary">{{
                      'pageDesigner.secondary' | dcTranslate
                    }}</mat-button-toggle>
                  </mat-button-toggle-group>
                }
                @case ('cards') {
                  @for (card of b.items ?? []; track $index; let i = $index) {
                    <fieldset class="pg__card">
                      <legend>{{ 'pageDesigner.card' | dcTranslate }} {{ i + 1 }}</legend>
                      <mat-form-field subscriptSizing="dynamic">
                        <mat-label>{{ 'pageDesigner.icon' | dcTranslate }}</mat-label>
                        <input
                          matInput
                          dir="ltr"
                          [ngModel]="card.icon"
                          (ngModelChange)="editCard(b, i, { icon: $event.trim() })"
                        />
                      </mat-form-field>
                      <mat-form-field subscriptSizing="dynamic">
                        <mat-label>{{ 'pageDesigner.titleAr' | dcTranslate }}</mat-label>
                        <input
                          matInput
                          [ngModel]="card.titleAr"
                          (ngModelChange)="editCard(b, i, { titleAr: $event })"
                        />
                      </mat-form-field>
                      <mat-form-field subscriptSizing="dynamic">
                        <mat-label>{{ 'pageDesigner.titleEn' | dcTranslate }}</mat-label>
                        <input
                          matInput
                          dir="ltr"
                          [ngModel]="card.titleEn"
                          (ngModelChange)="editCard(b, i, { titleEn: $event })"
                        />
                      </mat-form-field>
                      <mat-form-field subscriptSizing="dynamic">
                        <mat-label>{{ 'pageDesigner.textAr' | dcTranslate }}</mat-label>
                        <input
                          matInput
                          [ngModel]="card.textAr"
                          (ngModelChange)="editCard(b, i, { textAr: $event })"
                        />
                      </mat-form-field>
                      <mat-form-field subscriptSizing="dynamic">
                        <mat-label>{{ 'pageDesigner.textEn' | dcTranslate }}</mat-label>
                        <input
                          matInput
                          dir="ltr"
                          [ngModel]="card.textEn"
                          (ngModelChange)="editCard(b, i, { textEn: $event })"
                        />
                      </mat-form-field>
                      <mat-form-field subscriptSizing="dynamic">
                        <mat-label>{{ 'pageDesigner.url' | dcTranslate }}</mat-label>
                        <input
                          matInput
                          dir="ltr"
                          [ngModel]="card.url"
                          (ngModelChange)="editCard(b, i, { url: $event.trim() })"
                          placeholder="/services"
                        />
                      </mat-form-field>
                      <button matButton type="button" (click)="removeCard(b, i)">
                        {{ 'config.delete' | dcTranslate }}
                      </button>
                    </fieldset>
                  }
                  <button matButton type="button" (click)="addCard(b)">
                    <mat-icon fontSet="material-symbols-outlined">add</mat-icon
                    >{{ 'pageDesigner.addCard' | dcTranslate }}
                  </button>
                }
                @case ('callout') {
                  <mat-button-toggle-group
                    [value]="b.tone ?? 'info'"
                    (change)="edit(b.id, { tone: $event.value })"
                  >
                    <mat-button-toggle value="info">{{
                      'pageDesigner.tone.info' | dcTranslate
                    }}</mat-button-toggle>
                    <mat-button-toggle value="success">{{
                      'pageDesigner.tone.success' | dcTranslate
                    }}</mat-button-toggle>
                    <mat-button-toggle value="warning">{{
                      'pageDesigner.tone.warning' | dcTranslate
                    }}</mat-button-toggle>
                  </mat-button-toggle-group>
                  <ng-container *ngTemplateOutlet="texts; context: { $implicit: b }" />
                }
                @case ('text') {
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'pageDesigner.textAr' | dcTranslate }}</mat-label>
                    <textarea
                      matInput
                      rows="6"
                      [ngModel]="b.textAr"
                      (ngModelChange)="edit(b.id, { textAr: $event })"
                      data-testid="pg-text-ar"
                    ></textarea>
                    <mat-hint>{{ 'pageDesigner.markdown' | dcTranslate }}</mat-hint>
                  </mat-form-field>
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'pageDesigner.textEn' | dcTranslate }}</mat-label>
                    <textarea
                      matInput
                      dir="ltr"
                      rows="6"
                      [ngModel]="b.textEn"
                      (ngModelChange)="edit(b.id, { textEn: $event })"
                    ></textarea>
                  </mat-form-field>
                }
                @case ('spacer') {
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ 'pageDesigner.size' | dcTranslate }}</mat-label>
                    <input
                      matInput
                      type="number"
                      min="4"
                      max="200"
                      [ngModel]="b.size ?? 32"
                      (ngModelChange)="edit(b.id, { size: +$event })"
                    />
                  </mat-form-field>
                }
              }
              @if (b.kind === 'heading' || b.kind === 'text' || b.kind === 'button') {
                <mat-button-toggle-group
                  [value]="b.align ?? 'start'"
                  (change)="edit(b.id, { align: $event.value })"
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
              <div class="pg__actions">
                <button matButton type="button" (click)="duplicate(b)">
                  <mat-icon fontSet="material-symbols-outlined">content_copy</mat-icon
                  >{{ 'print.duplicate' | dcTranslate }}
                </button>
                <button
                  matButton
                  type="button"
                  (click)="commit(removeBlock(design(), b.id)); select(null)"
                  data-testid="pg-delete-block"
                >
                  <mat-icon fontSet="material-symbols-outlined">delete</mat-icon
                  >{{ 'config.delete' | dcTranslate }}
                </button>
              </div>
            } @else if (selectedSection(); as sec) {
              <h3>{{ 'pageDesigner.section' | dcTranslate }} {{ sec.index + 1 }}</h3>
              <span class="pg__label">{{ 'pageDesigner.columns' | dcTranslate }}</span>
              <div class="pg__layouts">
                @for (l of layouts; track l) {
                  <button
                    type="button"
                    class="pg__layout"
                    [class.pg__layout--on]="sec.section.layout === l"
                    (click)="commit(updateSection(design(), sec.index, { layout: l }))"
                    [matTooltip]="'pageDesigner.layout.' + l | dcTranslate"
                  >
                    @for (w of layoutWidths(l); track $index) {
                      <span [style.flex]="w"></span>
                    }
                  </button>
                }
              </div>
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'pageDesigner.background' | dcTranslate }}</mat-label>
                <mat-select
                  [value]="sec.section.background"
                  (selectionChange)="
                    commit(updateSection(design(), sec.index, { background: $event.value }))
                  "
                >
                  @for (bg of backgrounds; track bg) {
                    <mat-option [value]="bg">{{
                      'pageDesigner.bg.' + bg | dcTranslate
                    }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
            } @else {
              <p class="dc-muted">{{ 'pageDesigner.nothingSelected' | dcTranslate }}</p>
            }
          </aside>
        }
      </div>
      @if (saved()) {
        <p class="pg__saved" role="status" data-testid="pg-saved">{{ saved()! }}</p>
      }
    } @else {
      <p class="dc-muted">{{ 'pageDesigner.choose' | dcTranslate }}</p>
    }

    <ng-template #texts let-b>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'pageDesigner.textAr' | dcTranslate }}</mat-label>
        <textarea
          matInput
          rows="4"
          [ngModel]="b.textAr"
          (ngModelChange)="edit(b.id, { textAr: $event })"
        ></textarea>
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'pageDesigner.textEn' | dcTranslate }}</mat-label>
        <textarea
          matInput
          dir="ltr"
          rows="4"
          [ngModel]="b.textEn"
          (ngModelChange)="edit(b.id, { textEn: $event })"
        ></textarea>
      </mat-form-field>
    </ng-template>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 8px;
      height: calc(100vh - 7.5rem);
      min-height: 36rem;
      outline: none;
    }
    .pg__header {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }
    .pg__pick {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .pg__pick mat-form-field {
      min-width: 20rem;
    }
    .pg__target {
      font-family: ui-monospace, monospace;
      font-weight: 600;
    }
    .pg__toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
      padding: 4px 8px;
      border: 1px solid var(--dga-gray-200);
      border-radius: 8px;
      background: #fff;
    }
    .pg__sep {
      width: 1px;
      height: 24px;
      background: var(--dga-gray-200);
    }
    .pg__grow {
      flex: 1;
    }
    .pg__body {
      display: grid;
      grid-template-columns: 12rem minmax(0, 1fr) 19rem;
      gap: 8px;
      flex: 1;
      min-height: 0;
    }
    .pg__body--preview {
      grid-template-columns: minmax(0, 1fr);
    }
    .pg__palette,
    .pg__side {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 10px;
      border: 1px solid var(--dga-gray-200);
      border-radius: 8px;
      background: #fff;
      overflow: auto;
    }
    .pg__palette h3,
    .pg__side h3 {
      margin: 4px 0 0;
      font-size: 15px;
    }
    .pg__layouts {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 6px;
    }
    .pg__layout {
      display: flex;
      gap: 3px;
      height: 30px;
      padding: 5px;
      border: 1px solid var(--dga-gray-300);
      border-radius: 6px;
      background: #fff;
      cursor: grab;
    }
    .pg__layout span {
      border-radius: 2px;
      background: var(--dga-gray-300);
    }
    .pg__layout--on {
      border-color: var(--dynamic-cube-gold);
      box-shadow: 0 0 0 1px var(--dynamic-cube-gold);
    }
    .pg__tool {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 10px;
      border: 1px solid var(--dga-gray-200);
      border-radius: 8px;
      background: var(--dga-gray-50);
      font: inherit;
      text-align: start;
      cursor: grab;
    }
    .pg__tool:hover,
    .pg__layout:hover {
      border-color: var(--dynamic-cube-gold);
    }
    .pg__tip {
      font-size: 12px;
    }
    .pg__canvas {
      overflow: auto;
      padding: 20px;
      border-radius: 8px;
      background: var(--dga-gray-100);
    }
    .pg__frame {
      margin-inline: auto;
      padding: 16px;
      border-radius: 8px;
      background: #fff;
      box-shadow: 0 2px 10px rgb(0 0 0 / 8%);
    }
    .pg__section {
      position: relative;
      padding: 28px 16px 16px;
      border: 1px dashed var(--dga-gray-300);
      border-radius: 10px;
    }
    .pg__section--light {
      background: var(--dga-gray-50);
    }
    .pg__section--brand {
      background: color-mix(in srgb, var(--dga-green-700, #1b8354) 8%, #fff);
    }
    .pg__section--dark {
      background: var(--dga-gray-900);
      color: #fff;
    }
    .pg__section--selected {
      border: 2px solid var(--dynamic-cube-gold);
    }
    .pg__section-bar {
      position: absolute;
      inset-block-start: 0;
      inset-inline: 0;
      display: flex;
      align-items: center;
      gap: 2px;
      padding-inline: 10px;
      font-size: 12px;
      color: var(--dga-gray-500);
    }
    .pg__section-bar span {
      flex: 1;
    }
    .pg__section-bar button {
      transform: scale(0.8);
    }
    .pg__section-drop {
      height: 12px;
    }
    .pg__section-drop--end {
      display: grid;
      place-items: center;
      height: 56px;
      margin-top: 8px;
      border: 1px dashed var(--dga-gray-300);
      border-radius: 10px;
      color: var(--dga-gray-500);
      font-size: 13px;
    }
    .pg__columns {
      display: grid;
      gap: 16px;
    }
    .pg__column {
      display: flex;
      flex-direction: column;
      gap: 10px;
      min-height: 64px;
      min-width: 0;
      padding: 6px;
      border: 1px dashed transparent;
      border-radius: 8px;
    }
    .pg__column--target {
      border-color: var(--dynamic-cube-gold);
      background: var(--dynamic-cube-gold-50);
    }
    .pg__block {
      position: relative;
      padding: 4px;
      border-radius: 6px;
      outline: 1px dashed transparent;
      cursor: grab;
    }
    .pg__block:hover {
      outline-color: var(--dga-gray-300);
    }
    .pg__block--selected,
    .pg__block--selected:hover {
      outline: 2px solid var(--dynamic-cube-gold);
    }
    .pg__block ::ng-deep a {
      pointer-events: none;
    }
    .pg__indicator {
      height: 3px;
      border-radius: 2px;
      background: var(--dynamic-cube-gold);
    }
    .pg__empty {
      margin: auto;
      color: var(--dga-gray-500);
      font-size: 13px;
    }
    .pg__side-tabs {
      align-self: stretch;
    }
    .pg__versions {
      display: grid;
      gap: 8px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .pg__versions li {
      display: grid;
      gap: 4px;
      padding: 8px;
      border: 1px solid var(--dga-gray-200);
      border-radius: 8px;
    }
    .pg__versions .dc-pill {
      margin-inline-start: 6px;
    }
    .pg__version-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
    }
    .pg__card {
      display: grid;
      gap: 6px;
      border: 1px solid var(--dga-gray-200);
      border-radius: 8px;
    }
    .pg__actions {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
      margin-top: 8px;
    }
    .pg__label {
      font-size: 13px;
      color: var(--dga-gray-600);
    }
    .pg__bad {
      color: var(--dga-red-700, #b42318);
      font-size: 13px;
    }
    .pg__saved {
      margin: 0;
      color: var(--dga-green-700, #1b8354);
    }
  `,
})
export class PageDesignerPage {
  /** Query parameter: the page to open. */
  readonly page = input<string | undefined>(undefined);

  private readonly api = inject(AppsControlApi);
  protected readonly drafts = inject(DraftsService);
  private readonly privileges = inject(PrivilegesService);
  private readonly i18n = inject(I18nService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly fullWindow = inject(FullWindow);

  protected readonly versionType = VERSION;
  protected readonly blocks = BLOCKS;
  protected readonly layouts = LAYOUTS;
  protected readonly backgrounds = BACKGROUNDS;
  protected readonly moveSection = moveSection;
  protected readonly removeBlock = removeBlock;
  protected readonly updateSection = updateSection;

  protected readonly current = signal<string | null>(null);
  protected readonly design = signal<PageDesign>(starterPage());
  protected readonly base = signal<number | null>(null);
  protected readonly past = signal<PageDesign[]>([]);
  protected readonly future = signal<PageDesign[]>([]);
  protected readonly selection = signal<Selection>(null);
  protected readonly device = signal<keyof typeof DEVICES>('desktop');
  protected readonly preview = signal(false);
  protected readonly publish = signal(true);
  protected readonly sidePanel = signal<'props' | 'versions'>('props');
  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly saved = signal<string | null>(null);
  protected readonly imageError = signal<string | null>(null);
  private readonly target = signal<BlockPlace | null>(null);

  protected readonly canCreate = computed(() => this.privileges.can('content.config', 'CREATE'));
  protected readonly canEdit = computed(() => this.privileges.can('content.config', 'EDIT'));
  protected readonly pages = rxResource({
    stream: () => this.api.list(PAGE).pipe(catchError(() => of([] as ConfigRow[]))),
  });
  private readonly versions = rxResource({
    params: () => this.current() ?? undefined,
    stream: ({ params }) =>
      this.api.list(VERSION, params).pipe(catchError(() => of([] as ConfigRow[]))),
  });
  protected readonly versionRows = computed(() => this.versions.value() ?? []);
  protected readonly pageRow = computed(
    () => (this.pages.value() ?? []).find((p) => p.code === this.current()) ?? null,
  );
  protected readonly publishedNumber = computed(
    () => (this.pageRow()?.['publishedVersion'] as number | null | undefined) ?? null,
  );
  protected readonly nextNumber = computed(
    () => Math.max(0, ...this.versionRows().map((v) => this.number(v))) + 1,
  );
  protected readonly deviceWidth = computed(() => DEVICES[this.device()]);
  protected readonly selectedBlock = computed(() => {
    const s = this.selection();
    if (s?.type !== 'block') return null;
    const place = findBlock(this.design(), s.id);
    return place ? this.design().sections[place.section].columns[place.column][place.index] : null;
  });
  protected readonly selectedSection = computed(() => {
    const s = this.selection();
    return s?.type === 'section' && this.design().sections[s.index]
      ? { index: s.index, section: this.design().sections[s.index] }
      : null;
  });

  constructor() {
    effect(() => {
      const code = this.page();
      untracked(() => {
        if (code && code !== this.current()) this.current.set(code);
      });
    });
    // A chosen page opens on its published version (or its latest, or a starting layout).
    let opened: string | null = null;
    effect(() => {
      const code = this.current();
      const rows = this.versions.value();
      if (!code || !rows || !this.pages.hasValue() || opened === code) return;
      opened = code;
      untracked(() => {
        const start =
          this.publishedNumber() ??
          (rows.length ? Math.max(...rows.map((v) => this.number(v))) : null);
        if (start) void this.openVersion(start);
        else this.reset(this.fromBody(), null);
      });
    });
  }

  /** A first layout from what the page shows today: its title and its Markdown body. */
  private fromBody(): PageDesign {
    const page = this.pageRow();
    if (!page) return starterPage();
    const bodyAr = String(page['bodyAr'] ?? '');
    const bodyEn = String(page['bodyEn'] ?? '');
    return {
      version: 1,
      sections: [
        {
          id: 's1',
          layout: '1',
          background: 'none',
          columns: [
            [
              { id: 'b1', kind: 'heading', level: 1, textAr: page.nameAr, textEn: page.nameEn },
              ...(bodyAr || bodyEn
                ? [{ id: 'b2', kind: 'text' as const, textAr: bodyAr, textEn: bodyEn }]
                : []),
            ],
          ],
        },
      ],
    };
  }

  protected number(row: ConfigRow): number {
    return Number(String(row.code).split('/').pop());
  }

  protected layoutWidths(layout: PageLayout): number[] {
    return PAGE_LAYOUTS[layout];
  }

  protected gridOf(layout: PageLayout): string {
    return PAGE_LAYOUTS[layout].map((w) => `minmax(0, ${w}fr)`).join(' ');
  }

  protected choose(code: string): void {
    this.current.set(code);
    void this.router.navigate([], { queryParams: { page: code }, replaceUrl: true });
  }

  private reset(design: PageDesign, base: number | null): void {
    this.design.set(design);
    this.base.set(base);
    this.past.set([]);
    this.future.set([]);
    this.selection.set(null);
    this.saved.set(null);
  }

  protected async openVersion(n: number): Promise<void> {
    const code = this.current();
    if (!code) return;
    try {
      const row = await firstValueFrom(this.api.get(VERSION, `${code}/${n}`));
      this.reset(readPageDesign(row['design']) ?? starterPage(), n);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  protected commit(next: PageDesign): void {
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

  protected select(selection: Selection): void {
    this.selection.set(selection);
    this.imageError.set(null);
    if (selection) this.sidePanel.set('props');
  }

  protected isBlock(id: string): boolean {
    const s = this.selection();
    return s?.type === 'block' && s.id === id;
  }

  protected isSection(index: number): boolean {
    const s = this.selection();
    return s?.type === 'section' && s.index === index;
  }

  protected isTarget(section: number, column: number, index: number): boolean {
    const t = this.target();
    return !!t && t.section === section && t.column === column && t.index === index;
  }

  // Drag and drop: new blocks and sections from the palette, existing blocks to another place.
  protected dragNewBlock(event: DragEvent, kind: PageBlockKind): void {
    event.dataTransfer?.setData(BLOCK_MIME, kind);
  }

  protected dragNewSection(event: DragEvent, layout: PageLayout): void {
    event.dataTransfer?.setData(SECTION_MIME, layout);
  }

  protected dragExisting(event: DragEvent, id: string): void {
    event.stopPropagation();
    event.dataTransfer?.setData(MOVE_MIME, id);
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move';
  }

  protected allowDrop(event: DragEvent): void {
    event.preventDefault();
  }

  protected overBlock(event: DragEvent, section: number, column: number, index: number): void {
    event.preventDefault();
    event.stopPropagation();
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const after = event.clientY > rect.top + rect.height / 2;
    this.setTarget({ section, column, index: after ? index + 1 : index });
  }

  protected overColumn(event: DragEvent, section: number, column: number, length: number): void {
    event.preventDefault();
    const t = this.target();
    if (!t || t.section !== section || t.column !== column)
      this.setTarget({ section, column, index: length });
  }

  private setTarget(place: BlockPlace): void {
    const t = this.target();
    if (!t || t.section !== place.section || t.column !== place.column || t.index !== place.index)
      this.target.set(place);
  }

  protected leave(): void {
    // The indicator stays while moving between blocks; it goes when the drop ends.
  }

  protected drop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    const place = this.target();
    this.target.set(null);
    if (!place) return;
    const moving = event.dataTransfer?.getData(MOVE_MIME);
    const kind = event.dataTransfer?.getData(BLOCK_MIME) as PageBlockKind | '';
    if (moving) {
      this.commit(moveBlock(this.design(), moving, place));
      this.select({ type: 'block', id: moving });
    } else if (kind && BLOCKS.some((b) => b.kind === kind)) {
      const block = newBlock(kind, nextId(this.design(), 'b'));
      this.commit(insertBlock(this.design(), block, place));
      this.select({ type: 'block', id: block.id });
    }
  }

  protected dropSection(event: DragEvent, at: number): void {
    event.preventDefault();
    this.target.set(null);
    const layout = event.dataTransfer?.getData(SECTION_MIME) as PageLayout | '';
    const kind = event.dataTransfer?.getData(BLOCK_MIME) as PageBlockKind | '';
    const moving = event.dataTransfer?.getData(MOVE_MIME);
    if (layout && layout in PAGE_LAYOUTS) {
      this.addSectionAt(at, layout);
    } else if (kind || moving) {
      // A block dropped between sections starts a new one-column section there.
      const id = nextId(this.design(), 's');
      let next = addSection(this.design(), at, id, '1');
      if (moving) next = moveBlock(next, moving, { section: at, column: 0, index: 0 });
      else if (BLOCKS.some((b) => b.kind === kind))
        next = insertBlock(next, newBlock(kind as PageBlockKind, nextId(next, 'b')), {
          section: at,
          column: 0,
          index: 0,
        });
      this.commit(next);
    }
  }

  protected addSectionAt(at: number, layout: PageLayout): void {
    this.commit(addSection(this.design(), at, nextId(this.design(), 's'), layout));
    this.select({ type: 'section', index: at });
  }

  protected removeSectionAt(index: number): void {
    this.commit(removeSection(this.design(), index));
    this.select(null);
  }

  /** A click on the palette adds the block after the selected one (or at the end of the selected or last section). */
  protected addBlockToSelection(kind: PageBlockKind): void {
    const design = this.design();
    const block = newBlock(kind, nextId(design, 'b'));
    const s = this.selection();
    let place: BlockPlace | null = null;
    if (s?.type === 'block') {
      const at = findBlock(design, s.id);
      if (at) place = { ...at, index: at.index + 1 };
    } else if (s?.type === 'section') {
      place = { section: s.index, column: 0, index: design.sections[s.index].columns[0].length };
    } else if (design.sections.length) {
      const last = design.sections.length - 1;
      place = { section: last, column: 0, index: design.sections[last].columns[0].length };
    }
    if (!place) {
      const next = addSection(design, 0, nextId(design, 's'), '1');
      this.commit(insertBlock(next, block, { section: 0, column: 0, index: 0 }));
    } else {
      this.commit(insertBlock(design, block, place));
    }
    this.select({ type: 'block', id: block.id });
  }

  protected edit(id: string, change: Partial<PageBlock>): void {
    this.commit(updateBlock(this.design(), id, change));
  }

  protected duplicate(block: PageBlock): void {
    const place = findBlock(this.design(), block.id);
    if (!place) return;
    const copy = { ...structuredClone(block), id: nextId(this.design(), 'b') };
    this.commit(insertBlock(this.design(), copy, { ...place, index: place.index + 1 }));
    this.select({ type: 'block', id: copy.id });
  }

  protected editCard(block: PageBlock, index: number, change: Partial<PageCard>): void {
    this.edit(block.id, {
      items: (block.items ?? []).map((c, i) => (i === index ? { ...c, ...change } : c)),
    });
  }

  protected addCard(block: PageBlock): void {
    this.edit(block.id, {
      items: [
        ...(block.items ?? []),
        { icon: 'star', titleAr: 'عنوان', titleEn: 'Title', textAr: '', textEn: '' },
      ],
    });
  }

  protected removeCard(block: PageBlock, index: number): void {
    this.edit(block.id, { items: (block.items ?? []).filter((_, i) => i !== index) });
  }

  protected loadImage(id: string, input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (file.size > MAX_IMAGE_BYTES) {
      this.imageError.set('pageDesigner.imageTooBig');
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      this.imageError.set(null);
      this.edit(id, { src: String(reader.result) });
    };
    reader.readAsDataURL(file);
  }

  /** Adds the next version (and, when chosen, makes it the published one in the same change set). */
  protected async save(submit = false): Promise<void> {
    const code = this.current();
    const page = this.pageRow();
    if (!code || !page) return;
    const n = this.nextNumber();
    const key = `${code}/${n}`;
    const version = {
      nameAr: `الإصدار ${n}`,
      nameEn: `Version ${n}`,
      isActive: true,
      design: this.design(),
    };
    const pagePayload = { ...payloadOf(page), publishedVersion: n };
    this.busy.set(true);
    this.errors.set([]);
    try {
      if (!this.publish()) {
        await this.drafts.addChange(VERSION, key, 'CREATE', version, { submit });
      } else if (submit && this.drafts.needsApproval(VERSION)) {
        // Both changes in one change set of their own, sent for approval together.
        const area =
          (await firstValueFrom(this.drafts.types())).find((t) => t.entityType === VERSION)?.area ??
          'cms.content';
        const created = await firstValueFrom(
          this.api.createChangeSet(
            area,
            `${code} — ${this.i18n.translate('pageDesigner.versionWord')} ${n}`,
          ),
        );
        await firstValueFrom(
          this.api.addItem(created.changeSet.id, VERSION, key, 'CREATE', version),
        );
        await firstValueFrom(
          this.api.addItem(created.changeSet.id, PAGE, code, 'UPDATE', pagePayload),
        );
        await firstValueFrom(this.api.submit(created.changeSet.id));
        this.snackBar
          .open(this.i18n.translate('drafts.submitted'), this.i18n.translate('drafts.open'), {
            duration: 6000,
          })
          .onAction()
          .subscribe(
            () => void this.router.navigate(['/apps-control/change-sets', created.changeSet.id]),
          );
      } else {
        await this.drafts.addChange(VERSION, key, 'CREATE', version);
        await this.drafts.addChange(PAGE, code, 'UPDATE', pagePayload);
      }
      this.saved.set(
        `${key} — ${this.i18n.translate(this.publish() ? 'pageDesigner.savedPublish' : 'pageDesigner.saved')}`,
      );
      this.versions.reload();
      this.pages.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.busy.set(false);
    }
  }

  /** Makes an existing version the one the portal shows. */
  protected async publishVersion(n: number): Promise<void> {
    const code = this.current();
    const page = this.pageRow();
    if (!code || !page) return;
    this.errors.set([]);
    try {
      await this.drafts.addChange(PAGE, code, 'UPDATE', {
        ...payloadOf(page),
        publishedVersion: n,
      });
      this.pages.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  protected onKey(event: KeyboardEvent): void {
    const target = event.target as HTMLElement;
    if (target.closest('input, textarea, mat-select, .mat-mdc-select')) return;
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') {
      event.preventDefault();
      if (event.shiftKey) this.redo();
      else this.undo();
    } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'y') {
      event.preventDefault();
      this.redo();
    } else if (event.key === 'Delete') {
      const s = this.selection();
      if (s?.type === 'block') {
        this.commit(removeBlock(this.design(), s.id));
        this.select(null);
      }
    }
  }
}
