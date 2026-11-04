import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSelect } from '@angular/material/select';
import { By } from '@angular/platform-browser';
import { SearchableSelectModule, SELECT_SEARCH_MIN_OPTIONS } from './select-auto-search';

@Component({
  imports: [SearchableSelectModule],
  template: `
    <mat-select data-testid="long">
      @for (c of cities(); track c) {
        <mat-option [value]="c">{{ c }}</mat-option>
      }
    </mat-select>
    <mat-select data-testid="short">
      <mat-option value="yes">Yes</mat-option>
      <mat-option value="no">No</mat-option>
    </mat-select>
  `,
})
class Host {
  readonly cities = signal([
    'Riyadh',
    'Jeddah',
    'Dammam',
    'Makkah',
    'Madinah',
    'Abha',
    'Tabuk',
    'Hail',
  ]);
}

const tick = () => new Promise((resolve) => setTimeout(resolve, 20));

async function open(testId: string) {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const select = fixture.debugElement
    .query(By.css(`[data-testid=${testId}]`))
    .injector.get(MatSelect);
  select.open();
  fixture.detectChanges();
  await tick();
  fixture.detectChanges();
  return { fixture, select };
}

describe('SelectAutoSearch', () => {
  it('adds a search box to long lists that hides the choices not matching', async () => {
    const { select } = await open('long');
    const box = document.querySelector<HTMLInputElement>('[data-testid=select-search]');
    expect(box).not.toBeNull();

    box!.value = 'ma';
    box!.dispatchEvent(new Event('input'));
    const shown = select.options
      .filter((o) => o._getHostElement().style.display !== 'none')
      .map((o) => o.value);
    expect(shown).toEqual(['Dammam', 'Makkah', 'Madinah']);

    select.close();
    await tick();
    expect(document.querySelector('[data-testid=select-search]')).toBeNull();
    expect(select.options.toArray().every((o) => o._getHostElement().style.display === '')).toBe(
      true,
    );
  });

  it('leaves short lists as they are', async () => {
    expect(SELECT_SEARCH_MIN_OPTIONS).toBeGreaterThan(2);
    await open('short');
    expect(document.querySelector('[data-testid=select-search]')).toBeNull();
  });
});
