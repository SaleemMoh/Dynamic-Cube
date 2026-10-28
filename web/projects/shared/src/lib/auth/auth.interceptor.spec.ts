import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  const auth = { accessToken: vi.fn(() => 'token-1'), login: vi.fn(() => Promise.resolve()) };

  beforeEach(() => {
    auth.login.mockClear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), { provide: AuthService, useValue: auth }],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('sends the access token to the API only', () => {
    http.get('/api/apps-control/v1/me').subscribe();
    http.get('/config.json').subscribe();

    expect(controller.expectOne('/api/apps-control/v1/me').request.headers.get('Authorization')).toBe('Bearer token-1');
    expect(controller.expectOne('/config.json').request.headers.has('Authorization')).toBe(false);
  });

  it('starts sign-in when the API answers 401', () => {
    http.get('/api/apps-control/v1/me').subscribe({ error: () => undefined });

    controller.expectOne('/api/apps-control/v1/me').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.login).toHaveBeenCalledOnce();
  });
});
