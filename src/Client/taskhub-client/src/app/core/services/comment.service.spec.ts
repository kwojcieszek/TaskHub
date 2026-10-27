import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { appSettings } from '../config/app-settings';
import { CommentService } from './comment.service';

describe('CommentService', () => {
  const projectUrl = `${appSettings.apiUrl}/projects/p1`;

  let service: CommentService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(CommentService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the comments of the project when no task is given', () => {
    service.getAll('p1', null).subscribe();

    const request = http.expectOne(`${projectUrl}/comments`);
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('reads the comments of a task when one is given', () => {
    service.getAll('p1', 't1').subscribe();

    http.expectOne(`${projectUrl}/tasks/t1/comments`).flush([]);
  });

  it('adds a comment to the project or to the task', () => {
    service.add('p1', null, 'On the project').subscribe();
    const onProject = http.expectOne(`${projectUrl}/comments`);
    expect(onProject.request.method).toBe('POST');
    expect(onProject.request.body).toEqual({ content: 'On the project' });
    onProject.flush({});

    service.add('p1', 't1', 'On the task').subscribe();
    const onTask = http.expectOne(`${projectUrl}/tasks/t1/comments`);
    expect(onTask.request.body).toEqual({ content: 'On the task' });
    onTask.flush({});
  });

  it('changes and deletes a comment through the project, whatever it is attached to', () => {
    service.update('p1', 'c1', 'Corrected').subscribe();
    const update = http.expectOne(`${projectUrl}/comments/c1`);
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual({ content: 'Corrected' });
    update.flush({});

    service.delete('p1', 'c1').subscribe();
    const remove = http.expectOne(`${projectUrl}/comments/c1`);
    expect(remove.request.method).toBe('DELETE');
    remove.flush(null);
  });
});
