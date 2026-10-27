import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { appSettings } from '../config/app-settings';
import { UserGroupService } from './user-group.service';

describe('UserGroupService', () => {
  const groupsUrl = `${appSettings.apiUrl}/user-groups`;

  let service: UserGroupService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(UserGroupService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('creates a team', () => {
    service.create({ name: 'Developers', description: 'Backend' }).subscribe();

    const request = http.expectOne(groupsUrl);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ name: 'Developers', description: 'Backend' });
    request.flush({});
  });

  it('renames a team by its id', () => {
    service.update('g1', { name: 'Engineers', description: '' }).subscribe();

    const request = http.expectOne(`${groupsUrl}/g1`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ name: 'Engineers', description: '' });
    request.flush({});
  });

  it('adds and removes a member of a team', () => {
    service.addMember('g1', { userId: 'u1' }).subscribe();
    const add = http.expectOne(`${groupsUrl}/g1/members`);
    expect(add.request.body).toEqual({ userId: 'u1' });
    add.flush({});

    service.removeMember('g1', 'u1').subscribe();
    expect(http.expectOne(`${groupsUrl}/g1/members/u1`).request.method).toBe('DELETE');
  });

  it('deletes a team', () => {
    service.delete('g1').subscribe();

    expect(http.expectOne(`${groupsUrl}/g1`).request.method).toBe('DELETE');
  });
});
