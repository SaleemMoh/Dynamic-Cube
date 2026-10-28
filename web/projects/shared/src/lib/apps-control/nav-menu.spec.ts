import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideDynamicCubeUi } from '../theme/theme';
import { MenuGroupNode } from './apps-control.models';
import { NavMenu } from './nav-menu';

const groups: MenuGroupNode[] = [
  {
    code: 'SERVICES',
    nameAr: 'الخدمات',
    nameEn: 'Services',
    icon: null,
    items: [
      { code: 'svc.requests', nameAr: 'الطلبات', nameEn: 'Requests', itemType: 'SCREEN', target: '/services/requests', icon: null, descriptionAr: null, descriptionEn: null, children: [] },
    ],
  },
];

describe('NavMenu', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ imports: [NavMenu], providers: [provideRouter([]), provideDynamicCubeUi()] });
  });

  function render() {
    const fixture = TestBed.createComponent(NavMenu);
    fixture.componentRef.setInput('groups', groups);
    fixture.componentRef.setInput('storageKey', 'test.nav');
    fixture.detectChanges();
    return fixture;
  }

  it('closes and reopens a section from its title and remembers closed sections', () => {
    const fixture = render();
    const element = fixture.nativeElement as HTMLElement;
    const title = element.querySelector<HTMLButtonElement>('[data-nav-group=SERVICES]')!;
    expect(title.getAttribute('aria-expanded')).toBe('true');
    expect(element.querySelector('[data-menu="svc.requests"]')).not.toBeNull();

    title.click();
    fixture.detectChanges();

    expect(title.getAttribute('aria-expanded')).toBe('false');
    expect(element.querySelector('[data-menu="svc.requests"]')).toBeNull();
    expect(JSON.parse(localStorage.getItem('test.nav')!)).toEqual(['SERVICES']);

    const reloaded = render().nativeElement as HTMLElement;
    expect(reloaded.querySelector('[data-menu="svc.requests"]')).toBeNull();

    title.click();
    fixture.detectChanges();
    expect(element.querySelector('[data-menu="svc.requests"]')).not.toBeNull();
    expect(JSON.parse(localStorage.getItem('test.nav')!)).toEqual([]);
  });
});
