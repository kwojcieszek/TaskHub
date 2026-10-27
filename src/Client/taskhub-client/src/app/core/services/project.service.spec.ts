import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { appSettings } from '../config/app-settings';
import { ProjectService } from './project.service';

describe('ProjectService', () => {
  const projectUrl = `${appSettings.apiUrl}/projects/p1`;

  let service: ProjectService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ProjectService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the teams of a project and the ones that can still be added', () => {
    service.getTeams('p1').subscribe();
    http.expectOne(`${projectUrl}/teams`).flush([]);

    service.getAvailableTeams('p1').subscribe();
    http.expectOne(`${projectUrl}/teams/available`).flush([]);
  });

  it('adds a team with its role and removes it by the id of the team', () => {
    service.addTeam('p1', { userGroupId: 'g1', role: 'Editor' }).subscribe();
    const add = http.expectOne(`${projectUrl}/teams`);
    expect(add.request.method).toBe('POST');
    expect(add.request.body).toEqual({ userGroupId: 'g1', role: 'Editor' });
    add.flush({});

    service.removeTeam('p1', 'g1').subscribe();
    const remove = http.expectOne(`${projectUrl}/teams/g1`);
    expect(remove.request.method).toBe('DELETE');
    remove.flush(null);
  });

  it('adds and removes a member', () => {
    service.addMember('p1', { userId: 'u1', role: 'Viewer' }).subscribe();
    const add = http.expectOne(`${projectUrl}/members`);
    expect(add.request.body).toEqual({ userId: 'u1', role: 'Viewer' });
    add.flush({});

    service.removeMember('p1', 'u1').subscribe();
    expect(http.expectOne(`${projectUrl}/members/u1`).request.method).toBe('DELETE');
  });

  it('moves a project into a group or out of every group', () => {
    service.setGroup('p1', 'g1').subscribe();
    const into = http.expectOne(`${projectUrl}/group`);
    expect(into.request.method).toBe('PUT');
    expect(into.request.body).toEqual({ projectGroupId: 'g1' });
    into.flush({});

    service.setGroup('p1', null).subscribe();
    expect(http.expectOne(`${projectUrl}/group`).request.body).toEqual({ projectGroupId: null });
  });
});
