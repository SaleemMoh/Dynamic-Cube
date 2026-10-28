import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ACTING_PARTY_HEADER, ActingPartyService, actingPartyInterceptor } from './acting-party';
import { ActingContext, ContextsResult } from './party.models';

const context = (partyId: string, identifier: string): ActingContext => ({
  partyId,
  kind: 'ESTABLISHMENT',
  nameAr: 'مؤسسة',
  nameEn: 'Est.',
  identifierType: 'CR_NUMBER',
  identifier,
  relationshipCode: 'OWNER',
  relationshipNameAr: 'مالك',
  relationshipNameEn: 'Owner',
  services: [],
  validTo: null,
});

describe('ActingPartyService', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let acting: ActingPartyService;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([actingPartyInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    acting = TestBed.inject(ActingPartyService);
  });

  afterEach(() => controller.verify());

  async function load(result: ContextsResult): Promise<void> {
    const loading = acting.load();
    controller.expectOne('/api/party/v1/me/contexts').flush(result);
    await loading;
  }

  it('acts for oneself until a context is chosen, then sends the chosen party', async () => {
    await load({ individual: { ...context('me', '1000000001'), kind: 'INDIVIDUAL' }, contexts: [context('est-1', '1010000001')], refreshedAt: null, warnings: [] });

    http.get('/api/party/v1/me/acting').subscribe();
    expect(controller.expectOne('/api/party/v1/me/acting').request.headers.has(ACTING_PARTY_HEADER)).toBe(false);

    acting.select('est-1');
    http.get('/api/party/v1/me/acting').subscribe();
    http.get('/config.json').subscribe();

    expect(acting.current()?.identifier).toBe('1010000001');
    expect(controller.expectOne('/api/party/v1/me/acting').request.headers.get(ACTING_PARTY_HEADER)).toBe('est-1');
    expect(controller.expectOne('/config.json').request.headers.has(ACTING_PARTY_HEADER)).toBe(false);
  });

  it('drops a remembered choice the applicant may no longer act for', async () => {
    acting.select('gone');

    await load({ individual: { ...context('me', '1000000001'), kind: 'INDIVIDUAL' }, contexts: [], refreshedAt: null, warnings: [] });

    expect(acting.headerValue()).toBeNull();
    expect(sessionStorage.getItem('dc.actingParty')).toBeNull();
  });
});
