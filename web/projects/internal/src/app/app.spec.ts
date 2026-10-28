import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RUNTIME_CONFIG, provideDynamicCubeUi } from '@dynamic-cube/shared';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideDynamicCubeUi(),
        { provide: RUNTIME_CONFIG, useValue: { app: 'INTERNAL', auth: { authority: 'http://localhost/realms/test', clientId: 'test' } } },
      ],
    }).compileComponents();
  });

  it('renders the Arabic app title in a right-to-left document', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();

    const header = (fixture.nativeElement as HTMLElement).querySelector('.dc-topbar__title');
    expect(header?.textContent).toContain('ديناميك كيوب');
    expect(document.documentElement.dir).toBe('rtl');
  });

  it('opens and closes the side navigation from the top bar and remembers it', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    const toggle = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('[data-testid=nav-toggle]')!;
    expect(toggle.getAttribute('aria-expanded')).toBe('true');

    toggle.click();
    fixture.detectChanges();

    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(localStorage.getItem('dc.internal.nav.open')).toBe('false');
  });
});
