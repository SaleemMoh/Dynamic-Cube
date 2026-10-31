import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { APPS_CONTROL_API, COMPANY_HEADER } from './apps-control.api';
import { WorkspaceInfo } from './apps-control.models';
import { CompanyContext, companyInterceptor } from './workspace';

const company = (code: string, entityCode = 'ORG') => ({
  code,
  nameAr: code,
  nameEn: code,
  entityCode,
  entityNameAr: entityCode,
  entityNameEn: entityCode,
});

describe('CompanyContext', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let context: CompanyContext;

  function setUp(): void {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([companyInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    context = TestBed.inject(CompanyContext);
  }

  afterEach(() => {
    controller.verify();
    localStorage.clear();
  });

  async function load(info: WorkspaceInfo): Promise<void> {
    const loading = context.load();
    controller.expectOne(`${APPS_CONTROL_API}/me/workspaces`).flush(info);
    await loading;
  }

  it('sends the company the server works with, and keeps a company a call names itself', async () => {
    setUp();
    await load({ company: 'MAIN', chosen: true, companies: [company('MAIN'), company('SUB')] });
    expect(context.needsChoice()).toBe(false);
    expect(localStorage.getItem('dc.company')).toBe('MAIN');

    http.get('/api/services/v1/requests').subscribe();
    expect(
      controller.expectOne('/api/services/v1/requests').request.headers.get(COMPANY_HEADER),
    ).toBe('MAIN');
    http
      .get('/api/apps-control/v1/me/business-apps', { headers: { [COMPANY_HEADER]: 'SUB' } })
      .subscribe();
    expect(
      controller
        .expectOne('/api/apps-control/v1/me/business-apps')
        .request.headers.get(COMPANY_HEADER),
    ).toBe('SUB');
    http.get('/assets/config.json').subscribe();
    expect(controller.expectOne('/assets/config.json').request.headers.has(COMPANY_HEADER)).toBe(
      false,
    );
  });

  it('asks to choose when several companies are open and none was chosen, forgetting a company no longer open', async () => {
    localStorage.setItem('dc.company', 'GONE');
    setUp();
    await load({
      company: 'MAIN',
      chosen: false,
      companies: [company('MAIN'), company('SUB', 'OTHER')],
    });
    expect(context.needsChoice()).toBe(true);
    expect(context.severalOrganizations()).toBe(true);
    expect(context.current()).toBeNull();
    expect(localStorage.getItem('dc.company')).toBeNull();
  });

  it('never asks someone with one company', async () => {
    setUp();
    await load({ company: 'MAIN', chosen: true, companies: [company('MAIN')] });
    expect(context.needsChoice()).toBe(false);
    expect(context.current()).toBe('MAIN');
  });
});
