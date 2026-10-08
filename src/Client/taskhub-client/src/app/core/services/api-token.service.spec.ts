import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { ApiTokenService } from './api-token.service';

describe('ApiTokenService', () => {
  const tokensUrl = `${environment.apiUrl}/tokens`;

  let service: ApiTokenService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ApiTokenService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists the tokens of the signed-in user', () => {
    service.getAll().subscribe();

    const request = http.expectOne(tokensUrl);
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('creates a token with the number of days it stays valid', () => {
    service.create({ name: 'Script', expiresInDays: 30 }).subscribe();

    const request = http.expectOne(tokensUrl);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ name: 'Script', expiresInDays: 30 });
    request.flush({});
  });

  it('sends null for a token that never expires', () => {
    service.create({ name: 'Forever', expiresInDays: null }).subscribe();

    const request = http.expectOne(tokensUrl);
    expect(request.request.body.expiresInDays).toBeNull();
    request.flush({});
  });

  it('revokes a token by its id', () => {
    service.revoke('abc').subscribe();

    const request = http.expectOne(`${tokensUrl}/abc`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
  });
});
