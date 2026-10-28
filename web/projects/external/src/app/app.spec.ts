import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
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
        { provide: RUNTIME_CONFIG, useValue: { app: 'EXTERNAL', auth: { authority: 'http://localhost/realms/test', clientId: 'test' } } },
      ],
    }).compileComponents();
  });

  it('renders the Arabic app title in a right-to-left document', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    // The shell loads branding, texts and the public catalog; answer them all (empty) so the page settles.
    const http = TestBed.inject(HttpTestingController);
    for (const request of http.match(() => true)) {
      request.flush(request.request.url.endsWith('/portal/catalog') ? { categories: [], services: [], lastUpdated: null } : null);
    }
    await fixture.whenStable();

    const header = (fixture.nativeElement as HTMLElement).querySelector('.dc-shell__title');
    expect(header?.textContent).toContain('ديناميك كيوب');
    expect(document.documentElement.dir).toBe('rtl');
  });
});
